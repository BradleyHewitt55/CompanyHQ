using System.Net.Mail;
using Abstractions.Companies;
using Abstractions.Data;
using Abstractions.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Companies.Services;

public sealed class CompanyCreation(
    ICompanyRegistry registry,
    ICompanyDatabases databases,
    IServerPaths paths,
    IServiceScopeFactory scopeFactory)
{
    public async Task CreateAsync(
        string companyName,
        ICompanyConsole console,
        CancellationToken cancellationToken = default)
    {
        await registry.RegisterAsync(companyName, cancellationToken: cancellationToken);

        var name = await PromptRequiredAsync(console, "Admin name: ", cancellationToken);
        var surname = await PromptRequiredAsync(console, "Admin surname: ", cancellationToken);
        var email = await PromptEmailAsync(console, cancellationToken);
        var contactNumber = await PromptRequiredAsync(console, "Contact number: ", cancellationToken);
        var password = await PromptPasswordAsync(console, cancellationToken);

        string? externalConnectionString = null;
        while (true)
        {
            await console.WriteLineAsync("[1] Host on this server  [2] Use an external database", cancellationToken);
            await console.WriteAsync("Database option: ", cancellationToken);
            var option = await console.ReadLineAsync(cancellationToken);
            if (option == "1")
            {
                break;
            }

            if (option == "2")
            {
                externalConnectionString = await PromptRequiredAsync(
                    console, "Connection string: ", cancellationToken);
                break;
            }

            await console.WriteLineAsync("Choose 1 or 2.", cancellationToken);
        }

        await databases.EnsureDatabaseAsync(companyName, externalConnectionString, cancellationToken);

        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICompanyContext>().Current = companyName;
        await scope.ServiceProvider.GetRequiredService<IUserService>().SeedAdminAsync(
            new NewUser(name, surname, email, contactNumber, password), cancellationToken);

        await console.WriteLineAsync($"Created {paths.CompanyDirectory(companyName)}", cancellationToken);
        await console.WriteLineAsync("Open https://{your-domain}/admin", cancellationToken);
    }

    private static async Task<string> PromptRequiredAsync(
        ICompanyConsole console,
        string prompt,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await console.WriteAsync(prompt, cancellationToken);
            var value = await console.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            await console.WriteLineAsync("A value is required.", cancellationToken);
        }
    }

    private static async Task<string> PromptEmailAsync(
        ICompanyConsole console,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var email = await PromptRequiredAsync(console, "Admin email: ", cancellationToken);
            if (MailAddress.TryCreate(email, out var address)
                && address.Address.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
                return email;
            }

            await console.WriteLineAsync("Enter a valid email address.", cancellationToken);
        }
    }

    private static async Task<string> PromptPasswordAsync(
        ICompanyConsole console,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await console.WriteAsync("Password: ", cancellationToken);
            var password = await console.ReadSecretAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(password))
            {
                await console.WriteLineAsync("A value is required.", cancellationToken);
                continue;
            }

            if (!PasswordPolicy.IsValid(password))
            {
                await console.WriteLineAsync(PasswordPolicy.Requirement, cancellationToken);
                continue;
            }

            await console.WriteAsync("Confirm password: ", cancellationToken);
            var confirmation = await console.ReadSecretAsync(cancellationToken);
            if (password == confirmation)
            {
                return password;
            }

            await console.WriteLineAsync("Passwords do not match.", cancellationToken);
        }
    }
}
