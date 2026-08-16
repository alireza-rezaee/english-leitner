using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.EFDesign.Models;

public class MeaningItem
{
    [Key]
    public int Id { get; set; }

    public int? Number { get; set; }

    public Cefr? Cefr { get; set; }

    public string? Grammar { get; set; }

    public string? Definition { get; set; }

    public string? Variants { get; set; }

    public string? Usage { get; set; }

    public ICollection<string> Refs { get; set; } = [];

    public ICollection<Example>? Examples { get; set; }

    public ICollection<string> Topics { get; set; } = [];

    [ForeignKey(nameof(MeaningGroupId))]
    public MeaningGroup MeaningGroup { get; set; } = null!;
    public int MeaningGroupId { get; set; }
}
