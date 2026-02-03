using System.Text.Json;
using System.Text.Json.Serialization;

namespace Profiqo.Application.Integrations.Whatsapp;

// public: Api, Worker, Infrastructure erişebilsin
public sealed record WhatsappCredentialSecret(
    string AccessToken,
    string PhoneNumberId,
    string WabaId)
{
    public static WhatsappCredentialSecret FromJson(string json)
    {
        var obj = JsonSerializer.Deserialize<WhatsappCredentialSecret>(json)
                  ?? throw new InvalidOperationException("Invalid WhatsApp credential secret json.");

        if (string.IsNullOrWhiteSpace(obj.AccessToken) ||
            string.IsNullOrWhiteSpace(obj.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(obj.WabaId))
            throw new InvalidOperationException("Invalid WhatsApp credential secret json.");

        return obj;
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}