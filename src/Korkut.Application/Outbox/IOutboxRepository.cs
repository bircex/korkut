using Korkut.Core;
using Korkut.Domain.Outbox;

namespace Korkut.Application.Outbox;

/// <summary>Persistence port for the outbound-message review queue; listings are newest first.</summary>
public interface IOutboxRepository : IRepository<OutboundMessage>;
