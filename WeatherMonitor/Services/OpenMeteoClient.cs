using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherMonitor.Services;

public class OpenMeteoClient(HttpClient http)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // Use InvariantCulture so coordinates always use '.' as decimal separator
    // regardless of the OS locale (critical on Russian Windows where default is ',')
    private static string Coord(double v) => v.ToString("F6", CultureInfo.InvariantCulture);

    public async Task<WeatherResponse> GetWeatherAsync(double lat, double lon, int pastDays,
        CancellationToken ct = default)
    {
        var url = $"https://api.open-meteo.com/v1/forecast"
                + $"?latitude={Coord(lat)}&longitude={Coord(lon)}"
                + $"&hourly=temperature_2m,relative_humidity_2m,surface_pressure"
                + $"&timezone=auto&past_days={pastDays}&forecast_days=1";

        var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<WeatherResponse>(_json, ct))!;
    }

    public async Task<AirQualityResponse> GetAirQualityAsync(double lat, double lon, int pastDays,
        CancellationToken ct = default)
    {
        var url = $"https://air-quality-api.open-meteo.com/v1/air-quality"
                + $"?latitude={Coord(lat)}&longitude={Coord(lon)}"
                + $"&hourly=us_aqi"
                + $"&timezone=auto&past_days={pastDays}&forecast_days=1";

        var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AirQualityResponse>(_json, ct))!;
    }
}

public record WeatherResponse(WeatherHourly Hourly);

public record WeatherHourly(
    [property: JsonPropertyName("time")] string[] Time,
    [property: JsonPropertyName("temperature_2m")] double?[] Temperature2m,
    [property: JsonPropertyName("relative_humidity_2m")] double?[] RelativeHumidity2m,
    [property: JsonPropertyName("surface_pressure")] double?[] SurfacePressure
);

public record AirQualityResponse(AirQualityHourly Hourly);

public record AirQualityHourly(
    [property: JsonPropertyName("time")] string[] Time,
    [property: JsonPropertyName("us_aqi")] int?[] UsAqi
);
