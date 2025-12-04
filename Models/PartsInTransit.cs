using System.ComponentModel.DataAnnotations;
namespace PartTracker.Models;

public class PartsInTransit {
    [Key] public int Id { get; set; }
    [Required] public int PlanPkt { get; set; }
    [Required, StringLength(5)] public required string LevNr { get; set; }
    [Required] public int ArtNr { get; set; }
    [Required] public int FsNr { get; set; }
    [Required] public DateTime AvsDat { get; set; }
    public DateTime? FavTid { get; set; }
    [Required] public int FavArtan { get; set; }
    public DateTime? AviTid { get; set; }
    [Required] public int AviArtan { get; set; }
    public DateTime? MotTid { get; set; }
    [Required] public int MotAntal { get; set; }
}