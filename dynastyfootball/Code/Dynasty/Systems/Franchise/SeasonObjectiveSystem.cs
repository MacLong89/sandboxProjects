using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Assigns and tracks one owner mandate per season — gives direction in early weeks.
/// </summary>
public sealed class SeasonObjectiveSystem : ILeagueSystem
{
	public string SystemId => "season_objective";

	private InboxSystem _inbox;
	private FranchiseRetentionSystem _retention;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void SetFranchiseRetentionSystem( FranchiseRetentionSystem retention ) => _retention = retention;

	public void OnLeagueCreated( LeagueState state ) => EnsureObjective( state );

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.RegularSeason && state.CurrentWeek == 1 )
			EnsureObjective( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }

	public void OnSeasonEnded( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var obj = state.FranchiseProgress?.SeasonObjective;
		if ( obj == null || obj.Completed || obj.Failed || obj.AssignedSeason != state.CurrentSeason )
			return;

		if ( obj.ObjectiveId == "wins_7" && state.Teams.TryGetValue( human, out var team ) )
		{
			obj.Progress = team.Record.Wins;
			if ( obj.Progress >= obj.Target )
				CompleteObjective( state, human, obj );
			else
				FailObjective( state, human, obj );
		}
	}

	public void OnHumanWin( LeagueState state, TeamId humanTeamId )
	{
		var obj = state.FranchiseProgress?.SeasonObjective;
		if ( obj == null || obj.Completed || obj.Failed || obj.AssignedSeason != state.CurrentSeason )
			return;

		if ( obj.ObjectiveId is "wins_4_early" or "wins_7" )
		{
			obj.Progress++;
			if ( obj.Progress >= obj.Target )
				CompleteObjective( state, humanTeamId, obj );
		}
	}

	public void OnGamePlanSet( LeagueState state, TeamId teamId )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, teamId ) )
			return;

		var obj = state.FranchiseProgress?.SeasonObjective;
		if ( obj == null || obj.Completed || obj.Failed || obj.ObjectiveId != "game_plans_4" )
			return;

		obj.Progress++;
		if ( obj.Progress >= obj.Target )
			CompleteObjective( state, teamId, obj );
	}

	public void OnFacilityUpgraded( LeagueState state, TeamId teamId, int newLevel )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, teamId ) )
			return;

		var obj = state.FranchiseProgress?.SeasonObjective;
		if ( obj == null || obj.Completed || obj.Failed || obj.ObjectiveId != "facility_3" )
			return;

		if ( newLevel >= obj.Target )
			CompleteObjective( state, teamId, obj );
	}

	void EnsureObjective( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		var progress = state.FranchiseProgress ??= new FranchiseProgressState();
		if ( progress.SeasonObjective != null && progress.SeasonObjective.AssignedSeason == state.CurrentSeason )
			return;

		var (id, title, desc, target) = PickObjective( state, team );
		progress.SeasonObjective = new SeasonObjectiveState
		{
			ObjectiveId = id,
			Title = title,
			Description = desc,
			Target = target,
			Progress = 0,
			AssignedSeason = state.CurrentSeason
		};

		_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
			$"Owner objective: {title}",
			desc,
			false, human, navigateTab: "legacy" );
		state.BumpRevision( "season_objective" );
	}

	static (string id, string title, string desc, int target) PickObjective( LeagueState state, Domain.Teams.TeamState team )
	{
		if ( team.BuildingWindow == TeamBuildingWindow.Rebuilding )
		{
			return ("wins_4_early", "Win 2 of your first 4",
				"The owner wants signs of life — win 2 of your first 4 games.", 2);
		}

		if ( team.BuildingWindow is TeamBuildingWindow.WinNow or TeamBuildingWindow.Contending )
		{
			return ("wins_7", "Seven wins",
				"Playoffs are the minimum bar. Get to 7 wins this season.", 7);
		}

		return ("game_plans_4", "Set four game plans",
			"Show weekly preparation — set your game plan in 4 different weeks.", 4);
	}

	void CompleteObjective( LeagueState state, TeamId teamId, SeasonObjectiveState obj )
	{
		obj.Completed = true;
		obj.Progress = Math.Max( obj.Progress, obj.Target );
		_retention?.AddDynastyScore( state, 40 );
		_inbox?.Add( state, InboxCategory.League, InboxPriority.High,
			$"Objective complete: {obj.Title}",
			"The owner is pleased. Job security improved.",
			false, teamId, navigateTab: "legacy" );

		if ( state.FranchiseProgress != null )
			state.FranchiseProgress.OwnerJobSecurity = Math.Clamp( state.FranchiseProgress.OwnerJobSecurity + 12, 0, 100 );

		state.BumpRevision( "season_objective_complete" );
	}

	void FailObjective( LeagueState state, TeamId teamId, SeasonObjectiveState obj )
	{
		obj.Failed = true;
		_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
			$"Objective missed: {obj.Title}",
			"The owner notes the failure. Deliver next season.",
			false, teamId, navigateTab: "legacy" );

		if ( state.FranchiseProgress != null )
			state.FranchiseProgress.OwnerJobSecurity = Math.Clamp( state.FranchiseProgress.OwnerJobSecurity - 8, 0, 100 );

		state.BumpRevision( "season_objective_failed" );
	}
}
