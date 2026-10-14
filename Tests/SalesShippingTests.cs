using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Sales.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Postgres.Data;
using CompanyHQ.Postgres.Extensions;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Models;
using CompanyHQ.Sales.Contracts.Ports;
using CompanyHQ.Sales.Extensions;
using CompanyHQ.Sales.Services;
using CompanyHQ.Shipping.Contracts.Dtos;
using CompanyHQ.Shipping.Contracts.Enums;
using CompanyHQ.Shipping.Contracts.Forms;
using CompanyHQ.Shipping.Contracts.Ports;
using CompanyHQ.Users.Services;
using CompanyHQ.Shipping.Extensions;
using CompanyHQ.Shipping.Services;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Users.Extensions;
using Testcontainers.PostgreSql;
using Xunit;
namespace CompanyHQ.Tests;

public sealed class SalesRulesTests
{
    [Fact]
    public void Contracts_TextEnumsPreserveIndependentNumericStorage()
    {
        Assert.Equal("\"Issuing\"", JsonSerializer.Serialize(QuoteStatus.Issuing));
        Assert.Equal("\"Courier\"", JsonSerializer.Serialize(ShippingMethodType.Courier));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuoteStatus>("5"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ShippingMethodType>("1"));
        var optionId = Guid.NewGuid();
        var definition = new SaveProductRequest("Storage", "storage", [new(Guid.NewGuid(), "storage", 50)],
            Type: ProductType.Bundle, Options: [new("Size", ["Small"], optionId)]);
        var stored = SalesRules.Write(definition);
        using var envelope = JsonDocument.Parse(stored);
        Assert.Equal(1, envelope.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Number, envelope.RootElement.GetProperty("data").GetProperty("type").ValueKind);
        var restored = SalesRules.Read<SaveProductRequest>(stored);
        Assert.Equal(ProductType.Bundle, restored.Type);
        Assert.Equal(optionId, restored.Options![0].Id);
        Assert.Equal(definition.Variants[0].Id, restored.Variants[0].Id);
    }
    [Fact]
    public void Pricing_RecursiveBundlesRoundingExtraLengthAndCouponSelection()
    {
        var id=Guid.NewGuid();var standard=new SaveVariantRequest(Guid.NewGuid(),"standard",100,100,new Dictionary<string,string>{{"Length","Standard"}});
        var extra=standard with {Id=Guid.NewGuid(),Sku="extra",Cost=900,Options=new Dictionary<string,string>{{"length","Extra"}}};
        var p=new SaveProductRequest("Bed","bed",[standard,extra],Options:[new("Length",["Standard","Extra"])]);
        var graph=new Dictionary<Guid,SaveProductRequest>{{id,p}};var policy=new SalesOptions{EnableExtraLengthPricing=true,ExtraLengthOptionName="Length",ExtraLengthValueContains="Extra",StandardLengthValueContains="Standard",ExtraLengthMultiplier=1.15m};var pricing=new CatalogPricing(graph,[],new(2030,1,1),policy);
        Assert.Equal(200,pricing.Price(id,standard).Selling);Assert.Equal(230,pricing.Price(id,extra).Selling);
        Assert.Equal(99,SalesRules.Retail(p with {EnableRounding=true,RoundingTarget=99,RoundingDirection=RoundingDirection.RoundDown},standard,100));
        Assert.Equal(199,SalesRules.Retail(p with {EnableRounding=true,RoundingTarget=99},standard,100));
        Assert.Equal(9.99m,SalesRules.Money(9.985m));
        var bundle=Guid.NewGuid();var bv=new SaveVariantRequest(Guid.NewGuid(),"bundle",0);
        graph[bundle]=new("Bundle","bundle",[bv],Type:ProductType.Bundle,BundleComponents:[new(id,2,10,FallbackVariantId:standard.Id)]);
        pricing=new(graph,[],new(2030,1,1));var result=pricing.Price(bundle,bv);
        Assert.Equal(400,result.Selling);Assert.Equal(360,result.Effective);Assert.Equal(200,result.Cost);
        var c1=new CouponRule(Guid.NewGuid(),new("STACK",DiscountType.Percentage,20,IsAutomatic:true,AllowsCombination:true));
        var c2=new CouponRule(Guid.NewGuid(),new("SINGLE",DiscountType.Percentage,40,IsAutomatic:true));
        result=new CatalogPricing(graph,[c1,c2],new(2030,1,1)).Price(bundle,bv);
        Assert.Equal(240,result.Effective);Assert.Single(result.Discounts);Assert.Equal("SINGLE",result.Discounts[0].CouponCode);
        result=new CatalogPricing(graph,[c1],new(2030,1,1)).Price(bundle,bv);Assert.Equal(280,result.Effective);
        var nested=Guid.NewGuid();graph[nested]=new("Nested","nested",[bv with {Id=Guid.NewGuid()}],Type:ProductType.Bundle,BundleComponents:[new(bundle,2,20,DiscountType.Fixed)]);
        result=new CatalogPricing(graph,[],new(2030,1,1)).Price(nested,graph[nested].Variants[0]);Assert.Equal(680,result.Effective);
        graph[id]=p with {Type=ProductType.Bundle,BundleComponents=[new(nested)]};
        Assert.Throws<ArgumentException>(()=>new CatalogPricing(graph,[],new(2030,1,1)).Price(nested,graph[nested].Variants[0]));
    }
    [Fact]
    public void Cart_NormalizesIdentityAndRecursivelyRemovesOrphans()
    {
        var product=Guid.NewGuid();var parent=Guid.NewGuid();var duplicate=Guid.NewGuid();var child=Guid.NewGuid();var missing=Guid.NewGuid();var orphan=Guid.NewGuid();var descendant=Guid.NewGuid();
        var input=new SalesLineRequest[]{new(parent,product,Quantity:1,Options:new Dictionary<string,string>{{"Size"," Queen "}}),
            new(duplicate,product,Quantity:2,Options:new Dictionary<string,string>{{" size ","queen"}}),new(child,Guid.NewGuid(),UpsellParentLineId:duplicate),
            new(orphan,Guid.NewGuid(),UpsellParentLineId:missing),new(descendant,Guid.NewGuid(),UpsellParentLineId:orphan)};
        var result=SalesRules.NormalizeCart(input);Assert.Equal(2,result.Lines.Count);Assert.Equal(3,result.Lines[0].Quantity);Assert.Equal(parent,result.Lines[1].UpsellParentLineId);
        Assert.Contains(duplicate,result.RemovedLineIds);Assert.Contains(orphan,result.RemovedLineIds);Assert.Contains(descendant,result.RemovedLineIds);
        Assert.Equal(2,SalesRules.NormalizeCart([input[0],input[1] with {BundleInstanceId=Guid.NewGuid()}]).Lines.Count);
        Assert.Throws<ArgumentException>(()=>SalesRules.NormalizeCart([new(parent,product,UpsellParentLineId:child),new(child,product,UpsellParentLineId:parent)]));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PriceSaleRequest>("{\"lines\":[],\"taxAmount\":10}",SalesRules.Json));
    }
    [Fact]
    public void ShippingPlanner_ProvesSharedGlobalMinimumOrFailsOnBudget()
    {
        var origin=Guid.NewGuid();var shared=new ShippingPlanner.Candidate(new(Guid.NewGuid(),origin),6);
        IReadOnlyList<IReadOnlyList<ShippingPlanner.Candidate>> choices=[
            new[]{new ShippingPlanner.Candidate(new(Guid.NewGuid(),origin),4),shared},
            new[]{new ShippingPlanner.Candidate(new(Guid.NewGuid(),origin),4),shared}];
        var result=ShippingPlanner.Cheapest(choices);Assert.All(result,x=>Assert.Equal(shared.Leg,x.Leg));
        Assert.Throws<ShippingComplexityException>(()=>ShippingPlanner.Cheapest(choices,1));
        Assert.True(ShippingRules.Matches(PostalCodeRuleType.Range,"7400-7500","7500"));
        Assert.False(ShippingRules.Matches(PostalCodeRuleType.Range,"7400-7500","7501"));
        Assert.True(ShippingRules.Matches(PostalCodeRuleType.Wildcard,"74*","7447"));
        Assert.Equal(0,ShippingRules.DistanceKm(0,0,0,0));
        var today=new DateOnly(2030,1,2);var coupon=new SaveCouponRequest("DATE",DiscountType.Percentage,10,StartDate:today,EndDate:today);
        Assert.True(SalesRules.Valid(coupon,today));Assert.False(SalesRules.Valid(coupon,today.AddDays(1)));
        var category=Guid.NewGuid();Assert.True(SalesRules.Targets(coupon with {ProductIds=[Guid.NewGuid()],CategoryIds=[category]},Guid.NewGuid(),[category]));
    }
}

