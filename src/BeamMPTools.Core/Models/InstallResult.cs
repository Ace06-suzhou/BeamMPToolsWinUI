namespace BeamMPTools.Core.Models;

public sealed record InstallResult(
    bool Success,
    string? Version,
    string? InstalledExecutable,
    string? ErrorMessage)
{
    public static InstallResult Failed(string message) =>
        new(false, null, null, message);
}

public sealed record InstallProgress(
    string Stage,
    long BytesReceived = 0,
    long? TotalBytes = null)
{
    public int? Percent => TotalBytes is > 0
        ? (int)Math.Clamp(BytesReceived * 100d / TotalBytes.Value, 0, 100)
        : null;
}
