using System.Text.Json;

namespace Profiqo.Application.Integrations.Whatsapp;

internal sealed record WhatsappConnectionSecret(string WabaId, string PhoneNumberId)
{
    public static WhatsappConnectionSecret FromJson(string json)
    {
        var obj = JsonSerializer.Deserialize<WhatsappConnectionSecret>(json)
                  ?? throw new InvalidOperationException("Invalid WhatsApp connection secret json.");
        if (string.IsNullOrWhiteSpace(obj.WabaId) || string.IsNullOrWhiteSpace(obj.PhoneNumberId))
            throw new InvalidOperationException("Invalid WhatsApp connection secret json.");
        return obj;
    }
}