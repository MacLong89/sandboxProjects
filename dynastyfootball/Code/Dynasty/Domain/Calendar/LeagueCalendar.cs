using Dynasty.Core.Enums;
using Dynasty.Domain.League;

namespace Dynasty.Domain.Calendar;

public static class LeagueCalendar
{
	public static LeagueCalendarState Sync( LeagueState state )
	{
		state.Calendar ??= new LeagueCalendarState();
		var cal = state.Calendar;

		cal.Month = ResolveMonth( state );
		cal.Label = FormatLabel( state, cal.Month );
		return cal;
	}

	public static CalendarMonth ResolveMonth( LeagueState state )
	{
		return state.Phase switch
		{
			LeaguePhase.Playoffs when state.CurrentWeek >= state.Settings.PlayoffWeeks => CalendarMonth.February,
			LeaguePhase.Offseason => state.OffseasonSubPhase switch
			{
				OffseasonSubPhase.Retirements => CalendarMonth.February,
				OffseasonSubPhase.CoachingChanges => CalendarMonth.March,
				OffseasonSubPhase.Scouting => CalendarMonth.May,
				_ => CalendarMonth.February
			},
			LeaguePhase.FreeAgency => CalendarMonth.April,
			LeaguePhase.Draft => CalendarMonth.June,
			LeaguePhase.Preseason when state.CurrentWeek <= 2 => CalendarMonth.July,
			LeaguePhase.Preseason => CalendarMonth.August,
			LeaguePhase.RegularSeason => state.CurrentWeek switch
			{
				<= 4 => CalendarMonth.September,
				<= 8 => CalendarMonth.October,
				<= 12 => CalendarMonth.November,
				<= 16 => CalendarMonth.December,
				_ => CalendarMonth.January
			},
			LeaguePhase.Playoffs => CalendarMonth.January,
			_ => CalendarMonth.August
		};
	}

	static string FormatLabel( LeagueState state, CalendarMonth month )
	{
		var monthName = month.ToString();
		return $"{monthName} · Season {state.CurrentSeason} · Week {state.CurrentWeek} · {state.Phase}";
	}

	public static int SubPhaseWeeks( OffseasonSubPhase phase ) => phase switch
	{
		OffseasonSubPhase.Retirements => 1,
		OffseasonSubPhase.CoachingChanges => 2,
		OffseasonSubPhase.Scouting => 2,
		OffseasonSubPhase.FacilityUpgrades => 1,
		_ => 1
	};
}
