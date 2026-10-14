namespace CompanyHQ.Services.Sales.Contracts.Models;
/// <summary>Company policy without provider credentials.</summary>
public sealed class SalesOptions
{
    public const string SectionName = "Sales";
    public string BusinessTimeZone { get; set; } = "UTC";
    public string DefaultCurrency { get; set; } = "USD";
    public string InvoicePrefix { get; set; } = "INV";
    public string PreferredProfitShippingMethodCode { get; set; } = "";
    public string QuotePrefix { get; set; } = "QUO";
    public int GuestTokenLifetimeDays { get; set; } = 30;
    public bool RequireShippingAddress { get; set; }
    public bool EnableAuthenticatedCheckout { get; set; } = true;
    public bool EnableExtraLengthPricing { get; set; }
    public string ExtraLengthOptionName { get; set; } = "";
    public string ExtraLengthValueContains { get; set; } = "";
    public string StandardLengthValueContains { get; set; } = "";
    public decimal ExtraLengthMultiplier { get; set; } = 1;
}
