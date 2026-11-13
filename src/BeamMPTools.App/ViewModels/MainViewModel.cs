using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BeamMPTools.App.Commands;
using BeamMPTools.Core.Detection;
using BeamMPTools.Core.Installation;
using BeamMPTools.Core.Launching;
using BeamMPTools.Core.Logging;
using BeamMPTools.Core.Models;
using Microsoft.UI.Dispatching;

namespace BeamMPTools.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly PathDetector _pathDetector;
    private readonly ClientInstaller _clientInstaller;
    private readonly SteamLauncher _steamLauncher;
    private readonly IAppLogger _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _initialized;
    private bool _isBusy;
    private string _busyText = string.Empty;
    private string _beamNGStatus = "尚未检测";
    private string _beamNGPath = "—";
    private string _beamMPStatus = "尚未检测";
    private string _beamMPPath = "—";
    private string _resourcesStatus = "尚未检测";
    private string _resourcesPath = "—";
    private bool _hasBeamMPInstalled;
    private bool _hasResources;
    private CancellationTokenSource? _activeCancellation;
    private string? _manualBeamMPInstallPath;
    public string BeamMPInstallPath =>
    _manualBeamMPInstallPath ?? (HasBeamMPInstalled ? BeamMPPath : string.Empty);
    public bool SetBeamMPInstallPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        string fullPath = Path.GetFullPath(path);

        bool launcherExists =
            File.Exists(Path.Combine(fullPath, "BeamMP-Launcher.exe")) ||
            File.Exists(Path.Combine(fullPath, "BeamMP.exe"));

        if (!launcherExists)
            return false;

        _manualBeamMPInstallPath = fullPath;

        BeamMPPath = fullPath;
        BeamMPStatus = "已找到（手动指定）";
        HasBeamMPInstalled = true;

        OnPropertyChanged(nameof(BeamMPInstallPath));

        _logger.Info($"用户手动指定 BeamMP 安装目录：{fullPath}");

        return true;
    }

    public void ClearManualBeamMPInstallPath()
    {
        _manualBeamMPInstallPath = null;

        OnPropertyChanged(nameof(BeamMPInstallPath));

        _logger.Info("已恢复 BeamMP 自动路径检测。");
    }
    public MainViewModel(
        PathDetector pathDetector,
        ClientInstaller clientInstaller,
        SteamLauncher steamLauncher,
        IAppLogger logger,
        DispatcherQueue dispatcherQueue)
    {
        _pathDetector = pathDetector;
        _clientInstaller = clientInstaller;
        _steamLauncher = steamLauncher;
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;

        LogEntries = new ObservableCollection<LogEntry>();
        _logger.EntryWritten += OnLogEntryWritten;

        DetectCommand = new AsyncRelayCommand(DetectAsync, () => IsNotBusy);
        LaunchBeamNGCommand = new AsyncRelayCommand(LaunchBeamNGAsync, () => IsNotBusy);
        LaunchBeamMPCommand = new AsyncRelayCommand(LaunchBeamMPAsync, () => IsNotBusy && HasBeamMPInstalled);
        OpenResourcesCommand = new RelayCommand(OpenResources, () => !IsBusy && _hasResources);
        OpenDocsCommand = new RelayCommand(OpenDocs, () => !IsBusy);
        ValidateSteamCommand = new AsyncRelayCommand(ValidateSteamAsync, () => IsNotBusy);
        ClearLogsCommand = new RelayCommand(ClearLogs, () => LogEntries.Count > 0);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => CanCancel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LogEntry> LogEntries { get; }

    public AsyncRelayCommand DetectCommand { get; }
    public AsyncRelayCommand LaunchBeamNGCommand { get; }
    public AsyncRelayCommand LaunchBeamMPCommand { get; }
    public RelayCommand OpenResourcesCommand { get; }
    public RelayCommand OpenDocsCommand { get; }
    public AsyncRelayCommand ValidateSteamCommand { get; }
    public RelayCommand ClearLogsCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanCancel));
                NotifyCommandStates();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;
    public bool CanCancel => IsBusy && _activeCancellation is not null;

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    public string BeamNGStatus
    {
        get => _beamNGStatus;
        private set => SetProperty(ref _beamNGStatus, value);
    }

    public string BeamNGPath
    {
        get => _beamNGPath;
        private set => SetProperty(ref _beamNGPath, value);
    }

    public string BeamMPStatus
    {
        get => _beamMPStatus;
        private set => SetProperty(ref _beamMPStatus, value);
    }

    public string BeamMPPath
    {
        get => _beamMPPath;
        private set => SetProperty(ref _beamMPPath, value);
    }

    public string ResourcesStatus
    {
        get => _resourcesStatus;
        private set => SetProperty(ref _resourcesStatus, value);
    }

    public string ResourcesPath
    {
        get => _resourcesPath;
        private set => SetProperty(ref _resourcesPath, value);
    }

    public bool HasBeamMPInstalled
    {
        get => _hasBeamMPInstalled;
        private set
        {
            if (SetProperty(ref _hasBeamMPInstalled, value))
                NotifyCommandStates();
        }
    }

    public bool HasResources
    {
        get => _hasResources;
        private set
        {
            if (SetProperty(ref _hasResources, value))
                NotifyCommandStates();
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        _logger.Info("喵 1 2 3");
        _logger.Info("阿里嘎多");
        await DetectAsync();
    }

    public async Task DetectAsync()
    {
        if (IsBusy)
            return;

        await RunBusyAsync(async token =>
        {
            _logger.Info("开始检测本地 BeamNG / BeamMP 环境…");
            var progress = new Progress<DetectionProgress>(item => BusyText = item.Stage);
            DetectionResult result = await _pathDetector.DetectAsync(progress, token);
            ApplyDetectionResult(result);
            LogDetectionResult(result);
        });
    }
    public string GetDefaultBeamMPInstallPath()
    {
        // 已经检测到安装时，优先使用现有安装目录
        if (HasBeamMPInstalled &&
            !string.IsNullOrWhiteSpace(BeamMPPath) &&
            Directory.Exists(BeamMPPath))
        {
            return Path.GetFullPath(BeamMPPath);
        }

        // 首次安装使用 BeamMP 官方默认目录
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BeamMP-Launcher");
    }

    public async Task InstallAsync(string targetDirectory)
    {
        if (IsBusy)
            return;

        await RunBusyAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new InvalidOperationException("安装目录不能为空。");

            string installDirectory = Path.GetFullPath(targetDirectory);

            _logger.Info($"准备在所选目录中安装/更新 BeamMP Launcher：{installDirectory}");

            var progress = new Progress<InstallProgress>(item =>
            {
                BusyText = item.Percent.HasValue
                    ? $"{item.Stage} {item.Percent.Value}%"
                    : item.Stage;
            });

            InstallResult result = await _clientInstaller.InstallAsync(
                installDirectory,
                progress,
                token);

            if (!result.Success)
            {
                _logger.Error($"BeamMP Launcher 安装/更新失败：{result.ErrorMessage}");
                return;
            }

            // 更新当前记录的安装路径
            BeamMPPath = installDirectory;
            BeamMPStatus = "已找到";
            HasBeamMPInstalled = true;

            _logger.Info($"BeamMP Launcher {result.Version} 安装/更新成功：{result.InstalledExecutable}");
            _logger.Info("操作完成。");
        });
    }


    private async Task LaunchBeamNGAsync()
    {
        if (IsBusy)
            return;

        await RunBusyAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            await _steamLauncher.LaunchBeamNGAsync();
        });
    }

    private async Task LaunchBeamMPAsync()
    {
        if (IsBusy)
            return;

        await RunBusyAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(BeamMPInstallPath))
                throw new InvalidOperationException("尚未指定 BeamMP 安装目录。");

            string launcherPath = Path.Combine(
                BeamMPInstallPath,
                "BeamMP-Launcher.exe");

            await _steamLauncher.LaunchBeamMPAsync(launcherPath, token);
        });
    }

    private async Task ValidateSteamAsync()
    {
        if (IsBusy)
            return;

        await RunBusyAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            await _steamLauncher.ValidateBeamNGAsync();
        });
    }

    private void OpenResources()
    {
        if (!HasResources || string.IsNullOrWhiteSpace(ResourcesPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ResourcesPath,
                UseShellExecute = true
            });
            _logger.Info($"已打开 BeamMP Resources：{ResourcesPath}");
        }
        catch (Exception exception)
        {
            _logger.Error($"打开 Resources 文件夹失败：{exception.Message}");
        }
    }

    public void OpenBeamMPSite()
    {
        const string url = "https://beammp.com/";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            _logger.Info($"已打开 BeamMP 官方网站：{url}");
        }
        catch (Exception exception)
        {
            _logger.Error($"打开 BeamMP 官方网站失败：{exception.Message}");
        }
    }

    private void OpenDocs()
    {
        const string url = "https://docs.beammp.com/zh/";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            _logger.Info($"已打开 BeamMP 中文文档：{url}");
        }
        catch (Exception exception)
        {
            _logger.Error($"打开 BeamMP 文档失败：{exception.Message}");
        }
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> operation)
    {
        using var cancellation = new CancellationTokenSource();
        _activeCancellation = cancellation;
        OnPropertyChanged(nameof(CanCancel));
        CancelCommand.NotifyCanExecuteChanged();
        IsBusy = true;
        BusyText = "正在处理…";
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("操作已取消。");
        }
        catch (Exception exception)
        {
            _logger.Error(exception.Message);
        }
        finally
        {
            _activeCancellation = null;
            OnPropertyChanged(nameof(CanCancel));
            CancelCommand.NotifyCanExecuteChanged();
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    private void CancelCurrentOperation()
    {
        _activeCancellation?.Cancel();
    }

    private void ApplyDetectionResult(DetectionResult result)
    {
        BeamNGStatus = result.IsBeamNGFound ? "已找到" : "未找到";
        BeamNGPath = result.BeamNGPath ?? "未检测到 BeamNG.drive 安装目录";

        BeamMPStatus = result.IsBeamMPFound ? "已找到" : "未找到";
        BeamMPPath = result.BeamMPPath ?? "未检测到 BeamMP Launcher";
        HasBeamMPInstalled = result.IsBeamMPFound;

        ResourcesStatus = result.IsResourcesFound ? "已找到" : "未找到";
        ResourcesPath = result.BeamMPResourcesPath ?? "未检测到 Resources 文件夹";
        HasResources = result.IsResourcesFound;
    }

    private void LogDetectionResult(DetectionResult result)
    {
        if (result.IsBeamNGFound)
            _logger.Info($"找到 BeamNG.drive：{result.BeamNGPath}");
        else
            _logger.Warning("未找到 BeamNG.drive 安装目录。");

        if (result.IsBeamMPFound)
            _logger.Info($"找到 BeamMP：{result.BeamMPPath}");
        else
            _logger.Warning("未找到 BeamMP Launcher。");

        if (result.IsResourcesFound)
            _logger.Info($"找到 BeamMP Resources：{result.BeamMPResourcesPath}");
        else
            _logger.Info("未检测到 BeamMP Resources 文件夹；可能尚未加入服务器或尚未下载服务器资源。");
    }

    private void ClearLogs()
    {
        LogEntries.Clear();
        ClearLogsCommand.NotifyCanExecuteChanged();
    }

    private void OnLogEntryWritten(LogEntry entry)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            LogEntries.Insert(0, entry);
            while (LogEntries.Count > 500)
                LogEntries.RemoveAt(LogEntries.Count - 1);
            ClearLogsCommand.NotifyCanExecuteChanged();
        });
    }

    private void NotifyCommandStates()
    {
        DetectCommand?.NotifyCanExecuteChanged();
        LaunchBeamNGCommand?.NotifyCanExecuteChanged();
        LaunchBeamMPCommand?.NotifyCanExecuteChanged();
        OpenResourcesCommand?.NotifyCanExecuteChanged();
        OpenDocsCommand?.NotifyCanExecuteChanged();
        ValidateSteamCommand?.NotifyCanExecuteChanged();
        ClearLogsCommand?.NotifyCanExecuteChanged();
        CancelCommand?.NotifyCanExecuteChanged();
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
