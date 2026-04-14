using CombinedEffect.Services.Interfaces;

namespace CombinedEffect.Services;

internal sealed class LoggerConfiguration : ILoggerConfiguration
{
    public long MaxLogFileSizeBytes => 512 * 1024;
    public int MaxRetentionDays => 31;
}