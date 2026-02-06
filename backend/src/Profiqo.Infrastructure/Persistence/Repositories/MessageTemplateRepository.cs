using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Messaging;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class MessageTemplateRepository : IMessageTemplateRepository
{
    private readonly ProfiqoDbContext _db;

    public MessageTemplateRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<MessageTemplate?> GetByIdAsync(MessageTemplateId id, CancellationToken cancellationToken)
        => _db.MessageTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(MessageTemplate template, CancellationToken cancellationToken)
    {
        await _db.MessageTemplates.AddAsync(template, cancellationToken);
    }
}