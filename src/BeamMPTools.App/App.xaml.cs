using Microsoft.UI.Xaml;

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

        _mainWindow.Activate();
    }
}