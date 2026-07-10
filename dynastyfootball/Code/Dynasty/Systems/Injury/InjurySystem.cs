using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Injury;

public sealed class InjurySystem : ILeagueSystem
{
	public string SystemId => "injury";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Playoffs) )
			return;

		ProcessRecovery( state );
		RollNewInjuries( state );
	}

	public void OnSeasonEnded( LeagueState state ) => ProcessRecovery( state );

	void ProcessRecovery( LeagueState state )
	{
		foreach ( var player in state.Players.Values.Where( p => p.Injury.Severity != InjurySeverity.None ) )
		{
			player.Injury.WeeksRemaining = Math.Max( 0, player.Injury.WeeksRemaining - 1 );
			if ( player.Injury.WeeksRemaining == 0 )
			{
				player.Injury.Severity = InjurySeverity.None;
				player.Injury.Description = "";
			}
		}
	}

	void RollNewInjuries( LeagueState state )
	{
		foreach ( var player in state.Players.Values.Where( p => !p.IsRetired && p.Injury.Severity == InjurySeverity.None ) )
		{
			var baseChance = 0.04f;
			if ( player.Traits.Contains( PlayerTrait.InjuryProne ) ) baseChance += 0.06f;
			if ( player.Traits.Contains( PlayerTrait.IronMan ) ) baseChance -= 0.03f;

			if ( !_context.Random.Chance( baseChance ) )
				continue;

			var severity = _context.Random.Chance( 0.08f )
				? InjurySeverity.SeasonEnding
				: _context.Random.Chance( 0.4f ) ? InjurySeverity.Out : InjurySeverity.Questionable;

			var weeks = severity switch
			{
				InjurySeverity.Questionable => 1,
				InjurySeverity.Out => _context.Random.NextInt( 2, 6 ),
				InjurySeverity.SeasonEnding => _context.Random.NextInt( 8, 18 ),
				_ => 1
			};

			player.Injury.Severity = severity;
			player.Injury.WeeksRemaining = weeks;
			player.Injury.Description = "Injured during game";

			_context.Events.Publish( new PlayerInjuredEvent(
				_context.Events.NextSequence(),
				_context.Clock.UtcNow,
				player.Id,
				player.TeamId,
				severity,
				weeks ) );
		}

		state.BumpRevision( "injuries" );
	}
}
