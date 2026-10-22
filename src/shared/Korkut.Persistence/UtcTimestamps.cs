using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Korkut.Persistence;

/// <summary>
/// Every timestamp is written as UTC. Postgres <c>timestamp with time zone</c> stores an instant, and
/// Npgsql refuses a <see cref="DateTimeOffset"/> with any other offset, so a time a client sends as
/// Turkish time (+03:00) used to fail the save. Converting on the way in keeps the same instant;
/// reads come back as UTC, as before.
/// </summary>
public static class UtcTimestamps
{
    public static void UseUtcTimestamps(this ModelConfigurationBuilder configuration)
    {
        configuration.Properties<DateTimeOffset>().HaveConversion<ToUtc>();
        configuration.Properties<DateTimeOffset?>().HaveConversion<ToUtc>();
    }

    private sealed class ToUtc() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);
}
