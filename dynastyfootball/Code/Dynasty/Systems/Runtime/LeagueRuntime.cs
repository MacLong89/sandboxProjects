using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Calendar;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;
using Dynasty.Systems.Coaching;
using Dynasty.Systems.Draft;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.Season;

namespace Dynasty.Systems.Runtime;

/// <summary>
/// Composes modular systems and drives phase/week/day lifecycle.
/// </summary>
public sealed class LeagueRuntime
{
	private readonly List<ILeagueSystem> _systems = new();
	private LeagueSystemContext _context;
	private FranchiseWorkflowSystem _workflow;
	private CoachingCarouselSystem _coachingCarousel;

	public LeagueEventBus Events => _context.Events;
	public ILeagueRandom Random => _context.Random;

	public void Configure( LeagueSystemContext context, IEnumerable<ILeagueSystem> systems, FranchiseWorkflowSystem workflow = null, CoachingCarouselSystem coachingCarousel = null )
	{
		_context = context;
		_workflow = workflow;
		_coachingCarousel = coachingCarousel;
		_systems.Clear();
		_systems.AddRange( systems );

		context.Events.ClearHandlers();

		foreach ( var system in _systems )
			system.Register( _context );
	}

	public void OnLeagueCreated( LeagueState state )
	{
		foreach ( var system in _systems )
			system.OnLeagueCreated( state );

		LeagueCalendar.Sync( state );
	}

	public void EnterPhase( LeagueState state, LeaguePhase phase )
	{
		var previous = state.Phase;
		state.Phase = phase;
		state.BumpRevision( "phase_enter" );

		_context.Events.Publish( new PhaseChangedEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			previous,
			phase ) );

		foreach ( var system in _systems )
			system.OnPhaseEntered( phase, state );

		if ( phase == LeaguePhase.RegularSeason )
		{
			ResetSeasonRecords( state );
			ResetCalendar( state );
		}

		if ( phase == LeaguePhase.Offseason )
			state.OffseasonSubPhase = OffseasonSubPhase.Retirements;

