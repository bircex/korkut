using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Courts;

/// <summary>EF/Postgres adapter for <see cref="ICourtRepository"/> over the ambient DbContext.</summary>
public sealed class CourtRepository(DbContext db) : Repository<Court>(db), ICourtRepository;
