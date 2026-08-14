using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.OxfordWebScraper.DTOs;

public class MeaningItem
{
    [Key]
    public int Id { get; set; }

    public int? Number { get; set; }

    public int? Cefr { get; set; }

    public string? Grammar { get; set; }

    public string? Definition { get; set; }

    public string? Variants { get; set; }

    public string? Usage { get; set; }

    public List<string> Refs { get; set; } = [];

    public List<Example>? Examples { get; set; }

    public List<string> Topics { get; set; } = [];

    [ForeignKey(nameof(MeaningGroupId))]
    public MeaningGroup MeaningGroup { get; set; } = null!;
    public int MeaningGroupId { get; set; }
}
