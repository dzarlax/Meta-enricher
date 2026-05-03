using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MetaEnricher.Services;

namespace MetaEnricher.Models;

public partial class AppState : ObservableObject
{
    private static AppState? _instance;
    public static AppState Instance => _instance ??= new AppState();

    private readonly PhotoScanner _scanner = new();
    private readonly string _settingsPath;
    private readonly string _notesPath;
    private Dictionary<string, string> _sessionNotesCache = new();

    [ObservableProperty]
    private bool _hasCompletedOnboarding;

    [ObservableProperty]
    private LibrarySchema _librarySchema = LibrarySchema.MetaEnricher;

    [ObservableProperty]
    private string _picksFolderName = AppConstants.DefaultPicksFolder;

    [ObservableProperty]
    private string? _cameraRootPath;
    private bool _initialized = false;

    partial void OnCameraRootPathChanged(string? value)
    {
        if (_initialized && value != null)
            _ = LoadSessionsAsync();
    }

    [ObservableProperty]
    private string _ollamaUrl = AppConstants.DefaultOllamaUrl;

    [ObservableProperty]
    private string _ollamaModel = AppConstants.DefaultOllamaModel;

    [ObservableProperty]
    private string _ollamaApiKey = "";

    [ObservableProperty]
    private string _defaultCreator = "";

    [ObservableProperty]
    private string _defaultCopyright = "";

    [ObservableProperty]
    private ObservableCollection<PhotoSession> _sessions = new();

    [ObservableProperty]
    private PhotoSession? _selectedSession;

    partial void OnSelectedSessionChanged(PhotoSession? value)
    {
        if (value == null) return;

        // Auto-switch view mode based on what the session actually contains:
        // - Edited mode but no edited photos → switch to Originals (avoid empty grid)
        // - Originals mode but no JPEG/RAW → switch back to Edited (rare)
        // Setting ViewMode here triggers OnViewModeChanged which calls SelectSessionAsync,
        // so we return early to avoid double-loading.
        bool hasOriginals = value.OriginalsCount > 0 || value.RawCount > 0;
        if (ViewMode == ViewMode.Edited && value.EditedCount == 0 && hasOriginals)
        {
            ViewMode = ViewMode.Originals;
            return;
        }
        if (ViewMode == ViewMode.Originals && !hasOriginals && value.EditedCount > 0)
        {
            ViewMode = ViewMode.Edited;
            return;
        }

        _ = SelectSessionAsync(value);
    }

    [ObservableProperty]
    private ObservableCollection<Photo> _photos = new();

    [ObservableProperty]
    private Photo? _selectedPhoto;

    [ObservableProperty]
    private HashSet<string> _selectedPhotoIds = new();

    [ObservableProperty]
    private ViewMode _viewMode = ViewMode.Edited;

    partial void OnViewModeChanged(ViewMode value)
    {
        if (SelectedSession != null)
            _ = SelectSessionAsync(SelectedSession);
    }

    [ObservableProperty]
    private bool _isLoadingPhotos;

    [ObservableProperty]
    private ObservableCollection<string> _enrichingIds = new();

    [ObservableProperty]
    private bool _isEnriching;

    [ObservableProperty]
    private int _enrichmentTotal;

    [ObservableProperty]
    private int _enrichmentDone;

    [ObservableProperty]
    private string _enrichmentCurrentFile = "";

    [ObservableProperty]
    private string _sessionNotes = "";

    public bool IsBusy => IsEnriching || IsLoadingPhotos;

    partial void OnIsEnrichingChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsLoadingPhotosChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

