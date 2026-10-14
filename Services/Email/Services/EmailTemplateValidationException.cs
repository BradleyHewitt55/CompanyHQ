namespace CompanyHQ.Email.Services;

public sealed class EmailTemplateValidationException(IReadOnlyList<string> errors) : Exception
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
