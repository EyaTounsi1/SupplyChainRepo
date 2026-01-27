using System.ComponentModel.DataAnnotations;

namespace PartTracker.Models;

public class SafetyStockItem
{
    [Key]
    public required string? PartNumber { get; set; }
    public float SafetyStockNrOfParts { get; set; }
    public string MfgSupplierCode { get; set; } = string.Empty;
}