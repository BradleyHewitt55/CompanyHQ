using System.ComponentModel.DataAnnotations;
using CompanyHQ.Sales.Contracts.Enums;
namespace CompanyHQ.Sales.Contracts.Forms;
/// <summary>Staff quote uses server prices and may target an authenticated customer profile.</summary>
public sealed record SaveQuoteRequest([Required] PlaceOrderRequest Order,DateTimeOffset ValidUntil,Guid? UserId=null,StaffPricingRequest? Pricing=null);
/// <summary>Quote transition. Conversion copies the accepted immutable snapshot.</summary>
public sealed record ChangeQuoteStatusRequest(QuoteStatus Status);
