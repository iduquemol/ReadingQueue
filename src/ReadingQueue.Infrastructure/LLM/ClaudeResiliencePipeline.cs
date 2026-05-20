using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ReadingQueue.Infrastructure.LLM;

public static class ClaudeResiliencePipeline
{
    public static ResiliencePipeline Build(ILogger logger, int timeoutSeconds = 30, int maxRetries = 3)
    {
        var builder = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            });

        if (maxRetries > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                ShouldHandle     = new PredicateBuilder().Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Reintento {Attempt} a Claude. Delay: {Delay}ms.",
                        args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            });
        }

        return builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio      = 1.0,
                MinimumThroughput = 5,
                SamplingDuration  = TimeSpan.FromSeconds(30),
                BreakDuration     = TimeSpan.FromSeconds(60),
                ShouldHandle      = new PredicateBuilder().Handle<HttpRequestException>(),
                OnOpened = args =>
                {
                    logger.LogWarning(
                        "Circuit breaker de Claude ABIERTO. Fallback activo por 60s.");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation(
                        "Circuit breaker de Claude CERRADO. Llamadas normales restablecidas.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
