using Korkut.Application.Courts;
using Korkut.Domain.Courts;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Infrastructure.Courts;

/// <summary>EF/Postgres adapter for <see cref="ICourtRepository"/>.</summary>
public sealed class CourtRepository(KorkutDbContext db) : Repository<Court>(db), ICourtRepository;
