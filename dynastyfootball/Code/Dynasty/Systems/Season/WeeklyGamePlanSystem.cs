using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Systems.Franchise;

namespace Dynasty.Systems.Season;

/// <summary>
/// Weekly game plan choice — gives the human GM a meaningful in-season decision each week.
/// </summary>
public sealed class WeeklyGamePlanSystem : ILeagueSystem
{
	public string SystemId => "weekly_game_plan";

	private InboxSystem _inbox;
	private SeasonObjectiveSystem _seasonObjective;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void SetSeasonObjectiveSystem( SeasonObjectiveSystem seasonObjective ) => _seasonObjective = seasonObjective;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.RegularSeason )
			ResetHumanGamePlan( state );
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		ResetHumanGamePlan( state );

		if ( state.Phase != LeaguePhase.RegularSeason )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		if ( team.WeeklyGamePlan != WeeklyGamePlan.None )
			return;

		_inbox?.Add( state, InboxCategory.Roster, InboxPriority.Normal,
			"Set this week's game plan",
			"Choose Run Heavy, Pass Heavy, Defensive Focus, or Balanced before your game for a tactical edge.",
			true, human, navigateTab: "team" );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	public bool TrySetGamePlan( LeagueState state, TeamId teamId, WeeklyGamePlan plan )
	{
		if ( plan == WeeklyGamePlan.None )
			return false;

		var ftuePreseason = state.Phase == LeaguePhase.Preseason && FtueHelper.IsFtueActive( state );
		if ( state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Playoffs) && !ftuePreseason )
			return false;

		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		team.WeeklyGamePlan = plan;
		_seasonObjective?.OnGamePlanSet( state, teamId );
		state.BumpRevision( "game_plan_set" );
		return true;
	}

	static void ResetHumanGamePlan( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		team.WeeklyGamePlan = WeeklyGamePlan.None;
	}

	public static void ApplyGamePlanBonus( Domain.Teams.TeamState team, Domain.Simulation.TeamSimulationProfile profile )
	{
		switch ( team.WeeklyGamePlan )
		{
			case WeeklyGamePlan.RunHeavy:
				profile.OffenseRating = Math.Clamp( profile.OffenseRating + 3, 0, 99 );
				break;
			case WeeklyGamePlan.PassHeavy:
				profile.OffenseRating = Math.Clamp( profile.OffenseRating + 3, 0, 99 );
				profile.DefenseRating = Math.Clamp( profile.DefenseRating - 1, 0, 99 );
				break;
			case WeeklyGamePlan.DefensiveFocus:
				profile.DefenseRating = Math.Clamp( profile.DefenseRating + 4, 0, 99 );
				break;
			case WeeklyGamePlan.Balanced:
				profile.CoachingRating = Math.Clamp( profile.CoachingRating + 2, 0, 99 );
				profile.ChemistryRating = Math.Clamp( profile.ChemistryRating + 2, 0, 99 );
				break;
		}
	}
}
