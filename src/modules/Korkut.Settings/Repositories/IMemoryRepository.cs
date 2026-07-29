using Korkut.Core;
using Korkut.Settings.Entities;

namespace Korkut.Settings.Repositories;

/// <summary>Persistence port for office memory items; listings are newest first.</summary>
public interface IMemoryRepository : IRepository<MemoryItem>;
