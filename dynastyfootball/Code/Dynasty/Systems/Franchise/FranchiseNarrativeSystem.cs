using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Lightweight multi-week narrative pressure for the human GM.
/// </summary>
public sealed class FranchiseNarrativeSystem : ILeagueSystem
{
	public string SystemId => "franchise_narrative";

	private LeagueSystemContext _context;
	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state ) => PushOwnerIntro( state );

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.RegularSeason && state.CurrentSeason == 1 && state.CurrentWeek == 1 )
			PushChallengeBrief( state );
	}

	public void OnWeekAdvanced( LeagueState state ) => EvaluatePressure( state );

	public void OnSeasonEnded( LeagueState state ) { }

	void PushOwnerIntro( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var challenge = state.Settings.ChallengeMode switch
		{
			ChallengeMode.Rebuild => "The owner expects a playoff berth within three seasons. Budget is tight — spend wisely.",
			ChallengeMode.WinNow => "Win-now mandate: a championship within five seasons or you're out.",
			ChallengeMode.DraftGenius => "The board wants three draft steals in your first class. Make them believers.",
			_ => "The owner trusts your vision. Keep the fans happy and the cap clean."
		};

		_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
			"Owner's expectations",
			challenge,
			false, human, navigateTab: "home" );
	}

	void PushChallengeBrief( LeagueState state )
	{
		if ( state.Settings.ChallengeMode == ChallengeMode.Standard )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
			$"Challenge mode: {state.Settings.ChallengeMode}",
			"Your season goals are tied to this challenge. Check Legacy to track franchise milestones.",
			false, human, navigateTab: "legacy" );
	}

	void EvaluatePressure( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		if ( state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Playoffs) )
			return;

		var streak = team.Record.Wins + team.Record.Losses;
		if ( streak < 3 )
			return;

		if ( team.Record.Losses >= 3 && team.Record.Wins == 0 && state.CurrentWeek == 3 )
		{
			_inbox?.Add( state, InboxCategory.Roster, InboxPriority.High,
				"Owner concerned about 0-3 start",
				"Media pressure is mounting. A win soon would calm the front office.",
				false, human, navigateTab: "schedule" );
		}
		else if ( team.Record.Wins >= 3 && team.Record.Losses == 0 && state.CurrentWeek == 3 )
		{
			_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
				"Owner thrilled with undefeated start",
				"Keep the momentum — fan happiness is surging.",
				false, human, navigateTab: "home" );
		}
	}
}
