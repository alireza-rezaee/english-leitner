using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.EFDesign.Models;

public class Review
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(WordId))]
    public Word? Word { get; set; }
    public int WordId { get; set; }

    public bool IsRemembered { get; set; }

    public DateTime Time { get; set; }
}