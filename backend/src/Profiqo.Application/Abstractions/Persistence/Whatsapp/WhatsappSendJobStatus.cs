// Path: backend/src/Profiqo.Application/Abstractions/Persistence/Whatsapp/WhatsappSendJobStatus.cs
namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public enum WhatsappSendJobStatus : short
{
    Queued = 1,
    Running = 2,
    Retrying = 3,
    Succeeded = 4,
    Failed = 5
}