using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using BeamMPTools.Core.Logging;

namespace BeamMPTools.Core.Launching;

/// <summary>
/// Launches BeamNG through its Steam AppID so Steam can record play time.
/// For BeamMP, the Launcher is started first in --no-launch mode, then Steam
/// starts BeamNG so the game session is still owned by Steam.
/// </summary>
public sealed class SteamLauncher
{
    public const int BeamNGAppId = 284160;
    private const int DefaultBeamMPPort = 4444;
    private static readonly TimeSpan LauncherReadyTimeout = TimeSpan.FromSeconds(20);
    private static readonly Regex LauncherPortRegex = new(
        @"""Port""\s*:\s*(?<port>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IAppLogger _logger;

    public SteamLauncher(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task LaunchBeamNGAsync()
    {
        LaunchSteamUri($"steam://launch/{BeamNGAppId}");
        _logger.Info("已向 Steam 提交 BeamNG.drive 启动请求（AppID 284160）。");
        return Task.CompletedTask;
    }

    public async Task LaunchBeamMPAsync(string launcherExecutable, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(launcherExecutable))
            throw new FileNotFoundException("未找到 BeamMP Launcher 可执行文件。", launcherExecutable);

        int launcherPort = ReadLauncherPort(launcherExecutable);
        Process? launcherProcess = FindRunningLauncher(launcherExecutable);
        if (launcherProcess is null)
        {
            string workingDirectory = Path.GetDirectoryName(launcherExecutable)
                ?? Environment.CurrentDirectory;

            var startInfo = new ProcessStartInfo
            {
                FileName = launcherExecutable,
                Arguments = "--no-launch",
                WorkingDirectory = workingDirectory,
                // Preserve the Launcher's normal Windows console/window behavior.
                UseShellExecute = true
            };

            launcherProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows 未能创建 BeamMP Launcher 进程。");

            _logger.Info("已启动 BeamMP Launcher（--no-launch），正在等待客户端初始化…");
        }
        else
        {
            _logger.Info("检测到 BeamMP Launcher 已在运行，将复用现有进程。");
        }

        using (launcherProcess)
        {
            bool ready = await WaitForLauncherAsync(launcherProcess, launcherPort, cancellationToken);
            if (!ready)
            {
                throw new TimeoutException(
                    $"等待 BeamMP Launcher 通信端口 {launcherPort} 超时。为避免启动成普通 BeamNG 会话，已取消 Steam 启动。请单独运行 BeamMP Launcher 检查错误、端口设置或防火墙。");
            }

            _logger.Info($"BeamMP Launcher 本地通信端口 {launcherPort} 已就绪。");
            cancellationToken.ThrowIfCancellationRequested();
            LaunchSteamUri($"steam://launch/{BeamNGAppId}");
            _logger.Info("已通过 Steam 启动 BeamNG.drive，Steam 将按 AppID 284160 记录游戏时长。");
        }
    }

    public Task ValidateBeamNGAsync()
    {
        LaunchSteamUri($"steam://validate/{BeamNGAppId}");
        _logger.Info("已向 Steam 提交 BeamNG.drive 文件完整性验证请求。");
        return Task.CompletedTask;
    }

    private static Process? FindRunningLauncher(string launcherExecutable)
    {
        string expectedPath = Path.GetFullPath(launcherExecutable);

        foreach (string processName in new[] { "BeamMP-Launcher", "BeamMP" })
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                bool matches = false;
                try
                {
                    string? actualPath = process.MainModule?.FileName;
                    matches = !string.IsNullOrWhiteSpace(actualPath)
                        && string.Equals(Path.GetFullPath(actualPath), expectedPath, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    // A protected process may not expose MainModule to this application.
                }

                if (matches)
                    return process;

                process.Dispose();
            }
        }

        return null;
    }

    private static int ReadLauncherPort(string launcherExecutable)
    {
        string? launcherDirectory = Path.GetDirectoryName(Path.GetFullPath(launcherExecutable));
        if (string.IsNullOrWhiteSpace(launcherDirectory))
            return DefaultBeamMPPort;

        string configPath = Path.Combine(launcherDirectory, "launcher.cfg");
        try
        {
            if (!File.Exists(configPath))
                return DefaultBeamMPPort;

            string configuration = File.ReadAllText(configPath);
            Match match = LauncherPortRegex.Match(configuration);
            if (match.Success
                && int.TryParse(match.Groups["port"].Value, out int port)
                && port is >= 1 and <= 65535)
            {
                return port;
            }
        }
        catch
        {
            // Use BeamMP's documented default when the optional config is unreadable.
        }

        return DefaultBeamMPPort;
    }

    private static async Task<bool> WaitForLauncherAsync(
        Process launcherProcess,
        int launcherPort,
        CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < LauncherReadyTimeout)
        {
            token.ThrowIfCancellationRequested();

            if (IsLocalTcpPortListening(launcherPort))
                return true;

            if (launcherProcess.HasExited)
                throw new InvalidOperationException(
                    "BeamMP Launcher 在初始化完成前退出。请单独运行 Launcher 查看其错误信息后重试。");

            await Task.Delay(250, token);
        }

        return false;
    }

    private static bool IsLocalTcpPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    private static void LaunchSteamUri(string uri)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}
