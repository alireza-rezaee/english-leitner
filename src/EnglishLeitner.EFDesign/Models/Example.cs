using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.EFDesign.Models;

public class Example
{
    [Key]
    public int Id { get; set; }

    public string? Text { get; set; }

    public string? Description { get; set; }

    [ForeignKey(nameof(MeaningItemId))]
    public MeaningItem? MeaningItem { get; set; }
    public int? MeaningItemId { get; set; }
}
