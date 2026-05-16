using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Data.Models;

namespace WeatherMonitor.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<WeatherRecord> WeatherRecords => Set<WeatherRecord>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<WeatherRecord>(e =>
        {
            e.HasIndex(r => new { r.CityId, r.SourceId, r.Timestamp }).IsUnique();
            e.HasIndex(r => new { r.CityId, r.Timestamp });
            e.HasOne(r => r.City).WithMany(c => c.WeatherRecords).HasForeignKey(r => r.CityId);
            e.HasOne(r => r.Source).WithMany(s => s.WeatherRecords).HasForeignKey(r => r.SourceId);
        });

        mb.Entity<ErrorLog>(e => e.HasIndex(l => l.LoggedAt));
    }
}
