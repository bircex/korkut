using Korkut.Core;

namespace Korkut.Clients;

/// <summary>Persistence port for clients. Reads exclude soft-deleted rows and are ordered by name.</summary>
public interface IClientRepository : ISoftDeleteRepository<Client>;
