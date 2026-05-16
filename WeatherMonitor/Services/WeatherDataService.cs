using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Data;
using WeatherMonitor.Data.Models;

namespace WeatherMonitor.Services;

public class WeatherDataService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<City>> GetCitiesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Cities
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<WeatherRecord>> GetWeatherDataAsync(
        int cityId, DateTime? from, DateTime? to)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.WeatherRecords.Where(r => r.CityId == cityId);

        if (from.HasValue)
            query = query.Where(r => r.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.Timestamp <= to.Value);

        return await query
            .OrderBy(r => r.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }
}
