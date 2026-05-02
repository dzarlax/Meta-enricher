using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MetaEnricher.Services;

public class ThumbnailService
{
    private static ThumbnailService? _instance;
    public static ThumbnailService Instance => _instance ??= new ThumbnailService();

    private readonly Dictionary<string, SoftwareBitmapSource> _cache = new();
    private readonly Queue<string> _cacheOrder = new();
    private const int MaxCacheSize = 200;

    public async Task<SoftwareBitmapSource?> GetThumbnailAsync(string filePath, int size)
    {
        var key = $"{filePath}|{size}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);

            uint origW = decoder.PixelWidth;
            uint origH = decoder.PixelHeight;

            double scale = Math.Min((double)size / origW, (double)size / origH);
            if (scale > 1) scale = 1;

            uint newW = Math.Max(1, (uint)(origW * scale));
            uint newH = Math.Max(1, (uint)(origH * scale));

            var transform = new BitmapTransform
            {
                ScaledWidth = newW,
                ScaledHeight = newH,
                InterpolationMode = BitmapInterpolationMode.Linear
            };

            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);

            // Manage cache size
            if (_cache.Count >= MaxCacheSize && _cacheOrder.Count > 0)
            {
                var oldest = _cacheOrder.Dequeue();
                _cache.Remove(oldest);
            }

            _cache[key] = source;
            _cacheOrder.Enqueue(key);
            return source;
        }
        catch
        {
            return null;
        }
    }

    public void Invalidate(string filePath)
    {
        var keysToRemove = new List<string>();
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(filePath + "|"))
                keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
            _cache.Remove(key);
    }

    public void PurgeAll()
    {
        _cache.Clear();
        _cacheOrder.Clear();
    }
}
