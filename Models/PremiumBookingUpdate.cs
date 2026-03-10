using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("premiumbookingupdate")]
public class PremiumBookingUpdate
{
    public int Id { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? CompletionTime { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Language { get; set; }
    public string? BookingType { get; set; }
    public string? TONumber { get; set; }
    public string? MFGCode { get; set; }
    public string? SHPCode { get; set; }
    public string? Reasons { get; set; }
    public string? CommentMain { get; set; }
}