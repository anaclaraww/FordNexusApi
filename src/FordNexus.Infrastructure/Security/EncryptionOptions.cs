using System.ComponentModel.DataAnnotations;

namespace FordNexus.Infrastructure.Security;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    [Required]
    public string Key { get; set; } = string.Empty;

    public byte[] KeyBytes() => Convert.FromBase64String(Key);

    public bool IsValid()
    {
        try { return KeyBytes().Length == 32; }
        catch (FormatException) { return false; }
    }
}
