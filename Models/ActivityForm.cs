using System.ComponentModel.DataAnnotations;
namespace PartTracker.Models;
public class ActivityForm
    {
        public int Id { get; set; }                      // from Excel "Id" (0..N)

        public DateTime? StartTime { get; set; }         // "Start time"
        public DateTime? CompletionTime { get; set; }    // "Completion time"

        public string Email { get; set; } = string.Empty; // "Email" (unique)

        public string? Name { get; set; }                // "Name"
        public string? Language { get; set; }            // "Language"

        // "Premium Booking, Aftermarket collection or Speed-up"
        public string? CollectionType { get; set; }
        public DateTime? Date { get; set; }

        public string? TONumber { get; set; }            // "TO number"
        public string? MfgCode { get; set; }             // "MFG Code"
        public string? ShpCode { get; set; }             // "SHP Code"

        public string? Reasons1 { get; set; }            // "Reasons1"
        public string? Reasons { get; set; }             // "Reasons"
        public string? Comment { get; set; }             // "Comment"

        public string? PN { get; set; }                  // "PN"
        public string? Reasons2 { get; set; }            // "Reasons2"

        public int? Quantity { get; set; }               // "Quantity"

        public string? CollectedBy { get; set; }         // "Collected by:"
        public string? Comment1 { get; set; }            // "Comment1"

        public string? TONumber1 { get; set; }           // "TO Number1"
        public string? PartNumber { get; set; }          // "Part Number"
        public string? Reasons3 { get; set; }            // "Reasons3"
        public string? MfgCode1 { get; set; }            // "MFG code1"
        public string? Comment2 { get; set; }            // "Comment2"
    }