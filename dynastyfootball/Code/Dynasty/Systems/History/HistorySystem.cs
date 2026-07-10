using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.History;
using Dynasty.Domain.League;
using Dynasty.Systems.Stats;

namespace Dynasty.Systems.History;

public sealed class HistorySystem : ILeagueSystem
{
	public string SystemId => "history";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context )
	{
		_context = context;
		context.Events.Subscribe<ChampionshipWonEvent>( OnChampionship );
		context.Events.Subscribe<PlayerRetiredEvent>( OnRetired );
	}

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) { }

	public void OnSeasonEnded( LeagueState state )
	{
		PlayerAwardsCalculator.GrantSeasonAwards( state );
		state.BumpRevision( "history_season" );
	}

	void OnChampionship( ChampionshipWonEvent e )
	{
		// Championship record added via service with league reference
	}

	void OnRetired( PlayerRetiredEvent e )
	{
		// Retirement record added via service
	}

	public void RecordChampionship( LeagueState state, TeamId champion, TeamId runnerUp, int champScore, int runnerScore )
	{
		state.History.Championships.Add( new ChampionshipRecord
		{
			Season = state.CurrentSeason,
			ChampionId = champion,
			RunnerUpId = runnerUp,
			ChampionScore = champScore,
			RunnerUpScore = runnerScore
		} );

		if ( state.Teams.TryGetValue( champion, out var championTeam ) )
		{
			foreach ( var playerId in championTeam.RosterPlayerIds )
			{
				if ( state.Players.TryGetValue( playerId, out var player ) && !player.IsRetired )
					player.Career.ChampionshipRings++;
			}
		}

		_context.Events.Publish( new ChampionshipWonEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			state.CurrentSeason,
			champion,
			runnerUp ) );

		state.BumpRevision( "championship" );
	}

	public void RecordRetirement( LeagueState state, Core.Identifiers.PlayerId playerId, Core.Identifiers.TeamId teamId )
	{
		state.History.RetiredPlayers.Add( new RetiredPlayerRecord
		{
			PlayerId = playerId,
			RetirementSeason = state.CurrentSeason,
			FinalTeamId = teamId
		} );

		if ( state.Players.TryGetValue( playerId, out var player ) )
			HallOfFameEvaluator.TryInduct( state, player, state.CurrentSeason );

		_context.Events.Publish( new PlayerRetiredEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			playerId,
			teamId ) );
	}
}
