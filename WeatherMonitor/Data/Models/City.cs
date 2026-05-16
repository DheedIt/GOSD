using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherMonitor.Data.Models;

[Table("Cities")]
public class City
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,6)")]
    public decimal Latitude { get; set; }

    [Column(TypeName = "decimal(10,6)")]
    public decimal Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WeatherRecord> WeatherRecords { get; set; } = [];
}
