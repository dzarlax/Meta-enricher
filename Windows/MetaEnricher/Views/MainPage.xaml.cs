using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetaEnricher.Models;
using MetaEnricher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace MetaEnricher.Views;

public sealed partial class MainPage : Page
{
    public AppState AppState => AppState.Instance;
    private int _currentRating = 0;

    public MainPage()
    {
        this.InitializeComponent();
        ViewModeSelector.SelectedItem = AppState.ViewMode == ViewMode.Originals ? SbiOriginals : SbiEdited;
        AppState.PropertyChanged += AppState_PropertyChanged;
        AppState.Photos.CollectionChanged += Photos_CollectionChanged;
        AppState.Sessions.CollectionChanged += Sessions_CollectionChanged;
        Loaded += MainPage_Loaded;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Remove focus from SessionsList to avoid stray focus rectangle after returning from fullscreen
        this.Focus(FocusState.Programmatic);
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // Unsubscribe from singleton events to prevent leaks when this page is recreated
        AppState.PropertyChanged -= AppState_PropertyChanged;
        AppState.Photos.CollectionChanged -= Photos_CollectionChanged;
        AppState.Sessions.CollectionChanged -= Sessions_CollectionChanged;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await AppState.LoadSessionsAsync();
        UpdateEmptyState();
    }

