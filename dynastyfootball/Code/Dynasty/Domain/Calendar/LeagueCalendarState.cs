using Dynasty.Core.Enums;

namespace Dynasty.Domain.Calendar;

public sealed class LeagueCalendarState
{
	public CalendarMonth Month { get; set; } = CalendarMonth.August;
	public int DayOfMonth { get; set; } = 1;
	public int DayOfWeek { get; set; } = 1;
	public string Label { get; set; } = "";
}
