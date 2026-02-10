using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("premiums2024")]
public class Premium2024
{
    public ulong Id { get; set; }
    
    [Column("date")]
    public DateTime? Date { get; set; }
    
    [Column("to_number")]
    public long? ToNumber { get; set; }
    
    [Column("loadnumber")]
    public long? LoadNumber { get; set; }
    
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