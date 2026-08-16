using System.ComponentModel.DataAnnotations;

namespace EnglishLeitner.EFDesign.Models;

public class Word
{
    [Key]
    public int Id { get; set; }

    public string? HeadWord { get; set; }

    public string? Position { get; set; }

    public string? Grammar { get; set; }

    public Cefr? Cefr { get; set; }

    public ICollection<Pronunciation>? Pronunciations { get; set; }

    public string? WebPage { get; set; }
    
    public ICollection<MeaningGroup>? Meanings { get; set; }

    public ICollection<Review>? Reviews { get; set; }
}
