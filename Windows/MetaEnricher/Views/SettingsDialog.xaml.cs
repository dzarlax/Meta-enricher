using System;
using System.Collections.Generic;
using MetaEnricher.Models;
using MetaEnricher.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MetaEnricher.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public AppState AppState => AppState.Instance;

    public SettingsDialog()
    {
        this.InitializeComponent();
        LoadCurrentSettings();
        Loaded += SettingsDialog_Loaded;
        PrimaryButtonClick += SettingsDialog_PrimaryButtonClick;
    }

    private bool IsCloudMode => AppState.OllamaUrl.StartsWith(AppConstants.CloudOllamaUrl, StringComparison.OrdinalIgnoreCase);

    private void LoadCurrentSettings()
    {
        TbLibraryPath.Text = AppState.CameraRootPath ?? "Not set";
        TbPicksFolder.Text = AppState.PicksFolderName;
        TbOllamaUrl.Text = AppState.OllamaUrl;
        TbApiKey.Password = AppState.OllamaApiKey;
        TbDefaultCreator.Text = AppState.DefaultCreator;
        TbDefaultCopyright.Text = AppState.DefaultCopyright;
        TbPullModel.Text = AppState.OllamaModel;

        if (IsCloudMode)
        {
            OllamaModeSelector.SelectedItem = SbiCloud;
            ApplyCloudMode();
        }
        else
        {
            OllamaModeSelector.SelectedItem = SbiLocal;
        }
    }

    private void OllamaModeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        if (sender.SelectedItem == SbiCloud)
        {
            TbOllamaUrl.Text = AppConstants.CloudOllamaUrl;
            ApplyCloudMode();
        }
        else
        {
            if (TbOllamaUrl.Text == AppConstants.CloudOllamaUrl)
                TbOllamaUrl.Text = AppConstants.DefaultOllamaUrl;
            ApplyLocalMode();
        }
    }

    private void ApplyCloudMode()
    {
        LocalUrlPanel.Visibility = Visibility.Collapsed;
        CloudKeyPanel.Visibility = Visibility.Visible;
    }

    private void ApplyLocalMode()
    {
        LocalUrlPanel.Visibility = Visibility.Visible;
        CloudKeyPanel.Visibility = Visibility.Collapsed;
    }

    private async void SettingsDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshModels();
        await CheckOllamaStatus();
        CheckExifToolStatus();
    }

    private void CheckExifToolStatus()
    {
        if (ExifService.Instance.IsExifToolAvailable())
        {
            ExifToolDot.Fill = new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)); // green
            TbExifToolStatus.Text = "exiftool.exe found — metadata writing enabled";
        }
        else
        {
            ExifToolDot.Fill = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // red
            TbExifToolStatus.Text =
                "exiftool.exe not found. Download the Windows standalone from exiftool.org, " +
                "rename to exiftool.exe and place it next to MetaEnricher.exe.";
        }
    }

    private async System.Threading.Tasks.Task RefreshModels()
    {
        var models = await OllamaService.Instance.ListModelsAsync(TbOllamaUrl.Text, TbApiKey.Password);
        CbModel.Items.Clear();
        foreach (var m in models)
            CbModel.Items.Add(m);

        if (CbModel.Items.Contains(AppState.OllamaModel))
            CbModel.SelectedItem = AppState.OllamaModel;
        else if (CbModel.Items.Count > 0)
            CbModel.SelectedIndex = 0;
        else
            CbModel.PlaceholderText = "No models — check connection";
    }

    private async System.Threading.Tasks.Task CheckOllamaStatus()
    {
        OllamaStatusDot.Fill = new SolidColorBrush(Color.FromArgb(255, 156, 163, 175)); // gray
        TbOllamaStatus.Text = "Checking...";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool ok = await OllamaService.Instance.CheckOllamaAsync(TbOllamaUrl.Text, TbApiKey.Password);
        sw.Stop();

        OllamaStatusDot.Fill = new SolidColorBrush(ok
            ? Color.FromArgb(255, 34, 197, 94)   // green
            : Color.FromArgb(255, 239, 68, 68)); // red
        TbOllamaStatus.Text = ok ? $"Connected ({sw.ElapsedMilliseconds} ms)" : "Not reachable";
    }

    private async void BtnTestOllama_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        var origContent = btn?.Content;
        if (btn != null) { btn.IsEnabled = false; btn.Content = "Testing..."; }

        try { await CheckOllamaStatus(); }
        finally
        {
            if (btn != null) { btn.IsEnabled = true; btn.Content = origContent; }
        }
    }

    private async void BtnRefreshModels_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModels();
    }

    private async void BtnUnloadModel_Click(object sender, RoutedEventArgs e)
    {
        var model = CbModel.SelectedItem as string ?? TbPullModel.Text.Trim();
        if (string.IsNullOrWhiteSpace(model)) return;

        BtnUnload.IsEnabled = false;
        BtnUnload.Content = "Unloading...";

        var ok = await OllamaService.Instance.UnloadModelAsync(model, TbOllamaUrl.Text, TbApiKey.Password);

        BtnUnload.Content = "Unload";
        BtnUnload.IsEnabled = true;
        TbPullStatus.Visibility = Visibility.Visible;
        TbPullStatus.Text = ok ? $"Model '{model}' unloaded from memory." : "Unload failed — model may not be loaded.";
    }

    private async void BtnPullModel_Click(object sender, RoutedEventArgs e)
    {
        var modelName = TbPullModel.Text.Trim();
        if (string.IsNullOrWhiteSpace(modelName)) return;

        PullProgress.Visibility = Visibility.Visible;
        TbPullStatus.Visibility = Visibility.Visible;
        PullProgress.IsIndeterminate = true;

        try
        {
            var progress = new Progress<MetaEnricher.Services.PullProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    TbPullStatus.Text = p.Status;
                    if (p.Fraction >= 0)
                    {
                        PullProgress.IsIndeterminate = false;
                        PullProgress.Value = p.Fraction;
                    }
                    else
                    {
                        PullProgress.IsIndeterminate = true;
                    }
                });
            });

            await OllamaService.Instance.PullModelAsync(modelName, TbOllamaUrl.Text, TbApiKey.Password, progress);
            await RefreshModels();
            TbPullStatus.Text = "Pull complete!";
        }
        catch (Exception ex)
        {
            TbPullStatus.Text = $"Error: {ex.Message}";
        }
    }

    private async void BtnBrowseLibrary_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
            TbLibraryPath.Text = folder.Path;
    }

    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        ThumbnailService.Instance.PurgeAll();
        TbPullStatus.Text = "Thumbnail cache cleared.";
        TbPullStatus.Visibility = Visibility.Visible;
    }

    private void SettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Save settings
        if (TbLibraryPath.Text != "Not set")
            AppState.CameraRootPath = TbLibraryPath.Text;
        AppState.PicksFolderName = string.IsNullOrWhiteSpace(TbPicksFolder.Text) ? "Edited export" : TbPicksFolder.Text;
        AppState.OllamaUrl = TbOllamaUrl.Text;
        AppState.OllamaApiKey = TbApiKey.Password;
        if (CbModel.SelectedItem is string model)
            AppState.OllamaModel = model;
        AppState.DefaultCreator = TbDefaultCreator.Text;
        AppState.DefaultCopyright = TbDefaultCopyright.Text;

        if (!AppState.SaveSettings())
        {
            args.Cancel = true;
            TbPullStatus.Visibility = Visibility.Visible;
            TbPullStatus.Text = "Failed to save settings — check folder permissions.";
        }
    }
}