public sealed class SalesShippingTests(SalesShippingFixture fixture) : IClassFixture<SalesShippingFixture>
{
    private static PageRequest Page=>new();private static SearchRequest Search=>new();
    private static SaveProductRequest Definition(string name,decimal cost=100,decimal profit=100)=>new(name,"p-"+Guid.NewGuid().ToString("N"),[new(Guid.NewGuid(),name,cost,profit)]);
    [Fact]
    public async Task InvoiceDelivery_RetryKeepsReservedRevisionWhileNewRequestSnapshotsRepricing()
    {
        await using var setup=fixture.Services.CreateAsyncScope();
        var sp=setup.ServiceProvider;
        var product=await sp.GetRequiredService<SalesCatalogService>().SaveAsync(null,Definition("Invoice revision",profit:0),fixture.Admin);
        var request=new SaveStaffOrderRequest(new(new([new(Guid.NewGuid(),product.Id,product.Variants[0].Id)]),"Guest","Buyer","first@example.test"));
        var receipt=await sp.GetRequiredService<SalesOrderService>().PlaceStaffAsync(request,fixture.Admin);
        var requestId=Guid.NewGuid();var provider=new PausedDocumentDelivery();
        await using var send=fixture.Services.CreateAsyncScope();
        var sending=Documents(send.ServiceProvider,provider).SendInvoiceAsync(receipt.OrderId,new(requestId),fixture.Admin);
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(send.ServiceProvider.GetRequiredService<PostgresDbContext>().Database.CurrentTransaction);
        await using(var edit=fixture.Services.CreateAsyncScope())
        {
            await edit.ServiceProvider.GetRequiredService<SalesOrderService>().UpdateStaffAsync(receipt.OrderId,
                request with {Order=request.Order with {CustomerEmail="second@example.test",Sale=new([request.Order.Sale.Lines[0] with {Quantity=2}])},InvoiceNumber=900001},fixture.Admin);
        }
        await using(var replay=fixture.Services.CreateAsyncScope())
        {
            var replayProvider=new PausedDocumentDelivery();replayProvider.Release.SetResult(new(false,null,null,Queued:true));
            var result=await Documents(replay.ServiceProvider,replayProvider).SendInvoiceAsync(receipt.OrderId,new(requestId),fixture.Admin);
            Assert.True(result!.Queued);Assert.False(result.Accepted);
            Assert.Equal(JsonSerializer.Serialize(provider.Document,SalesRules.Json),JsonSerializer.Serialize(replayProvider.Document,SalesRules.Json));
            Assert.Equal(100,replayProvider.Document!.TotalAmount);Assert.Equal("first@example.test",replayProvider.Document.Recipient);
        }
        provider.Release.SetResult(new(true,"original-provider",null));Assert.True((await sending)!.Accepted);
        await using var fresh=fixture.Services.CreateAsyncScope();
        var freshProvider=new PausedDocumentDelivery();freshProvider.Release.SetResult(new(false,null,null,Queued:true));
        await Documents(fresh.ServiceProvider,freshProvider).SendInvoiceAsync(receipt.OrderId,new(Guid.NewGuid()),fixture.Admin);
        Assert.Equal(200,freshProvider.Document!.TotalAmount);Assert.Equal("second@example.test",freshProvider.Document.Recipient);
        Assert.Equal("INV-900001",freshProvider.Document.Reference);Assert.NotEqual(provider.Document!.IdempotencyKey,freshProvider.Document.IdempotencyKey);
        var rows=await fresh.ServiceProvider.GetRequiredService<IPersistence>().Query<InvoiceDeliveryRequest>().Where(x=>x.OrderId==receipt.OrderId).ToListAsync();
        Assert.Equal(2,rows.Count);Assert.NotNull(rows.Single(x=>x.RequestId==requestId).AcceptedAt);
        Assert.Null(rows.Single(x=>x.RequestId!=requestId).AcceptedAt);
        Assert.Equal("delivery.document",JsonDocument.Parse(rows[0].DocumentJson).RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Catalog_CurrencyEditsValidateInboundAncestorsAndRuntimePricing()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var catalog=scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
        var childDefinition=Definition("Currency child",profit:0) with {Currency="ZAR"};
        var child=await catalog.SaveAsync(null,childDefinition,fixture.Admin);
        var bundleDefinition=Definition("Currency bundle",profit:0) with {Currency="ZAR",Type=ProductType.Bundle,BundleComponents=[new(child.Id)]};
        var bundle=await catalog.SaveAsync(null,bundleDefinition,fixture.Admin);
        await Assert.ThrowsAsync<ArgumentException>(()=>catalog.SaveAsync(child.Id,childDefinition with {Currency="USD"},fixture.Admin));
        var nested=await catalog.SaveAsync(null,Definition("Currency nested",profit:0) with {Currency="ZAR",Type=ProductType.Bundle,BundleComponents=[new(bundle.Id)]},fixture.Admin);
        await Assert.ThrowsAsync<ArgumentException>(()=>catalog.SaveAsync(child.Id,childDefinition with {Currency="USD"},fixture.Admin));
        await Assert.ThrowsAsync<ArgumentException>(()=>catalog.SaveAsync(bundle.Id,bundleDefinition with {Currency="USD"},fixture.Admin));
        await using var verify=fixture.Services.CreateAsyncScope();
        var current=verify.ServiceProvider.GetRequiredService<SalesCatalogService>();
        Assert.Equal("ZAR",(await current.GetAsync(child.Slug))!.Currency);
        Assert.Equal(100,(await current.GetAsync(nested.Slug))!.Price.EffectivePrice);
        var graph=await current.Graph([nested.Id],default);
        graph[child.Id]=childDefinition with {Currency="USD"};
        Assert.Throws<ArgumentException>(()=>new CatalogPricing(graph,[],new(2030,1,1)).Price(nested.Id,graph[nested.Id].Variants[0]));
        // A pre-existing invalid outer edge must also be checked while editing a descendant.
        graph[child.Id]=childDefinition;var validOuter=graph[nested.Id];graph[nested.Id]=validOuter with {Currency="USD"};
        var db=verify.ServiceProvider.GetRequiredService<IPersistence>();
        var outer=await db.Tracked<Product>().Where(x=>x.Id==nested.Id).FirstAsync();
        outer.DefinitionJson=SalesRules.Write(graph[nested.Id]);await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>current.SaveAsync(child.Id,childDefinition with {Name="Must roll back"},fixture.Admin));
        await using var check=fixture.Services.CreateAsyncScope();
        Assert.Equal(childDefinition.Name,(await check.ServiceProvider.GetRequiredService<SalesCatalogService>().GetAsync(child.Slug))!.Name);
        var repair=check.ServiceProvider.GetRequiredService<IPersistence>();
        var repaired=await repair.Tracked<Product>().Where(x=>x.Id==nested.Id).FirstAsync();
        repaired.DefinitionJson=SalesRules.Write(validOuter);await repair.SaveChangesAsync();
    }

