using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BeamMPTools.Core.Models;
using Microsoft.Win32;

namespace BeamMPTools.Core.Detection;

/// <summary>
/// Detects BeamNG.drive, BeamMP Launcher, and the BeamMP Resources folder.
/// Long-running drive scans run away from the UI thread, can be cancelled,
/// and never follow directory junctions or symbolic links.
/// </summary>
public sealed class PathDetector
{
    private const int MaximumScanDepth = 18;
    private static readonly Regex VdfPathRegex = new(
        @"""path""\s*""(?<path>(?:\\\\|[^""])*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InstallDirRegex = new(
        @"""installdir""\s*""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "ProgramData", "AppData", "Temp", "$RECYCLE.BIN", "System Volume Information",
        "node_modules", ".git", ".vs", "bin", "obj", "packages", "cache",
        "Debug", "Release", "PerfLogs", "$WinREAgent", "WindowsApps"
    };

    public Task<DetectionResult> DetectAsync(
        IProgress<DetectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => DetectCore(progress, cancellationToken),
            cancellationToken);
    }

    private static DetectionResult DetectCore(
        IProgress<DetectionProgress>? progress,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        progress?.Report(new DetectionProgress("正在检测 BeamNG.drive…"));
        string? beamNG = DetectRunningBeamNG()
            ?? DetectBeamNGFromSteam()
            ?? DetectBeamNGFromCommonPaths();

        if (beamNG is null)
        {
            progress?.Report(new DetectionProgress("未在常见位置找到 BeamNG，开始扫描固定磁盘…"));
            beamNG = DeepScanExecutable("BeamNG.drive.exe", isBeamNG: true, progress, token);
        }

        token.ThrowIfCancellationRequested();
        progress?.Report(new DetectionProgress("正在检测 BeamMP Launcher…"));
        string? beamMP = DetectRunningBeamMP()
            ?? DetectBeamMPFromRecent()
            ?? DetectBeamMPFromKnownPaths();

        if (beamMP is null)
        {
            progress?.Report(new DetectionProgress("未在常见位置找到 BeamMP，开始扫描固定磁盘…"));
            beamMP = DeepScanExecutable("BeamMP-Launcher.exe", isBeamNG: false, progress, token)
                ?? DeepScanExecutable("BeamMP.exe", isBeamNG: false, progress, token);
        }

        token.ThrowIfCancellationRequested();
        string? resources = beamMP is null ? null : DetectResources(beamMP);

        progress?.Report(new DetectionProgress("环境检测完成。"));
        return new DetectionResult(beamNG, beamMP, resources);
    }

    private static string? DetectRunningBeamNG()
    {
        string[] processNames = { "BeamNG.drive", "BeamNG.drive.x64", "BeamNG.drive.x64.Release" };
        return DetectRunningProcess(processNames, isBeamNG: true);
    }

    private static string? DetectRunningBeamMP()
    {
        string[] processNames = { "BeamMP-Launcher", "BeamMP" };
        return DetectRunningProcess(processNames, isBeamNG: false);
    }

    private static string? DetectRunningProcess(IEnumerable<string> processNames, bool isBeamNG)
    {
        var names = new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase);

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (!names.Contains(process.ProcessName))
                        continue;

                    string? executable = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(executable))
                        continue;

                    string? directory = Path.GetDirectoryName(executable);
                    for (int depth = 0; !string.IsNullOrWhiteSpace(directory) && depth <= 3; depth++)
                    {
                        if (isBeamNG ? IsValidBeamNGPath(directory) : IsValidBeamMPPath(directory))
                            return directory;

                        directory = Directory.GetParent(directory)?.FullName;
                    }
                }
                catch
                {
                    // Access to another process' MainModule may be denied.
                }
            }
        }

        return null;
    }

    private static string? DetectBeamNGFromSteam()
    {
        foreach (string library in GetSteamLibraries())
        {
            string manifestPath = Path.Combine(library, "steamapps", "appmanifest_284160.acf");
            string installDirectoryName = "BeamNG.drive";

            if (File.Exists(manifestPath))
            {
                try
                {
                    Match match = InstallDirRegex.Match(File.ReadAllText(manifestPath));
                    if (match.Success)
                        installDirectoryName = match.Groups["path"].Value;
                }
                catch
                {
                    // Use the known Steam install directory name if the manifest is unreadable.
                }
            }

            string candidate = Path.Combine(library, "steamapps", "common", installDirectoryName);
            if (IsValidBeamNGPath(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> GetSteamLibraries()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string steamRoot in GetSteamInstallPaths())
        {
            if (!Directory.Exists(steamRoot))
                continue;

            libraries.Add(steamRoot);
            string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
                continue;

            try
            {
                string contents = File.ReadAllText(vdfPath);
                foreach (Match match in VdfPathRegex.Matches(contents))
                {
                    string libraryPath = match.Groups["path"].Value.Replace("\\\\", "\\");
                    if (Path.IsPathRooted(libraryPath))
                        libraries.Add(libraryPath);
                }
            }
            catch
            {
                // Continue with the Steam root and other registered libraries.
            }
        }

        return libraries;
    }

    private static IEnumerable<string> GetSteamInstallPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] registryKeys =
        {
            @"Software\Valve\Steam",
            @"Software\WOW6432Node\Valve\Steam"
        };

        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (string registryPath in registryKeys)
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                    using RegistryKey? key = baseKey.OpenSubKey(registryPath);
                    string? value = key?.GetValue("SteamPath") as string
                        ?? key?.GetValue("InstallPath") as string;

                    if (!string.IsNullOrWhiteSpace(value))
                        paths.Add(value.Replace('/', '\\'));
                }
                catch
                {
                    // Try other registry views / keys.
                }

                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
                    using RegistryKey? key = baseKey.OpenSubKey(registryPath);
                    string? value = key?.GetValue("SteamPath") as string
                        ?? key?.GetValue("InstallPath") as string;

                    if (!string.IsNullOrWhiteSpace(value))
                        paths.Add(value.Replace('/', '\\'));
                }
                catch
                {
                    // Steam may be installed for another registry view only.
                }
            }
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            string fallback = Path.Combine(programFiles, "Steam");
            if (Directory.Exists(fallback))
                paths.Add(fallback);
        }

        return paths;
    }

    private static string? DetectBeamNGFromCommonPaths()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string library in GetSteamLibraries())
            candidates.Add(Path.Combine(library, "steamapps", "common", "BeamNG.drive"));

        foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
        {
            try
            {
                if (!driveInfo.IsReady || driveInfo.DriveType != DriveType.Fixed)
                    continue;

                string drive = driveInfo.RootDirectory.FullName;
                candidates.Add(Path.Combine(drive, "SteamLibrary", "steamapps", "common", "BeamNG.drive"));
                candidates.Add(Path.Combine(drive, "Games", "BeamNG.drive"));
                candidates.Add(Path.Combine(drive, "Games", "Steam", "steamapps", "common", "BeamNG.drive"));
            }
            catch
            {
                // Ignore one unavailable or damaged drive and keep checking the others.
            }
        }

        foreach (string candidate in candidates)
        {
            if (IsValidBeamNGPath(candidate))
                return candidate;
        }

        return null;
    }

    private static string? DetectBeamMPFromRecent()
    {
        string recentDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Recent");

        if (!Directory.Exists(recentDirectory))
            return null;

        try
        {
            foreach (string shortcutPath in Directory.EnumerateFiles(recentDirectory, "*.lnk"))
            {
                if (!Path.GetFileName(shortcutPath).Contains("beammp", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? target = ResolveShortcutTarget(shortcutPath);
                if (string.IsNullOrWhiteSpace(target))
                    continue;

                string? directory = Directory.Exists(target)
                    ? target
                    : Path.GetDirectoryName(target);

                if (!string.IsNullOrWhiteSpace(directory) && IsValidBeamMPPath(directory))
                    return directory;
            }
        }
        catch
        {
            // Recent shortcuts are an optional detection source.
        }

        return null;
    }

    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return null;

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return null;

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: new object[] { shortcutPath });

            if (shortcut is null)
                return null;

            return shortcut.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                binder: null,
                target: shortcut,
                args: null) as string;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static string? DetectBeamMPFromKnownPaths()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Current BeamMP Windows installer defaults to %APPDATA% (Roaming).
            Path.Combine(roaming, "BeamMP-Launcher"),
            Path.Combine(roaming, "BeamMP"),
            Path.Combine(local, "BeamMP-Launcher"),
            Path.Combine(local, "BeamMP"),
            @"C:\BeamMP", @"D:\BeamMP", @"E:\BeamMP",
            @"C:\Games\BeamMP", @"D:\Games\BeamMP",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BeamMP"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "BeamMP")
        };

        foreach (string candidate in candidates)
        {
            if (IsValidBeamMPPath(candidate))
                return candidate;
        }

        return null;
    }

    private static string? DetectResources(string beamMPPath)
    {
        string[] candidates =
        {
            Path.Combine(beamMPPath, "Resources"),
            Path.Combine(beamMPPath, "resources"),
            Path.Combine(beamMPPath, "Client", "Resources"),
            Path.Combine(beamMPPath, "Client", "resources")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? DeepScanExecutable(
        string executableName,
        bool isBeamNG,
        IProgress<DetectionProgress>? progress,
        CancellationToken token)
    {
        long scannedDirectories = 0;

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            token.ThrowIfCancellationRequested();

            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                    continue;

                var pending = new Stack<(string Path, int Depth)>();
                pending.Push((drive.RootDirectory.FullName, 0));
                progress?.Report(new DetectionProgress(
                    $"正在扫描 {drive.Name}（已检查 {scannedDirectories:N0} 个目录）…",
                    scannedDirectories));

                while (pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    (string currentPath, int depth) = pending.Pop();

                    string directoryName = Path.GetFileName(currentPath.TrimEnd(Path.DirectorySeparatorChar));
                    if (IgnoredDirectoryNames.Contains(directoryName))
                        continue;

                    try
                    {
                        string candidate = Path.Combine(currentPath, executableName);
                        if (File.Exists(candidate) &&
                            (isBeamNG ? IsValidBeamNGPath(currentPath) : IsValidBeamMPPath(currentPath)))
                        {
                            return currentPath;
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible paths.
                    }

                    scannedDirectories++;
                    if (scannedDirectories % 250 == 0)
                    {
                        progress?.Report(new DetectionProgress(
                            $"正在扫描 {drive.Name}（已检查 {scannedDirectories:N0} 个目录）…",
                            scannedDirectories));
                    }

                    if (depth >= MaximumScanDepth)
                        continue;

                    try
                    {
                        foreach (string childDirectory in Directory.EnumerateDirectories(currentPath))
                        {
                            try
                            {
                                FileAttributes attributes = File.GetAttributes(childDirectory);
                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                    continue;

                                string childName = Path.GetFileName(childDirectory.TrimEnd(Path.DirectorySeparatorChar));
                                if (!IgnoredDirectoryNames.Contains(childName))
                                    pending.Push((childDirectory, depth + 1));
                            }
                            catch
                            {
                                // Access denied, dangling link, or a directory removed mid-scan.
                            }
                        }
                    }
                    catch
                    {
                        // Access denied or an unreadable directory.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Continue with the next fixed drive.
            }
        }

        return null;
    }

    private static bool IsValidBeamNGPath(string path)
    {
        try
        {
            return File.Exists(Path.Combine(path, "BeamNG.drive.exe"));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidBeamMPPath(string path)
    {
        try
        {
            return File.Exists(Path.Combine(path, "BeamMP-Launcher.exe"))
                || File.Exists(Path.Combine(path, "BeamMP.exe"));
        }
        catch
        {
            return false;
        }
    }
}
