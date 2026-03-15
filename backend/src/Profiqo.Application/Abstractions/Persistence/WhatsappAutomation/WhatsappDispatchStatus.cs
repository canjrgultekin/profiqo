namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public enum WhatsappDispatchStatus : short
{
    Queued = 1,
    Running = 2,
    SentDummy = 3,
    SuppressedLimit = 4,
    Failed = 5
}