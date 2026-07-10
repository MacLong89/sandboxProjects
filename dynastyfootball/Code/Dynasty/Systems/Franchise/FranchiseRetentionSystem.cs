using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Stats;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Systems.Season;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Challenge tracking, dynasty score, owner pressure, building window, and record-chase alerts.
/// </summary>
public sealed class FranchiseRetentionSystem : ILeagueSystem
{
	public string SystemId => "franchise_retention";

	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state )
	{
		state.FranchiseProgress ??= new FranchiseProgressState();
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase is LeaguePhase.RegularSeason or LeaguePhase.Playoffs )
			CheckRecordChases( state );
	}

	public void OnSeasonEnded( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		var progress = EnsureProgress( state );
		var wins = team.Record.Wins;
		var losses = team.Record.Losses;

		progress.DynastyScore += wins * 3 - losses * 1;

		if ( team.Record.PlayoffStatus != PlayoffRound.None )
		{
			progress.PlayoffAppearances++;
			progress.DynastyScore += 25;
		}

		UpdateBuildingWindow( state, team );
		EvaluateChallenge( state, team, progress );
		UpdateOwnerSecurityFromSeason( state, team, progress, wins, losses );
		state.BumpRevision( "franchise_retention_season" );
	}

	public void OnHumanGameResult( LeagueState state, TeamId humanTeamId, bool won )
	{
		var progress = EnsureProgress( state );
		progress.OwnerJobSecurity = Math.Clamp( progress.OwnerJobSecurity + (won ? 2 : -4), 0, 100 );
		CheckOwnerFiring( state, humanTeamId, progress );

		if ( progress.OwnerJobSecurity <= 25 && !progress.ChallengeFailed && !progress.IsFired )
		{
			_inbox?.Add( state, InboxCategory.League, InboxPriority.Urgent,
				"Owner patience is wearing thin",
				"Job security is critical. A winning streak would help.",
				false, humanTeamId, navigateTab: "legacy" );
		}
	}

	public void OnDraftPick( LeagueState state, int overallPick, TeamId teamId, PlayerId prospectId )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, teamId ) )
			return;

		if ( state.Settings.ChallengeMode != ChallengeMode.DraftGenius )
			return;

		if ( !state.Players.TryGetValue( prospectId, out var player ) )
			return;

		var isSteal = overallPick >= 100 && player.Ratings.Overall >= 78
			|| overallPick >= 80 && player.Ratings.Potential >= 88;

		if ( !isSteal )
			return;

		var progress = EnsureProgress( state );
		progress.DraftStealsFound++;

		_inbox?.Add( state, InboxCategory.Draft, InboxPriority.High,
			$"Draft steal: {player.Identity.LastName}",
			$"Pick {overallPick} · {player.Ratings.Overall} OVR / {player.Ratings.Potential} POT — {progress.DraftStealsFound}/3 steals found.",
			false, teamId, navigateTab: "legacy" );

		if ( progress.DraftStealsFound >= 3 && !progress.ChallengeCompleted )
		{
			progress.ChallengeCompleted = true;
			progress.DynastyScore += 100;
			_inbox?.Add( state, InboxCategory.League, InboxPriority.High,
				"Challenge complete: Draft Genius",
				"You found three draft steals. The board is impressed.",
				false, teamId, navigateTab: "legacy" );
		}
	}

	public void OnChampionship( LeagueState state, TeamId championId )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, championId ) )
			return;

		var progress = EnsureProgress( state );
		progress.DynastyScore += 200;
		progress.OwnerJobSecurity = Math.Clamp( progress.OwnerJobSecurity + 25, 0, 100 );

		if ( state.Settings.ChallengeMode == ChallengeMode.WinNow && !progress.ChallengeCompleted )
		{
			progress.ChallengeCompleted = true;
			_inbox?.Add( state, InboxCategory.League, InboxPriority.High,
				"Challenge complete: Win Now",
				"Championship delivered. Your job is safe.",
				false, championId, navigateTab: "legacy" );
		}
	}

	void EvaluateChallenge( LeagueState state, Domain.Teams.TeamState team, FranchiseProgressState progress )
	{
		if ( progress.ChallengeCompleted || progress.ChallengeFailed )
			return;

		if ( state.Settings.ChallengeMode == ChallengeMode.Standard )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		var season = state.CurrentSeason;

		switch ( state.Settings.ChallengeMode )
		{
			case ChallengeMode.Rebuild:
				if ( team.Record.PlayoffStatus != PlayoffRound.None && season <= 3 )
				{
					progress.ChallengeCompleted = true;
					progress.DynastyScore += 75;
					_inbox?.Add( state, InboxCategory.League, InboxPriority.High,
						"Challenge complete: Rebuild",
						"Playoffs achieved within three seasons. Owner celebrates.",
						false, human, navigateTab: "legacy" );
				}
				else if ( season > 3 && progress.PlayoffAppearances == 0 )
				{
					progress.ChallengeFailed = true;
					progress.OwnerJobSecurity = 10;
					CheckOwnerFiring( state, human, progress );
					_inbox?.Add( state, InboxCategory.League, InboxPriority.Urgent,
						"Challenge failed: Rebuild",
						"No playoffs in three seasons. Owner is furious.",
						false, human, navigateTab: "legacy" );
				}

				break;

			case ChallengeMode.WinNow:
				if ( season > 5 && progress.ChallengeCompleted == false
					&& state.History.Championships.All( c => c.ChampionId.Value != human.Value ) )
				{
					progress.ChallengeFailed = true;
					progress.OwnerJobSecurity = 5;
					CheckOwnerFiring( state, human, progress );
					_inbox?.Add( state, InboxCategory.League, InboxPriority.Urgent,
						"Challenge failed: Win Now",
						"No championship in five seasons. You're on the hot seat.",
						false, human, navigateTab: "legacy" );
				}

				break;
		}
	}

	static void UpdateOwnerSecurityFromSeason(
		LeagueState state,
		Domain.Teams.TeamState team,
		FranchiseProgressState progress,
		int wins,
		int losses )
	{
		if ( wins >= 10 )
			progress.OwnerJobSecurity = Math.Clamp( progress.OwnerJobSecurity + 10, 0, 100 );
		else if ( wins <= 4 )
			progress.OwnerJobSecurity = Math.Clamp( progress.OwnerJobSecurity - 12, 0, 100 );
	}

	static void UpdateBuildingWindow( LeagueState state, Domain.Teams.TeamState team )
	{
		var roster = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

		if ( roster.Count == 0 )
			return;

		var avgAge = roster.Average( p => p.Identity.Age );
		var wins = team.Record.Wins;
		var isChamp = state.History.Championships.Any( c =>
			c.Season == state.CurrentSeason && c.ChampionId.Value == team.Id.Value );

		if ( isChamp && wins >= 11 )
			team.BuildingWindow = TeamBuildingWindow.Dynasty;
		else if ( wins >= 10 && avgAge < 28 )
			team.BuildingWindow = TeamBuildingWindow.Contending;
		else if ( wins >= 9 && avgAge >= 28 )
			team.BuildingWindow = TeamBuildingWindow.WinNow;
		else if ( wins <= 5 || avgAge < 26 )
			team.BuildingWindow = TeamBuildingWindow.Rebuilding;
		else if ( avgAge >= 30 && wins < 8 )
			team.BuildingWindow = TeamBuildingWindow.Declining;
	}

	void CheckRecordChases( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var progress = EnsureProgress( state );
		var roster = state.Teams[human].RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

		foreach ( var record in state.History.Records )
		{
			if ( record.Value <= 0 )
				continue;

			var alertKey = $"{state.CurrentSeason}:{record.StatKey}";
			if ( progress.RecordChaseAlertsSent.Contains( alertKey ) )
				continue;

			foreach ( var player in roster )
			{
				var current = player.Career.SeasonStats.GetValueOrDefault( record.StatKey );
				if ( current <= 0 )
					continue;

				var threshold = record.Value * 0.9f;
				if ( current < threshold )
					continue;

				progress.RecordChaseAlertsSent.Add( alertKey );
				_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
					$"Record chase: {player.Identity.LastName}",
					$"{player.Identity.FullName} has {current} {PlayerStatKeys.FormatLabel( record.StatKey )} — within reach of the league mark ({record.Value}).",
					false, human, navigateTab: "legacy" );
				break;
			}
		}
	}

	static FranchiseProgressState EnsureProgress( LeagueState state )
	{
		state.FranchiseProgress ??= new FranchiseProgressState();
		return state.FranchiseProgress;
	}

	void CheckOwnerFiring( LeagueState state, TeamId humanTeamId, FranchiseProgressState progress )
	{
		if ( progress.IsFired )
			return;

		if ( progress.OwnerJobSecurity > 0 && !progress.ChallengeFailed )
			return;

		progress.IsFired = true;
		progress.OwnerJobSecurity = 0;

		_inbox?.Add( state, InboxCategory.League, InboxPriority.Urgent,
			"You've been fired",
			"The owner has relieved you of your duties. Start a new franchise to continue your GM career.",
			true, humanTeamId, navigateTab: "legacy" );
	}

	public static bool IsHumanFired( LeagueState state )
		=> state?.FranchiseProgress?.IsFired == true;

	public void AddDynastyScore( LeagueState state, int amount )
	{
		if ( amount <= 0 )
			return;

		var progress = EnsureProgress( state );
		progress.DynastyScore += amount;
		state.BumpRevision( "dynasty_score" );
	}
}
