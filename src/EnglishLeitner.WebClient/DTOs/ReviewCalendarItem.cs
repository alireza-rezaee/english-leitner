namespace EnglishLeitner.WebClient.DTOs;

public class ReviewCalendarItem
{
    public required DateOnly Date { get; set; }
    public required string Tooltip { get; set; }
    public required string CssClass { get; set; }
}
