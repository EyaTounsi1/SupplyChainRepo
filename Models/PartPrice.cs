using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("Part_Price")]
public class PartPrice
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [Column("PART_NUMBER")]
    public string PartNumber { get; set; } = string.Empty;
    
    [Column("SITE")]
    public string? Site { get; set; }
    
    [Column("STANDARD_PRICE")]
    public decimal StandardPrice { get; set; }
    
    [Column("CURRENCY")]
    public string? Currency { get; set; }
    
    [Column("UPLOADED_FROM_SOURCE")]
    public DateTime? UploadedFromSource { get; set; }
}
