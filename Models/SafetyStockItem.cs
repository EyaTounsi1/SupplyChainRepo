using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[NotMapped]
public class SafetyStockItem
{
    public string Site { get; set; } = string.Empty;
    public string PlanningPoint { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string MfgSupplierCode { get; set; } = string.Empty;
    public int SafetyStockNrOfParts { get; set; }
    public int SafetyStockLeadTime { get; set; }
    public string UploadedFromSource { get; set; } = string.Empty;
}