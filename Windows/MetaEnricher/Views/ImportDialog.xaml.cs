using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MetaEnricher.Models;
using MetaEnricher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MetaEnricher.Views;

public sealed partial class ImportDialog : ContentDialog
{
    public AppState AppState => AppState.Instance;
    private CancellationTokenSource? _cts;
    private string _destPath = "";

    public ImportDialog()
    {
        this.InitializeComponent();
        Loaded += ImportDialog_Loaded;
    }

    private void ImportDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate drive list
        var drives = ImportService.Instance.FindDriveRoots();
        CbDrives.Items.Clear();
        foreach (var d in drives)
            CbDrives.Items.Add(d);

        if (CbDrives.Items.Count > 0)
            CbDrives.SelectedIndex = 0;

        // Set default destination
        _destPath = AppState.CameraRootPath ?? "";
        TbDestPath.Text = string.IsNullOrEmpty(_destPath) ? "Not set" : _destPath;
    }

    private async void BtnBrowseDest_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            _destPath = folder.Path;
            TbDestPath.Text = _destPath;
        }
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        if (CbDrives.SelectedItem is not string sourceDrive) return;
        if (string.IsNullOrEmpty(_destPath)) return;

        var sourcePath = Path.Combine(sourceDrive, "DCIM");
        if (!Directory.Exists(sourcePath))
        {
            TbError.Text = "DCIM folder not found on selected drive.";
            TbError.Visibility = Visibility.Visible;
            return;
        }

        TbError.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnImport.IsEnabled = false;
        BtnCancel.Visibility = Visibility.Visible;

        _cts = new CancellationTokenSource();

        var progress = new Progress<ImportProgress>(p =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (p.Total > 0)
                {
                    ImportProgressBar.IsIndeterminate = false;
                    ImportProgressBar.Value = (double)p.Copied / p.Total;
                }
                else
                {
                    ImportProgressBar.IsIndeterminate = true;
                }

                TbImportStatus.Text = p.Done ? "Import complete!" : $"Copying: {p.CurrentFile}";
                TbImportCount.Text = $"{p.Copied} / {p.Total} files";

                if (p.Error != null)
                {
                    TbError.Text = p.Error;
                    TbError.Visibility = Visibility.Visible;
                }

                if (p.Done)
                {
                    BtnImport.IsEnabled = true;
                    BtnCancel.Visibility = Visibility.Collapsed;
                    _ = AppState.LoadSessionsAsync();
                }
            });
        });

        try
        {
            await ImportService.Instance.ImportAsync(sourcePath, _destPath, "metaEnricher", progress);
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TbError.Text = ex.Message;
                TbError.Visibility = Visibility.Visible;
                BtnImport.IsEnabled = true;
                BtnCancel.Visibility = Visibility.Collapsed;
            });
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.Visibility = Visibility.Collapsed;
        BtnImport.IsEnabled = true;
        TbImportStatus.Text = "Cancelled.";
    }
}
