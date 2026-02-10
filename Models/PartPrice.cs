using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("Part_Price")]
public class PartPrice
{
    [Key]
    [Column("PART_NUMBER")]
    public string PartNumber { get; set; } = string.Empty;
    
    [Column("STANDARD_PRICE")]
    public decimal StandardPrice { get; set; }
}
