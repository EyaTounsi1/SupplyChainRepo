using System.ComponentModel.DataAnnotations.Schema;

namespace PartTracker.Models;

[Table("aftermarketcollection")]
public class AftermarketCollectionModel
{
    public int Id { get; set; } // Assuming auto-increment primary key

    [Column("PN")]
    public string? PN { get; set; }

    [Column("Quantity")]
    public int? Quantity { get; set; }

    [Column("Reasons1")]
    public string? Reasons1 { get; set; }

    [Column("CollectedBy")]
    public string? CollectedBy { get; set; }

    [Column("Comment1")]
    public string? Comment1 { get; set; }
}