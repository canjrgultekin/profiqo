using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Configuration;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Domain.Common.Types;

namespace Profiqo.Whatsapp.Automation.Worker.Security;

internal sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string Algo = "AES-256-GCM";
    private readonly byte[] _key;

    public AesGcmSecretProtector(IConfiguration cfg)
    {
        var raw = cfg["Profiqo:Crypto:MasterKey"];
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 32)
            throw new InvalidOperationException("Profiqo:Crypto:MasterKey must be set (min 32 chars).");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw)); // 32 byte
    }

    public EncryptedSecret Protect(string plaintext)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        var nonce = RandomNumberGenerator.GetBytes(12);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, pt, ct, tag);

        var payload = Convert.ToBase64String(Combine(nonce, tag, ct));
        return new EncryptedSecret(payload, keyId: "local-masterkey-v1", algorithm: Algo);
    }

    public string Unprotect(EncryptedSecret secret)
    {
        var data = Convert.FromBase64String(secret.CipherText);

        var nonce = data.AsSpan(0, 12).ToArray();
        var tag = data.AsSpan(12, 16).ToArray();
        var ct = data.AsSpan(28).ToArray();

        var pt = new byte[ct.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, ct, tag, pt);

        return Encoding.UTF8.GetString(pt);
    }

    private static byte[] Combine(byte[] nonce, byte[] tag, byte[] ct)
    {
        var buf = new byte[nonce.Length + tag.Length + ct.Length];
        Buffer.BlockCopy(nonce, 0, buf, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, buf, nonce.Length, tag.Length);
        Buffer.BlockCopy(ct, 0, buf, nonce.Length + tag.Length, ct.Length);
        return buf;
    }
}
