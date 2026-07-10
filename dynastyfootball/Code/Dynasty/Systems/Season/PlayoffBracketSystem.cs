using System.Linq;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Factories;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.History;
using Dynasty.Systems.News;

namespace Dynasty.Systems.Season;

public sealed class PlayoffBracketSystem : ILeagueSystem
{
	public string SystemId => "playoff_bracket";

	private LeagueSystemContext _context;
	private HistorySystem _history;
	private NewsSystem _news;
	private FranchiseRetentionSystem _retention;
	private FranchiseMilestoneSystem _milestones;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetHistorySystem( HistorySystem history ) => _history = history;

	public void SetNewsSystem( NewsSystem news ) => _news = news;

	public void SetFranchiseRetentionSystem( FranchiseRetentionSystem retention ) => _retention = retention;

	public void SetFranchiseMilestoneSystem( FranchiseMilestoneSystem milestones ) => _milestones = milestones;

	public void OnLeagueCreated( LeagueState state ) => EnsureBracket( state );

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.Playoffs )
			EnsureBracket( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }

	public void OnSeasonEnded( LeagueState state ) { }

	public void EnsureBracket( LeagueState state )
	{
		if ( state?.Phase != LeaguePhase.Playoffs )
			return;

		PlayoffBracketGenerator.EnsurePlayoffBracket( state, _context.Random );
		NotifyPlayoffClinched( state );
	}

	public void HandleGameCompleted( LeagueState state, ScheduledGame game )
	{
		if ( !game.IsPlayoffGame || game.Result == null )
			return;

		PlayoffBracketGenerator.AdvanceBracket( state, game );

		if ( !PlayoffBracketGenerator.IsChampionshipGame( game ) )
			return;

		var hs = game.Result.HomeScore;
		var aws = game.Result.AwayScore;
		var winner = hs > aws ? game.HomeTeamId : game.AwayTeamId;
		var loser = PlayoffBracketGenerator.GetChampionshipLoser( game );

		ApplyChampionshipRewards( state, winner, loser );
		_history?.RecordChampionship( state, winner, loser, Math.Max( hs, aws ), Math.Min( hs, aws ) );
		_retention?.OnChampionship( state, winner );
		_milestones?.OnChampionship( state, winner );
		_news?.PublishChampionship( state, winner, loser );
	}

	void NotifyPlayoffClinched( LeagueState state )
	{
		foreach ( var team in state.Teams.Values.Where( t => t.Record.PlayoffStatus != PlayoffRound.None ) )
			_milestones?.OnPlayoffClinched( state, team.Id );
	}

	static void ApplyChampionshipRewards( LeagueState league, TeamId champion, TeamId runnerUp )
	{
		if ( league.Teams.TryGetValue( champion, out var champ ) )
		{
			champ.Prestige.Prestige = Math.Clamp( champ.Prestige.Prestige + 10, 0, 100 );
			champ.Prestige.RecentSuccessScore = Math.Min( 100, champ.Prestige.RecentSuccessScore + 25 );
			champ.Fans.Happiness = Math.Min( 100, champ.Fans.Happiness + 15 );
		}

		if ( league.Teams.TryGetValue( runnerUp, out var runner ) )
		{
			runner.Prestige.Prestige = Math.Clamp( runner.Prestige.Prestige + 4, 0, 100 );
			runner.Prestige.RecentSuccessScore = Math.Min( 100, runner.Prestige.RecentSuccessScore + 10 );
		}
	}
}
