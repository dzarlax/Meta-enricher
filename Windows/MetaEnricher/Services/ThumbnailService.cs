using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MetaEnricher.Services;

public class ThumbnailService
{
    private static ThumbnailService? _instance;
    public static ThumbnailService Instance => _instance ??= new ThumbnailService();

    // Key: "filePath|decodeWidth"  →  (image, approximate decoded bytes)
    private readonly Dictionary<string, (BitmapImage Image, long Bytes)> _cache = new();
    private readonly Queue<string> _cacheOrder = new();
    private long _totalBytes = 0;
    // Cap at ~150MB of decoded thumbnail pixels — works for 1000s of small thumbs or 100s of large ones
    private const long MaxCacheBytes = 150 * 1024 * 1024;

    public BitmapImage? GetThumbnail(string filePath, int decodeWidth)
    {
        var key = $"{filePath}|{decodeWidth}";
        if (_cache.TryGetValue(key, out var cached))
            return cached.Image;

        if (!File.Exists(filePath))
            return null;

        var bmp = new BitmapImage
        {
            DecodePixelWidth = decodeWidth,
            DecodePixelType = DecodePixelType.Logical,
            UriSource = new Uri(filePath)
        };

        // Estimate: 4 bytes/pixel BGRA, assume square aspect for safety (real ratio unknown until decode)
        long approxBytes = (long)decodeWidth * decodeWidth * 4;

        // Evict oldest until under budget
        while (_totalBytes + approxBytes > MaxCacheBytes && _cacheOrder.Count > 0)
        {
            var oldKey = _cacheOrder.Dequeue();
            if (_cache.Remove(oldKey, out var evicted))
                _totalBytes -= evicted.Bytes;
        }

        _cache[key] = (bmp, approxBytes);
        _cacheOrder.Enqueue(key);
        _totalBytes += approxBytes;
        return bmp;
    }

    public void Invalidate(string filePath)
    {
        var toRemove = new List<string>();
        foreach (var key in _cache.Keys)
            if (key.StartsWith(filePath + "|"))
                toRemove.Add(key);
        foreach (var key in toRemove)
        {
            if (_cache.Remove(key, out var entry))
                _totalBytes -= entry.Bytes;
        }
    }

    public void PurgeAll()
    {
        _cache.Clear();
        _cacheOrder.Clear();
        _totalBytes = 0;
    }
}
