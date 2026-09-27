using System.Security.Cryptography;

namespace FordNexus.Infrastructure.Security;

internal static class EphemeralSecrets
{
    private static readonly Lazy<string> JwtKey = new(() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
    private static readonly Lazy<string> DataKey = new(() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public static string JwtSigningKey => JwtKey.Value;
    public static string EncryptionKey => DataKey.Value;
}
