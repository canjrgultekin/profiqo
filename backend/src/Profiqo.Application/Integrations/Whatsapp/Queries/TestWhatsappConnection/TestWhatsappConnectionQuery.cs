using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Whatsapp.Queries.TestWhatsappConnection;

public sealed record TestWhatsappConnectionQuery(Guid ConnectionId) : IQuery<object>;