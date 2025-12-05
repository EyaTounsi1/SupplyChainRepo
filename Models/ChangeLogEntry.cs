using System.ComponentModel.DataAnnotations;

namespace PartTracker.Models;

public class ChangeLogEntry
{
    [Key]
    public int Id { get; set; }

    public string? ExcelId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? CompletionTime { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Language { get; set; }
    public string? PremiumBooking { get; set; }
    public string? ToNumber { get; set; }
    public string? MfgCode { get; set; }
    public string? ShpCode { get; set; }
    public string? Reasons { get; set; }
    public string? Comment { get; set; }
    public string? Pn { get; set; }
    public string? Quantity { get; set; }
    public string? Reasons1 { get; set; }
    public string? CollectedBy { get; set; }
    public string? Comment1 { get; set; }
    public string? ToNumber1 { get; set; }
    public string? PartNumber { get; set; }
    public string? MfgCode1 { get; set; }
    public string? Reasons2 { get; set; }
    public string? Comment2 { get; set; }

    public decimal? Cost { get; set; }

    public DateTime LastUpdated { get; set; }
}