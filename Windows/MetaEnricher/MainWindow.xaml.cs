using MetaEnricher.Models;
using MetaEnricher.Services;
using MetaEnricher.Views;
using Microsoft.UI.Xaml;

namespace MetaEnricher;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.Title = "Meta Enricher";

        // Window/taskbar icon — Self-contained unpackaged apps need to set this explicitly.
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
                AppWindow.SetIcon(iconPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] SetIcon failed: {ex.Message}");
        }

        AppWindow.Closing += OnWindowClosing;

        var appState = AppState.Instance;
        if (appState.HasCompletedOnboarding && !string.IsNullOrEmpty(appState.CameraRootPath))
            RootFrame.Navigate(typeof(MainPage));
        else
            RootFrame.Navigate(typeof(OnboardingPage));
    }

    private bool _closing = false;

    private async void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
    {
        if (_closing) return;

        // If fullscreen viewer is open — go back instead of closing the app
        if (RootFrame.CurrentSourcePageType == typeof(FullscreenPage))
        {
            e.Cancel = true;
            DispatcherQueue.TryEnqueue(() => RootFrame.GoBack());
            return;
        }

        var appState = AppState.Instance;

        // If something's going on — close gracefully
        bool needsCleanup = !string.IsNullOrWhiteSpace(appState.OllamaModel) || appState.IsEnriching;
        if (!needsCleanup) return;

        e.Cancel = true;
        try
        {
            // Wait briefly for in-flight enrichment to finish current photo (max 3s)
            // — exiftool write is atomic so we won't corrupt files anyway, but this is cleaner.
            if (appState.IsEnriching)
            {
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (appState.IsEnriching && DateTime.UtcNow < deadline)
                    await System.Threading.Tasks.Task.Delay(100);
            }

            // Unload model from Ollama RAM/VRAM
            if (!string.IsNullOrWhiteSpace(appState.OllamaModel))
            {
                await OllamaService.Instance.UnloadModelAsync(
                    appState.OllamaModel,
                    appState.OllamaUrl,
                    appState.OllamaApiKey);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Cleanup on close failed: {ex.Message}");
        }
        finally
        {
            _closing = true;
            sender.Destroy();
        }
    }

    public IntPtr GetWindowHandle()
    {
        return WinRT.Interop.WindowNative.GetWindowHandle(this);
    }
}
