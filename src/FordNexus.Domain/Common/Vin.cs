using System.Text.RegularExpressions;

namespace FordNexus.Domain.Common;

public static partial class Vin
{
    public const string Pattern = "^[A-HJ-NPR-Za-hj-npr-z0-9]{17}$";

    [GeneratedRegex(Pattern)]
    private static partial Regex VinRegex();

    public static bool IsValid(string? vin) => vin is not null && VinRegex().IsMatch(vin);

    public static string Normalize(string vin) => vin.Trim().ToUpperInvariant();
}
