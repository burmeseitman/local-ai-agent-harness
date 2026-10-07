using Harness.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Harness.Core.Resilience;

public class RetryPolicy : IRetryPolicy
{
    private readonly ILogger<RetryPolicy>? _logger;

    public RetryPolicy(ILogger<RetryPolicy>? logger = null)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<int, Task<T>> operation,
        Func<T, bool> shouldRetry,
        int maxRetries = 2,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(150);
        int attempt = 0;

        while (true)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await operation(attempt);

                if (attempt > maxRetries || !shouldRetry(result))
                {
                    return result;
                }

                _logger?.LogWarning("Operation marked for retry. Attempt {Attempt}/{MaxRetries}. Waiting {DelayMs}ms...",
                    attempt, maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2.0); // Exponential backoff
            }
            catch (Exception ex) when (attempt <= maxRetries && !cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Operation threw exception on attempt {Attempt}. Retrying after {DelayMs}ms...",
                    attempt, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2.0);
            }
        }
    }
}
