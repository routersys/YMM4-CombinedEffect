using System.Globalization;
using System.IO;
using System.Text;

namespace CombinedEffect.Infrastructure;

internal static class AtomicFileWriter
{
    private const string CrcSeparator = "\n<|CRC32|>";

    public static string? ReadVerified(string path) =>
        WithRetry(() => TryReadVerified(path) ?? TryReadVerified(path + ".bak") ?? TryReadVerified(path + ".tmp"));

    public static void WriteAtomic(string path, string content)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var crc = Crc32.Compute(contentBytes);
        var fullContent = $"{content}{CrcSeparator}{crc:X8}";
        var tempPath = path + ".tmp";
        WithRetry(() =>
        {
            try
            {
                File.WriteAllText(tempPath, fullContent, Encoding.UTF8);
                try
                {
                    File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
                }
                catch (FileNotFoundException)
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
                throw;
            }
            return true;
        });
    }

    public static async Task WriteAtomicAsync(string path, string content)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var crc = Crc32.Compute(contentBytes);
        var fullContent = $"{content}{CrcSeparator}{crc:X8}";
        var tempPath = path + ".tmp";
        await WithRetryAsync(async () =>
        {
            try
            {
                await File.WriteAllTextAsync(tempPath, fullContent, Encoding.UTF8).ConfigureAwait(false);
                try
                {
                    File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
                }
                catch (FileNotFoundException)
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
                throw;
            }
            return true;
        }).ConfigureAwait(false);
    }

    private static string? TryReadVerified(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var raw = File.ReadAllText(path, Encoding.UTF8);
            var separatorIndex = raw.LastIndexOf(CrcSeparator, StringComparison.Ordinal);
            if (separatorIndex < 0) return raw;

            var content = raw[..separatorIndex];
            var storedHex = raw[(separatorIndex + CrcSeparator.Length)..].Trim();
            if (!uint.TryParse(storedHex, NumberStyles.HexNumber, null, out var storedCrc))
                return null;

            var contentBytes = Encoding.UTF8.GetBytes(content);
            return Crc32.Compute(contentBytes) == storedCrc ? content : null;
        }
        catch { return null; }
    }

    private static T WithRetry<T>(Func<T> action, int maxRetries = 3)
    {
        var delay = 50;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return action();
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(delay);
                delay *= 2;
            }
        }
        return action();
    }

    private static async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
    {
        var delay = 50;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
                delay *= 2;
            }
        }
        return await action().ConfigureAwait(false);
    }
}