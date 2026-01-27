using System.ComponentModel.DataAnnotations;

namespace PartTracker.Models;

public class ForecastItem
{
    [Key]
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public required string? Product { get; set; }
    public decimal ForecastValue { get; set; }
}