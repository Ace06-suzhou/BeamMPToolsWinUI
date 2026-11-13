using BeamMPTools.App.ViewModels;
using BeamMPTools.Core.Detection;
using BeamMPTools.Core.Installation;
using BeamMPTools.Core.Launching;
using BeamMPTools.Core.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace BeamMPTools.App.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        var logger = new AppLogger();
        ViewModel = new MainViewModel(
            new PathDetector(),
            new ClientInstaller(),
            new SteamLauncher(logger),
            logger,
            DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("找不到 UI DispatcherQueue。"));

        DataContext = ViewModel;
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private async void ChooseBeamMPInstallPath_Click(
    object sender,
    RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };

        picker.FileTypeFilter.Add("*");

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            App.MainWindowInstance!);

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder? folder = await picker.PickSingleFolderAsync();

        if (folder is null)
            return;

        if (!ViewModel.SetBeamMPInstallPath(folder.Path))
        {
            var dialog = new ContentDialog
            {
                Title = "目录无效",
                Content = "所选目录中没有找到 BeamMP-Launcher.exe，请选择正确的安装目录。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
    private async void ResetBeamMPInstallPath_Click(
    object sender,
    RoutedEventArgs e)
    {
        ViewModel.ClearManualBeamMPInstallPath();

        await ViewModel.DetectAsync();
    }
    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        // 取得默认安装目录
        string defaultDirectory = ViewModel.GetDefaultBeamMPInstallPath();

        string defaultDescription = ViewModel.HasBeamMPInstalled
            ? "当前检测到的 BeamMP 安装目录"
            : "BeamMP 官方默认安装目录";

        // 第一步：选择默认位置还是自定义位置
        var locationDialog = new ContentDialog
        {
            Title = "选择 BeamMP 安装位置",
            Content = $"默认位置：\n{defaultDirectory}\n\n请选择安装位置。",
            PrimaryButtonText = "默认位置",
            SecondaryButtonText = "自定义位置",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        ContentDialogResult choice = await locationDialog.ShowAsync();

        string targetDirectory;

        if (choice == ContentDialogResult.Primary)
        {
            targetDirectory = defaultDirectory;
        }
        else if (choice == ContentDialogResult.Secondary)
        {
            // 第二步：打开 Windows 文件夹选择器
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            picker.FileTypeFilter.Add("*");

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                App.MainWindowInstance!);

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFolder? selectedFolder =
                await picker.PickSingleFolderAsync();

            // 用户取消选择
            if (selectedFolder is null)
                return;

            targetDirectory = selectedFolder.Path;
        }
        else
        {
            return;
        }

        // 第三步：检查目标目录
        bool launcherExists =
            File.Exists(Path.Combine(targetDirectory, "BeamMP-Launcher.exe")) ||
            File.Exists(Path.Combine(targetDirectory, "BeamMP.exe"));

        string operationText = launcherExists
            ? "更新现有 BeamMP Launcher"
            : "向所选目录部署 BeamMP Launcher";

        var confirmDialog = new ContentDialog
        {
            Title = "确认操作",
            Content = $"{operationText}\n\n目标目录：\n{targetDirectory}\n\n请确认目标位置正确，并关闭正在运行的 BeamMP Launcher。",
            PrimaryButtonText = "开始",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        // 第四步：执行操作
        await ViewModel.InstallAsync(targetDirectory);
    }
}