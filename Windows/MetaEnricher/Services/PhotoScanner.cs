using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MetaEnricher.Models;

namespace MetaEnricher.Services;

public class PhotoScanner
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
    private static readonly string[] RawExtensions = { ".arw", ".cr2", ".cr3", ".nef", ".raf", ".rw2", ".dng", ".orf", ".srw", ".pef" };

    public async Task<List<PhotoSession>> FindSessionsAsync(string rootPath, string picksFolderName = AppConstants.DefaultPicksFolder)
    {
        return await Task.Run(() =>
        {
            var sessions = new List<PhotoSession>();

            if (!Directory.Exists(rootPath)) return sessions;

            // Root -> Year folders (4-digit)
            foreach (var yearDir in Directory.EnumerateDirectories(rootPath))
            {
                var yearName = Path.GetFileName(yearDir);
                if (!Regex.IsMatch(yearName, @"^\d{4}$")) continue;

                // Year -> Date folders
                foreach (var dateDir in Directory.EnumerateDirectories(yearDir))
                {
                    var dateName = Path.GetFileName(dateDir);
                    var match = Regex.Match(dateName, @"^(\d{4}-\d{2}-\d{2})(?:\s+(.+))?$");
                    if (!match.Success) continue;

                    var dateString = match.Groups[1].Value;
                    var label = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;

                    // Look for picks subfolder or JPEG subfolder
                    var editedPath = Path.Combine(dateDir, picksFolderName);
                    var jpegPath = Path.Combine(dateDir, AppConstants.OriginalsSubFolder);
                    var rawPath = Path.Combine(dateDir, AppConstants.RawSubFolder);

                    int editedCount = Directory.Exists(editedPath)
                        ? CountImages(editedPath) : 0;
                    int originalsCount = Directory.Exists(jpegPath)
                        ? CountImages(jpegPath) : 0;
                    int rawCount = Directory.Exists(rawPath)
                        ? CountRaw(rawPath) : 0;

                    // Only include sessions that have at least some images
                    if (editedCount == 0 && originalsCount == 0 && rawCount == 0) continue;

                    // Ensure picks folder exists so the user can drop edited JPEGs there.
                    // Failures (read-only volume, permission) are non-fatal.
                    if (!Directory.Exists(editedPath))
                    {
                        try { Directory.CreateDirectory(editedPath); } catch { }
                    }

                    var firstPhoto = GetFirstPhoto(editedPath) ?? GetFirstPhoto(jpegPath);

                    var session = new PhotoSession
                    {
                        Id = dateDir,
                        FolderPath = dateDir,
                        DateString = dateString,
                        Label = label,
                        EditedCount = editedCount,
                        OriginalsCount = originalsCount,
                        RawCount = rawCount,
                        ThumbnailPath = firstPhoto,
                    };

                    sessions.Add(session);
                }
            }

            // Sort descending by date
            sessions.Sort((a, b) => string.Compare(b.DateString, a.DateString, StringComparison.Ordinal));
            return sessions;
        });
    }

    public async Task<List<Photo>> LoadPhotosAsync(
        PhotoSession session,
        ViewMode mode,
        string picksFolderName = AppConstants.DefaultPicksFolder,
        System.Threading.CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var photos = new List<Photo>();

            string subFolder = mode == ViewMode.Edited ? picksFolderName : AppConstants.OriginalsSubFolder;
            var folderPath = Path.Combine(session.FolderPath, subFolder);

            if (!Directory.Exists(folderPath)) return photos;

            var exifService = ExifService.Instance;
            var files = Directory.EnumerateFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !IsJunkFile(Path.GetFileName(f)))
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var meta = await exifService.ReadMetaAsync(file);
                photos.Add(new Photo { Id = file, FilePath = file, Meta = meta });
            }

            return photos;
        }, ct);
    }

    public async Task<SessionEnrichmentStatus> CheckEnrichmentStatusAsync(PhotoSession session, string picksFolderName = AppConstants.DefaultPicksFolder)
    {
        return await Task.Run(async () =>
        {
            var exifService = ExifService.Instance;
            var editedPath = Path.Combine(session.FolderPath, picksFolderName);
            if (!Directory.Exists(editedPath))
                editedPath = Path.Combine(session.FolderPath, AppConstants.OriginalsSubFolder);
            if (!Directory.Exists(editedPath)) return SessionEnrichmentStatus.Unknown;

            var files = Directory.EnumerateFiles(editedPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Take(3)
                .ToList();

            if (files.Count == 0) return SessionEnrichmentStatus.Unknown;

            int enriched = 0;
            int partial = 0;
            foreach (var file in files)
            {
                var meta = await exifService.ReadMetaAsync(file);
                if (meta.Title != null && meta.Description != null)
                    enriched++;
                else if (meta.Title != null || meta.Keywords.Count > 0)
                    partial++;
            }

            if (enriched == files.Count) return SessionEnrichmentStatus.Enriched;
            if (enriched > 0 || partial > 0) return SessionEnrichmentStatus.Partial;
            return SessionEnrichmentStatus.Pending;
        });
    }

    public string? FirstPhotoPath(PhotoSession session)
    {
        var editedPath = Path.Combine(session.FolderPath, AppConstants.DefaultPicksFolder);
        var result = GetFirstPhoto(editedPath);
        if (result != null) return result;

        var jpegPath = Path.Combine(session.FolderPath, AppConstants.OriginalsSubFolder);
        return GetFirstPhoto(jpegPath);
    }

    // macOS creates "._<name>" sidecar files on FAT/exFAT cards — they look like JPEGs but aren't
    private static bool IsJunkFile(string fileName) =>
        fileName.StartsWith("._", StringComparison.Ordinal) ||
        fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase);

    private static int CountImages(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return 0;
        return Directory.EnumerateFiles(dirPath)
            .Count(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
                        && !IsJunkFile(Path.GetFileName(f)));
    }

    private static int CountRaw(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return 0;
        return Directory.EnumerateFiles(dirPath)
            .Count(f => RawExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
                        && !IsJunkFile(Path.GetFileName(f)));
    }

    private static string? GetFirstPhoto(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return null;
        return Directory.EnumerateFiles(dirPath)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !IsJunkFile(Path.GetFileName(f)))
            .OrderBy(f => f)
            .FirstOrDefault();
    }
}
