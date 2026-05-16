using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherMonitor.Data.Models;

[Table("DataSources")]
public class DataSource
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Type tag: "open_meteo", "iot", etc.</summary>
    [Required, MaxLength(50)]
    public string SourceType { get; set; } = string.Empty;

    public string? ApiUrl { get; set; }

    [Column(TypeName = "json")]
    public string? Config { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WeatherRecord> WeatherRecords { get; set; } = [];
}
