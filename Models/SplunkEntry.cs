using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("splunk")]
public class SplunkEntry
{
    [Column("LoadNumber")]
    public string? LoadNumber { get; set; }
    
    [Column("TransportOrder")]
    public string? TransportOrder { get; set; }
    
    [Column("TotalCostEUR")]
    public decimal? TotalCostEUR { get; set; }
    
    [Column("Currency")]
    public string? Currency { get; set; }
    
    [Column("SupplierCode")]
    public string? SupplierCode { get; set; }
    
    [Column("FDP")]
    public string? FDP { get; set; }
    
    [Column("DeliveryContactPerson")]
    public string? DeliveryContactPerson { get; set; }
    
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
