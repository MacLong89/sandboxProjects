using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.News;

namespace Dynasty.Systems.News;

public sealed class NewsSystem : ILeagueSystem
{
	public string SystemId => "news";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context )
	{
		_context = context;
		context.Events.Subscribe<GameSimulatedEvent>( OnGameSimulated );
		context.Events.Subscribe<PlayerInjuredEvent>( OnPlayerInjured );
		context.Events.Subscribe<TradeCompletedEvent>( OnTradeCompleted );
		context.Events.Subscribe<DraftPickMadeEvent>( OnDraftPick );
		context.Events.Subscribe<ChampionshipWonEvent>( OnChampionship );
	}

	public void OnLeagueCreated( LeagueState state )
	{
		Publish( state, NewsCategory.General, "League founded", "A new dynasty begins." );
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		var headline = phase switch
		{
			LeaguePhase.Preseason => "Preseason underway",
			LeaguePhase.RegularSeason => $"Regular season begins — Season {state.CurrentSeason}",
			LeaguePhase.Playoffs => "Playoffs begin",
			LeaguePhase.Offseason => "Offseason begins",
			LeaguePhase.Draft => "NFL Draft is here",
			LeaguePhase.FreeAgency => "Free agency opens",
			_ => phase.ToString()
		};

		Publish( state, NewsCategory.General, headline, $"League enters {phase}." );
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase == LeaguePhase.Preseason )
		{
			Publish( state, NewsCategory.General,
				$"Preseason Week {state.CurrentWeek}",
				"Teams finalize rosters before the regular season." );
		}
	}
	public void OnSeasonEnded( LeagueState state ) { }

	void OnGameSimulated( GameSimulatedEvent e ) { }

	public void PublishGameResult( LeagueState state, Domain.Schedule.ScheduledGame game )
	{
		if ( !state.Teams.TryGetValue( game.HomeTeamId, out var home ) )
			return;

		if ( !state.Teams.TryGetValue( game.AwayTeamId, out var away ) )
			return;

		var hs = game.Result.HomeScore;
		var aws = game.Result.AwayScore;
		var winner = hs > aws ? home.Identity.Abbreviation : aws > hs ? away.Identity.Abbreviation : "Tie";

		Publish( state, NewsCategory.General,
			$"{away.Identity.Abbreviation} {aws}, {home.Identity.Abbreviation} {hs}",
			$"Week {game.Week}: {away.Identity.City} {away.Identity.Name} at {home.Identity.City} {home.Identity.Name}. Winner: {winner}." );
	}

	void OnPlayerInjured( PlayerInjuredEvent e )
	{
		// Headline generated when league context available via service callback
	}

	void OnTradeCompleted( TradeCompletedEvent e ) { }
	void OnDraftPick( DraftPickMadeEvent e ) { }
	void OnChampionship( ChampionshipWonEvent e ) { }

	public void PublishChampionship( LeagueState state, Core.Identifiers.TeamId championId, Core.Identifiers.TeamId runnerUpId )
	{
		if ( !state.Teams.TryGetValue( championId, out var champion ) )
			return;

		if ( !state.Teams.TryGetValue( runnerUpId, out var runnerUp ) )
			return;

		Publish( state, NewsCategory.Championship,
			$"{champion.Identity.City} {champion.Identity.Name} win the championship!",
			$"{champion.Identity.Abbreviation} defeat {runnerUp.Identity.Abbreviation} to claim the title in Season {state.CurrentSeason}." );
	}

	public void Publish( LeagueState state, NewsCategory category, string headline, string body )
	{
		var item = new NewsItem
		{
			Season = state.CurrentSeason,
			Week = state.CurrentWeek,
			Category = category,
			Headline = headline,
			Body = body,
			PublishedUtc = _context.Clock.UtcNow
		};

		state.News.Insert( 0, item );
		if ( state.News.Count > 500 )
			state.News.RemoveAt( state.News.Count - 1 );

		_context.Events.Publish( new NewsPublishedEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			item.Id,
			category,
			headline ) );

		state.BumpRevision( "news" );
	}

	public void PublishInjury( LeagueState state, PlayerInjuredEvent e )
	{
		if ( !state.Players.TryGetValue( e.PlayerId, out var player ) )
			return;

		Publish( state, NewsCategory.Injury,
			$"{player.Identity.FullName} injured ({e.Severity})",
			$"{player.Identity.FullName} expected out {e.WeeksOut} weeks." );
	}
}
