namespace CombinedEffect.Services.Interfaces;

internal interface IResilienceService
{
    void Execute(string operationName, Action action, int maxAttempts = 3);
    T Execute<T>(string operationName, Func<T> action, int maxAttempts = 3);
    Task ExecuteAsync(string operationName, Func<Task> action, int maxAttempts = 3, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(string operationName, Func<Task<T>> action, int maxAttempts = 3, CancellationToken cancellationToken = default);
}