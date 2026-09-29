namespace CompanyHQ.Services.Faqs.Services;

public sealed class FaqKeyConflictException : Exception
{
    public FaqKeyConflictException() : base("A FAQ entry with this key already exists.")
    {
    }
}
