using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLeitner.EFDesign.Models;

public class Review
{
    [Key]
    public Guid Id { get; set; }

    [ForeignKey(nameof(WordId))]
    public Word? Word { get; set; }
    public int WordId { get; set; }

    public bool IsRemembered { get; set; }

    public DateTime Time { get; set; }

    public bool Equals(Review? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return WordId == other.WordId
            && IsRemembered == other.IsRemembered
            && Time == other.Time;
    }

    public override bool Equals(object? obj)
        => Equals(obj as Review);

    public override int GetHashCode()
        => HashCode.Combine(WordId, IsRemembered, Time);
}