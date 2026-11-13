namespace BeamMPTools.Core.Models;

public sealed record LogEntry(DateTime Timestamp, string Level, string Message)
{
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
}
