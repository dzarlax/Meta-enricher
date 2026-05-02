using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MetaEnricher.Services;

public record ImportProgress(int Total, int Copied, string CurrentFile, bool Done, string? Error);

public class ImportService
{
    private static ImportService? _instance;
    public static ImportService Instance => _instance ??= new ImportService();

    private static readonly string[] JpegExtensions = { ".jpg", ".jpeg" };
    private static readonly string[] RawExtensions = { ".arw", ".nef", ".cr2", ".dng", ".raf", ".orf", ".rw2", ".cr3" };

    public List<string> FindDriveRoots()
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                var dcim = Path.Combine(drive.RootDirectory.FullName, "DCIM");
                if (Directory.Exists(dcim))
                    roots.Add(drive.RootDirectory.FullName);
            }
            catch { }
        }
        return roots;
    }

    public async Task ImportAsync(string sourcePath, string destPath, string schema, IProgress<ImportProgress> progress)
    {
        await Task.Run(async () =>
        {
            var allExtensions = JpegExtensions.Concat(RawExtensions).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var files = new List<string>();
            try
            {
                foreach (var f in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (allExtensions.Contains(ext))
                        files.Add(f);
                }
            }
            catch (Exception ex)
            {
                progress.Report(new ImportProgress(0, 0, "", true, ex.Message));
                return;
            }

            int total = files.Count;
            int copied = 0;

            foreach (var file in files)
            {
                progress.Report(new ImportProgress(total, copied, Path.GetFileName(file), false, null));

                try
                {
                    var date = ExtractDate(file);
                    var year = date.Year.ToString("D4");
                    var dateStr = date.ToString("yyyy-MM-dd");

                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var isRaw = RawExtensions.Contains(ext);
                    var subDir = isRaw ? "RAW" : "JPEG";

                    var destDir = Path.Combine(destPath, year, dateStr, subDir);
                    Directory.CreateDirectory(destDir);

                    var destFile = Path.Combine(destDir, Path.GetFileName(file));

                    // Skip if same size
                    if (File.Exists(destFile))
                    {
                        var srcInfo = new FileInfo(file);
                        var dstInfo = new FileInfo(destFile);
                        if (srcInfo.Length == dstInfo.Length)
                        {
                            copied++;
                            continue;
                        }
                    }

                    File.Copy(file, destFile, overwrite: false);
                    copied++;
                }
                catch (Exception ex)
                {
                    progress.Report(new ImportProgress(total, copied, Path.GetFileName(file), false, ex.Message));
                }
            }

            progress.Report(new ImportProgress(total, copied, "", true, null));
        });
    }

    private static DateTime ExtractDate(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        // Try DSC_YYYYMMDD or IMG_YYYYMMDD patterns
        var match = Regex.Match(name, @"(\d{4})(\d{2})(\d{2})");
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int y) &&
            int.TryParse(match.Groups[2].Value, out int mo) &&
            int.TryParse(match.Groups[3].Value, out int d))
        {
            try { return new DateTime(y, mo, d); } catch { }
        }

        // Fallback to file modification time
        return File.GetLastWriteTime(filePath);
    }
}
