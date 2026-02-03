using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PartTracker.Models;

[Keyless]
public class ForecastItem
{
    public string? Site { get; set; }
    public string? Part_Number { get; set; }
    public DateTime Date { get; set; }
    public decimal Git_Balance { get; set; }
    public decimal Wip_Balance { get; set; }
    public decimal Price { get; set; }
    public decimal Git_Value_Sek { get; set; }
    public decimal Wip_Value_Sek { get; set; }
    public decimal Git_Value_M_Sek { get; set; }
    public decimal Wip_Value_M_Sek { get; set; }
    public decimal Total_Capital_M_Sek { get; set; }
    public string? Mfg_Supplier_Code { get; set; }
    public decimal? Safety_Stock_Nr_Of_Parts { get; set; }
    public decimal? Safety_Stock_Lead_Time { get; set; }
    public decimal? Wip_Minus_Ss { get; set; }
    public decimal? Days_Until_Stockout { get; set; }
    public decimal? Capital_At_Risk_Sek { get; set; }
    public string? Ss_Deviation_Flag { get; set; }
}