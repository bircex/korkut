using Korkut.Application.Jobs;

namespace Korkut.Application.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void Backoff_grows_exponentially_and_is_capped()
    {
        var policy = new ExponentialRetryPolicy(maxAttempts: 6, baseSeconds: 2, maxSeconds: 60);

        Assert.Equal(TimeSpan.FromSeconds(2), policy.BackoffFor(1));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.BackoffFor(2));
        Assert.Equal(TimeSpan.FromSeconds(8), policy.BackoffFor(3));
        Assert.Equal(TimeSpan.FromSeconds(60), policy.BackoffFor(10)); // capped
    }

    [Fact]
    public void Max_attempts_is_exposed()
    {
        Assert.Equal(6, new ExponentialRetryPolicy(maxAttempts: 6).MaxAttempts);
    }
}
