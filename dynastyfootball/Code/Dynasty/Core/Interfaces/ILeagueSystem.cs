using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Domain.League;

namespace Dynasty.Core.Interfaces;

/// <summary>
/// Modular league behavior. Each system owns a slice of domain logic and registers with the league runtime.
/// </summary>
public interface ILeagueSystem
{
	string SystemId { get; }

	void Register( LeagueSystemContext context );

	void OnLeagueCreated( LeagueState state );

	void OnPhaseEntered( LeaguePhase phase, LeagueState state );

	void OnWeekAdvanced( LeagueState state );

	void OnSeasonEnded( LeagueState state );
}

public sealed class LeagueSystemContext
{
	public LeagueEventBus Events { get; init; }
	public ILeagueRandom Random { get; init; }
	public ILeagueClock Clock { get; init; }
	public ILeagueDataDefinitions Definitions { get; init; }
}

public interface ILeagueRandom
{
	int NextInt( int minInclusive, int maxExclusive );
	float NextFloat();
	bool Chance( float probability );
	T Pick<T>( IReadOnlyList<T> items );
}

public interface ILeagueClock
{
	DateTime UtcNow { get; }
}

public interface ILeagueDataDefinitions
{
	IReadOnlyList<TeamDefinition> Teams { get; }
	IReadOnlyDictionary<Position, IReadOnlyList<string>> AttributeKeysByPosition { get; }
	IReadOnlyList<string> FirstNames { get; }
	IReadOnlyList<string> LastNames { get; }
	IReadOnlyList<string> Colleges { get; }
}

public sealed class TeamDefinition
{
	public string Key { get; set; }
	public string City { get; set; }
	public string Name { get; set; }
	public string Abbreviation { get; set; }
	public string PrimaryColor { get; set; }
	public string SecondaryColor { get; set; }
	public string Stadium { get; set; }
}
