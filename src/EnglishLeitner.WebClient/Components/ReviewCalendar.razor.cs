using EnglishLeitner.WebClient.DTOs;
using Microsoft.AspNetCore.Components;

namespace EnglishLeitner.WebClient.Components;

public partial class ReviewCalendar
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public required DateOnlyRange DateRange { get; set; }

    [Parameter]
    public ICollection<ReviewCalendarItem>? Items { get; set; }

    private IEnumerable<IGrouping<DayOfWeek, DateOnly>> GroupedDateRange
    {
        get
        {
            int startDayOfWeek = (int)DateRange.Start.DayOfWeek;
            DateOnly start = DateRange.Start.AddDays(-startDayOfWeek);

            int endDayOfWeek = (int)DateRange.End.DayOfWeek;
            DateOnly end = DateRange.End.AddDays(6 - startDayOfWeek);

            int daysCount = end.DayNumber - start.DayNumber + 1;
            return Enumerable.Range(0, daysCount)
                .Select(offset => start.AddDays(offset))
                .GroupBy(date => date.DayOfWeek);
        }
    }
}
