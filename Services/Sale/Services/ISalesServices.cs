using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Forms;
namespace CompanyHQ.Services.Sales.Services;
public interface ISalesCatalogService
{
    Task<PagedResult<ClientProductDto>> ListAsync(PageRequest paging,SearchRequest search,ProductFilterRequest filter,ResponseAudience audience=ResponseAudience.Client,Guid? actor=null,CancellationToken ct=default);
    Task<ClientProductDto?> GetAsync(string slug,ResponseAudience audience=ResponseAudience.Client,Guid? actor=null,CancellationToken ct=default);
    Task<AdminProductDto> SaveAsync(Guid? id,SaveProductRequest request,Guid? actor,CancellationToken ct=default);
    Task<bool> DeleteAsync(Guid id,Guid? actor,CancellationToken ct=default);
}
public interface ISalesCheckoutService
{
    Task<PricedSaleDto> PriceAsync(PriceSaleRequest request,CancellationToken ct=default);
}
public interface ISalesOrderService
{
    Task<PricedSaleDto> PriceStaffAsync(SaveStaffOrderRequest request,Guid? actor,CancellationToken ct=default);
    Task<OrderReceiptDto> PlaceStaffAsync(SaveStaffOrderRequest request,Guid? actor,CancellationToken ct=default);
    Task<ClientOrderDto?> UpdateStaffAsync(Guid id,SaveStaffOrderRequest request,Guid? actor,CancellationToken ct=default);
    Task<NumberAvailabilityDto> InvoiceAvailabilityAsync(int number,Guid? excludeOrder,Guid? actor,CancellationToken ct=default);
    Task<OrderReceiptDto> PlaceAsync(PlaceOrderRequest request,Guid? actor,CancellationToken ct=default);
    Task<ClientOrderDto?> GetAsync(Guid id,Guid? actor,string? token,CancellationToken ct=default);
    Task<PagedResult<ClientOrderSummaryDto>> ListAsync(PageRequest page,Guid actor,SearchRequest? search=null,CancellationToken ct=default);
    Task<OrderUpsellContextDto?> ContextAsync(Guid id,Guid? actor,string? token,CancellationToken ct=default);
    Task<UpsellAcceptanceDto?> AcceptUnpaidAsync(Guid id,AcceptUpsellRequest request,Guid? actor,string? token,CancellationToken ct=default);
    Task<ClientOrderDto?> FinalizeAsync(Guid id,Guid? actor,string? token,CancellationToken ct=default);
    Task<ClientOrderDto?> ChangeStatusAsync(Guid id,ChangeOrderStatusRequest request,Guid? actor,CancellationToken ct=default);
}

public interface ISalesBasketPricing
{
    CartDto NormalizeCart(IReadOnlyList<SalesLineRequest> lines);
    Task<CompanyHQ.Services.Sales.Contracts.Ports.SalesShippingDiscount> ShippingDiscountAsync(CompanyHQ.Services.Sales.Contracts.Ports.SalesShippingDiscountRequest request,CancellationToken ct=default);
}

public interface ISalesPromotionService
{
    Task<PagedResult<AdminCouponDto>> CouponsAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default);
    Task<AdminCouponDto?> GetCouponAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<AdminCouponDto> SaveCouponAsync(Guid? id,SaveCouponRequest request,Guid? actor,CancellationToken ct=default);
    Task<bool> DeleteCouponAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<PagedResult<AdminUpsellOfferDto>> UpsellsAsync(PageRequest page,Guid? actor,CancellationToken ct=default);
    Task<AdminUpsellOfferDto?> GetUpsellAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<AdminUpsellOfferDto> SaveUpsellAsync(Guid? id,SaveUpsellRequest request,Guid? actor,CancellationToken ct=default);
    Task<bool> DeleteUpsellAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<AdminSalesSettingsDto> SettingsAsync(Guid? actor,CancellationToken ct=default);
    Task<AdminSalesSettingsDto> SaveSettingsAsync(SaveSalesSettingsRequest request,Guid? actor,CancellationToken ct=default);
    Task<PagedResult<ClientPaymentMethodDto>> PaymentMethodsAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default);
    Task<ClientPaymentMethodDto?> GetPaymentMethodAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<AdminPaymentMethodDto> SavePaymentMethodAsync(Guid? id,SavePaymentMethodRequest request,Guid? actor,CancellationToken ct=default);
    Task<bool> DeletePaymentMethodAsync(Guid id,Guid? actor,CancellationToken ct=default);
}

public interface ISalesQuoteService
{
    Task<PagedResult<AdminQuoteSummaryDto>> ListAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default);
    Task<AdminQuoteDto?> GetAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<AdminQuoteDto> SaveAsync(Guid? id,SaveQuoteRequest request,Guid? actor,CancellationToken ct=default);
    Task<bool> DeleteAsync(Guid id,Guid? actor,CancellationToken ct=default);
    Task<AdminQuoteDto?> ChangeStatusAsync(Guid id,ChangeQuoteStatusRequest request,Guid? actor,CancellationToken ct=default);
    Task<OrderReceiptDto?> ConvertAsync(Guid id,Guid? actor,CancellationToken ct=default);
}
