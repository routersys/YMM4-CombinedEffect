namespace CombinedEffect.Services.Interfaces;

internal interface ILoggerConfiguration
{
    long MaxLogFileSizeBytes { get; }
    int MaxRetentionDays { get; }
}