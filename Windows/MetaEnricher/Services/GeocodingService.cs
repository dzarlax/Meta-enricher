using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MetaEnricher.Services;

public class GeocodingService
{
    private static GeocodingService? _instance;
    public static GeocodingService Instance => _instance ??= new GeocodingService();

    private readonly HttpClient _http;
    // Nominatim ToS: max 1 req/sec. Serialize calls + enforce minimum interval.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastCallUtc = DateTime.MinValue;
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1100);

    // Round to ~3 decimal places (~111m) to share lookups between nearby photos
    private readonly Dictionary<string, (string? City, string? Country)> _cache = new();

    public GeocodingService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(AppConstants.GeocodingTimeoutSec) };
        _http.DefaultRequestHeaders.Add("User-Agent", "MetaEnricher/1.0");
    }

    public async Task<(string? City, string? Country)> ReverseGeocodeAsync(double lat, double lon)
    {
        var key = $"{Math.Round(lat, 3):F3},{Math.Round(lon, 3):F3}";
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;
        }

        await _gate.WaitAsync();
        try
        {
            var elapsed = DateTime.UtcNow - _lastCallUtc;
            if (elapsed < MinInterval)
                await Task.Delay(MinInterval - elapsed);

            var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&zoom=10&addressdetails=1";
            var json = await _http.GetStringAsync(url);
            _lastCallUtc = DateTime.UtcNow;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("address", out var address))
            {
                lock (_cache) _cache[key] = (null, null);
                return (null, null);
            }

            string? city = null;
            foreach (var k in new[] { "city", "town", "village", "municipality", "county" })
            {
                if (address.TryGetProperty(k, out var v))
                {
                    city = v.GetString();
                    if (!string.IsNullOrWhiteSpace(city)) break;
                }
            }

            string? country = address.TryGetProperty("country", out var c) ? c.GetString() : null;

            var result = (city, country);
            lock (_cache) _cache[key] = result;
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Geocoding] Reverse lookup failed for {lat},{lon}: {ex.Message}");
            return (null, null);
        }
        finally
        {
            _gate.Release();
        }
    }
}
