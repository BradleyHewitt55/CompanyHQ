using Abstractions.Companies;

namespace CompanyHQ.Cli;

public sealed class SystemCompanyConsole : ICompanyConsole
{
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Console.ReadLine());

    public Task<string?> ReadSecretAsync(CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected)
        {
            return Task.FromResult(Console.ReadLine());
        }

        var value = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return Task.FromResult<string?>(new string([.. value]));
            }

            if (key.Key == ConsoleKey.Backspace && value.Count > 0)
            {
                value.RemoveAt(value.Count - 1);
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                value.Add(key.KeyChar);
                Console.Write('*');
            }
        }
    }

    public Task WriteAsync(string value, CancellationToken cancellationToken = default)
    {
        Console.Write(value);
        return Task.CompletedTask;
    }

    public Task WriteLineAsync(string value, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(value);
        return Task.CompletedTask;
    }
}
