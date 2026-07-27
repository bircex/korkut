using Korkut.Courts.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Courts;

/// <summary>EF/Postgres adapter for <see cref="ICourtRepository"/> over the ambient DbContext.</summary>
internal sealed class CourtRepository(DbContext db) : Repository<Court>(db), ICourtRepository;
