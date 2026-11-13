using BeamMPTools.Core.Models;

namespace BeamMPTools.Core.Logging;

public interface IAppLogger
{
    event Action<LogEntry>? EntryWritten;

    void Info(string message);
    void Warning(string message);
    void Error(string message);
}
