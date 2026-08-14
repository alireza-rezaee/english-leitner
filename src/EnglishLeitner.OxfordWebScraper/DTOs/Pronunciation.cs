using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.OxfordWebScraper.DTOs;

public class Pronunciation
{
    [Key]
    public int Id { get; set; }

    public bool IsUK { get; set; }

    public string? Phonetics { get; set; }

    public string? Mp3Url { get; set; }

    public string? OggUrl { get; set; }

    [ForeignKey(nameof(WordId))]
    public Word Word { get; set; } = null!;
    public int WordId { get; set; }
}
