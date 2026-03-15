using System.Text;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Domain.Common.Types;

namespace Profiqo.Whatsapp.Automation.Worker.Security;

// Dummy/local worker için: gerçek şifreleme yapmaz.
// Sadece DI validation ve TrendyolSyncProcessor activation ihtiyaçlarını karşılar.
internal sealed class NoopSecretProtector : ISecretProtector
{
    private const string KeyId = "noop";
    private const string Algo = "noop-base64";

    public EncryptedSecret Protect(string plaintext)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));
        var ct = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
        return new EncryptedSecret(ct, KeyId, Algo);
    }

    public string Unprotect(EncryptedSecret secret)
    {
        if (secret is null) throw new ArgumentNullException(nameof(secret));

        try
        {
            var bytes = Convert.FromBase64String(secret.CipherText);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // yanlışlıkla base64 değilse bile app patlamasın
            return secret.CipherText;
        }
    }
}