using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Services;
using CompanyHQ.Users.Extensions;
namespace CompanyHQ.Sales.Controllers;

[ApiController,Route("api/sales"),Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public sealed class SalesController(SalesCatalogService catalog,SalesPromotionService promotions,SalesBasketPricing basket,SalesCheckoutService checkout,SalesOrderService orders,SalesQuoteService quotes,SalesDocumentService documents,SalesProfitScenarioService scenarios) : ControllerBase
{
    private Guid? Actor=>User.GetUserId();
    private string? Token=>Request.Headers["X-Order-Access-Token"].FirstOrDefault();
    private async Task<ActionResult<T>> Run<T>(Func<Task<T?>> work,string? created=null)
    {
        try
        {
            var result=await work();
            if(result is null)return NotFound();
            if(created is null)return Ok(result);
            var location=result switch
            {
                AdminProductDto p=>$"/api/sales/products/{p.Slug}",
                ClientCategoryDto c=>$"/api/sales/categories/{c.Id}",
                AdminSupplierDto x=>$"/api/sales/suppliers/{x.Id}",
                AdminCouponDto x=>$"/api/sales/coupons/{x.Id}",
                AdminUpsellOfferDto x=>$"/api/sales/upsell-offers/{x.Id}",
                AdminPaymentMethodDto x=>$"/api/sales/payment-methods/{x.Id}",
                AdminQuoteDto x=>$"/api/sales/quotes/{x.Id}",
                OrderReceiptDto x=>$"/api/sales/orders/{x.OrderId}",
                _=>created
            };
            return Created(location,result);
        }
        catch(SalesCapabilityUnavailableException e){Response.Headers.RetryAfter="5";return StatusCode(503,new ProblemDetails{Title=e.Message,Status=503});}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(KeyNotFoundException){return NotFound();}
        catch(ArgumentException e){return BadRequest(new ProblemDetails{Title="Invalid sales request.",Detail=e.Message,Status=400});}
        catch(SalesConflictException e){return Conflict(new ProblemDetails{Title="Order changed.",Detail=e.Message,Status=409});}
        catch(UniqueConstraintException){return Conflict(new ProblemDetails{Title="A unique identifier is already in use.",Status=409});}
    }
    private async Task<IActionResult> Delete(Func<Task<bool>> action)
    {try{return await action()?NoContent():NotFound();}catch(UnauthorizedAccessException){return Forbid();}catch(ArgumentException e){return BadRequest(new ProblemDetails{Title=e.Message,Status=400});}catch(SalesConflictException e){return Conflict(new ProblemDetails{Title=e.Message,Status=409});}}

    /// <summary>Visible catalog. All selected option filters must match one variant. Managers may read inactive catalog and costs.</summary>
    [HttpGet("products"),AllowAnonymous]
    [McpAction(McpCapabilityCatalog.ListProducts,McpCapabilityCatalog.ListProducts,
        "List catalog products with their selling prices. A connection whose role grants sales.manage also gets hidden products and each variant's cost and markup; anyone else sees visible products only, with no cost data. Match a keyword against one named field, or every text field when none is named; page with page and pageSize.")]
    [ProducesResponseType<PagedResult<ClientProductDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<ClientProductDto>>> Products([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,[FromQuery] ProductFilterRequest filter,CancellationToken ct)=>
        Run<PagedResult<ClientProductDto>>(async()=>await catalog.ListAsync(paging,search,filter,User.Audience(AccessPermissions.ManageSales),Actor,ct));
    [HttpGet("products/{slug}"),AllowAnonymous]
    [McpAction(McpCapabilityCatalog.GetProduct,McpCapabilityCatalog.GetProduct,
        "Get one catalog product by stable slug, with its variants and selling prices. A connection whose role grants sales.manage also gets hidden products and each variant's cost and markup; anyone else gets a visible product with no cost data, or nothing.")]
    [ProducesResponseType<ClientProductDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientProductDto>> Product(string slug,CancellationToken ct)=>Run(()=>catalog.GetAsync(slug,User.Audience(AccessPermissions.ManageSales),Actor,ct));
    [HttpPost("products"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminProductDto>> CreateProduct(SaveProductRequest request,CancellationToken ct)=>await Run<AdminProductDto>(async()=>await catalog.SaveAsync(null,request,Actor,ct),$"/api/sales/products/{request.Slug}");
    [HttpPut("products/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminProductDto>> UpdateProduct(Guid id,SaveProductRequest request,CancellationToken ct)=>Run<AdminProductDto>(async()=>await catalog.SaveAsync(id,request,Actor,ct));
    [HttpDelete("products/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteProduct(Guid id,CancellationToken ct)=>Delete(()=>catalog.DeleteAsync(id,Actor,ct));

    [HttpGet("categories"),AllowAnonymous]
    [ProducesResponseType<PagedResult<ClientCategoryDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<ClientCategoryDto>>> Categories([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,CancellationToken ct)=>Run<PagedResult<ClientCategoryDto>>(async()=>await catalog.CategoriesAsync(paging,search,ct));
    [HttpGet("categories/{id:guid}"),AllowAnonymous]
    [ProducesResponseType<ClientCategoryDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientCategoryDto>> Category(Guid id,CancellationToken ct)=>Run(()=>catalog.GetCategoryAsync(id,ct));
    [HttpPost("categories"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<ClientCategoryDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<ClientCategoryDto>> CreateCategory(SaveCategoryRequest request,CancellationToken ct)=>Run<ClientCategoryDto>(async()=>await catalog.SaveCategoryAsync(null,request,Actor,ct),"/api/sales/categories");
    [HttpPut("categories/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<ClientCategoryDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientCategoryDto>> UpdateCategory(Guid id,SaveCategoryRequest request,CancellationToken ct)=>Run<ClientCategoryDto>(async()=>await catalog.SaveCategoryAsync(id,request,Actor,ct));
    [HttpDelete("categories/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteCategory(Guid id,CancellationToken ct)=>Delete(()=>catalog.DeleteCategoryAsync(id,Actor,ct));

    [HttpGet("suppliers"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<PagedResult<AdminSupplierDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<AdminSupplierDto>>> Suppliers([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,CancellationToken ct)=>Run<PagedResult<AdminSupplierDto>>(async()=>await catalog.SuppliersAsync(paging,search,Actor,ct));
    [HttpGet("suppliers/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminSupplierDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminSupplierDto>> Supplier(Guid id,CancellationToken ct)=>Run(()=>catalog.GetSupplierAsync(id,Actor,ct));
    [HttpPost("suppliers"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminSupplierDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<AdminSupplierDto>> CreateSupplier(SaveSupplierRequest request,CancellationToken ct)=>Run<AdminSupplierDto>(async()=>await catalog.SaveSupplierAsync(null,request,Actor,ct),"/api/sales/suppliers");
    [HttpPut("suppliers/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminSupplierDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminSupplierDto>> UpdateSupplier(Guid id,SaveSupplierRequest request,CancellationToken ct)=>Run<AdminSupplierDto>(async()=>await catalog.SaveSupplierAsync(id,request,Actor,ct));
    [HttpDelete("suppliers/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteSupplier(Guid id,CancellationToken ct)=>Delete(()=>catalog.DeleteSupplierAsync(id,Actor,ct));

    [HttpGet("coupons"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<PagedResult<AdminCouponDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<AdminCouponDto>>> Coupons([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,CancellationToken ct)=>Run<PagedResult<AdminCouponDto>>(async()=>await promotions.CouponsAsync(paging,search,Actor,ct));
    [HttpGet("coupons/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminCouponDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminCouponDto>> Coupon(Guid id,CancellationToken ct)=>Run(()=>promotions.GetCouponAsync(id,Actor,ct));
    [HttpPost("coupons"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminCouponDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<AdminCouponDto>> CreateCoupon(SaveCouponRequest request,CancellationToken ct)=>Run<AdminCouponDto>(async()=>await promotions.SaveCouponAsync(null,request,Actor,ct),"/api/sales/coupons");
    [HttpPut("coupons/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminCouponDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminCouponDto>> UpdateCoupon(Guid id,SaveCouponRequest request,CancellationToken ct)=>Run<AdminCouponDto>(async()=>await promotions.SaveCouponAsync(id,request,Actor,ct));
    [HttpDelete("coupons/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteCoupon(Guid id,CancellationToken ct)=>Delete(()=>promotions.DeleteCouponAsync(id,Actor,ct));

    [HttpGet("upsell-offers"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<PagedResult<AdminUpsellOfferDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<AdminUpsellOfferDto>>> Upsells([FromQuery] PageRequest paging,CancellationToken ct)=>Run<PagedResult<AdminUpsellOfferDto>>(async()=>await promotions.UpsellsAsync(paging,Actor,ct));
    [HttpGet("upsell-offers/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminUpsellOfferDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminUpsellOfferDto>> Upsell(Guid id,CancellationToken ct)=>Run(()=>promotions.GetUpsellAsync(id,Actor,ct));
    [HttpPost("upsell-offers"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminUpsellOfferDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<AdminUpsellOfferDto>> CreateUpsell(SaveUpsellRequest request,CancellationToken ct)=>Run<AdminUpsellOfferDto>(async()=>await promotions.SaveUpsellAsync(null,request,Actor,ct),"/api/sales/upsell-offers");
    [HttpPut("upsell-offers/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminUpsellOfferDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminUpsellOfferDto>> UpdateUpsell(Guid id,SaveUpsellRequest request,CancellationToken ct)=>Run<AdminUpsellOfferDto>(async()=>await promotions.SaveUpsellAsync(id,request,Actor,ct));
    [HttpDelete("upsell-offers/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteUpsell(Guid id,CancellationToken ct)=>Delete(()=>promotions.DeleteUpsellAsync(id,Actor,ct));

    [HttpGet("settings"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminSalesSettingsDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminSalesSettingsDto>> Settings(CancellationToken ct)=>Run<AdminSalesSettingsDto>(async()=>await promotions.SettingsAsync(Actor,ct));
    [HttpPut("settings"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminSalesSettingsDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminSalesSettingsDto>> SaveSettings(SaveSalesSettingsRequest request,CancellationToken ct)=>Run<AdminSalesSettingsDto>(async()=>await promotions.SaveSettingsAsync(request,Actor,ct));
    [HttpGet("payment-methods"),AllowAnonymous]
    [ProducesResponseType<PagedResult<ClientPaymentMethodDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<ClientPaymentMethodDto>>> PaymentMethods([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,CancellationToken ct)=>Run<PagedResult<ClientPaymentMethodDto>>(async()=>await promotions.PaymentMethodsAsync(paging,search,Actor,ct));
    [HttpGet("payment-methods/{id:guid}"),AllowAnonymous]
    [ProducesResponseType<ClientPaymentMethodDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientPaymentMethodDto>> PaymentMethod(Guid id,CancellationToken ct)=>Run(()=>promotions.GetPaymentMethodAsync(id,Actor,ct));
    [HttpPost("payment-methods"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminPaymentMethodDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<AdminPaymentMethodDto>> CreatePaymentMethod(SavePaymentMethodRequest request,CancellationToken ct)=>Run<AdminPaymentMethodDto>(async()=>await promotions.SavePaymentMethodAsync(null,request,Actor,ct),"/api/sales/payment-methods");
    [HttpPut("payment-methods/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminPaymentMethodDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminPaymentMethodDto>> UpdatePaymentMethod(Guid id,SavePaymentMethodRequest request,CancellationToken ct)=>Run<AdminPaymentMethodDto>(async()=>await promotions.SavePaymentMethodAsync(id,request,Actor,ct));
    [HttpDelete("payment-methods/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeletePaymentMethod(Guid id,CancellationToken ct)=>Delete(()=>promotions.DeletePaymentMethodAsync(id,Actor,ct));

    /// <summary>Canonical client cart, including recursively removed or merged line ids.</summary>
    [HttpPost("cart/normalize"),AllowAnonymous,EnableRateLimiting("sales.public")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<CartDto>> Normalize(PriceSaleRequest request,CancellationToken ct)=>Run<CartDto>(()=>Task.FromResult<CartDto?>(basket.NormalizeCart(request.Lines)));
    /// <summary>Fresh server totals; shipping availability is reported in the response.</summary>
    [HttpPost("price"),AllowAnonymous,EnableRateLimiting("sales.public")]
    [ProducesResponseType<PricedSaleDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<PricedSaleDto>> Price(PriceSaleRequest request,CancellationToken ct)=>Run<PricedSaleDto>(async()=>await checkout.PriceAsync(request,ct));
    /// <summary>Pending unpaid checkout. Guest token is returned once; signed-in identity comes from the account.</summary>
    [HttpPost("orders"),AllowAnonymous,EnableRateLimiting("sales.public")]
    [ProducesResponseType<OrderReceiptDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<OrderReceiptDto>> PlaceOrder(PlaceOrderRequest request,CancellationToken ct)=>Run<OrderReceiptDto>(async()=>await orders.PlaceAsync(request,Actor,ct),"/api/sales/orders");
    [HttpGet("orders"),Authorize]
    [ProducesResponseType<PagedResult<ClientOrderSummaryDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<ClientOrderSummaryDto>>> Orders([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,CancellationToken ct)=>Run<PagedResult<ClientOrderSummaryDto>>(async()=>await orders.ListAsync(paging,Actor!.Value,search,ct));
    [HttpGet("orders/{id:guid}"),AllowAnonymous]
    [ProducesResponseType<ClientOrderDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientOrderDto>> Order(Guid id,CancellationToken ct)=>Run(()=>orders.GetAsync(id,Actor,Token,ct));
    /// <summary>Protected complete order context and eligible current upsells.</summary>
    [HttpGet("orders/{id:guid}/upsell-context"),AllowAnonymous]
    [ProducesResponseType<OrderUpsellContextDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<OrderUpsellContextDto>> UpsellContext(Guid id,CancellationToken ct)=>Run(()=>orders.ContextAsync(id,Actor,Token,ct));
    /// <summary>Replay-protected, server-priced unpaid order addition. Does not charge a payment provider.</summary>
    [HttpPost("orders/{id:guid}/upsells/unpaid"),AllowAnonymous,EnableRateLimiting("sales.public")]
    [ProducesResponseType<UpsellAcceptanceDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<UpsellAcceptanceDto>> AcceptUpsell(Guid id,AcceptUpsellRequest request,CancellationToken ct)=>Run(()=>orders.AcceptUnpaidAsync(id,request,Actor,Token,ct));
    /// <summary>Closes further offers atomically. Leaves payment unpaid and order pending.</summary>
    [HttpPost("orders/{id:guid}/finalize"),AllowAnonymous,EnableRateLimiting("sales.public")]
    [ProducesResponseType<ClientOrderDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientOrderDto>> FinalizeOrder(Guid id,CancellationToken ct)=>Run(()=>orders.FinalizeAsync(id,Actor,Token,ct));
    [HttpPut("orders/{id:guid}/status"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<ClientOrderDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientOrderDto>> Status(Guid id,ChangeOrderStatusRequest request,CancellationToken ct)=>Run(()=>orders.ChangeStatusAsync(id,request,Actor,ct));
    [HttpGet("quotes"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<PagedResult<AdminQuoteSummaryDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<AdminQuoteSummaryDto>>> Quotes([FromQuery] PageRequest paging,[FromQuery] SearchRequest search,CancellationToken ct)=>Run<PagedResult<AdminQuoteSummaryDto>>(async()=>await quotes.ListAsync(paging,search,Actor,ct));
    [HttpGet("quotes/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminQuoteDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminQuoteDto>> Quote(Guid id,CancellationToken ct)=>Run(()=>quotes.GetAsync(id,Actor,ct));
    [HttpPost("quotes"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminQuoteDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<AdminQuoteDto>> CreateQuote(SaveQuoteRequest request,CancellationToken ct)=>Run<AdminQuoteDto>(async()=>await quotes.SaveAsync(null,request,Actor,ct),"/api/sales/quotes");
    [HttpPut("quotes/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminQuoteDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminQuoteDto>> UpdateQuote(Guid id,SaveQuoteRequest request,CancellationToken ct)=>Run<AdminQuoteDto>(async()=>await quotes.SaveAsync(id,request,Actor,ct));
    [HttpDelete("quotes/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteQuote(Guid id,CancellationToken ct)=>Delete(()=>quotes.DeleteAsync(id,Actor,ct));
    [HttpPut("quotes/{id:guid}/status"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<AdminQuoteDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<AdminQuoteDto>> QuoteStatus(Guid id,ChangeQuoteStatusRequest request,CancellationToken ct)=>Run(()=>quotes.ChangeStatusAsync(id,request,Actor,ct));
    [HttpPost("quotes/{id:guid}/convert"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<OrderReceiptDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<OrderReceiptDto>> ConvertQuote(Guid id,CancellationToken ct)=>Run(()=>quotes.ConvertAsync(id,Actor,ct));

    [HttpPost("staff/price"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<PricedSaleDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<PricedSaleDto>> StaffPrice(SaveStaffOrderRequest request,CancellationToken ct)=>Run<PricedSaleDto>(async()=>await orders.PriceStaffAsync(request,Actor,ct));
    [HttpPost("staff/orders"),Authorize(Policy=AccessPermissions.ManageSales),ProducesResponseType<OrderReceiptDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<OrderReceiptDto>> StaffOrder(SaveStaffOrderRequest request,CancellationToken ct)=>Run<OrderReceiptDto>(async()=>await orders.PlaceStaffAsync(request,Actor,ct),"/api/sales/orders");
    [HttpPut("staff/orders/{id:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<ClientOrderDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientOrderDto>> EditStaffOrder(Guid id,SaveStaffOrderRequest request,CancellationToken ct)=>Run(()=>orders.UpdateStaffAsync(id,request,Actor,ct));
    [HttpGet("staff/invoice-number-availability"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<NumberAvailabilityDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<NumberAvailabilityDto>> InvoiceAvailability([FromQuery] int number,[FromQuery] Guid? excludeOrderId,CancellationToken ct)=>Run<NumberAvailabilityDto>(async()=>await orders.InvoiceAvailabilityAsync(number,excludeOrderId,Actor,ct));

    [HttpPost("orders/{id:guid}/invoice/send"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<CompanyHQ.Sales.Contracts.Ports.SalesDocumentDeliveryResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<CompanyHQ.Sales.Contracts.Ports.SalesDocumentDeliveryResult>> SendInvoice(Guid id,SendSalesDocumentRequest request,CancellationToken ct)=>Run(()=>documents.SendInvoiceAsync(id,request,Actor,ct));
    [HttpPost("quotes/{id:guid}/send"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<CompanyHQ.Sales.Contracts.Ports.SalesDocumentDeliveryResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<CompanyHQ.Sales.Contracts.Ports.SalesDocumentDeliveryResult>> SendQuote(Guid id,SendSalesDocumentRequest request,CancellationToken ct)=>Run(()=>documents.SendQuoteAsync(id,request,Actor,ct));

    [HttpGet("products/{id:guid}/reviews"),AllowAnonymous]
    [ProducesResponseType<PagedResult<ClientProductReviewDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<ClientProductReviewDto>>> ProductReviews(Guid id,[FromQuery] PageRequest paging,CancellationToken ct)=>Run<PagedResult<ClientProductReviewDto>>(async()=>await catalog.ReviewsAsync(id,paging,ct));
    [HttpPost("products/{id:guid}/reviews"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<ClientProductReviewDto>(StatusCodes.Status201Created)]
    public Task<ActionResult<ClientProductReviewDto>> CreateReview(Guid id,SaveProductReviewRequest request,CancellationToken ct)=>Run<ClientProductReviewDto>(async()=>await catalog.SaveReviewAsync(id,null,request,Actor,ct),$"/api/sales/products/{id}/reviews");
    [HttpPut("products/{id:guid}/reviews/{reviewId:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<ClientProductReviewDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClientProductReviewDto>> UpdateReview(Guid id,Guid reviewId,SaveProductReviewRequest request,CancellationToken ct)=>Run<ClientProductReviewDto>(async()=>await catalog.SaveReviewAsync(id,reviewId,request,Actor,ct));
    [HttpDelete("products/{id:guid}/reviews/{reviewId:guid}"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteReview(Guid id,Guid reviewId,CancellationToken ct)=>Delete(()=>catalog.DeleteReviewAsync(id,reviewId,Actor,ct));

    [HttpGet("products/{id:guid}/profit-scenarios"),Authorize(Policy=AccessPermissions.ManageSales)]
    [ProducesResponseType<IReadOnlyList<AdminProductProfitScenarioDto>>(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<AdminProductProfitScenarioDto>>> ProfitScenarios(Guid id,CancellationToken ct)=>Run<IReadOnlyList<AdminProductProfitScenarioDto>>(async()=>await scenarios.CalculateAsync(id,Actor,ct));

}
