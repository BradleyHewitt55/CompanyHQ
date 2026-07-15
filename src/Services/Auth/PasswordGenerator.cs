using System.Security.Cryptography;

namespace Services.Auth;

public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@$%*-_+";

    public static string Generate(int length = 20)
    {
        if (length < 12) throw new ArgumentOutOfRangeException(nameof(length));
        var characters = new List<char> { Pick(Upper), Pick(Lower), Pick(Digits), Pick(Symbols) };
        var all = Upper + Lower + Digits + Symbols;
        while (characters.Count < length) characters.Add(Pick(all));
        for (var index = characters.Count - 1; index > 0; index--)
        {
            var target = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[target]) = (characters[target], characters[index]);
        }
        return new string([.. characters]);
    }

    private static char Pick(string value) => value[RandomNumberGenerator.GetInt32(value.Length)];
}
