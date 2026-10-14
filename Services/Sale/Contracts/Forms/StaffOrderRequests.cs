using System.ComponentModel.DataAnnotations;
using CompanyHQ.Sales.Contracts.Enums;
namespace CompanyHQ.Sales.Contracts.Forms;
/// <summary>Staff-only line adjustment applied after calculated promotions.</summary>
public sealed record StaffLineDiscountRequest(Guid LineId,DiscountType Type,[Range(0,1000000000)] decimal Value);
/// <summary>Authorized negotiated pricing. No field is accepted by self-service checkout.</summary>
public sealed record StaffPricingRequest(IReadOnlyList<StaffLineDiscountRequest>? LineDiscounts=null,
    DiscountType? ManualDiscountType=null,[Range(0,1000000000)] decimal ManualDiscountValue=0,
    [Range(0,1000000000)] decimal? NegotiatedShippingCost=null,[Range(0,1000000000)] decimal TaxAmount=0);
/// <summary>Staff creates/replaces a pending unpaid order. Customer assignment resolves the actual profile.</summary>
public sealed record SaveStaffOrderRequest([Required] PlaceOrderRequest Order,StaffPricingRequest? Pricing=null,
    Guid? UserId=null,[Range(1,int.MaxValue)] int? InvoiceNumber=null);
