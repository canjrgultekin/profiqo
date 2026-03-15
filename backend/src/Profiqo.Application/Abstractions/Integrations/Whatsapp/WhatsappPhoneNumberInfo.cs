namespace Profiqo.Application.Abstractions.Integrations.Whatsapp;

public sealed record WhatsappPhoneNumberInfo(
    string PhoneNumberId,
    string? DisplayPhoneNumber,
    string? VerifiedName);