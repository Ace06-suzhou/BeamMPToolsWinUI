using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System;
using System.IO;
using WinRT.Interop;
namespace BeamMPTools.App;

public partial class App : Application
{
    private Window? _mainWindow;

    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow ??= new MainWindow();

        MainWindowInstance = _mainWindow;

        SetWindowIcon(_mainWindow);

        _mainWindow.Activate();
    }
    private static void SetWindowIcon(Window window)
    {
        string iconPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "logo.ico");

        if (!File.Exists(iconPath))
            return;

        IntPtr hwnd = WindowNative.GetWindowHandle(window);

        WindowId windowId =
            Win32Interop.GetWindowIdFromWindow(hwnd);

        AppWindow appWindow =
            AppWindow.GetFromWindowId(windowId);

        appWindow.SetIcon(iconPath);
    }
}