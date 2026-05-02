using MetaEnricher.Models;
using MetaEnricher.Views;
using Microsoft.UI.Xaml;

namespace MetaEnricher;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;

        // Navigate to appropriate page based on onboarding state
        var appState = AppState.Instance;
        if (appState.HasCompletedOnboarding && !string.IsNullOrEmpty(appState.CameraRootPath))
        {
            RootFrame.Navigate(typeof(MainPage));
        }
        else
        {
            RootFrame.Navigate(typeof(OnboardingPage));
        }
    }

    public IntPtr GetWindowHandle()
    {
        return WinRT.Interop.WindowNative.GetWindowHandle(this);
    }
}