    public AppState()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, AppConstants.AppDataDirName);
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, AppConstants.SettingsFileName);
        _notesPath    = Path.Combine(dir, AppConstants.SessionNotesFileName);
        LoadSettings();
        LoadSessionNotesCache();
        _initialized = true;
    }

    public async Task LoadSessionsAsync()
    {
        if (string.IsNullOrEmpty(CameraRootPath) || !Directory.Exists(CameraRootPath))
            return;

        var sessions = await _scanner.FindSessionsAsync(CameraRootPath, PicksFolderName);
        var appState = this;
        App.CurrentWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            appState.Sessions.Clear();
            foreach (var s in sessions)
                appState.Sessions.Add(s);
        });
    }

    private System.Threading.CancellationTokenSource? _sessionLoadCts;

    public async Task SelectSessionAsync(PhotoSession session)
    {
        // Cancel any in-flight load
        _sessionLoadCts?.Cancel();
        _sessionLoadCts = new System.Threading.CancellationTokenSource();
        var ct = _sessionLoadCts.Token;

        IsLoadingPhotos = true;
        foreach (var p in Photos) p.IsSelected = false;
        Photos.Clear();
        SelectedPhoto = null;
        SelectedPhotoIds = new HashSet<string>();

        SessionNotes = _sessionNotesCache.TryGetValue(session.Id, out var notes) ? notes : "";

        try
        {
            var photos = await _scanner.LoadPhotosAsync(session, ViewMode, PicksFolderName, ct);
            if (ct.IsCancellationRequested) return;

            App.CurrentWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (ct.IsCancellationRequested) return;
                Photos.Clear();
                foreach (var p in photos)
                    Photos.Add(p);
                IsLoadingPhotos = false;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppState] SelectSessionAsync failed: {ex}");
            IsLoadingPhotos = false;
        }
    }

    public async Task ReloadCurrentSessionAsync()
    {
        if (SelectedSession != null)
            await SelectSessionAsync(SelectedSession);
    }

    public async Task PickCameraRootAsync(IntPtr hwnd)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            CameraRootPath = folder.Path;
            SaveSettings();
        }
    }

    public void UpdatePhoto(Photo updated)
    {
        for (int i = 0; i < Photos.Count; i++)
        {
            if (Photos[i].Id == updated.Id)
            {
                Photos[i] = updated;
                if (SelectedPhoto?.Id == updated.Id)
                    SelectedPhoto = updated;
                break;
            }
        }

        // Update session enriched count
        if (SelectedSession != null)
        {
            int enriched = 0;
            foreach (var p in Photos)
                if (p.IsEnriched) enriched++;
            SelectedSession.EnrichedCount = enriched;
        }
    }

    public void SaveSessionNotes(string sessionId, string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            _sessionNotesCache.Remove(sessionId);
        else
            _sessionNotesCache[sessionId] = notes;

        try
        {
            var json = JsonSerializer.Serialize(_sessionNotesCache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_notesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppState] SaveSessionNotes failed: {ex.Message}");
        }
    }

    private void LoadSessionNotesCache()
    {
        if (!File.Exists(_notesPath)) return;
        try
        {
            var json = File.ReadAllText(_notesPath);
            _sessionNotesCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppState] LoadSessionNotesCache failed: {ex.Message}");
        }
    }

    public bool SaveSettings()
    {
        var settings = new SettingsData
        {
            HasCompletedOnboarding = HasCompletedOnboarding,
            LibrarySchema = LibrarySchema.ToString(),
            PicksFolderName = PicksFolderName,
            CameraRootPath = CameraRootPath,
            OllamaUrl = OllamaUrl,
            OllamaModel = OllamaModel,
            OllamaApiKey = OllamaApiKey,
            DefaultCreator = DefaultCreator,
            DefaultCopyright = DefaultCopyright,
        };
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppState] SaveSettings failed: {ex.Message}");
            return false;
        }
    }

    public void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<SettingsData>(json);
            if (settings == null) return;

            HasCompletedOnboarding = settings.HasCompletedOnboarding;
            if (Enum.TryParse<LibrarySchema>(settings.LibrarySchema, out var schema))
                LibrarySchema = schema;
            PicksFolderName = settings.PicksFolderName ?? AppConstants.DefaultPicksFolder;
            CameraRootPath = settings.CameraRootPath;
            OllamaUrl = settings.OllamaUrl ?? AppConstants.DefaultOllamaUrl;
            OllamaModel = settings.OllamaModel ?? AppConstants.DefaultOllamaModel;
            OllamaApiKey = settings.OllamaApiKey ?? "";
            DefaultCreator = settings.DefaultCreator ?? "";
            DefaultCopyright = settings.DefaultCopyright ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppState] LoadSettings failed: {ex.Message}");
        }
    }

    private class SettingsData
    {
        public bool HasCompletedOnboarding { get; set; }
        public string? LibrarySchema { get; set; }
        public string? PicksFolderName { get; set; }
        public string? CameraRootPath { get; set; }
        public string? OllamaUrl { get; set; }
        public string? OllamaModel { get; set; }
        public string? OllamaApiKey { get; set; }
        public string? DefaultCreator { get; set; }
        public string? DefaultCopyright { get; set; }
    }
}
