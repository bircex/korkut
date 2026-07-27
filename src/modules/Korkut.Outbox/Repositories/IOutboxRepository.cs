using Korkut.Outbox.Entities;
using Korkut.Core;

namespace Korkut.Outbox;

/// <summary>Persistence port for the outbound-message review queue; listings are newest first.</summary>
public interface IOutboxRepository : IRepository<OutboundMessage>;
