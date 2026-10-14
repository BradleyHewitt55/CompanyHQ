using System.ComponentModel.DataAnnotations;
using System.Reflection;
namespace CompanyHQ.Services.Shipping.Contracts.Forms;
/// <summary>Validates record constructor attributes for direct service callers.</summary>
public static class ShippingValidation
{
    public static void ThrowIfInvalid(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type=request.GetType();
        foreach(var p in type.GetConstructors().OrderByDescending(x=>x.GetParameters().Length).First().GetParameters())
        {
            var value=type.GetProperty(p.Name!,BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase)?.GetValue(request);
            foreach(var a in p.GetCustomAttributes<ValidationAttribute>())if(!a.IsValid(value))throw new ArgumentException(a.FormatErrorMessage(p.Name!));
            if(value is double d && !double.IsFinite(d))throw new ArgumentException("Coordinates must be finite.");
        }
    }
}
