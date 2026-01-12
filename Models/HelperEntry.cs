using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("helper")]
public class HelperEntry
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("order_number")]
    public string? OrderNumber { get; set; }
    
    [Column("comment")]
    public string? Comment { get; set; }
    
    [Column("reason_code")]
    public string? ReasonCode { get; set; }
    
    [Column("delivery_id_left5")]
    public string? DeliveryIdLeft5 { get; set; }
    
    [Column("status_ready_user")]
    public string? StatusReadyUser { get; set; }
    
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
