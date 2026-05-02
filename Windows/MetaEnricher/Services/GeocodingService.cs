using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MetaEnricher.Services;

public class GeocodingService
{
    private static GeocodingService? _instance;
    public static GeocodingService Instance => _instance ??= new GeocodingService();

    private readonly HttpClient _http;

    public GeocodingService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", "MetaEnricher/1.0");
    }

    public async Task<(string? City, string? Country)> ReverseGeocodeAsync(double lat, double lon)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&zoom=10&addressdetails=1";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("address", out var address))
                return (null, null);

            string? city = null;
            foreach (var key in new[] { "city", "town", "village", "municipality", "county" })
            {
                if (address.TryGetProperty(key, out var v))
                {
                    city = v.GetString();
                    if (!string.IsNullOrWhiteSpace(city)) break;
                }
            }

            string? country = null;
            if (address.TryGetProperty("country", out var c))
                country = c.GetString();

            return (city, country);
        }
        catch
        {
            return (null, null);
        }
    }
}