		LeagueCalendar.Sync( state );
		_workflow?.UpdateSuggestedAction( state );
	}

	public bool AdvanceDay( LeagueState state )
	{
		if ( FranchiseWorkflowSystem.HasBlockingItems( state ) )
			return false;

		var weekRollover = state.Calendar.DayOfWeek >= 7;
		state.Calendar.DayOfWeek = weekRollover ? 1 : state.Calendar.DayOfWeek + 1;

		if ( weekRollover )
		{
			state.Calendar.DayOfMonth = Math.Min( 28, state.Calendar.DayOfMonth + 1 );
			return AdvanceWeek( state, skipDayIncrement: true );
		}

		state.BumpRevision( "day_advanced" );
		_workflow?.UpdateSuggestedAction( state );
		return true;
	}

	public bool AdvanceWeek( LeagueState state ) => AdvanceWeek( state, skipDayIncrement: false );

	bool AdvanceWeek( LeagueState state, bool skipDayIncrement )
	{
		if ( FranchiseWorkflowSystem.HasBlockingItems( state ) )
			return false;

		foreach ( var system in _systems )
			system.OnWeekAdvanced( state );

		DraftSystem.RevealHiddenTraitsForRookies( state );

		state.CurrentWeek++;
		state.BumpRevision( "week_advanced" );

		if ( !skipDayIncrement )
		{
			state.Calendar.DayOfWeek = 1;
			state.Calendar.DayOfMonth = Math.Min( 28, state.Calendar.DayOfMonth + 1 );
		}

		_context.Events.Publish( new WeekAdvancedEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			state.CurrentSeason,
			state.CurrentWeek,
			state.Phase ) );

		ApplyPhaseTransitions( state );
		LeagueCalendar.Sync( state );
		_workflow?.PurgeStaleState( state );
		_workflow?.UpdateSuggestedAction( state );
		return true;
	}

	public void CompleteDraftIfFinished( LeagueState state )
	{
		if ( state.Phase != LeaguePhase.Draft || state.Draft.IsActive )
			return;

		state.CurrentWeek = 1;
		EnterPhase( state, LeaguePhase.Preseason );
	}

	public bool AdvanceToNextEvent( LeagueState state )
	{
		var next = _workflow?.GetNextEvent( state );
		if ( next == null )
			return AdvanceWeek( state );

		_workflow.CompleteNextEvent( state );
		_workflow.PurgeStaleState( state );

		if ( next.Type == FranchiseEventType.GameDay )
			return AdvanceWeek( state );

		state.BumpRevision( "event_processed" );
		_workflow.UpdateSuggestedAction( state );
		return true;
	}

	void ApplyPhaseTransitions( LeagueState state )
	{
		switch ( state.Phase )
		{
			case LeaguePhase.Preseason:
				if ( state.CurrentWeek > FtueHelper.GetEffectivePreseasonWeeks( state ) )
				{
					state.CurrentWeek = 1;
					EnterPhase( state, LeaguePhase.RegularSeason );
				}

				break;

			case LeaguePhase.RegularSeason:
				if ( state.CurrentWeek > state.Settings.RegularSeasonWeeks )
				{
					state.CurrentWeek = 1;
					EnterPhase( state, LeaguePhase.Playoffs );
				}

				break;

			case LeaguePhase.Playoffs:
				if ( state.CurrentWeek > state.Settings.PlayoffWeeks )
					EndSeason( state );

				break;

			case LeaguePhase.Offseason:
				if ( state.CurrentWeek > LeagueCalendar.SubPhaseWeeks( state.OffseasonSubPhase ) )
				{
					var previousSubphase = state.OffseasonSubPhase;
					if ( !AdvanceOffseasonSubPhase( state ) )
					{
						state.CurrentWeek = 1;
						EnterPhase( state, LeaguePhase.FreeAgency );
					}
					else
					{
						state.CurrentWeek = 1;
						if ( previousSubphase != state.OffseasonSubPhase
							&& state.OffseasonSubPhase == OffseasonSubPhase.CoachingChanges )
						{
							_coachingCarousel?.RunCarousel( state );
						}

						_systems.OfType<InboxSystem>().FirstOrDefault()?.OnOffseasonSubphaseEntered( state );
					}
				}

				break;

			case LeaguePhase.FreeAgency:
				if ( state.CurrentWeek > state.Settings.FreeAgencyWeeks )
				{
					state.CurrentWeek = 1;
					EnterPhase( state, LeaguePhase.Draft );
				}

				break;

			case LeaguePhase.Draft:
				if ( !state.Draft.IsActive )
				{
					state.CurrentWeek = 1;
					EnterPhase( state, LeaguePhase.Preseason );
				}

				break;
		}
	}

	static bool AdvanceOffseasonSubPhase( LeagueState state )
	{
		state.OffseasonSubPhase = state.OffseasonSubPhase switch
		{
			OffseasonSubPhase.Retirements => OffseasonSubPhase.CoachingChanges,
			OffseasonSubPhase.CoachingChanges => OffseasonSubPhase.Scouting,
			OffseasonSubPhase.Scouting => OffseasonSubPhase.FacilityUpgrades,
			OffseasonSubPhase.FacilityUpgrades => OffseasonSubPhase.Complete,
			_ => OffseasonSubPhase.Complete
		};

		return state.OffseasonSubPhase != OffseasonSubPhase.Complete;
	}

	public void EndSeason( LeagueState state )
	{
		foreach ( var system in _systems )
			system.OnSeasonEnded( state );

		TeamRecordArchive.ArchiveAndResetAll( state, state.CurrentSeason );
		state.CurrentSeason++;
		state.CurrentWeek = 1;
		ResetCalendar( state );
		EnterPhase( state, LeaguePhase.Offseason );
		state.BumpRevision( "season_ended" );
	}

	static void ResetCalendar( LeagueState state )
	{
		state.Calendar ??= new Domain.Calendar.LeagueCalendarState();
		state.Calendar.DayOfWeek = 1;
		state.Calendar.DayOfMonth = 1;
	}

	static void ResetSeasonRecords( LeagueState state )
	{
		foreach ( var team in state.Teams.Values )
			TeamRecordArchive.ResetCurrent( team );
	}
}
