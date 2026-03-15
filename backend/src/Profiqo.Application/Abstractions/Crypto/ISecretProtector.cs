using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Types;

namespace Profiqo.Application.Abstractions.Crypto;

public interface ISecretProtector
{
    EncryptedSecret Protect(string plaintext);
    string Unprotect(EncryptedSecret secret);
}