namespace PartTracker.Models;

public class SafetyStockChange
{
    public string? Site { get; set; }
    public string? PlanningPoint { get; set; }
    public string? PartNumber { get; set; }
    public string? MfgSupplierCode { get; set; }

    public DateTime? YesterdayDate { get; set; }
    public DateTime? TodayDate { get; set; }

    public decimal? YSafetyStockLeadTime { get; set; }
    public decimal? TSafetyStockLeadTime { get; set; }
    public decimal? YSafetyStockNrOfParts { get; set; }
    public decimal? TSafetyStockNrOfParts { get; set; }
    public decimal? YFlsYardLeadtimeMinutes { get; set; }
    public decimal? TFlsYardLeadtimeMinutes { get; set; }
    public decimal? YFlsYardLeadtimeShiftsCalc { get; set; }
    public decimal? TFlsYardLeadtimeShiftsCalc { get; set; }

    public int ChangedFlag { get; set; }
    public int NumChangedRows { get; set; }
}
