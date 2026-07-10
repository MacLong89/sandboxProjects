using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;
using Dynasty.Core;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.News;
using Dynasty.Systems.Simulation;

namespace Dynasty.Systems.Season;

public sealed class SeasonSimulationSystem : ILeagueSystem
{
	public string SystemId => "season_simulation";

	private LeagueSystemContext _context;
	private readonly GameSimulationEngine _engine = new();
	private NewsSystem _news;
	private PlayoffBracketSystem _playoffs;
	private Franchise.PlayerMoraleSystem _morale;
	private Franchise.FranchiseRetentionSystem _retention;
	private Franchise.FranchiseMilestoneSystem _milestones;
	private Franchise.WeeklyRecapSystem _weeklyRecap;
	private Franchise.SeasonObjectiveSystem _seasonObjective;
	private Franchise.RivalrySystem _rivalry;
	private Franchise.NearMissSystem _nearMiss;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetNewsSystem( NewsSystem news ) => _news = news;

	public void SetPlayoffBracketSystem( PlayoffBracketSystem playoffs ) => _playoffs = playoffs;

	public void SetPlayerMoraleSystem( Franchise.PlayerMoraleSystem morale ) => _morale = morale;

	public void SetFranchiseRetentionSystem( Franchise.FranchiseRetentionSystem retention ) => _retention = retention;

	public void SetFranchiseMilestoneSystem( Franchise.FranchiseMilestoneSystem milestones ) => _milestones = milestones;

	public void SetWeeklyRecapSystem( Franchise.WeeklyRecapSystem weeklyRecap ) => _weeklyRecap = weeklyRecap;

	public void SetSeasonObjectiveSystem( Franchise.SeasonObjectiveSystem seasonObjective ) => _seasonObjective = seasonObjective;

	public void SetRivalrySystem( Franchise.RivalrySystem rivalry ) => _rivalry = rivalry;

	public void SetNearMissSystem( Franchise.NearMissSystem nearMiss ) => _nearMiss = nearMiss;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase is LeaguePhase.RegularSeason or LeaguePhase.Playoffs or LeaguePhase.Preseason )
			SimulateWeek( state, state.CurrentWeek );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	public void SimulateWeek( LeagueState state, int week )
	{
		var games = state.Schedule.Games
			.Where( g => g.Season == state.CurrentSeason && g.Week == week && !g.IsComplete )
			.Where( g => !g.HomeTeamId.IsEmpty && !g.AwayTeamId.IsEmpty )
			.ToList();

		foreach ( var game in games )
			SimulateGame( state, game );
	}

	public void SimulateGame( LeagueState state, ScheduledGame game )
	{
		var input = new Domain.Simulation.GameSimulationInput
		{
			GameId = game.Id,
			HomeTeamId = game.HomeTeamId,
			AwayTeamId = game.AwayTeamId,
			Home = TeamProfileBuilder.Build( state, game.HomeTeamId ),
			Away = TeamProfileBuilder.Build( state, game.AwayTeamId ),
			IsPlayoff = game.IsPlayoffGame
		};

		var result = _engine.Simulate( input, _context.Random );
		var boxScores = BoxScoreGenerator.Generate( state, game, result.HomeScore, result.AwayScore, _context.Random );
		game.IsComplete = true;
		game.Result = new GameResult
		{
			HomeScore = result.HomeScore,
			AwayScore = result.AwayScore,
			SimulationEvents = result.Events,
			PlayerBoxScores = boxScores,
			SimulatedUtc = _context.Clock.UtcNow
		};

		if ( state.Phase != LeaguePhase.Preseason )
			Stats.PlayerStatsSystem.ApplyBoxScores( state, game );

		UpdateRecords( state, game );
		ApplyPrestigeFromResult( state, game );
		_morale?.ApplyGameResultMorale( state, game.HomeTeamId, game.AwayTeamId, result.HomeScore, result.AwayScore );
		NotifyHumanGameResult( state, game, result.HomeScore, result.AwayScore );
		_news?.PublishGameResult( state, game );
		_playoffs?.HandleGameCompleted( state, game );

		_context.Events.Publish( new GameSimulatedEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			game.Id,
			game.HomeTeamId,
			game.AwayTeamId,
			result.HomeScore,
			result.AwayScore ) );

		state.BumpRevision( "game_simulated" );
	}

	public bool TrySimulateGame( LeagueState state, GameId gameId )
	{
		var game = state.Schedule.Games.FirstOrDefault( g => g.Id.Value == gameId.Value );
		if ( game == null || game.IsComplete )
			return false;

		if ( game.HomeTeamId.IsEmpty || game.AwayTeamId.IsEmpty )
			return false;

		if ( state.Phase is not (LeaguePhase.Preseason or LeaguePhase.RegularSeason or LeaguePhase.Playoffs) )
			return false;

		SimulateGame( state, game );
		return true;
	}

	static void UpdateRecords( LeagueState state, ScheduledGame game )
	{
		if ( state.Phase == LeaguePhase.Preseason )
			return;

		var home = state.Teams[game.HomeTeamId];
		var away = state.Teams[game.AwayTeamId];
		var hs = game.Result.HomeScore;
		var aws = game.Result.AwayScore;

		home.Record.PointsFor += hs;
		home.Record.PointsAgainst += aws;
		away.Record.PointsFor += aws;
		away.Record.PointsAgainst += hs;

		if ( hs > aws )
		{
			home.Record.Wins++;
			away.Record.Losses++;
		}
		else if ( aws > hs )
		{
			away.Record.Wins++;
			home.Record.Losses++;
		}
		else
		{
			home.Record.Ties++;
			away.Record.Ties++;
		}
	}

	static void ApplyPrestigeFromResult( LeagueState state, ScheduledGame game )
	{
		if ( game.Result == null )
			return;

		var hs = game.Result.HomeScore;
		var aws = game.Result.AwayScore;
		if ( hs == aws )
			return;

		var home = state.Teams[game.HomeTeamId];
		var away = state.Teams[game.AwayTeamId];
		var boost = game.IsPlayoffGame ? 3 : 1;

		if ( hs > aws )
			ApplyWinBoost( home, boost );
		else
			ApplyWinBoost( away, boost );
	}

	static void ApplyWinBoost( Domain.Teams.TeamState team, int amount )
	{
		team.Prestige.Prestige = Math.Clamp( team.Prestige.Prestige + amount, 0, 100 );
		team.Prestige.RecentSuccessScore = Math.Min( 100, team.Prestige.RecentSuccessScore + amount * 2 );
	}

	void NotifyHumanGameResult( LeagueState state, ScheduledGame game, int homeScore, int awayScore )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var isHome = game.HomeTeamId.Value == human.Value;
		var isAway = game.AwayTeamId.Value == human.Value;
		if ( !isHome && !isAway )
			return;

		_weeklyRecap?.OnHumanGameCompleted( state, game );

		if ( homeScore == awayScore )
			return;

		var won = isHome ? homeScore > awayScore : awayScore > homeScore;
		FtueHelper.OnHumanGameSimulated( state, won );
		_retention?.OnHumanGameResult( state, human, won );
		_milestones?.OnHumanGameResult( state, game, won );
		_nearMiss?.OnHumanGameResult( state, game, won );
		_rivalry?.OnHumanGameCompleted( state, game );

		if ( won )
			_seasonObjective?.OnHumanWin( state, human );
	}
}
