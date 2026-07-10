using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;
using Dynasty.Systems.News;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Division rival assignment and rivalry headlines for early-session drama.
/// </summary>
public sealed class RivalrySystem : ILeagueSystem
{
	public string SystemId => "rivalry";

	private InboxSystem _inbox;
	private NewsSystem _news;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void SetNewsSystem( NewsSystem news ) => _news = news;

	public void OnLeagueCreated( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var progress = FtueHelper.EnsureProgress( state );
		var rival = PickRival( state, human );
		if ( rival.IsEmpty )
			return;

		if ( !state.Teams.TryGetValue( rival, out var rivalTeam ) )
			return;

		progress.RivalTeamId = rival;
		progress.RivalTeamAbbreviation = rivalTeam.Identity.Abbreviation;

		_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
			$"Rival spotlight: {rivalTeam.Identity.Abbreviation}",
			$"{rivalTeam.Identity.City} {rivalTeam.Identity.Name} share your division. Beat them twice and the fanbase will never forget.",
			false, human, navigateTab: "schedule" );

		_news?.Publish( state, NewsCategory.General,
			$"{state.Teams[human].Identity.Abbreviation} vs {rivalTeam.Identity.Abbreviation}: rivalry renewed",
			"The division race starts with bad blood." );
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) => CheckUpcomingRivalGame( state );
	public void OnSeasonEnded( LeagueState state ) { }

	public void OnHumanGameCompleted( LeagueState state, ScheduledGame game )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || game.Result == null )
			return;

		var rivalId = state.FranchiseProgress?.RivalTeamId ?? default;
		if ( rivalId.IsEmpty )
			return;

		var isRivalGame = game.HomeTeamId.Value == rivalId.Value || game.AwayTeamId.Value == rivalId.Value;
		if ( !isRivalGame )
			return;

		var isHome = game.HomeTeamId.Value == human.Value;
		var userScore = isHome ? game.Result.HomeScore : game.Result.AwayScore;
		var oppScore = isHome ? game.Result.AwayScore : game.Result.HomeScore;
		var won = userScore > oppScore;
		var abbr = state.Teams[rivalId].Identity.Abbreviation;

		_news?.Publish( state, NewsCategory.General,
			won ? $"Rivalry win over {abbr}" : $"Rival {abbr} gets the last laugh",
			won
				? $"Your franchise took round one against {abbr} — {userScore}-{oppScore}."
				: $"Division foe {abbr} won the latest chapter — {oppScore}-{userScore}." );
	}

	void CheckUpcomingRivalGame( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		var rivalId = state.FranchiseProgress?.RivalTeamId ?? default;
		if ( human.IsEmpty || rivalId.IsEmpty )
			return;

		if ( state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Preseason) )
			return;

		var upcoming = state.Schedule.Games.FirstOrDefault( g =>
			g.Season == state.CurrentSeason
			&& g.Week == state.CurrentWeek
			&& !g.IsComplete
			&& ( g.HomeTeamId.Value == human.Value || g.AwayTeamId.Value == human.Value )
			&& ( g.HomeTeamId.Value == rivalId.Value || g.AwayTeamId.Value == rivalId.Value ) );

		if ( upcoming == null )
			return;

		var rival = state.Teams[rivalId];
		_inbox?.Add( state, InboxCategory.League, InboxPriority.High,
			$"Rival week: vs {rival.Identity.Abbreviation}",
			"Division bragging rights are on the line. Simulate from Schedule when ready.",
			false, human, navigateTab: "schedule" );
	}

	static TeamId PickRival( LeagueState state, TeamId humanTeamId )
	{
		var teams = state.Teams.Values.OrderBy( t => t.Identity.Abbreviation ).ToList();
		var humanIndex = teams.FindIndex( t => t.Id.Value == humanTeamId.Value );
		if ( humanIndex < 0 )
			return default;

		var groupStart = humanIndex / 4 * 4;
		for ( var i = groupStart; i < groupStart + 4 && i < teams.Count; i++ )
		{
			if ( teams[i].Id.Value == humanTeamId.Value )
				continue;

			return teams[i].Id;
		}

		return teams.FirstOrDefault( t => t.Id.Value != humanTeamId.Value )?.Id ?? default;
	}
}
