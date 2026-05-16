using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherMonitor.Data.Models;

[Table("WeatherData")]
public class WeatherRecord
{
    public long Id { get; set; }

    public int CityId { get; set; }
    public City City { get; set; } = default!;

    public int SourceId { get; set; }
    public DataSource Source { get; set; } = default!;

    public DateTime Timestamp { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? Temperature2m { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? RelativeHumidity2m { get; set; }

    [Column(TypeName = "decimal(7,2)")]
    public decimal? SurfacePressure { get; set; }

    public int? UsAqi { get; set; }
}
