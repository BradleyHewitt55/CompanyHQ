using Services.Companies.Contracts.Services;
using Services.Orders.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Services.Orders;

public sealed class OrdersModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(OrdersModule).Assembly);
    public IReadOnlyList<ServiceComponentRegistration> Components => [new("index", typeof(Pages.OrdersPage))];
    public void Configure(IServiceCollection services) { }
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/services/orders", async (IEntityService<Order> orders, CancellationToken token) => Results.Ok(await orders.Get(token))).RequireAuthorization();
        endpoints.MapPost("/api/services/orders", async (HttpContext context, CreateOrder request, IEntityService<Order> orders, CancellationToken token) =>
        {
            if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(request.Slug) || request.Total < 0) return Results.BadRequest();
            var order = await orders.Save(new Order { Slug = request.Slug.Trim(), Total = request.Total }, token);
            return Results.Created($"/api/services/orders/{order.Id}", order);
        }).RequireAuthorization("Contribute").WithMetadata(new RequireAntiforgeryTokenAttribute());
        endpoints.MapPost("/api/services/orders/form", async (HttpContext context, IEntityService<Order> orders, CancellationToken token) =>
        {
            if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync(token);
            if (string.IsNullOrWhiteSpace(form["Slug"]) || !decimal.TryParse(form["Total"], out var total) || total < 0) return Results.BadRequest();
            await orders.Save(new Order { Slug = form["Slug"].ToString().Trim(), Total = total }, token);
            return Results.Redirect("/admin/services/orders/index");
        }).RequireAuthorization("Contribute").WithMetadata(new RequireAntiforgeryTokenAttribute());
    }

    public sealed record CreateOrder(string Slug, decimal Total);
}
