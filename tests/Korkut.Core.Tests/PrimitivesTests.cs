using Korkut.Core;

namespace Korkut.Core.Tests;

public class PrimitivesTests
{
    [Fact]
    public void UuidV7_is_version_7_and_unique()
    {
        var a = UuidV7.NewId();
        var b = UuidV7.NewId();

        Assert.NotEqual(a, b);
        Assert.Equal(7, a.Version);
    }

    [Fact]
    public void UuidV7_is_time_ordered()
    {
        var earlier = UuidV7.NewId(DateTimeOffset.UnixEpoch);
        var later = UuidV7.NewId(DateTimeOffset.UnixEpoch.AddYears(30));

        // UUIDv7 embeds a millisecond timestamp in the high bytes, so a later id
        // sorts after an earlier one under the standard Guid comparer.
        Assert.True(Comparer<Guid>.Default.Compare(later, earlier) > 0);
    }

    [Fact]
    public void SystemClock_returns_utc()
    {
        Assert.Equal(TimeSpan.Zero, SystemClock.Instance.UtcNow.Offset);
    }

    [Fact]
    public void Entities_with_same_id_are_equal()
    {
        var id = UuidV7.NewId();
        var a = new TestEntity(id);
        var b = new TestEntity(id);
        var c = new TestEntity(UuidV7.NewId());

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    private sealed class TestEntity(Guid id) : Entity(id);
}
