using System.ComponentModel.DataAnnotations;

namespace EnglishLeitner.OxfordWebScraper.DTOs;

public class Word
{
    [Key]
    public int Id { get; set; }

    public string? HeadWord { get; set; }

    public string? Position { get; set; }

    public string? Grammar { get; set; }

    public int? Cefr { get; set; }

    public List<Pronunciation>? Pronunciations { get; set; }

    public string? WebPage { get; set; }
    
    public List<MeaningGroup>? Meanings { get; set; }
}
