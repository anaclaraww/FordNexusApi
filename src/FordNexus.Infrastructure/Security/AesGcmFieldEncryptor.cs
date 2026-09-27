using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FordNexus.Infrastructure.Security;

public interface IFieldEncryptor
{
    string Encrypt(string plaintext, string purpose);
    string Decrypt(string stored, string purpose);
}

public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    public const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmFieldEncryptor(IOptions<EncryptionOptions> options)
    {
        _key = options.Value.KeyBytes();
        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption:Key precisa ter 32 bytes (AES-256) em Base64.");
    }

    public string Encrypt(string plaintext, string purpose)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var buffer = new byte[NonceSize + TagSize + plain.Length];
        var nonce = buffer.AsSpan(0, NonceSize);
        var tag = buffer.AsSpan(NonceSize, TagSize);
        var cipher = buffer.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(purpose));

        return Prefix + Convert.ToBase64String(buffer);
    }

    public string Decrypt(string stored, string purpose)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        var buffer = Convert.FromBase64String(stored[Prefix.Length..]);
        if (buffer.Length < NonceSize + TagSize)
            throw new CryptographicException("Valor criptografado inválido.");

        var nonce = buffer.AsSpan(0, NonceSize);
        var tag = buffer.AsSpan(NonceSize, TagSize);
        var cipher = buffer.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(purpose));
        return Encoding.UTF8.GetString(plain);
    }
}
