using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;

namespace Dynasty.Core.Events;

/// <summary>
/// Base contract for all domain events. Events are immutable records consumed by systems, UI, and visualization.
/// </summary>
public interface ILeagueEvent
{
	string EventType { get; }
	long Sequence { get; }
	DateTime TimestampUtc { get; }
}

public abstract record LeagueEventBase( long Sequence, DateTime TimestampUtc ) : ILeagueEvent
{
	public abstract string EventType { get; }
}

public sealed record WeekAdvancedEvent(
	long Sequence,
	DateTime TimestampUtc,
	int Season,
	int Week,
	LeaguePhase Phase
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "week.advanced";
}

public sealed record PhaseChangedEvent(
	long Sequence,
	DateTime TimestampUtc,
	LeaguePhase PreviousPhase,
	LeaguePhase NewPhase
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "phase.changed";
}

public sealed record GameSimulatedEvent(
	long Sequence,
	DateTime TimestampUtc,
	GameId GameId,
	TeamId HomeTeamId,
	TeamId AwayTeamId,
	int HomeScore,
	int AwayScore
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "game.simulated";
}

public sealed record PlayerInjuredEvent(
	long Sequence,
	DateTime TimestampUtc,
	PlayerId PlayerId,
	TeamId TeamId,
	InjurySeverity Severity,
	int WeeksOut
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "player.injured";
}

public sealed record TradeCompletedEvent(
	long Sequence,
	DateTime TimestampUtc,
	Guid TradeId,
	IReadOnlyList<TeamId> InvolvedTeams
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "trade.completed";
}

public sealed record DraftPickMadeEvent(
	long Sequence,
	DateTime TimestampUtc,
	int Season,
	int Round,
	int Pick,
	TeamId TeamId,
	PlayerId ProspectId
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "draft.pick";
}

public sealed record ContractSignedEvent(
	long Sequence,
	DateTime TimestampUtc,
	PlayerId PlayerId,
	TeamId TeamId,
	int Years,
	int Salary
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "contract.signed";
}

public sealed record NewsPublishedEvent(
	long Sequence,
	DateTime TimestampUtc,
	Guid NewsId,
	NewsCategory Category,
	string Headline
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "news.published";
}

public sealed record ChampionshipWonEvent(
	long Sequence,
	DateTime TimestampUtc,
	int Season,
	TeamId ChampionId,
	TeamId RunnerUpId
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "championship.won";
}

public sealed record PlayerRetiredEvent(
	long Sequence,
	DateTime TimestampUtc,
	PlayerId PlayerId,
	TeamId LastTeamId
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "player.retired";
}

public sealed record LeagueStateMutatedEvent(
	long Sequence,
	DateTime TimestampUtc,
	ulong StateRevision,
	string MutationSource
) : LeagueEventBase( Sequence, TimestampUtc )
{
	public override string EventType => "league.state_mutated";
}
