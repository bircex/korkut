using Korkut.Domain.Primitives;
using Korkut.Domain.Ids;
using Korkut.Domain.Results;
using Korkut.Domain.Time;

namespace Korkut.Domain.Tests;

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
    public void Result_success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Result_failure_carries_error_and_blocks_value_access()
    {
        var error = new Error("matter.not_found", "Dosya bulunamadı");
        var result = Result.Failure<int>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Result_success_exposes_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
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
