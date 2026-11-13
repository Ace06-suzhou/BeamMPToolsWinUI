using BeamMPTools.App.Views;
using Microsoft.UI.Xaml;

namespace BeamMPTools.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "BeamMP Tools";
        RootFrame.Navigate(typeof(MainPage));
    }
}