    private void AppState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(AppState.SelectedPhoto):
                    UpdateInspector();
                    break;
                case nameof(AppState.IsLoadingPhotos):
                    UpdateEmptyState();
                    break;
                case nameof(AppState.ViewMode):
                    SyncViewModeSelector();
                    UpdateSessionCounts();
                    break;
            }
        });
    }

    private void SyncViewModeSelector()
    {
        var target = AppState.ViewMode == ViewMode.Originals ? SbiOriginals : SbiEdited;
        if (ViewModeSelector.SelectedItem != target)
            ViewModeSelector.SelectedItem = target;
    }

    private void Sessions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AssignSessionThumbnails();
            UpdateSessionCounts();
        });
    }

    private void AssignSessionThumbnails()
    {
        foreach (var session in AppState.Sessions)
        {
            if (session.ThumbnailSource == null && session.ThumbnailPath != null)
                session.ThumbnailSource = ThumbnailService.Instance.GetThumbnail(session.ThumbnailPath, 48);
        }
    }

    private void UpdateSessionCounts()
    {
        bool isEdited = AppState.ViewMode == ViewMode.Edited;
        foreach (var session in AppState.Sessions)
        {
            if (isEdited)
            {
                session.ActiveCountDisplay = session.EditedCount > 0
                    ? $"{session.EditedCount} edited" : "No edited photos";
            }
            else
            {
                var parts = new List<string>();
                if (session.OriginalsCount > 0) parts.Add($"{session.OriginalsCount} JPEG");
                if (session.RawCount > 0) parts.Add($"{session.RawCount} RAW");
                session.ActiveCountDisplay = parts.Count > 0 ? string.Join(" · ", parts) : "No originals";
            }
        }
    }

    private void Photos_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateEmptyState();
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ||
                e.NewItems?.Count > 0)
            {
                AssignThumbnails();
            }
        });
    }

    private void UpdateEmptyState()
    {
        bool hasPhotos = AppState.Photos.Count > 0;
        bool isLoading = AppState.IsLoadingPhotos;
        EmptyState.Visibility = (!hasPhotos && !isLoading) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Assigns a BitmapImage to each Photo.ThumbnailSource on the UI thread.
    /// BitmapImage starts loading the file asynchronously in the background by itself —
    /// no awaiting needed, and no TryGetElement index races.
    /// </summary>
    private void AssignThumbnails()
    {
        var size = (int)ZoomSlider.Value;
        foreach (var photo in AppState.Photos)
        {
            if (photo.ThumbnailSource == null)
                photo.ThumbnailSource = ThumbnailService.Instance.GetThumbnail(photo.FilePath, size);
        }
    }

    private void UpdateInspector()
    {
        var photo = AppState.SelectedPhoto;
        if (photo == null)
        {
            PreviewImage.Source = null;
            ClearForm();
            return;
        }

        PopulateForm(photo);

        // BitmapImage starts loading asynchronously in the background on its own.
        PreviewImage.Source = ThumbnailService.Instance.GetThumbnail(photo.FilePath, 280);

        var m = photo.Meta;
        LblDate.Text = m.DateTimeOriginal ?? "—";
        LblCamera.Text = (m.Make != null || m.Model != null)
            ? $"{m.Make} {m.Model}".Trim() : "—";
        LblFocal.Text = m.FocalLength ?? "—";
        LblAperture.Text = m.Aperture ?? "—";
        LblShutter.Text = m.ShutterSpeed ?? "—";
        LblIso.Text = m.Iso?.ToString() ?? "—";
        LblGps.Text = m.GpsLat.HasValue && m.GpsLon.HasValue
            ? $"{m.GpsLat.Value:F4}, {m.GpsLon.Value:F4}" : "—";
        LblLocationSrc.Text = m.LocationSource ?? "—";
    }

    private void PopulateForm(Photo photo)
    {
        var m = photo.Meta;
        TbTitle.Text = m.Title ?? "";
        TbDescription.Text = m.Description ?? "";
        TbKeywords.Text = string.Join(", ", m.Keywords);
        TbLocation.Text = m.Location ?? "";
        TbCreator.Text = m.Creator ?? AppState.DefaultCreator;
        TbCopyright.Text = m.Copyright ?? AppState.DefaultCopyright;
        _currentRating = m.Rating ?? 0;
        UpdateStarUI(_currentRating);
    }

    private void ClearForm()
    {
        TbTitle.Text = "";
        TbDescription.Text = "";
        TbKeywords.Text = "";
        TbLocation.Text = "";
        TbCreator.Text = "";
        TbCopyright.Text = "";
        _currentRating = 0;
        UpdateStarUI(0);
        LblDate.Text = "—";
        LblCamera.Text = "—";
        LblFocal.Text = "—";
        LblAperture.Text = "—";
        LblShutter.Text = "—";
        LblIso.Text = "—";
        LblGps.Text = "—";
        LblLocationSrc.Text = "—";
    }

    private void UpdateStarUI(int rating)
    {
        var amber = new SolidColorBrush(Color.FromArgb(255, 255, 185, 56));
        var gray = new SolidColorBrush(Color.FromArgb(255, 156, 163, 175));
        var stars = new[] { Star1, Star2, Star3, Star4, Star5 };
        for (int i = 0; i < 5; i++)
            stars[i].Foreground = i < rating ? amber : gray;
    }

    // ─── Event handlers ───────────────────────────────────────────

    private void BtnRefreshSessions_Click(object sender, RoutedEventArgs e)
    {
        _ = AppState.LoadSessionsAsync();
    }

    private async void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsDialog();
        dlg.XamlRoot = this.XamlRoot;
        await dlg.ShowAsync();
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ImportDialog();
        dlg.XamlRoot = this.XamlRoot;
        await dlg.ShowAsync();
    }

    private void ViewModeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        if (AppState == null) return;
        AppState.ViewMode = sender.SelectedItem == SbiOriginals ? ViewMode.Originals : ViewMode.Edited;
        UpdateSessionCounts();
    }

    private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (PhotoGrid != null)
        {
            double val = e.NewValue;
            PhotoGrid.MinItemWidth = val;
            PhotoGrid.MinItemHeight = val;
        }
        // Clear cached thumbnails so they re-decode at new size
        ThumbnailService.Instance.PurgeAll();
        foreach (var photo in AppState.Photos)
            photo.ThumbnailSource = null;
        AssignThumbnails();
    }

    private void TbSessionNotes_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AppState.SelectedSession != null)
            AppState.SaveSessionNotes(AppState.SelectedSession.Id, TbSessionNotes.Text);
    }

    private void PhotoCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border card || card.Tag is not string photoId) return;
        var photo = AppState.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo == null) return;

        bool ctrl = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrl)
        {
            // Toggle this photo in the multi-selection
            if (AppState.SelectedPhotoIds.Contains(photoId))
            {
                AppState.SelectedPhotoIds.Remove(photoId);
                photo.IsSelected = false;
            }
            else
            {
                AppState.SelectedPhotoIds.Add(photoId);
                photo.IsSelected = true;
            }
            AppState.SelectedPhoto = photo;
        }
        else
        {
            // Clear previous selection
            foreach (var p in AppState.Photos)
                p.IsSelected = false;
            AppState.SelectedPhotoIds.Clear();

            AppState.SelectedPhotoIds.Add(photoId);
            photo.IsSelected = true;
            AppState.SelectedPhoto = photo;
        }
    }

    private void PhotoCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is Border card && card.Tag is string photoId)
        {
            var photo = AppState.Photos.FirstOrDefault(p => p.Id == photoId);
            if (photo != null)
            {
                AppState.SelectedPhoto = photo;
                Frame.Navigate(typeof(FullscreenPage), photo.FilePath);
            }
        }
    }

    private void StarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int rating))
        {
            _currentRating = rating;
            UpdateStarUI(rating);
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var photo = AppState.SelectedPhoto;
        if (photo == null) return;

        string? city = null, country = null;
        if (!string.IsNullOrWhiteSpace(TbLocation.Text))
        {
            var parts = TbLocation.Text.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) city = parts[0];
            if (parts.Length > 1) country = parts[1];
        }

        var keywords = TbKeywords.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var metaWrite = new MetaWrite
        {
            Title = TbTitle.Text.Length > 0 ? TbTitle.Text : null,
            Description = TbDescription.Text.Length > 0 ? TbDescription.Text : null,
            Keywords = keywords.Count > 0 ? keywords : null,
            City = !string.IsNullOrWhiteSpace(city) ? city : null,
            Country = !string.IsNullOrWhiteSpace(country) ? country : null,
            Creator = TbCreator.Text.Length > 0 ? TbCreator.Text : null,
            Copyright = TbCopyright.Text.Length > 0 ? TbCopyright.Text : null,
            Rating = _currentRating > 0 ? _currentRating : null,
            GpsLat = photo.Meta.GpsLat,
            GpsLon = photo.Meta.GpsLon,
        };

        try
        {
            await ExifService.Instance.WriteMetaAsync(photo.FilePath, metaWrite);

            // Update local state
            photo.Meta.Title = metaWrite.Title;
            photo.Meta.Description = metaWrite.Description;
            photo.Meta.Keywords = metaWrite.Keywords ?? new List<string>();
            photo.Meta.Location = TbLocation.Text.Length > 0 ? TbLocation.Text : null;
            photo.Meta.Creator = metaWrite.Creator;
            photo.Meta.Copyright = metaWrite.Copyright;
            photo.Meta.Rating = metaWrite.Rating;
            AppState.UpdatePhoto(photo);

            // Invalidate thumbnail
            ThumbnailService.Instance.Invalidate(photo.FilePath);

            ShowInfoBar("Metadata saved successfully.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Save failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void BtnEnrichSingle_Click(object sender, RoutedEventArgs e)
    {
        var photo = AppState.SelectedPhoto;
        if (photo == null) return;
        await EnrichPhotosAsync(new[] { photo }, new HashSet<EnrichField>(Enum.GetValues<EnrichField>()));
    }

    private async void BtnEnrichSelected_Click(object sender, RoutedEventArgs e)
    {
        var photos = AppState.Photos
            .Where(p => AppState.SelectedPhotoIds.Contains(p.Id))
            .ToList();
        if (photos.Count == 0 && AppState.SelectedPhoto != null)
            photos = new List<Photo> { AppState.SelectedPhoto };
        await EnrichPhotosAsync(photos, new HashSet<EnrichField>(Enum.GetValues<EnrichField>()));
    }

    private async void BtnEnrichAll_Click(object sender, RoutedEventArgs e)
    {
        var photos = AppState.Photos.ToList();
        await EnrichPhotosAsync(photos, new HashSet<EnrichField>(Enum.GetValues<EnrichField>()));
    }

    private System.Threading.CancellationTokenSource? _enrichCts;

    private async Task EnrichPhotosAsync(IList<Photo> photos, HashSet<EnrichField> fields)
    {
        if (photos.Count == 0) return;
        if (AppState.IsEnriching)
        {
            ShowInfoBar("Enrichment already in progress.", InfoBarSeverity.Warning);
            return;
        }

        _enrichCts = new System.Threading.CancellationTokenSource();
        var ct = _enrichCts.Token;

        AppState.IsEnriching = true;
        AppState.EnrichmentTotal = photos.Count;
        AppState.EnrichmentDone = 0;
        AppState.EnrichmentCurrentFile = "";

        foreach (var photo in photos)
        {
            if (AppState.EnrichingIds.Contains(photo.Id)) continue;
            AppState.EnrichingIds.Add(photo.Id);
        }

        int successCount = 0;
        try
        {
            foreach (var photo in photos)
            {
                if (ct.IsCancellationRequested) break;
                AppState.EnrichmentCurrentFile = photo.FileName;

                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Enrich] Starting: {photo.FileName}");

                    var enriched = await OllamaService.Instance.EnrichPhotoAsync(
                        photo.FilePath,
                        AppState.OllamaUrl,
                        AppState.OllamaModel,
                        AppState.OllamaApiKey,
                        AppState.SessionNotes,
                        photo.Meta,
                        fields);

                    System.Diagnostics.Debug.WriteLine(
                        $"[Enrich] Ollama result — title='{enriched.Title}' " +
                        $"desc='{enriched.Description?.Substring(0, Math.Min(60, enriched.Description?.Length ?? 0))}' " +
                        $"keywords={enriched.Keywords.Count} location='{enriched.Location}'");

                    // GPS → geocode if needed
                    if (fields.Contains(EnrichField.Location) && enriched.GpsLat.HasValue && enriched.GpsLon.HasValue
                        && string.IsNullOrWhiteSpace(enriched.Location))
                    {
                        var (city, country) = await GeocodingService.Instance.ReverseGeocodeAsync(
                            enriched.GpsLat.Value, enriched.GpsLon.Value);
                        if (city != null || country != null)
                        {
                            var parts = new List<string>();
                            if (city != null) parts.Add(city);
                            if (country != null) parts.Add(country);
                            enriched.Location = string.Join(", ", parts);
                            enriched.LocationSource = "gps";
                        }
                    }

                    // Apply creator/copyright defaults
                    if (string.IsNullOrWhiteSpace(enriched.Creator))
                        enriched.Creator = AppState.DefaultCreator;
                    if (string.IsNullOrWhiteSpace(enriched.Copyright))
                        enriched.Copyright = AppState.DefaultCopyright;

                    // Write to file
                    var locationParts = enriched.Location?.Split(',', 2, StringSplitOptions.TrimEntries);
                    System.Diagnostics.Debug.WriteLine($"[Enrich] Writing EXIF to: {photo.FilePath}");
                    await ExifService.Instance.WriteMetaAsync(photo.FilePath, new MetaWrite
                    {
                        Title = enriched.Title,
                        Description = enriched.Description,
                        Keywords = enriched.Keywords,
                        City = locationParts?.Length > 0 ? locationParts[0] : null,
                        Country = locationParts?.Length > 1 ? locationParts[1] : null,
                        Creator = enriched.Creator,
                        Copyright = enriched.Copyright,
                        Rating = enriched.Rating,
                        GpsLat = enriched.GpsLat,
                        GpsLon = enriched.GpsLon,
                    });
                    System.Diagnostics.Debug.WriteLine($"[Enrich] EXIF write OK: {photo.FileName}");

                    photo.Meta = enriched;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        AppState.UpdatePhoto(photo);
                        if (AppState.SelectedPhoto?.Id == photo.Id)
                            UpdateInspector();
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Enrich] ERROR on {photo.FileName}: {ex}");
                    ShowInfoBar($"Enrich failed for {photo.FileName}: {ex.Message}", InfoBarSeverity.Error);
                }
                finally
                {
                    DispatcherQueue.TryEnqueue(() => AppState.EnrichingIds.Remove(photo.Id));
                    AppState.EnrichmentDone++;
                }
            }

            var msg = ct.IsCancellationRequested
                ? $"Enrichment cancelled — {successCount} of {photos.Count} done."
                : $"Enriched {successCount} of {photos.Count} photo(s).";
            ShowInfoBar(msg, ct.IsCancellationRequested ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Enrichment error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            AppState.IsEnriching = false;
            AppState.EnrichmentCurrentFile = "";
            _enrichCts?.Dispose();
            _enrichCts = null;
        }
    }

    private void BtnCancelEnrich_Click(object sender, RoutedEventArgs e)
    {
        _enrichCts?.Cancel();
    }

    private void ShowInfoBar(string message, InfoBarSeverity severity)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var infoBar = new InfoBar
            {
                Message = message,
                Severity = severity,
                IsOpen = true,
                IsClosable = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 20),
            };

            if (this.Content is Grid rootGrid)
            {
                Grid.SetColumnSpan(infoBar, 3);
                rootGrid.Children.Add(infoBar);
                // Errors stay longer — user needs to read them
                int delayMs = severity == InfoBarSeverity.Error ? 8000 : 3000;
                await Task.Delay(delayMs);
                if (rootGrid.Children.Contains(infoBar))
                    rootGrid.Children.Remove(infoBar);
            }
        });
    }
}
