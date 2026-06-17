using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Data;
using WeatherMonitor.Data.Models;

namespace WeatherMonitor.Services;

public class WeatherCollectorWorker(
    IDbContextFactory<AppDbContext> dbFactory,
    IServiceScopeFactory scopeFactory,       // used only for scoped OpenMeteoClient
    ILogger<WeatherCollectorWorker> logger,
    IConfiguration config) : BackgroundService
{
    private readonly TimeSpan _interval =
        TimeSpan.FromMinutes(config.GetValue<int>("CollectIntervalMinutes", 60));

    // Per-city request timeout — separate from the host stopping token
    private readonly TimeSpan _apiTimeout =
        TimeSpan.FromSeconds(config.GetValue<int>("ApiTimeoutSeconds", 60));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Collect 7 days of history on first startup
        await TriggerCollectionAsync(pastDays: 7, ct: stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await TriggerCollectionAsync(pastDays: 1, ct: stoppingToken);
    }

    public async Task TriggerCollectionAsync(int pastDays = 1, CancellationToken ct = default)
    {
        logger.LogInformation("Collection cycle started (past_days={Days})", pastDays);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // OpenMeteoClient is a Transient typed HTTP client — resolve from a scope
        using var scope = scopeFactory.CreateScope();
        var meteoClient = scope.ServiceProvider.GetRequiredService<OpenMeteoClient>();

        var source = await db.DataSources
            .FirstOrDefaultAsync(s => s.SourceType == "open_meteo" && s.IsActive, ct);
        if (source is null)
        {
            logger.LogWarning("No active open_meteo data source configured");
            return;
        }

        var cities = await db.Cities.Where(c => c.IsActive).ToListAsync(ct);

        foreach (var city in cities)
        {
            // Per-city CTS: cancels either when host stops OR when per-city timeout fires.
            // Keeps cities independent — one hanging request doesn't block the others.
            using var cityCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cityCts.CancelAfter(_apiTimeout);

            try
            {
                await CollectForCityAsync(db, meteoClient, city, source, pastDays, cityCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Host is shutting down — stop the loop cleanly without logging an error
                logger.LogInformation("Collection loop cancelled (host stopping)");
                return;
            }
            catch (OperationCanceledException ex)
            {
                // Per-city timeout fired (cityCts.CancelAfter) — not the host stopping
                var msg = $"API request timed out for {city.Name} (limit: {_apiTimeout.TotalSeconds}s)";
                logger.LogWarning(msg);
                await LogErrorAsync(db, city.Name, "TimeoutError", msg, ex.ToString());
            }
            catch (HttpRequestException ex)
            {
                var msg = $"HTTP error for {city.Name}: {ex.StatusCode}";
                logger.LogWarning(msg);
                await LogErrorAsync(db, city.Name, "HttpError", msg, ex.ToString());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error collecting data for {City}", city.Name);
                await LogErrorAsync(db, city.Name, "UnexpectedError", ex.Message, ex.ToString());
            }
        }

        logger.LogInformation("Collection cycle complete");
    }

    private async Task CollectForCityAsync(AppDbContext db, OpenMeteoClient client,
        City city, DataSource source, int pastDays, CancellationToken ct)
    {
        var lat = (double)city.Latitude;
        var lon = (double)city.Longitude;

        // Both API requests in parallel
        var weatherTask = client.GetWeatherAsync(lat, lon, pastDays, ct);
        var aqTask = client.GetAirQualityAsync(lat, lon, pastDays, ct);
        await Task.WhenAll(weatherTask, aqTask);

        var weather = weatherTask.Result;
        var aq = aqTask.Result;

        var aqiByTime = aq.Hourly.Time
            .Zip(aq.Hourly.UsAqi)
            .ToDictionary(p => p.First, p => p.Second);

        var times = weather.Hourly.Time.Select(DateTime.Parse).ToList();
        var minTs = times.Min();
        var maxTs = times.Max();

        // Bulk-load existing timestamps to avoid per-row duplicate checks
        var existing = (await db.WeatherRecords
            .Where(r => r.CityId == city.Id && r.SourceId == source.Id
                        && r.Timestamp >= minTs && r.Timestamp <= maxTs)
            .Select(r => r.Timestamp)
            .ToListAsync(ct))
            .ToHashSet();

        var toInsert = new List<WeatherRecord>();
        for (var i = 0; i < times.Count; i++)
        {
            var ts = times[i];
            if (existing.Contains(ts)) continue;

            var temp = weather.Hourly.Temperature2m[i];
            var humid = weather.Hourly.RelativeHumidity2m[i];
            var pressure = weather.Hourly.SurfacePressure[i];
            aqiByTime.TryGetValue(weather.Hourly.Time[i], out var aqi);

            if (temp is null && humid is null && pressure is null && aqi is null) continue;

            toInsert.Add(new WeatherRecord
            {
                CityId = city.Id,
                SourceId = source.Id,
                Timestamp = ts,
                Temperature2m = temp.HasValue ? (decimal)Math.Round(temp.Value, 2) : null,
                RelativeHumidity2m = humid.HasValue ? (decimal)Math.Round(humid.Value, 2) : null,
                SurfacePressure = pressure.HasValue ? (decimal)Math.Round(pressure.Value, 2) : null,
                UsAqi = aqi,
            });
        }

        if (toInsert.Count > 0)
        {
            db.WeatherRecords.AddRange(toInsert);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("{City}: +{New} new, {Skip} skipped",
            city.Name, toInsert.Count, times.Count - toInsert.Count);
    }

    private static async Task LogErrorAsync(AppDbContext db, string source,
        string errorType, string message, string? details = null)
    {
        try
        {
            db.ErrorLogs.Add(new ErrorLog
            {
                Source = source[..Math.Min(source.Length, 100)],
                ErrorType = errorType,
                Message = message,
                Details = details,
            });
            await db.SaveChangesAsync();
        }
        catch
        {
            // Never throw inside the error logger
        }
    }
}
