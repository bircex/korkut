using Korkut.Clients.Entities;
using Korkut.Core;

namespace Korkut.Clients.Repositories;

/// <summary>Persistence port for clients. Reads exclude soft-deleted rows and are ordered by name.</summary>
internal interface IClientRepository : ISoftDeleteRepository<Client>;
