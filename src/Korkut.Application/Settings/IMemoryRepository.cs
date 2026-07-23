using Korkut.Application.Common;
using Korkut.Domain.Settings;

namespace Korkut.Application.Settings;

/// <summary>Persistence port for office memory items; listings are newest first.</summary>
public interface IMemoryRepository : IRepository<MemoryItem>;
