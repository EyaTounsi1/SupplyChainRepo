using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("speedup")]
public class SpeedUpModel
{
    public int Id { get; set; } // Assuming auto-increment primary key

    [Column("TONumber1")]
    public string? TONumber1 { get; set; }

    [Column("PartNumber")]
    public string? PartNumber { get; set; }

    [Column("MFGCode1")]
    public string? MFGCode1 { get; set; }

    [Column("Reasons2")]
    public string? Reasons2 { get; set; }

    [Column("Comment2")]
    public string? Comment2 { get; set; }

    [Column("CollectedBy")]
    public string? CollectedBy { get; set; }

    [Column("StartTime")]
    public DateTime? StartTime { get; set; }

    [Column("CompletionTime")]
    public DateTime? CompletionTime { get; set; }

    [Column("Email")]
    public string? Email { get; set; }

    [Column("Name")]
    public string? Name { get; set; }
}