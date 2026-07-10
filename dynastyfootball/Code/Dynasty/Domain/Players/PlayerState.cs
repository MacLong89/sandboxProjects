using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Contracts;

namespace Dynasty.Domain.Players;

public sealed class PlayerState
{
	public PlayerId Id { get; set; }
	public PlayerIdentity Identity { get; set; } = new();
	public PlayerRatings Ratings { get; set; } = new();
	public List<PlayerTrait> Traits { get; set; } = new();
	public List<PlayerTrait> HiddenTraits { get; set; } = new();
	public bool HiddenTraitsRevealed { get; set; }
	public int RookieSeason { get; set; }
	public ContractState Contract { get; set; } = new();
	public PlayerCareer Career { get; set; } = new();
	public PlayerInjuryState Injury { get; set; } = new();
	public TeamId TeamId { get; set; }
	public bool IsRetired { get; set; }
	public int DevelopmentPoints { get; set; }
	public ScoutingRevealState Scouting { get; set; } = new();
	public PlayerPersonality Personality { get; set; } = new();
	public PlayerMoraleState Morale { get; set; } = new();
}

public sealed class PlayerIdentity
{
	public string FirstName { get; set; } = "";
	public string LastName { get; set; } = "";
	public int Age { get; set; }
	public Position Position { get; set; }
	public string College { get; set; } = "";
	public string Hometown { get; set; } = "";
	public string Backstory { get; set; } = "";

	public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed class PlayerRatings
{
	public int Overall { get; set; }
	public int Potential { get; set; }
	public Dictionary<string, int> Attributes { get; set; } = new();
}

public sealed class PlayerCareer
{
	public Dictionary<string, int> SeasonStats { get; set; } = new();
	public Dictionary<string, int> CareerStats { get; set; } = new();
	public List<PlayerSeasonStatEntry> SeasonHistory { get; set; } = new();
	public List<PlayerGameStatEntry> GameLogs { get; set; } = new();
	public List<string> Awards { get; set; } = new();
	public int ChampionshipRings { get; set; }
	public List<string> HistoryNotes { get; set; } = new();
}

public sealed class PlayerSeasonStatEntry
{
	public int Season { get; set; }
	public Dictionary<string, int> Stats { get; set; } = new();
}

public sealed class PlayerGameStatEntry
{
	public GameId GameId { get; set; }
	public int Season { get; set; }
	public int Week { get; set; }
	public string OpponentAbbreviation { get; set; } = "";
	public string Result { get; set; } = "";
	public Dictionary<string, int> Stats { get; set; } = new();
}

public sealed class PlayerInjuryState
{
	public InjurySeverity Severity { get; set; } = InjurySeverity.None;
	public int WeeksRemaining { get; set; }
	public string Description { get; set; } = "";
}

/// <summary>
/// Tracks what a team has learned about a prospect or player through scouting.
/// </summary>
public sealed class ScoutingRevealState
{
	public bool OverallRevealed { get; set; }
	public bool PotentialRevealed { get; set; }
	public HashSet<string> RevealedAttributes { get; set; } = new();
	public HashSet<PlayerTrait> RevealedTraits { get; set; } = new();
	public int ScoutConfidence { get; set; }
}
