using CompanyHQ.Sales.Contracts.Forms;
namespace CompanyHQ.Sales.Contracts.Dtos;
/// <summary>Public category identity.</summary>
public sealed record ClientCategoryDto(Guid Id, string Name, string Slug,Guid? ParentId=null,int Position=0,string? Sku=null,string? ImageUrl=null);
/// <summary>Supplier with staff-managed origin.</summary>
public sealed record AdminSupplierDto(Guid Id, string Name, SalesAddressDto? Origin, bool IsActive);
/// <summary>Canonical lines after merging equivalent identities and dropping orphaned descendants.</summary>
public sealed record CartDto(IReadOnlyList<SalesLineRequest> Lines, IReadOnlyList<Guid> RemovedLineIds);
/// <summary>Offer tied to one immutable order line.</summary>
public sealed record OrderUpsellOfferDto(Guid ParentLineId, UpsellOfferDto Offer);
/// <summary>Unpaid addition receipt. A provider charge is not performed.</summary>
public sealed record UpsellAcceptanceDto(Guid RequestId, Guid LineId, decimal AdditionalAmount, decimal OrderTotal, bool IsPaid);
/// <summary>Complete customer context including current offers and completion state.</summary>
public sealed record OrderUpsellContextDto(ClientOrderDto Order, IReadOnlyList<OrderUpsellOfferDto> Offers, bool IsFinalized);
