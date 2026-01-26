namespace Profiqo.Domain.Common.Types;

using Profiqo.Domain.Common;

public sealed record EncryptedSecret
{
    public string CipherText { get; init; }
    public string KeyId { get; init; }
    public string Algorithm { get; init; }

    private EncryptedSecret()
    {
        CipherText = string.Empty;
        KeyId = string.Empty;
        Algorithm = string.Empty;
    }

    public EncryptedSecret(string cipherText, string keyId, string algorithm)
    {
        CipherText = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(cipherText, nameof(cipherText)), 4096, nameof(cipherText));
        KeyId = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(keyId, nameof(keyId)), 128, nameof(keyId));
        Algorithm = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(algorithm, nameof(algorithm)), 64, nameof(algorithm));
    }
}