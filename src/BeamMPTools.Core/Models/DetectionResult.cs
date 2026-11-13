namespace BeamMPTools.Core.Models;

public sealed record DetectionResult(
    string? BeamNGPath,
    string? BeamMPPath,
    string? BeamMPResourcesPath)
{
    public bool IsBeamNGFound => !string.IsNullOrWhiteSpace(BeamNGPath);
    public bool IsBeamMPFound => !string.IsNullOrWhiteSpace(BeamMPPath);
    public bool IsResourcesFound => !string.IsNullOrWhiteSpace(BeamMPResourcesPath);
}

public sealed record DetectionProgress(string Stage, long DirectoriesScanned = 0);
