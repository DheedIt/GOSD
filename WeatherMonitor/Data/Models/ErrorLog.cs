using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherMonitor.Data.Models;

[Table("ErrorLog")]
public class ErrorLog
{
    public long Id { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(100)]
    public string? ErrorType { get; set; }

    public string? Message { get; set; }

    public string? Details { get; set; }
}
