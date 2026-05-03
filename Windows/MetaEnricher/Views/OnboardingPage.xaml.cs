using MetaEnricher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MetaEnricher.Views;

public sealed partial class OnboardingPage : Page
{
    private int _currentStep = 0;
    public AppState AppState => AppState.Instance;

    public OnboardingPage()
    {
        this.InitializeComponent();
    }

    private void ShowStep(int step)
    {
        _currentStep = step;
        StepWelcome.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        StepLibrary.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepDone.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnGetStarted_Click(object sender, RoutedEventArgs e)
    {
        ShowStep(1);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        ShowStep(0);
    }

    private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            AppState.CameraRootPath = folder.Path;
            TbSelectedPath.Text = folder.Path;
            TbSelectedPath.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 245, 245, 245));
            BtnContinue.IsEnabled = true;
        }
    }

    private void TbPicksFolderName_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = TbPicksFolderName.Text;
        AppState.PicksFolderName = string.IsNullOrWhiteSpace(text) ? "Edited export" : text;
        TbFolderStructure.Text = $"      {AppState.PicksFolderName}/  ← editable JPEGs";
    }

    private void BtnContinue_Click(object sender, RoutedEventArgs e)
    {
        ShowStep(2);
    }

    private void BtnOpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        AppState.HasCompletedOnboarding = true;
        AppState.SaveSettings();
        Frame.Navigate(typeof(MainPage));
    }
}
