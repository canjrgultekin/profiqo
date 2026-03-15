namespace Profiqo.Application.Abstractions.Integrations.Whatsapp;

public interface IWhatsappCloudValidator
{
    Task<(bool ok, string? verifiedName, string? displayPhone, string? rawError)> ValidateAsync(
        string accessToken,
        string phoneNumberId,
        CancellationToken ct);
}