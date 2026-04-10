using System;
using System.ComponentModel.DataAnnotations;

namespace PartTracker.Models
{
    public class ReportOut
    {
        [Key]
        public int Id { get; set; }
        public double ForecastThisMonth { get; set; }
        public double NewAnticipationStockInShifts { get; set; }
        public double NewSafetyStockInShifts { get; set; }
        public int NewSafetyStockInNumberOfParts { get; set; }
        public double NewLeadTime { get; set; }
        public string PartNumber { get; set; }
        public DateTime Timestamp { get; set; }
    }
}