using CombinedEffect.Services.Interfaces;
using System.IO;

namespace CombinedEffect.Services;

internal sealed class LoggerService : ILoggerService
{
    private readonly string _logDir;
    private readonly object _lock = new();
    private readonly ILoggerConfiguration _config;

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
                PurgeOldLogs();
                var activeFile = GetActiveLogFile();
                var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}\n";
                File.AppendAllText(activeFile, logLine);
            }
            catch { }
        }
    }

    private string GetActiveLogFile()
    {
        var files = Directory.GetFiles(_logDir, "log_*.txt")
                             .Select(f => new FileInfo(f))
                             .OrderByDescending(f => f.LastWriteTime)
                             .ToList();

        var latest = files.FirstOrDefault();
        if (latest != null && latest.Length < _config.MaxLogFileSizeBytes)
        {
            return latest.FullName;
        }

        return Path.Combine(_logDir, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
    }

    private void PurgeOldLogs()
    {
        var files = Directory.GetFiles(_logDir, "log_*.txt").Select(f => new FileInfo(f)).ToList();
        var now = DateTime.Now;

        foreach (var file in files)
        {
            if (file.LastWriteTime.Year != now.Year || file.LastWriteTime.Month != now.Month)
            {
                file.Delete();
            }
            else if ((now - file.LastWriteTime).TotalDays >= _config.MaxRetentionDays)
            {
                file.Delete();
            }
        }
    }
}