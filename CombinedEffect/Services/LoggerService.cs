using CombinedEffect.Services.Interfaces;
using System.IO;

namespace CombinedEffect.Services;

internal sealed class LoggerService : ILoggerService
{
    private readonly string _logDir;
    private readonly object _lock = new();
    private readonly ILoggerConfiguration _config;
    private DateTime _lastPurge = DateTime.MinValue;

    public LoggerService(ILoggerConfiguration config)
    {
        _config = config;
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _logDir = Path.Combine(baseDir, "user", "log", "CombinedEffect");
        Directory.CreateDirectory(_logDir);
    }

    public void LogInfo(string message) => WriteLog("INFO", message);

    public void LogError(string message, Exception? ex = null) =>
        WriteLog("ERROR", $"{message}{(ex != null ? "\n" + ex : "")}");

    private void WriteLog(string level, string message)
    {
        lock (_lock)
        {
            try
            {
                var now = DateTime.Now;
                if ((now - _lastPurge).TotalHours >= 1.0)
                {
                    PurgeOldLogs(now);
                    _lastPurge = now;
                }
                var activeFile = GetActiveLogFile(now);
                var logLine = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}\n";
                File.AppendAllText(activeFile, logLine);
            }
            catch { }
        }
    }

    private string GetActiveLogFile(DateTime now)
    {
        var files = Directory.GetFiles(_logDir, "log_*.txt")
                             .Select(f => new FileInfo(f))
                             .OrderByDescending(f => f.LastWriteTime)
                             .ToList();

        var latest = files.FirstOrDefault();
        if (latest is not null && latest.Length < _config.MaxLogFileSizeBytes)
            return latest.FullName;

        return Path.Combine(_logDir, $"log_{now:yyyyMMdd_HHmmss}.txt");
    }

    private void PurgeOldLogs(DateTime now)
    {
        foreach (var file in Directory.GetFiles(_logDir, "log_*.txt").Select(f => new FileInfo(f)))
        {
            if ((now - file.LastWriteTime).TotalDays >= _config.MaxRetentionDays)
                file.Delete();
        }
    }
}