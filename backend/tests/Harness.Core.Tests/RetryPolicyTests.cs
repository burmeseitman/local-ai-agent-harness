using FluentAssertions;
using Harness.Core.Resilience;
using Xunit;

namespace Harness.Core.Tests;

public class RetryPolicyTests
{
    private readonly RetryPolicy _retryPolicy = new();

    [Fact]
    public async Task ExecuteWithRetryAsync_WhenOperationSucceedsFirstTry_ShouldNotRetry()
    {
        int attempts = 0;
        var result = await _retryPolicy.ExecuteWithRetryAsync(
            attempt =>
            {
                attempts++;
                return Task.FromResult("SUCCESS");
            },
            res => res != "SUCCESS",
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(5));

        attempts.Should().Be(1);
        result.Should().Be("SUCCESS");
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WhenTransientFailureRecovers_ShouldRetryAndSucceed()
    {
        int attempts = 0;
        var result = await _retryPolicy.ExecuteWithRetryAsync(
            attempt =>
            {
                attempts++;
                if (attempts == 1) return Task.FromResult("TRANSIENT_ERROR");
                return Task.FromResult("RECOVERED");
            },
            res => res == "TRANSIENT_ERROR",
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(5));

        attempts.Should().Be(2);
        result.Should().Be("RECOVERED");
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WhenPolicyOrFatalFailure_ShouldNotRetry()
    {
        int attempts = 0;
        var result = await _retryPolicy.ExecuteWithRetryAsync(
            attempt =>
            {
                attempts++;
                return Task.FromResult("POLICY_BLOCKED");
            },
            res => false, // Do not retry permanent policy failures
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(5));

        attempts.Should().Be(1);
        result.Should().Be("POLICY_BLOCKED");
    }
}
