using Korkut.Core;
using Korkut.Domain.Courts;

namespace Korkut.Application.Courts;

/// <summary>Persistence port for the office's court reference data.</summary>
public interface ICourtRepository : IRepository<Court>;
