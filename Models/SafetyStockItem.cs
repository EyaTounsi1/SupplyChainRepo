using System.ComponentModel.DataAnnotations;

namespace PartTracker.Models;

public class SafetyStockItem
{
    [Key]
    public string PartNumber { get; set; }
    public float SafetyStockNrOfParts { get; set; }
}