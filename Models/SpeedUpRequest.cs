using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("ibl_emea_speedup_requests")]
public class SpeedUpRequest
{
    public int Id { get; set; }
    public string? Requester { get; set; }
    
    [Column("Load Number")]
    public string? LoadNumber { get; set; }
    
    [Column("TO Number")]
    public string? TONumber { get; set; }
    
    [Column("Load Original Pickup From")]
    public DateTime? LoadOriginalPickupFrom { get; set; }
    
    [Column("Load Original Pickup To")]
    public DateTime? LoadOriginalPickupTo { get; set; }
    
    [Column("Supplier Code")]
    public string? SupplierCode { get; set; }
    
    [Column("Carrier Code")]
    public string? CarrierCode { get; set; }
    
    [Column("Delivery Plant")]
    public string? DeliveryPlant { get; set; }
    
    [Column("Load Original Delivery From")]
    public DateTime? LoadOriginalDeliveryFrom { get; set; }
    
    [Column("Load Original Delivery To")]
    public DateTime? LoadOriginalDeliveryTo { get; set; }
    
    [Column("New Delivery Deadline")]
    public DateTime? NewDeliveryDeadline { get; set; }
    
    [Column("Reason Code")]
    public string? ReasonCode { get; set; }
    
    [Column("Impact & Comment")]
    public string? ImpactComment { get; set; }
    
    [Column("Request Status")]
    public string? RequestStatus { get; set; }
    
    [Column("Extra Cost (Euro)")]
    public decimal? ExtraCost { get; set; }
    
    [Column("New Agreed ETA")]
    public DateTime? NewAgreedETA { get; set; }
    
    [Column("IBL Working Note")]
    public string? IBLWorkingNote { get; set; }
    
    [Column("Approved Person")]
    public string? ApprovedPerson { get; set; }
    
    [Column("Confirmed Cost (Euro)")]
    public decimal? ConfirmedCost { get; set; }
    
    [Column("Confirmed Final ETA")]
    public DateTime? ConfirmedFinalETA { get; set; }
    
    [Column("Item Type")]
    public string? ItemType { get; set; }
    
    public string? Path { get; set; }
}
