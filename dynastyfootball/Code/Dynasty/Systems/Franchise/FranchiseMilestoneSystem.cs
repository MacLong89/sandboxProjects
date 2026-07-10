using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;
using Dynasty.Systems.DepthChart;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// One-time franchise milestones — dopamine hits that anchor early sessions and long careers.
/// </summary>
public sealed class FranchiseMilestoneSystem : ILeagueSystem
{
	public string SystemId => "franchise_milestone";

	private InboxSystem _inbox;
	private FranchiseRetentionSystem _retention;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void SetFranchiseRetentionSystem( FranchiseRetentionSystem retention ) => _retention = retention;

	public void OnLeagueCreated( LeagueState state )
	{
		state.FranchiseProgress ??= new FranchiseProgressState();
		state.FranchiseProgress.MilestonesReached ??= new HashSet<string>();
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase != LeaguePhase.Preseason )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var (filled, total) = DepthChartSystem.GetStarterCompletion( state, human );
		if ( total > 0 && filled >= total )
		{
			TryAward( state, human, "depth_chart_set", "Depth chart locked in",
				"Every starter slot is filled. You're ready for kickoff.",
				dynastyBonus: 5 );
		}
	}
	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public void OnHumanGameResult( LeagueState state, ScheduledGame game, bool won )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		if ( state.Phase == LeaguePhase.Preseason )
		{
			if ( won )
			{
				TryAward( state, human, "preseason_first_win", "First preseason W",
					"The dress rehearsal went well. Regular season is next.",
					dynastyBonus: 5 );
			}

			if ( FtueHelper.IsFtueActive( state ) && state.CurrentWeek == 1 )
			{
				TryAward( state, human, "preseason_kickoff", "Training camp complete",
					"You've played your first game. One preseason week is enough — advance to the regular season.",
					dynastyBonus: 5 );
			}

			return;
		}

		if ( won )
		{
			TryAward( state, human, "first_win", "First victory",
				"Your first W as GM. The locker room believes.",
				dynastyBonus: 15 );

			if ( team.Record.Wins >= 3 && team.Record.Losses == 0 )
			{
				TryAward( state, human, "undefeated_3", "Undefeated start",
					"3-0 to open the season. The league is watching.",
					dynastyBonus: 25 );
			}

			if ( CountRecentWins( team ) >= 3 )
			{
				TryAward( state, human, "win_streak_3", "Three-game win streak",
					"Momentum is building across the franchise.",
					dynastyBonus: 20 );
			}
		}
		else if ( team.Record.Wins + team.Record.Losses >= 4 && team.Record.Wins == 0 )
		{
			TryAward( state, human, "survived_0_4", "Survived an 0-4 start",
				"Owner stayed patient. Time to prove them right.",
				dynastyBonus: 10 );
		}

		if ( team.Record.Wins >= 6 )
		{
			TryAward( state, human, "six_wins", "Six wins",
				"Playoff picture is coming into focus.",
				dynastyBonus: 30 );
		}
	}

	public void OnPlayoffClinched( LeagueState state, TeamId teamId )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, teamId ) )
			return;

		TryAward( state, teamId, "playoff_clinched", "Playoffs clinched",
			"Your franchise is playing meaningful football in January.",
			dynastyBonus: 50 );
	}

	public void OnChampionship( LeagueState state, TeamId championId )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, championId ) )
			return;

		TryAward( state, championId, "first_championship", "First championship",
			"A banner forever. You built this.",
			dynastyBonus: 100 );
	}

	void TryAward(
		LeagueState state,
		TeamId teamId,
		string key,
		string subject,
		string body,
		int dynastyBonus )
	{
		var progress = state.FranchiseProgress ??= new FranchiseProgressState();
		progress.MilestonesReached ??= new HashSet<string>();

		if ( !progress.MilestonesReached.Add( key ) )
			return;

		_retention?.AddDynastyScore( state, dynastyBonus );

		_inbox?.Add( state, InboxCategory.League, InboxPriority.High,
			subject, body, false, teamId, navigateTab: "legacy" );
		state.BumpRevision( "milestone" );
	}

	static int CountRecentWins( Domain.Teams.TeamState team )
	{
		var total = team.Record.Wins + team.Record.Losses + team.Record.Ties;
		if ( total < 3 )
			return team.Record.Wins;

		return team.Record.Wins >= 3 ? 3 : team.Record.Wins;
	}
}
