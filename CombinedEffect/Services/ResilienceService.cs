using CombinedEffect.Services.Interfaces;

namespace CombinedEffect.Services;

internal sealed class ResilienceService(ILoggerService logger) : IResilienceService
{
    private readonly ILoggerService _logger = logger;

    public void Execute(string operationName, Action action, int maxAttempts = 3)
    {
        _ = ExecuteInternal(operationName, () =>
        {
            action();
            return true;
        }, maxAttempts);
    }

    public T Execute<T>(string operationName, Func<T> action, int maxAttempts = 3) =>
        ExecuteInternal(operationName, action, maxAttempts);

    public Task ExecuteAsync(string operationName, Func<Task> action, int maxAttempts = 3, CancellationToken cancellationToken = default) =>
        ExecuteAsyncInternal(operationName, async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, maxAttempts, cancellationToken);

    public Task<T> ExecuteAsync<T>(string operationName, Func<Task<T>> action, int maxAttempts = 3, CancellationToken cancellationToken = default) =>
        ExecuteAsyncInternal(operationName, action, maxAttempts, cancellationToken);

    private T ExecuteInternal<T>(string operationName, Func<T> action, int maxAttempts)
    {
        var attempts = NormalizeAttempts(maxAttempts);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                lastException = ex;
                var isLast = attempt >= attempts;
                _logger.LogError($"{operationName} failed ({attempt}/{attempts})", ex);
                if (isLast)
                    throw;
            }
        }

        throw lastException ?? new InvalidOperationException(operationName);
    }

    private async Task<T> ExecuteAsyncInternal<T>(string operationName, Func<Task<T>> action, int maxAttempts, CancellationToken cancellationToken)
    {
        var attempts = NormalizeAttempts(maxAttempts);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;
                var isLast = attempt >= attempts;
                _logger.LogError($"{operationName} failed ({attempt}/{attempts})", ex);
                if (isLast)
                    throw;
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException ?? new InvalidOperationException(operationName);
    }

    private static int NormalizeAttempts(int maxAttempts) =>
        maxAttempts < 1 ? 1 : maxAttempts;

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(300, 40 * attempt * attempt));
}