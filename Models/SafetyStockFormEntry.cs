using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

public class SafetyStockFormEntry
{
    [Column("id")]
    public int Id { get; set; }
    
    [Column("start_time")]
    public DateTime? StartTime { get; set; }
    
    [Column("completion_time")]
    public DateTime? CompletionTime { get; set; }
    
    [Column("email")]
    public string? Email { get; set; }
    
    [Column("name")]
    public string? Name { get; set; }
    
    [Column("language")]
    public string? Language { get; set; }
    
    [Column("by_pn_or_mfg_code")]
    public string? ByPnOrMfgCode { get; set; }
    
    [Column("part_number")]
    public string? PartNumber { get; set; }
    
    [Column("mfg_code")]
    public string? MfgCode { get; set; }
    
    [Column("shp_code")]
    public string? ShpCode { get; set; }
    
    [Column("old_safety_stock_shifts")]
    public decimal? OldSafetyStockShifts { get; set; }
    
    [Column("new_safety_stock_shifts")]
    public decimal? NewSafetyStockShifts { get; set; }
    
    [Column("old_safety_stock_pieces")]
    public decimal? OldSafetyStockPieces { get; set; }
    
    [Column("new_safety_stock_pieces")]
    public decimal? NewSafetyStockPieces { get; set; }
    
    [Column("old_pou_offsets_shifts")]
    public decimal? OldPouOffsetsShifts { get; set; }
    
    [Column("new_pou_offsets_shifts")]
    public decimal? NewPouOffsetsShifts { get; set; }
    
    [Column("comment")]
    public string? Comment { get; set; }
    
    [Column("mfg_code1")]
    public string? MfgCode1 { get; set; }
    
    [Column("shp_code1")]
    public string? ShpCode1 { get; set; }
    
    [Column("old_safety_stock_per_shift")]
    public decimal? OldSafetyStockPerShift { get; set; }
    
    [Column("old_safety_stock_per_piece")]
    public decimal? OldSafetyStockPerPiece { get; set; }
    
    [Column("new_safety_stock_per_shift")]
    public decimal? NewSafetyStockPerShift { get; set; }
    
    [Column("new_safety_stock_per_piece")]
    public decimal? NewSafetyStockPerPiece { get; set; }
    
    [Column("comment1")]
    public string? Comment1 { get; set; }
}
