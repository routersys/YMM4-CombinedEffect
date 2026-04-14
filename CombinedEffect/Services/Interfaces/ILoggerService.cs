namespace CombinedEffect.Services.Interfaces;

internal interface ILoggerService
{
    void LogInfo(string message);
    void LogError(string message, Exception? ex = null);
}