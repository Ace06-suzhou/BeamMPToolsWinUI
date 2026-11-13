using BeamMPTools.Core.Models;

namespace BeamMPTools.Core.Logging;

/// <summary>
/// Thread-safe application logger. It has no dependency on a UI framework.
/// </summary>
public sealed class AppLogger : IAppLogger
{
    private readonly object _sync = new();
    private readonly string _logFilePath;

    public event Action<LogEntry>? EntryWritten;

    public AppLogger()
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BeamMPTools",
            "Logs");

        _logFilePath = Path.Combine(logDirectory, $"BeamMPTools-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message ?? string.Empty);
        string line = entry.DisplayText + Environment.NewLine;

        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
                File.AppendAllText(_logFilePath, line);
            }
        }
        catch
        {
            // Logging must never crash an installation or launch operation.
        }

        try
        {
            EntryWritten?.Invoke(entry);
        }
        catch
        {
            // A UI/log subscriber must never break the operation being logged.
        }
    }
}
