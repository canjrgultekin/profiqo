using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Messaging;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface IMessageTemplateRepository
{
    Task<MessageTemplate?> GetByIdAsync(MessageTemplateId id, CancellationToken cancellationToken);
    Task AddAsync(MessageTemplate template, CancellationToken cancellationToken);
}