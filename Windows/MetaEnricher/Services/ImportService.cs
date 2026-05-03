using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MetaEnricher.Services;

public record ImportItem(string SourcePath, string DestPath, string FileName, DateTime Date, bool IsNew);
public record ScanResult(List<ImportItem> NewFiles, int AlreadyCopied, int TotalOnCard);
public record ImportProgress(int Total, int Copied, string CurrentFile, bool Done, string? Error);

public class ImportService
{
    private static ImportService? _instance;
    public static ImportService Instance => _instance ??= new ImportService();

    private static readonly string[] JpegExtensions = { ".jpg", ".jpeg" };
    private static readonly string[] RawExtensions  = { ".arw", ".nef", ".cr2", ".dng", ".raf", ".orf", ".rw2", ".cr3" };

    public List<string> FindDriveRoots()
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (Directory.Exists(Path.Combine(drive.RootDirectory.FullName, "DCIM")))
                    roots.Add(drive.RootDirectory.FullName);
            }
            catch { }
        }
        return roots;
    }

    public async Task<ScanResult> ScanAsync(string sourceDcimPath, string destPath)
    {
        return await Task.Run(() =>
        {
            var allExtensions = JpegExtensions.Concat(RawExtensions)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allFiles = new List<string>();
            try
            {
                allFiles = Directory.EnumerateFiles(sourceDcimPath, "*", SearchOption.AllDirectories)
                    .Where(f => allExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Where(f => !Path.GetFileName(f).StartsWith("._", StringComparison.Ordinal))
                    .ToList();
            }
            catch { }

            var newFiles = new List<ImportItem>();
            int alreadyCopied = 0;

            foreach (var file in allFiles)
            {
                var date    = ExtractDate(file);
                var year    = date.Year.ToString("D4");
                var dateStr = date.ToString("yyyy-MM-dd");
                var ext     = Path.GetExtension(file).ToLowerInvariant();
                var subDir  = RawExtensions.Contains(ext) ? AppConstants.RawSubFolder : AppConstants.OriginalsSubFolder;
                var destDir  = Path.Combine(destPath, year, dateStr, subDir);
                var destFile = Path.Combine(destDir, Path.GetFileName(file));

                bool exists = File.Exists(destFile) &&
                              new FileInfo(file).Length == new FileInfo(destFile).Length;

                if (exists)
                    alreadyCopied++;
                else
                    newFiles.Add(new ImportItem(file, destFile, Path.GetFileName(file), date, true));
            }

            return new ScanResult(newFiles, alreadyCopied, allFiles.Count);
        });
    }

    public async Task ImportAsync(
        IList<ImportItem> items,
        IProgress<ImportProgress> progress,
        CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            int total  = items.Count;
            int copied = 0;

            foreach (var item in items)
            {
                if (ct.IsCancellationRequested) break;

                progress.Report(new ImportProgress(total, copied, item.FileName, false, null));
                try
                {
                    if (File.Exists(item.DestPath)) { copied++; continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(item.DestPath)!);
                    File.Copy(item.SourcePath, item.DestPath);
                    copied++;
                }
                catch (Exception ex)
                {
                    progress.Report(new ImportProgress(total, copied, item.FileName, false, ex.Message));
                }
            }

            progress.Report(new ImportProgress(total, copied, "", true, null));
        }, ct);
    }

    private static DateTime ExtractDate(string filePath)
    {
        var match = Regex.Match(Path.GetFileNameWithoutExtension(filePath), @"(\d{4})(\d{2})(\d{2})");
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int y) &&
            int.TryParse(match.Groups[2].Value, out int mo) &&
            int.TryParse(match.Groups[3].Value, out int d))
        {
            try { return new DateTime(y, mo, d); } catch { }
        }
        return File.GetLastWriteTime(filePath);
    }
}
