using System;
using MetaEnricher.Models;
using MetaEnricher.Views;
using Microsoft.UI.Xaml;

namespace MetaEnricher;

public partial class App : Application
{
    public static Window? CurrentWindow { get; private set; }
    public AppState AppState => AppState.Instance;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        CurrentWindow = window;

        // Force dark theme
        if (window.Content is FrameworkElement fe)
            fe.RequestedTheme = ElementTheme.Dark;

        window.Activate();
    }
}
