using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EnglishLeitner.EFDesign.Models;

[Index(nameof(Slug), IsUnique = false)]
public class Word
{
    [Key]
    public int Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string? HeadWord { get; set; }

    public string? Position { get; set; }

    public string? Grammar { get; set; }

    public Cefr? Cefr { get; set; }

    public ICollection<Pronunciation> Pronunciations { get; set; } = [];

    public string? WebPage { get; set; }
    
    public ICollection<MeaningGroup> Meanings { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public DateTime? NextTryUTC { get; set; }

    public int LeitnerLevel { get; set; }
}
