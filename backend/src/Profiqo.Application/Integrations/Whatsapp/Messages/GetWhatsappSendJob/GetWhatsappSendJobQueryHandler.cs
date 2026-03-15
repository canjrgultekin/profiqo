using MediatR;

using Profiqo.Application.Abstractions.Persistence.Whatsapp;

namespace Profiqo.Application.Integrations.Whatsapp.Messages.GetWhatsappSendJob;

internal sealed class GetWhatsappSendJobQueryHandler : IRequestHandler<GetWhatsappSendJobQuery, WhatsappSendJobDto?>
{
    private readonly IWhatsappSendJobRepository _jobs;

    public GetWhatsappSendJobQueryHandler(IWhatsappSendJobRepository jobs)
    {
        _jobs = jobs;
    }

    public Task<WhatsappSendJobDto?> Handle(GetWhatsappSendJobQuery request, CancellationToken ct)
        => _jobs.GetAsync(request.JobId, ct);
}