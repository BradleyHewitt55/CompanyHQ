namespace CompanyHQ.Services.Companies.Services;

public static class NameValidator
{
    public static void Validate(string value, string parameterName, string label)
    {
        // Names become directory and database identifiers, so one shared rule keeps
        // every filesystem entry safe regardless of which service creates it.
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 64
            || !value.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_'))
        {
            throw new ArgumentException(
                $"{label} name '{value}' is invalid: 1-64 letters, digits, spaces, dashes, or underscores.",
                parameterName);
        }
    }
}
