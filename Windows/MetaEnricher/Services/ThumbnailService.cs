using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MetaEnricher.Services;

public class ThumbnailService
{
    private static ThumbnailService? _instance;
    public static ThumbnailService Instance => _instance ??= new ThumbnailService();

    // Key: "filePath|decodeWidth"
    private readonly Dictionary<string, BitmapImage> _cache = new();
    private readonly Queue<string> _cacheOrder = new();
    private const int MaxCacheSize = 300;

    /// <summary>
    /// Returns a BitmapImage for the given path and decode width.
    /// Must be called on the UI thread. BitmapImage loads the file asynchronously
    /// on its own — no await needed here.
    /// </summary>
    public BitmapImage? GetThumbnail(string filePath, int decodeWidth)
    {
        var key = $"{filePath}|{decodeWidth}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        if (!File.Exists(filePath))
            return null;

        var bmp = new BitmapImage
        {
            DecodePixelWidth = decodeWidth,
            DecodePixelType = DecodePixelType.Logical,
            // UriSource starts the async file load immediately on a background thread.
            UriSource = new Uri(filePath)
        };

        if (_cache.Count >= MaxCacheSize && _cacheOrder.Count > 0)
            _cache.Remove(_cacheOrder.Dequeue());

        _cache[key] = bmp;
        _cacheOrder.Enqueue(key);
        return bmp;
    }

    public void Invalidate(string filePath)
    {
        var toRemove = new List<string>();
        foreach (var key in _cache.Keys)
            if (key.StartsWith(filePath + "|"))
                toRemove.Add(key);
        foreach (var key in toRemove)
            _cache.Remove(key);
    }

    public void PurgeAll()
    {
        _cache.Clear();
        _cacheOrder.Clear();
    }
}
