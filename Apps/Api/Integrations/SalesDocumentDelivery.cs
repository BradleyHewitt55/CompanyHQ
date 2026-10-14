using System.Net;
using System.Globalization;
using CompanyHQ.Email.Services;
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.Email.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Ports;
namespace Api.Integrations;
internal sealed class SalesDocumentDelivery(IEmailService email,IEmailDeliveryQuery deliveries) : ISalesDocumentDelivery
{
    public async Task<SalesDocumentDeliveryResult> DeliverAsync(SalesDocument document,CancellationToken cancellationToken=default)
    {
        var text=$"{document.Kind} {document.Reference}\n{document.Status}\n"+string.Join("\n",document.Lines.Select(l=>$"{l.Quantity} x {l.ProductName}: {document.Currency} {l.FinalTotalPrice.ToString("F2",CultureInfo.InvariantCulture)}"))+
            $"\nSubtotal: {document.Subtotal}\nDiscount: {document.DiscountAmount}\nShipping: {document.ShippingCost}\nTax: {document.TaxAmount}\nTotal: {document.Currency} {document.TotalAmount.ToString("F2",CultureInfo.InvariantCulture)}";
        var result=await email.SendAsync(new([document.Recipient],$"{document.Kind} {document.Reference}","<pre>"+WebUtility.HtmlEncode(text)+"</pre>",text,document.IdempotencyKey),cancellationToken);
        if(!result.IsSuccess)return new(false,null,result.Error);
        var state=await deliveries.GetAsync(document.IdempotencyKey,cancellationToken);
        return state?.Status switch
        {
            QueuedEmailStatus.Sent=>new(true,state.ProviderReference,null),
            QueuedEmailStatus.Pending=>new(false,null,null,Queued:true),
            QueuedEmailStatus.Failed or QueuedEmailStatus.Abandoned=>new(false,null,"Email provider did not accept the document."),
            _=>new(false,null,"Email delivery state is unavailable.")
        };
    }
}
