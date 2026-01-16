using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("premiums2025")]
public class Premium2025
{
    public int Id { get; set; }
    
    [Column("shipment_date")]
    public DateTime? ShipmentDate { get; set; }
    
    [Column("to_number")]
    public string? ToNumber { get; set; }
    
    [Column("load_number")]
    public string? LoadNumber { get; set; }
    
    [Column("carrier_billing_name")]
    public string? CarrierBillingName { get; set; }
    
    [Column("consignor_name")]
    public string? ConsignorName { get; set; }
    
    [Column("consignor_country_code")]
    public string? ConsignorCountryCode { get; set; }
    
    [Column("recipient_name")]
    public string? RecipientName { get; set; }
    
    [Column("recipient_country_code")]
    public string? RecipientCountryCode { get; set; }
    
    [Column("delivery_name")]
    public string? DeliveryName { get; set; }
    
    [Column("service_type")]
    public string? ServiceType { get; set; }
    
    [Column("reason_code")]
    public string? ReasonCode { get; set; }
    
    [Column("comment")]
    public string? Comment { get; set; }
    
    [Column("currency_org")]
    public string? CurrencyOrg { get; set; }
    
    [Column("total_costs_sek")]
    public decimal? TotalCostsSek { get; set; }
    
    [Column("total_costs_org")]
    public decimal? TotalCostsOrg { get; set; }
}
