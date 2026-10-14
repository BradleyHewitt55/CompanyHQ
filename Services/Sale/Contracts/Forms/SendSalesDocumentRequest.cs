namespace CompanyHQ.Services.Sales.Contracts.Forms;
/// <summary>Stable caller request id for repeat-safe provider delivery.</summary>
public sealed record SendSalesDocumentRequest(Guid RequestId);
