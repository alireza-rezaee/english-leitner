using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.OxfordWebScraper.DTOs;

public class MeaningGroup
{
    [Key]
    public int Id { get; set; }
    
    public string? Head { get; set; }
    
    public List<MeaningItem>? Meanings { get; set; }

    [ForeignKey(nameof(WordId))]
    public Word Word { get; set; } = null!;
    public int WordId { get; set; }
}