    [Fact]
    public async Task Upsells_RepricedOrderSuppressesLifetimeOffersAndRetainsRequestReplay()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var sp=scope.ServiceProvider;
        var catalog=sp.GetRequiredService<SalesCatalogService>();var promotions=sp.GetRequiredService<SalesPromotionService>();var orders=sp.GetRequiredService<SalesOrderService>();
        var parent=await catalog.SaveAsync(null,Definition("Historical parent",profit:0),fixture.Admin);
        var secondParent=await catalog.SaveAsync(null,Definition("Other historical parent",profit:0),fixture.Admin);
        var child=await catalog.SaveAsync(null,Definition("Historical child",profit:0),fixture.Admin);
        var other=await catalog.SaveAsync(null,Definition("Available child",profit:0),fixture.Admin);
        var offer=await promotions.SaveUpsellAsync(null,new(parent.Id,child.Id),fixture.Admin);
        var another=await promotions.SaveUpsellAsync(null,new(secondParent.Id,other.Id),fixture.Admin);
        var line=new SalesLineRequest(Guid.NewGuid(),parent.Id,parent.Variants[0].Id);
        var request=new SaveStaffOrderRequest(new(new([line]),"Guest","Buyer","guest@example.test"));
        var receipt=await orders.PlaceStaffAsync(request,fixture.Admin);var accept=new AcceptUpsellRequest(offer.Id,line.Id,Guid.NewGuid());
        var accepted=await orders.AcceptUnpaidAsync(receipt.OrderId,accept,fixture.Admin,null);
        var replacement=request with {Order=request.Order with {Sale=new([line,new(Guid.NewGuid(),secondParent.Id,secondParent.Variants[0].Id)])}};
        await orders.UpdateStaffAsync(receipt.OrderId,replacement,fixture.Admin);
        var context=await orders.ContextAsync(receipt.OrderId,fixture.Admin,null);
        Assert.DoesNotContain(context!.Offers,x=>x.Offer.UpsellOfferId==offer.Id);
        Assert.Contains(context.Offers,x=>x.Offer.UpsellOfferId==another.Id);
        Assert.Equal(accepted,await orders.AcceptUnpaidAsync(receipt.OrderId,accept,fixture.Admin,null));
        await Assert.ThrowsAsync<ArgumentException>(()=>orders.AcceptUnpaidAsync(receipt.OrderId,accept with {RequestId=Guid.NewGuid()},fixture.Admin,null));
        Assert.Equal(1,await sp.GetRequiredService<IPersistence>().Query<OrderUpsellAcceptance>().CountAsync(x=>x.OrderId==receipt.OrderId));
    }

    [Fact]
    public async Task HqDocumentDelivery_QueueAndProviderRejectionCannotMarkQuoteSent()
    {
        foreach(var acceptProvider in new[]{false,true})
        {
            await using var scope=fixture.Services.CreateAsyncScope();var sp=scope.ServiceProvider;
            var product=await sp.GetRequiredService<SalesCatalogService>().SaveAsync(null,Definition("Queue outcome",profit:0),fixture.Admin);
            var quote=await sp.GetRequiredService<SalesQuoteService>().SaveAsync(null,new(new(new([new(Guid.NewGuid(),product.Id,product.Variants[0].Id)]),"Guest","Buyer","guest@example.test"),new(2030,2,1,0,0,0,TimeSpan.Zero)),fixture.Admin);
            var adapter=(ISalesDocumentDelivery)ActivatorUtilities.CreateInstance(sp,typeof(Program).Assembly.GetType("Api.Integrations.SalesDocumentDelivery",true)!);
            var request=new SendSalesDocumentRequest(Guid.NewGuid());
            var queued=await Documents(sp,adapter).SendQuoteAsync(quote.Id,request,fixture.Admin);
            Assert.True(queued!.Queued);Assert.False(queued.Accepted);Assert.Null(queued.ProviderReference);
            var quotes=sp.GetRequiredService<SalesQuoteService>();
            Assert.Equal(QuoteStatus.Issuing,(await quotes.GetAsync(quote.Id,fixture.Admin))!.Status);
            await Assert.ThrowsAsync<SalesConflictException>(()=>quotes.ChangeStatusAsync(quote.Id,new(QuoteStatus.Accepted),fixture.Admin));
            var provider=new PausedEmailOutcome(acceptProvider);
            await using var worker=fixture.Services.CreateAsyncScope();
            var processorType=typeof(CompanyHQ.Email.Services.EmailService).Assembly.GetType("CompanyHQ.Email.Services.EmailQueueProcessor",true)!;
            var processor=ActivatorUtilities.CreateInstance(worker.ServiceProvider,processorType,provider);
            var processing=(Task<int>)processorType.GetMethod("ProcessBatchAsync")!.Invoke(processor,[CancellationToken.None])!;
            await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False((await Documents(sp,adapter).SendQuoteAsync(quote.Id,request,fixture.Admin))!.Accepted);
            provider.Release.SetResult();await processing;
            var outcome=await Documents(sp,adapter).SendQuoteAsync(quote.Id,request,fixture.Admin);
            Assert.Equal(acceptProvider,outcome!.Accepted);Assert.False(outcome.Queued);
            Assert.Equal(acceptProvider?QuoteStatus.Sent:QuoteStatus.Issuing,(await quotes.GetAsync(quote.Id,fixture.Admin))!.Status);
            Assert.Equal(1,provider.Calls);
            if(acceptProvider){await quotes.ChangeStatusAsync(quote.Id,new(QuoteStatus.Accepted),fixture.Admin);Assert.Equal(100,(await quotes.ConvertAsync(quote.Id,fixture.Admin))!.TotalAmount);}
            else await Assert.ThrowsAsync<SalesConflictException>(()=>quotes.ConvertAsync(quote.Id,fixture.Admin));
        }
    }

    private sealed class PausedEmailOutcome(bool accepted) : CompanyHQ.Email.Contracts.Ports.IEmailProvider
    {
        public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls {get;private set;}
        public async Task<CompanyHQ.Email.Contracts.Ports.EmailSendResult> SendAsync(CompanyHQ.Email.Contracts.Ports.EmailMessage message,CancellationToken cancellationToken=default)
        {
            Calls++;Entered.TrySetResult();await Release.Task.WaitAsync(cancellationToken);
            return accepted?CompanyHQ.Email.Contracts.Ports.EmailSendResult.Sent("isolated-provider-id"):CompanyHQ.Email.Contracts.Ports.EmailSendResult.Failed(false,"isolated rejection");
        }
    }
    [Fact]
    public async Task QuoteDelivery_FreezesBeforeProviderAndRetriesTheSameRevision()
    {
        await using var setup = fixture.Services.CreateAsyncScope();
        var catalog = setup.ServiceProvider.GetRequiredService<SalesCatalogService>();
        var product = await catalog.SaveAsync(null, Definition("Issued", profit: 0), fixture.Admin);
        var request = new SaveQuoteRequest(new(new([new(Guid.NewGuid(), product.Id, product.Variants[0].Id)]),
            "Guest", "Buyer", "guest@example.test"), new(2030, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var quote = await setup.ServiceProvider.GetRequiredService<SalesQuoteService>().SaveAsync(null, request, fixture.Admin);
        var provider = new PausedDocumentDelivery();
        await using var sendScope = fixture.Services.CreateAsyncScope();
        var sender = Documents(sendScope.ServiceProvider, provider);
        var sending = sender.SendQuoteAsync(quote.Id, new(Guid.NewGuid()), fixture.Admin);
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var editScope = fixture.Services.CreateAsyncScope())
        {
            var quotes = editScope.ServiceProvider.GetRequiredService<SalesQuoteService>();
            var replacement = request with { Order = request.Order with { Sale = new([new(Guid.NewGuid(), product.Id, product.Variants[0].Id, 2)]) } };
            await Assert.ThrowsAsync<SalesConflictException>(() => quotes.SaveAsync(quote.Id, replacement, fixture.Admin));
            await Assert.ThrowsAsync<SalesConflictException>(() => quotes.ChangeStatusAsync(quote.Id, new(QuoteStatus.Accepted), fixture.Admin));
        }
        provider.Release.SetResult(new(false, null, "temporary failure"));
        Assert.False((await sending)!.Accepted);
        await using var retryScope = fixture.Services.CreateAsyncScope();
        var retryProvider = new PausedDocumentDelivery();
        retryProvider.Release.SetResult(new(true, "accepted", null));
        Assert.True((await Documents(retryScope.ServiceProvider, retryProvider).SendQuoteAsync(quote.Id, new(Guid.NewGuid()), fixture.Admin))!.Accepted);
        Assert.Equal(JsonSerializer.Serialize(provider.Document, SalesRules.Json), JsonSerializer.Serialize(retryProvider.Document, SalesRules.Json));
        var retryQuotes = retryScope.ServiceProvider.GetRequiredService<SalesQuoteService>();
        await retryQuotes.ChangeStatusAsync(quote.Id, new(QuoteStatus.Accepted), fixture.Admin);
        Assert.Equal(100, (await retryQuotes.ConvertAsync(quote.Id, fixture.Admin))!.TotalAmount);
    }

    [Fact]
    public async Task UpsellAcceptance_RechecksTotalAndRouteLimitsBeforeMutation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var catalog = sp.GetRequiredService<SalesCatalogService>();
        var promotions = sp.GetRequiredService<SalesPromotionService>();
        await promotions.SaveSettingsAsync(new(0), fixture.Admin);
        var parent = await catalog.SaveAsync(null, Definition("Limited parent", profit: 0), fixture.Admin);
        var child = await catalog.SaveAsync(null, Definition("Limited child", profit: 0), fixture.Admin);
        var offer = await promotions.SaveUpsellAsync(null, new(parent.Id, child.Id), fixture.Admin);
        var route = new TestSalesShipping();
        var checkout = new SalesCheckoutService(sp.GetRequiredService<SalesBasketPricing>(), catalog, sp.GetRequiredService<SalesOptions>(), route);
        var orders = new SalesOrderService(sp.GetRequiredService<IPersistence>(), catalog, checkout,
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<SalesOptions>(), shipping: route);
        foreach (var distanceLimit in new[] { false, true })
        {
            var method = await promotions.SavePaymentMethodAsync(null,
                new("Limited", MaxOrderTotal: distanceLimit ? null : 150, MaxDistanceKm: distanceLimit ? 1 : null), fixture.Admin);
            var line = Guid.NewGuid();
            var receipt = await orders.PlaceAsync(new(new([new(line, parent.Id, parent.Variants[0].Id)],
                ShippingAddress: new(Latitude: 0, Longitude: 0)), "Guest", "Buyer", "guest@example.test", PaymentMethodId: method.Id), null);
            var accept = new AcceptUpsellRequest(offer.Id, line, Guid.NewGuid());
            route.AdditionDistance = distanceLimit ? 2 : 0;
            await Assert.ThrowsAsync<ArgumentException>(() => orders.AcceptUnpaidAsync(receipt.OrderId, accept, null, receipt.GuestAccessToken));
            var persisted = await orders.GetAsync(receipt.OrderId, null, receipt.GuestAccessToken);
            Assert.Equal(100, persisted!.TotalAmount);
            Assert.Single(persisted.Lines);
            Assert.False(await sp.GetRequiredService<IPersistence>().Query<OrderUpsellAcceptance>().AnyAsync(x => x.OrderId == receipt.OrderId));
        }
    }

    [Fact]
    public async Task ShippingDiscount_BridgeRetainsDiscountedUpsellBasketIdentity()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var catalog = sp.GetRequiredService<SalesCatalogService>();
        var promotions = sp.GetRequiredService<SalesPromotionService>();
        await promotions.SaveSettingsAsync(new(0), fixture.Admin);
        var parent = await catalog.SaveAsync(null, Definition("Shipping parent", profit: 0), fixture.Admin);
        var child = await catalog.SaveAsync(null, Definition("Shipping child", profit: 0), fixture.Admin);
        await promotions.SaveUpsellAsync(null, new(parent.Id, child.Id, Value: 50), fixture.Admin);
        var shipping = sp.GetRequiredService<IShippingService>();
        var origin = await shipping.CreateOriginAsync(new("Basket origin", new(Latitude: 0, Longitude: 0)), fixture.Admin);
        await shipping.CreateMethodAsync(new("Basket method", 9, ProductIds: [parent.Id, child.Id], FulfillmentOriginIds: [origin.Id]), fixture.Admin);
        await promotions.SaveCouponAsync(null, new("THRESHOLD" + parent.Id.ToString("N"), DiscountType.FreeShipping, 0,
            IsAutomatic: true, AppliesToShipping: true, ProductIds: [parent.Id], MinimumPurchase: 180), fixture.Admin);
        var parentId = Guid.NewGuid();
        var basket = new PriceSaleRequest([new(parentId, parent.Id, parent.Variants[0].Id, BundleInstanceId: Guid.NewGuid()),
            new(Guid.NewGuid(), child.Id, child.Variants[0].Id, UpsellParentLineId: parentId)], new(Latitude: 0, Longitude: 0));
        var price = await sp.GetRequiredService<SalesCheckoutService>().PriceAsync(basket);
        Assert.Equal(150, price.Lines.Sum(x => x.FinalTotalPrice));
        Assert.Equal(9, price.ShippingCost);
        Assert.Equal(159, price.TotalAmount);
        Assert.DoesNotContain(price.Coupons, x => x.AppliesToShipping);
    }

    [Fact]
    public async Task Catalog_OptionDependencyRoundtripAndPublicRelationsAreBounded()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
        var related = await catalog.SaveAsync(null, Definition("Related"), fixture.Admin);
        var hidden = await catalog.SaveAsync(null, Definition("Hidden related") with { IsVisible = false }, fixture.Admin);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var definition = Definition("Dependencies") with
        {
            Options = [new("Size", ["Small"], first), new("Color", ["Oak"], second, [first])],
            Variants = [new(Guid.NewGuid(), "dependency", 50, Options: new Dictionary<string, string> { ["Size"] = "Small", ["Color"] = "Oak" })],
            RelatedProductIds = [related.Id, hidden.Id]
        };
        var saved = await catalog.SaveAsync(null, definition, fixture.Admin);
        var client = await catalog.GetAsync(saved.Slug);
        Assert.Equal(first, client!.Options[0].Id);
        Assert.Equal(new[] { first }, client.Options[1].DependencyOptionIds);
        Assert.Equal(related.Id, Assert.Single(client.RelatedProducts!).Id);
        Assert.DoesNotContain("cost", JsonSerializer.Serialize(client.RelatedProducts, SalesRules.Json), StringComparison.OrdinalIgnoreCase);
        var replaced = await catalog.SaveAsync(saved.Id, definition with
            { Options = definition.Options.Select(x => x with { Id = Guid.Empty }).ToArray() }, fixture.Admin);
        Assert.Equal(new[] { first, second }, replaced.Options.Select(x => x.Id));
        foreach (var dependencies in new[] { new[] { second }, new[] { Guid.NewGuid() }, new[] { first, first } })
            await Assert.ThrowsAsync<ArgumentException>(() => catalog.SaveAsync(saved.Id,
                definition with { Options = [definition.Options[0], definition.Options[1] with { DependencyOptionIds = dependencies }] }, fixture.Admin));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SaveAsync(saved.Id,
            definition with { Options = [definition.Options[0] with { DependencyOptionIds = [second] }, definition.Options[1]] }, fixture.Admin));
    }

    [Fact]
    public async Task Catalog_PriceBoundsUseDisplayedMinimumAndOptionsControlSorting()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
        var category = await catalog.SaveCategoryAsync(null, new("Price bounds", "bounds-" + Guid.NewGuid().ToString("N")), fixture.Admin);
        var definition = Definition("Two prices") with
        {
            CategoryIds = [category.Id], Options = [new("Size", ["Small", "Large"])],
            Variants = [new(Guid.NewGuid(), "small", 50, Options: new Dictionary<string, string> { ["Size"] = "Small" }),
                new(Guid.NewGuid(), "large", 150, Options: new Dictionary<string, string> { ["Size"] = "Large" })]
        };
        var product = await catalog.SaveAsync(null, definition, fixture.Admin);
        var other = await catalog.SaveAsync(null, definition with { Name = "Other prices", Slug = "other-" + Guid.NewGuid().ToString("N"),
            Variants = definition.Variants.Select(v => v with { Id = Guid.NewGuid(), Cost = 100 }).ToArray() }, fixture.Admin);
        var page = await catalog.ListAsync(Page, Search, new(CategoryId: category.Id, MinPrice: 100));
        Assert.DoesNotContain(page.Items, x => x.Id == product.Id);
        Assert.Contains(page.Items, x => x.Id == other.Id);
        page = await catalog.ListAsync(Page, Search, new(CategoryId: category.Id, MaxPrice: 100, Options: new() { ["Size"] = "Large" }));
        Assert.Equal(50, page.Items.Single(x => x.Id == product.Id).Price.EffectivePrice);
        page = await catalog.ListAsync(Page, Search, new(CategoryId: category.Id, Options: new() { ["Size"] = "Large" }, Sort: "price-asc"));
        Assert.Equal(new[] { other.Id, product.Id }, page.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task Shipping_RejectsMalformedPortCoordinatesAndKeepsValidAlternatives()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var catalogPort = new TestShippingCatalog();
        var geoPort = new TestGeocoder();
        var service = new ShippingService(sp.GetRequiredService<IPersistence>(), sp.GetRequiredService<IUserAuthorizationService>(), catalogPort, geoPort);
        var productId = Guid.NewGuid();
        var method = await service.CreateMethodAsync(new("Coordinate boundary", 7, ProductIds: [productId], MaxDistanceKm: 1), fixture.Admin);
        var request = new ShippingQuoteRequest(new(Latitude: 0, Longitude: 0), [new(productId)]);
        foreach (var bad in new (double? Lat, double? Lon)[] { (double.NaN, 0), (0, double.PositiveInfinity), (91, 0), (0, -181), (0, null) })
        {
            catalogPort.Origins = [new(Guid.NewGuid(), "Invalid", bad.Lat, bad.Lon)];
            var result = await service.QuoteAsync(request);
            Assert.False(result.IsAvailable);
            Assert.Contains(result.Failures, x => x.Code == ShippingQuoteFailureCodes.OriginUnresolved);
            catalogPort.Origins = [.. catalogPort.Origins, new(Guid.NewGuid(), "Valid", 0, 0)];
            result = await service.QuoteAsync(request);
            Assert.True(result.IsAvailable);
            Assert.Equal("Valid", Assert.Single(result.RouteLegs).OriginName);
        }
        foreach (var coords in new[] { new ShippingCoordinates(double.NaN, 0), new(double.PositiveInfinity, 0), new(0, 181), new(-91, 0) })
        {
            geoPort.Coordinates = coords;
            var result = await service.QuoteAsync(request with { Address = new(City: "Provider resolved") });
            Assert.False(result.IsAvailable);
            Assert.Contains(result.Failures, x => x.Code == ShippingQuoteFailureCodes.DestinationUnresolved);
            Assert.Null(result.DestinationLatitude);
        }
    }

    private static SalesDocumentService Documents(IServiceProvider sp, ISalesDocumentDelivery provider) => new(
        sp.GetRequiredService<SalesCatalogService>(), sp.GetRequiredService<SalesOrderService>(), sp.GetRequiredService<SalesQuoteService>(), provider);

    [Fact]
    public async Task StaffOrders_NegotiatedPricingInvoiceUniquenessAndImmutableRevisions()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var catalog = sp.GetRequiredService<SalesCatalogService>();
        var promotions = sp.GetRequiredService<SalesPromotionService>();
        var orders = sp.GetRequiredService<SalesOrderService>();
        var definition = Definition("Staff pricing");
        var product = await catalog.SaveAsync(null, definition, fixture.Admin);
        var shipping = sp.GetRequiredService<IShippingService>();
        var origin = await shipping.CreateOriginAsync(new("Staff origin", new(Latitude: 0, Longitude: 0)), fixture.Admin);
        await shipping.CreateMethodAsync(new("Staff delivery", 10, ProductIds: [product.Id], FulfillmentOriginIds: [origin.Id]), fixture.Admin);
        await promotions.SaveCouponAsync(null, new("STAFFSHIP" + product.Id.ToString("N"), DiscountType.FreeShipping, 0,
            IsAutomatic: true, AppliesToShipping: true, ProductIds: [product.Id]), fixture.Admin);
        var lineId = Guid.NewGuid();
        var request = new SaveStaffOrderRequest(new(new([new(lineId, product.Id, product.Variants[0].Id)], new(Latitude: 0, Longitude: 0)),
            "Guest", "Buyer", "guest@example.test"), new([new(lineId, DiscountType.Fixed, 20)], DiscountType.Fixed, 10, 12, 5), InvoiceNumber: 910001);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => orders.PriceStaffAsync(request, fixture.Other));
        var price = await orders.PriceStaffAsync(request, fixture.Admin);
        Assert.Equal(187, price.TotalAmount);
        Assert.Equal(12, price.ShippingCost);
        Assert.Equal(0, price.ShippingDiscount);
        Assert.Equal(5, price.TaxAmount);
        Assert.DoesNotContain(price.Coupons, x => x.AppliesToShipping);
        var receipt = await orders.PlaceStaffAsync(request, fixture.Admin);
        Assert.Equal(910001, receipt.InvoiceNumber);
        Assert.False((await orders.InvoiceAvailabilityAsync(910001, null, fixture.Admin)).IsAvailable);
        await Assert.ThrowsAsync<SalesConflictException>(() => orders.PlaceStaffAsync(request, fixture.Admin));
        var db = sp.GetRequiredService<IPersistence>();
        var initial = await db.Query<SalesOrder>().SingleAsync(x => x.Id == receipt.OrderId);
        await catalog.SaveAsync(product.Id, definition with { Variants = [definition.Variants[0] with { Cost = 200 }] }, fixture.Admin);
        var edited = await orders.UpdateStaffAsync(receipt.OrderId, request with { InvoiceNumber = 910002 }, fixture.Admin);
        Assert.Equal(387, edited!.TotalAmount);
        Assert.Equal(910002, edited.InvoiceNumber);
        var retained = await db.Query<SalesOrder>().SingleAsync(x => x.Id == receipt.OrderId);
        Assert.Equal(initial.SnapshotJson, retained.SnapshotJson);
        Assert.Equal(1, retained.RevisionNumber);
        Assert.Single(await db.Query<SalesOrderRevision>().Where(x => x.OrderId == receipt.OrderId).ToListAsync());
        await orders.ChangeStatusAsync(receipt.OrderId, new(OrderStatus.Processing), fixture.Admin);
        await Assert.ThrowsAsync<SalesConflictException>(() => orders.UpdateStaffAsync(receipt.OrderId, request, fixture.Admin));
    }

    [Fact]
    public async Task Catalog_ProjectionBatchesResumeBeyond5000WithoutBlockingUnrelatedPage()
    {
        Guid visibleId;
        Guid categoryId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var catalog = sp.GetRequiredService<SalesCatalogService>();
            var category = await catalog.SaveCategoryAsync(null, new("Projection scope", "projection-" + Guid.NewGuid().ToString("N")), fixture.Admin);
            categoryId = category.Id;
            var visible = await catalog.SaveAsync(null, Definition("Visible projection", 75, 0) with { CategoryIds = [categoryId] }, fixture.Admin);
            visibleId = visible.Id;
            var db = sp.GetRequiredService<PostgresDbContext>();
            for (var index = 0; index < 5001; index++)
            {
                var definition = Definition("Hidden projection " + index, 50, 0) with { IsVisible = false, Currency = "ZAR", CategoryIds = [categoryId] };
                var product = new Product { Id = Guid.CreateVersion7(), Name = definition.Name, Slug = definition.Slug,
                    DefinitionJson = SalesRules.Write(definition), IsVisible = false, CategoryIds = [categoryId] };
                db.Add(product);
                db.Add(new CatalogVariant { Id = definition.Variants[0].Id, ProductId = product.Id, Sku = definition.Variants[0].Sku });
            }
            await db.SaveChangesAsync();
            var page = await catalog.ListAsync(new() { PageSize = 1 }, Search, new(CategoryId: categoryId));
            Assert.Equal(visibleId, Assert.Single(page.Items).Id);
            page = await catalog.ListAsync(Page, Search, new(CategoryId: categoryId, MinPrice: 70));
            Assert.Equal(visibleId, Assert.Single(page.Items).Id);
            Assert.Equal(75, page.Items[0].Price.EffectivePrice);
            await Assert.ThrowsAsync<SalesCapabilityUnavailableException>(() => catalog.ListAsync(Page, Search,
                new(CategoryId: categoryId, MinPrice: 40), ResponseAudience.Admin, fixture.Admin));
        }
        // Recreate the scope between batches to prove progress is durable, not a process cache.
        int firstCount;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
            var db = scope.ServiceProvider.GetRequiredService<IPersistence>();
            Assert.True(await catalog.RefreshPriceBatchAsync(default, db.Query<Product>().Where(x => !x.IsVisible)));
            firstCount = await scope.ServiceProvider.GetRequiredService<IPersistence>().Query<Product>().CountAsync(x => !x.IsVisible && x.PriceGeneration > 0);
            Assert.InRange(firstCount, 32, 64);
        }
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IPersistence>();
            Assert.True(await scope.ServiceProvider.GetRequiredService<SalesCatalogService>().RefreshPriceBatchAsync(default,
                db.Query<Product>().Where(x => !x.IsVisible)));
            var count = await scope.ServiceProvider.GetRequiredService<IPersistence>().Query<Product>().CountAsync(x => !x.IsVisible && x.PriceGeneration > 0);
            Assert.InRange(count - firstCount, 1, 32);
        }
    }

    [Fact]
    public async Task Migrations_UpgradeExistingDatabasePreservesRowsAndMatchesCurrentModel()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        var databaseName = "sales_upgrade_" + Guid.NewGuid().ToString("N");
        await using (var adminConnection = new Npgsql.NpgsqlConnection(connection.ConnectionString))
        {
            await adminConnection.OpenAsync();
            await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE {databaseName}", adminConnection);
            await create.ExecuteNonQueryAsync();
        }
        connection.Database = databaseName;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Postgres:ConnectionString"] = connection.ConnectionString, ["Postgres:AutoMigrate"] = "false", ["Postgres:AutoSeed"] = "false" }).Build();
        await using var db = PostgresServiceCollectionExtensions.CreateDesignTimeContext(configuration, typeof(Program).Assembly);
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20261004193337_AddPublishingAndConversationContexts");
        var user = new User { Email = "preserved@example.test", PasswordHash = "test-only", Role = UserRole.Client };
        db.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await migrator.MigrateAsync("20261013173418_AddSalesAndShipping");
        var legacyRequest = """{"version":1,"kind":"order.request","data":{"customerFirstName":"Legacy","customerEmail":"legacy@example.test"}}""";
        var emptySnapshot = "{}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sales_order (id, number, status, payment_status, request_json, snapshot_json,
                total_amount, currency, item_count, created_at, updated_at)
            SELECT gen_random_uuid(), n, 0, 0,
                {legacyRequest}::jsonb,
                {emptySnapshot}::jsonb, 100, 'USD', 1, now(), now() FROM generate_series(1,2) AS n;
            """);
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(user.Email, (await db.Set<User>().SingleAsync(x => x.Id == user.Id)).Email);
        Assert.Empty(await db.Set<SalesOrderRevision>().ToListAsync());
        Assert.Empty(await db.Set<InvoiceDeliveryRequest>().ToListAsync());
        Assert.Empty(await db.Set<ProductReview>().ToListAsync());
        Assert.Empty(await db.Set<CatalogPriceProjection>().ToListAsync());
        Assert.Empty(await db.Set<SalesQuote>().ToListAsync());
        var orders = await db.Set<SalesOrder>().OrderBy(x => x.Number).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, orders.Select(x => x.InvoiceNumber));
        Assert.Equal(new[] { "INV-0001", "INV-0002" }, orders.Select(x => x.InvoiceReference));
        Assert.All(orders, x => Assert.Equal("legacy@example.test", x.CustomerEmail));
    }

    private sealed class PausedDocumentDelivery : ISalesDocumentDelivery
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<SalesDocumentDeliveryResult> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SalesDocument? Document { get; private set; }
        public async Task<SalesDocumentDeliveryResult> DeliverAsync(SalesDocument document, CancellationToken cancellationToken = default)
        { Document = document; Entered.SetResult(); return await Release.Task.WaitAsync(cancellationToken); }
    }

    private sealed class TestSalesShipping : ISalesShipping
    {
        public double AdditionDistance { get; set; }
        public Task<SalesShippingQuote> QuoteAsync(SalesAddressDto address, IReadOnlyList<SalesLineRequest> lines,
            IReadOnlyList<string> couponCodes, CancellationToken cancellationToken = default) => Task.FromResult(new SalesShippingQuote(
                true, null, null, "Test route", null, null, 0, 0, 0, [],
                [new(Guid.Empty, "Test route", Guid.Empty, "Test origin", 0, lines.Count > 1 ? AdditionDistance : 0, 0, 0, lines.Select(x => x.ProductId).ToArray())], 0, 0));
    }

    private sealed class TestShippingCatalog : IShippingCatalog
    {
        public IReadOnlyList<ShippingCatalogOrigin> Origins { get; set; } = [];
        public Task<IReadOnlyList<ShippingCatalogProduct>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShippingCatalogProduct>>(productIds.Select(x => new ShippingCatalogProduct(x, "Port product", Origins)).ToArray());
    }

    private sealed class TestGeocoder : IShippingGeocoder
    {
        public ShippingCoordinates? Coordinates { get; set; }
        public Task<ShippingCoordinates?> LocateAsync(ShippingAddressDto address, CancellationToken cancellationToken = default) => Task.FromResult(Coordinates);
    }
    [Fact]
    public async Task CatalogCheckout_PersistedFiltersTotalsOwnershipAndImmutableCosts()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var sp=scope.ServiceProvider;var catalog=sp.GetRequiredService<SalesCatalogService>();var promos=sp.GetRequiredService<SalesPromotionService>();var orders=sp.GetRequiredService<SalesOrderService>();var db=sp.GetRequiredService<IPersistence>();var admin=fixture.Admin;
        var options=new ProductOptionDto[]{new("Color",["Oak","White"]),new("Size",["Small","Large"])};
        var def=Definition("Options") with {Options=options,Variants=[new(Guid.NewGuid(),"oak-small",100,100,new Dictionary<string,string>{{"Color","Oak"},{"Size","Small"}}),new(Guid.NewGuid(),"white-large",100,100,new Dictionary<string,string>{{"Color","White"},{"Size","Large"}})]};
        var product=await catalog.SaveAsync(null,def,admin);
        var page=await catalog.ListAsync(Page,Search,new(Options:new(){{"Color","oak"},{"Size","large"}}));Assert.DoesNotContain(page.Items,x=>x.Id==product.Id);
        page=await catalog.ListAsync(Page,Search,new(Options:new(){{"Color","oak"},{"Size","small"}}));Assert.Contains(page.Items,x=>x.Id==product.Id);
        var hidden=await catalog.SaveAsync(null,Definition("Hidden") with {IsVisible=false},admin);Assert.Null(await catalog.GetAsync(hidden.Slug));
        var category=await catalog.SaveCategoryAsync(null,new("Furniture","c-"+Guid.NewGuid().ToString("N")),admin);
        var pricedDef=Definition("Checkout") with {CategoryIds=[category.Id]};var pricedProduct=await catalog.SaveAsync(null,pricedDef,admin);
        await promos.SaveCouponAsync(null,new("AUTO"+pricedProduct.Id.ToString("N"),DiscountType.Percentage,10,IsAutomatic:true,ProductIds:[product.Id],CategoryIds:[category.Id]),admin);
        var code="FIX"+pricedProduct.Id.ToString("N");await promos.SaveCouponAsync(null,new(code,DiscountType.Fixed,30),admin);
        var sale=new PriceSaleRequest([new(Guid.NewGuid(),pricedProduct.Id,pricedDef.Variants[0].Id)],CouponCodes:[code]);
        var quote=await sp.GetRequiredService<SalesCheckoutService>().PriceAsync(sale);Assert.Equal(150,quote.TotalAmount);Assert.Equal(50,quote.DiscountAmount);Assert.Equal(30,quote.OrderCouponDiscount);
        var request=new PlaceOrderRequest(sale,"Guest","Buyer","guest@example.test");var receipt=await orders.PlaceAsync(request,null);Assert.NotNull(receipt.GuestAccessToken);
        Assert.Null(await orders.GetAsync(receipt.OrderId,null,"wrong"));Assert.Null(await orders.GetAsync(receipt.OrderId,fixture.Other,null));
        var order=await orders.GetAsync(receipt.OrderId,null,receipt.GuestAccessToken);Assert.Equal(PaymentStatus.Unpaid,order!.PaymentStatus);Assert.Equal(OrderStatus.Pending,order.Status);Assert.Equal(150,order.TotalAmount);
        db.Clear();await catalog.SaveAsync(pricedProduct.Id,pricedDef with {Variants=[pricedDef.Variants[0] with {Cost=500}]},admin);db.Clear();
        var persisted=Assert.IsType<AdminOrderDto>(await orders.GetAsync(receipt.OrderId,admin,null));Assert.Equal(150,persisted.TotalAmount);Assert.Equal(100,Assert.IsType<AdminSalesLineDto>(persisted.Lines[0]).CostPriceUsed);
        var signed=await orders.PlaceAsync(request with {CustomerEmail="impersonation@example.test"},fixture.Other);Assert.Null(signed.GuestAccessToken);
        Assert.Equal("member@example.test",(await orders.GetAsync(signed.OrderId,fixture.Other,null))!.CustomerEmail);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>catalog.SaveAsync(null,Definition("Denied"),fixture.Other));
        var bundleDef=Definition("Bundle") with {Type=ProductType.Bundle,BundleComponents=[new(product.Id,2,10)]};var bundle=await catalog.SaveAsync(null,bundleDef,admin);
        Assert.Equal(360,bundle.Price.EffectivePrice);
        await Assert.ThrowsAsync<ArgumentException>(()=>catalog.SaveAsync(product.Id,def with {Type=ProductType.Bundle,BundleComponents=[new(bundle.Id)]},admin));
        Assert.False((await catalog.GetAsync(product.Slug))!.Type==ProductType.Bundle);
    }
    [Fact]
    public async Task Upsells_UnpaidReplaySyncSuppressionAndFinalization()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var sp=scope.ServiceProvider;var cat=sp.GetRequiredService<SalesCatalogService>();var promos=sp.GetRequiredService<SalesPromotionService>();var orders=sp.GetRequiredService<SalesOrderService>();var db=sp.GetRequiredService<IPersistence>();
        var category=await cat.SaveCategoryAsync(null,new("Upsells","u-"+Guid.NewGuid().ToString("N")),fixture.Admin);
        var parentDef=Definition("Parent") with {Options=[new("Color",["Oak"])],Variants=[new(Guid.NewGuid(),"parent",100,100,new Dictionary<string,string>{{"Color","Oak"}})]};var parent=await cat.SaveAsync(null,parentDef,fixture.Admin);
        var childDef=Definition("Child") with {CategoryIds=[category.Id],Options=[new("color",["oak","white"])],Variants=[new(Guid.NewGuid(),"white",100,100,new Dictionary<string,string>{{"color","white"}}),new(Guid.NewGuid(),"oak",100,100,new Dictionary<string,string>{{"color","oak"}})]};var child=await cat.SaveAsync(null,childDef,fixture.Admin);
        var incompatibleDef=Definition("Incompatible") with {Options=[new("Color",["White"])],Variants=[new(Guid.NewGuid(),"only-white",100,100,new Dictionary<string,string>{{"Color","White"}})]};var incompatible=await cat.SaveAsync(null,incompatibleDef,fixture.Admin);
        await promos.SaveUpsellAsync(null,new(parent.Id,child.Id,2,15),fixture.Admin);await promos.SaveUpsellAsync(null,new(parent.Id,incompatible.Id),fixture.Admin);
        await promos.SaveCouponAsync(null,new("UP"+child.Id.ToString("N"),DiscountType.Percentage,10,IsAutomatic:true,CategoryIds:[category.Id]),fixture.Admin);
        await promos.SaveSettingsAsync(new(5),fixture.Admin);
        var parentLine=Guid.NewGuid();var receipt=await orders.PlaceAsync(new(new([new(parentLine,parent.Id,parentDef.Variants[0].Id)]),"Guest","Buyer","guest@example.test"),null);db.Clear();
        var context=await orders.ContextAsync(receipt.OrderId,null,receipt.GuestAccessToken);Assert.Single(context!.Offers);var offer=context.Offers[0].Offer;Assert.Equal(childDef.Variants[1].Id,offer.VariantId);Assert.Equal(140,offer.FinalUnitPrice);Assert.Equal(280,offer.FinalLineTotal);
        var request=new AcceptUpsellRequest(offer.UpsellOfferId,parentLine,Guid.NewGuid());var result=await orders.AcceptUnpaidAsync(receipt.OrderId,request,null,receipt.GuestAccessToken);Assert.False(result!.IsPaid);Assert.Equal(280,result.AdditionalAmount);db.Clear();
        Assert.Equal(result,await orders.AcceptUnpaidAsync(receipt.OrderId,request,null,receipt.GuestAccessToken));
        await Assert.ThrowsAsync<ArgumentException>(()=>orders.AcceptUnpaidAsync(receipt.OrderId,request with {RequestId=Guid.NewGuid()},null,receipt.GuestAccessToken));
        var finalized=await orders.FinalizeAsync(receipt.OrderId,null,receipt.GuestAccessToken);Assert.Equal(PaymentStatus.Unpaid,finalized!.PaymentStatus);Assert.Equal(480,finalized.TotalAmount);Assert.True((await orders.ContextAsync(receipt.OrderId,null,receipt.GuestAccessToken))!.IsFinalized);
        Assert.Empty((await orders.ContextAsync(receipt.OrderId,null,receipt.GuestAccessToken))!.Offers);
    }
    [Fact]
    public async Task Shipping_ExactSharedPlanInvalidOriginsZonesAndCouponPriority()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var sp=scope.ServiceProvider;var cat=sp.GetRequiredService<SalesCatalogService>();var promos=sp.GetRequiredService<SalesPromotionService>();var shipping=sp.GetRequiredService<IShippingService>();
        var p1=await cat.SaveAsync(null,Definition("Ship one"),fixture.Admin);var p2=await cat.SaveAsync(null,Definition("Ship two"),fixture.Admin);
        var origin=await shipping.CreateOriginAsync(new("Valid",new(Latitude:0,Longitude:0)),fixture.Admin);
        var missing=await shipping.CreateOriginAsync(new("Missing coords",new(City:"Unresolved")),fixture.Admin);
        var shared=await shipping.CreateMethodAsync(new("Shared",6,ProductIds:[p1.Id,p2.Id],FulfillmentOriginIds:[origin.Id],MaxDistanceKm:1,DeliveryZones:[new("Postal",Rules:[new(PostalCodeRuleType.Range,"7400-7500")])]),fixture.Admin);
        await shipping.CreateMethodAsync(new("First",4,ProductIds:[p1.Id],FulfillmentOriginIds:[origin.Id]),fixture.Admin);
        await shipping.CreateMethodAsync(new("Second",4,ProductIds:[p2.Id],FulfillmentOriginIds:[origin.Id]),fixture.Admin);
        await shipping.CreateMethodAsync(new("Invalid alternative",1,ProductIds:[p1.Id,p2.Id],FulfillmentOriginIds:[missing.Id],MaxDistanceKm:1),fixture.Admin);
        var request=new ShippingQuoteRequest(new(PostalCode:"7500",Latitude:0,Longitude:0),[new(p1.Id,Quantity:10),new(p2.Id)]);
        var q=await shipping.QuoteAsync(request);Assert.True(q.IsAvailable);Assert.Equal(6,q.BaseShippingCost);Assert.Single(q.RouteLegs);Assert.Equal(shared.Id,q.RouteLegs[0].ShippingMethodId);Assert.Equal(2,q.RouteLegs[0].ProductIds.Count);
        q=await shipping.QuoteAsync(request with {Address=new(PostalCode:"7501",Latitude:0,Longitude:0)});Assert.Equal(8,q.BaseShippingCost);
        var percent="PCT"+p1.Id.ToString("N");var free="FREE"+p1.Id.ToString("N");
        await promos.SaveCouponAsync(null,new(percent,DiscountType.Percentage,80,IsAutomatic:true,AppliesToShipping:true,ProductIds:[p1.Id]),fixture.Admin);
        await promos.SaveCouponAsync(null,new(free,DiscountType.FreeShipping,0,AppliesToShipping:true,ProductIds:[p1.Id],MinimumPurchase:200,ShippingMaxDistanceKm:1),fixture.Admin);
        q=await shipping.QuoteAsync(request with {CouponCodes=[free]});Assert.Equal(0,q.TotalShippingCost);Assert.Equal(new[]{free.ToUpperInvariant()},q.AppliedShippingCouponCodes);
        await Assert.ThrowsAsync<ArgumentException>(()=>shipping.QuoteAsync(request with {Items=[new(p1.Id,Quantity:0)]}));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>shipping.CreateMethodAsync(new("Denied",1),fixture.Other));
        var remote=await shipping.QuoteAsync(new(new(Latitude:10,Longitude:10),[new(Guid.NewGuid())]));Assert.False(remote.IsAvailable);Assert.Contains(remote.Failures,x=>x.Code==ShippingQuoteFailureCodes.ProductNotFound);
    }
    [Fact]
    public async Task Quotes_ConversionCopiesAcceptedSnapshotOnce()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var sp=scope.ServiceProvider;var catalog=sp.GetRequiredService<SalesCatalogService>();var quotes=sp.GetRequiredService<SalesQuoteService>();var db=sp.GetRequiredService<IPersistence>();
        var definition=Definition("Quoted");var product=await catalog.SaveAsync(null,definition,fixture.Admin);
        var quote=await quotes.SaveAsync(null,new(new(new([new(Guid.NewGuid(),product.Id,definition.Variants[0].Id)]),"Guest","Buyer","guest@example.test"),new(2030,2,1,0,0,0,TimeSpan.Zero)),fixture.Admin);
        Assert.Equal(200,quote.TotalAmount);db.Clear();await quotes.ChangeStatusAsync(quote.Id,new(QuoteStatus.Sent),fixture.Admin);db.Clear();await quotes.ChangeStatusAsync(quote.Id,new(QuoteStatus.Accepted),fixture.Admin);db.Clear();
        await catalog.SaveAsync(product.Id,definition with {Variants=[definition.Variants[0] with {Cost=200}]},fixture.Admin);db.Clear();
        var receipt=await quotes.ConvertAsync(quote.Id,fixture.Admin);Assert.Equal(200,receipt!.TotalAmount);db.Clear();
        var retained=await quotes.GetAsync(quote.Id,fixture.Admin);Assert.Equal(200,retained!.TotalAmount);Assert.Equal(QuoteStatus.Converted,retained.Status);Assert.Equal(receipt.OrderId,retained.ConvertedToOrderId);
        await Assert.ThrowsAsync<SalesConflictException>(()=>quotes.ConvertAsync(quote.Id,fixture.Admin));
        var order=await sp.GetRequiredService<SalesOrderService>().GetAsync(receipt.OrderId,null,receipt.GuestAccessToken);Assert.Equal(PaymentStatus.Unpaid,order!.PaymentStatus);
    }
    [Fact]
    public async Task Orders_ConcurrentNumberingIsUnique()
    {
        Guid product;Guid variant;
        await using(var scope=fixture.Services.CreateAsyncScope()){var p=await scope.ServiceProvider.GetRequiredService<SalesCatalogService>().SaveAsync(null,Definition("Concurrent"),fixture.Admin);product=p.Id;variant=p.Variants[0].Id;}
        var receipts=await Task.WhenAll(Enumerable.Range(0,4).Select(async _=>{await using var scope=fixture.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<SalesOrderService>().PlaceAsync(new(new([new(Guid.NewGuid(),product,variant)]),"Guest","Buyer","guest@example.test"),null);}));
        Assert.Equal(4,receipts.Select(x=>x.InvoiceNumber).Distinct().Count());Assert.Equal(4,receipts.Select(x=>x.OrderId).Distinct().Count());
    }
    [Fact]
    public async Task McpProducts_ShowCostsOnlyToConnectionsThatManageSales()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var catalog=scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
        var name="Mcp cost "+Guid.NewGuid().ToString("N");
        var visible=await catalog.SaveAsync(null,Definition(name,cost:1234.56m,profit:40),fixture.Admin);
        var hidden=await catalog.SaveAsync(null,Definition(name+" hidden",cost:777,profit:40) with {IsVisible=false},fixture.Admin);
        // Serialized as the MCP SDK returns tool results, so the JSON is what an agent receives.
        async Task<(string List,string Get,string Hidden)> Call(Guid user,params string[] permissions)
        {
            var claims=new List<Claim>{new(AuthClaims.Subject,user.ToString())};claims.AddRange(permissions.Select(p=>new Claim(AuthClaims.Permission,p)));
            var tools=new SalesTools(catalog,new HttpContextAccessor{HttpContext=new DefaultHttpContext{User=new(new ClaimsIdentity(claims,"agent-key"))}});
            string Json<T>(T value)=>JsonSerializer.Serialize(value,McpJsonUtilities.DefaultOptions);
            return (Json(await tools.ListProducts(keyword:name,pageSize:100)),Json(await tools.GetProduct(visible.Slug)),Json(await tools.GetProduct(hidden.Slug)));
        }
        var staff=await Call(fixture.Admin,[..AccessPermissions.Client,AccessPermissions.ManageSales]);
        Assert.Contains("\"cost\":1234.56",staff.Get);Assert.Contains("\"profitPercentage\":40",staff.Get);
        Assert.Contains("\"cost\":1234.56",staff.List);Assert.Contains(hidden.Slug,staff.List);Assert.Contains("\"cost\":777",staff.Hidden);
        // A client, a staff key narrowed to the client role, and a forged claim on a client account
        // all get the customer shape: selling price only, and no hidden products.
        foreach(var (user,permissions) in new[]{(fixture.Other,AccessPermissions.Client.ToArray()),(fixture.Admin,AccessPermissions.Client.ToArray()),
            (fixture.Other,[..AccessPermissions.Client,AccessPermissions.ManageSales])})
        {
            var client=await Call(user,permissions);
            Assert.Contains(visible.Slug,client.List);Assert.Contains("\"effectivePrice\":1728.38",client.Get);
            foreach(var json in new[]{client.List,client.Get}){Assert.DoesNotContain("\"cost\"",json);Assert.DoesNotContain("profitPercentage",json);Assert.DoesNotContain("1234.56",json);}
            Assert.DoesNotContain(hidden.Slug,client.List);Assert.Equal("null",client.Hidden);
        }
        Assert.Contains(McpCapabilityCatalog.ListProducts,AccessPermissions.Client);Assert.Contains(McpCapabilityCatalog.GetProduct,AccessPermissions.Client);
    }
}
public sealed class SalesShippingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgis/postgis:17-3.5").Build();
    public ServiceProvider Services {get;private set;}=null!;
    public string ConnectionString => postgres.GetConnectionString();
    public Guid Admin {get;private set;} public Guid Other {get;private set;}
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Postgres:ConnectionString",postgres.GetConnectionString()},{"Postgres:AutoMigrate","false"},{"Postgres:AutoSeed","false"},{"Sales:DefaultCurrency","ZAR"},{"Sales:BusinessTimeZone","Africa/Johannesburg"}}).Build();
        var services=new ServiceCollection();services.AddLogging();services.AddSingleton<TimeProvider>(new FrozenClock());services.AddPostgres(configuration,typeof(Program).Assembly);services.AddUsers(configuration);services.AddSales(configuration);services.AddShipping();
        CompanyHQ.Email.Extensions.EmailServiceCollectionExtensions.AddEmail(services,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Email:From","test@example.test"}}).Build());
        foreach(var (port,name) in new[]{(typeof(ISalesShipping),"SalesShippingBridge"),(typeof(ISalesCustomer),"SalesCustomerBridge"),(typeof(IShippingCatalog),"ShippingCatalogBridge"),(typeof(IShippingDiscountPolicy),"ShippingDiscountBridge")})services.AddScoped(port,typeof(Program).Assembly.GetType("Api.Integrations."+name,true)!);
        Services=services.BuildServiceProvider();await using var scope=Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PostgresDbContext>();await db.Database.MigrateAsync();
        var admin=new User{Email="admin@example.test",PasswordHash="test-only",Role=UserRole.Admin};var other=new User{Email="member@example.test",PasswordHash="test-only",Role=UserRole.Client,FirstName="Member",LastName="Buyer"};db.AddRange(admin,other);await db.SaveChangesAsync();Admin=admin.Id;Other=other.Id;
    }
    public async Task DisposeAsync(){if(Services is not null)await Services.DisposeAsync();await postgres.DisposeAsync();}
    private sealed class FrozenClock : TimeProvider {public override DateTimeOffset GetUtcNow()=>new(2030,1,1,22,1,0,TimeSpan.Zero);}
}
