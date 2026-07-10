using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Positions;
using Dynasty.Core.Stats;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Systems.Roster;

namespace Dynasty.UI.ViewModels;

public sealed class PlayerDetailViewModel
{
	public PlayerId Id { get; init; }
	public string Name { get; init; } = "";
	public int Age { get; init; }
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int Potential { get; init; }
	public string Health { get; init; } = "";
	public string InjuryDetail { get; init; } = "";
	public string Hometown { get; init; } = "";
	public string College { get; init; } = "";
	public string Backstory { get; init; } = "";
	public int Morale { get; init; }
	public bool TradeRequested { get; init; }
	public int DevelopmentPoints { get; init; }
	public int ChampionshipRings { get; init; }
	public int AttributeTrainingCost { get; init; } = RosterManagementSystem.AttributeTrainingCost;
	public int TraitUnlockCost { get; init; } = RosterManagementSystem.TraitUnlockCost;
	public IReadOnlyList<PlayerAttributeRow> Attributes { get; init; } = Array.Empty<PlayerAttributeRow>();
	public IReadOnlyList<string> Traits { get; init; } = Array.Empty<string>();
	public IReadOnlyList<PersonalityRow> Personality { get; init; } = Array.Empty<PersonalityRow>();
	public IReadOnlyList<StatRow> SeasonStats { get; init; } = Array.Empty<StatRow>();
	public IReadOnlyList<StatRow> CareerStats { get; init; } = Array.Empty<StatRow>();
	public IReadOnlyList<PlayerSeasonHistoryRow> SeasonHistory { get; init; } = Array.Empty<PlayerSeasonHistoryRow>();
	public IReadOnlyList<PlayerGameLogRow> GameLogs { get; init; } = Array.Empty<PlayerGameLogRow>();
	public IReadOnlyList<string> Awards { get; init; } = Array.Empty<string>();
	public ContractSummary Contract { get; init; } = new();
	public ReleasePreview ReleasePreview { get; init; } = new();
	public IReadOnlyList<TradePartnerRow> TradePartners { get; init; } = Array.Empty<TradePartnerRow>();

	public static PlayerDetailViewModel From( LeagueState state, TeamId teamId, PlayerId playerId )
	{
		if ( state == null || !state.Players.TryGetValue( playerId, out var player ) )
			return null;

		if ( teamId.IsEmpty || !state.Teams.ContainsKey( teamId ) )
			teamId = player.TeamId;

		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return null;

		var displayPos = RosterPositionHelper.GetDisplayPosition( player, team, state );
		var deadMoney = player.Contract.GuaranteedMoney / 2;
		var statKeys = PlayerStatKeys.ForPosition( player.Identity.Position );

		var partners = state.Teams.Values
			.Where( t => t.Id.Value != teamId.Value )
			.OrderBy( t => t.Identity.City )
			.Select( t => new TradePartnerRow
			{
				TeamId = t.Id,
				Label = $"{t.Identity.City} {t.Identity.Name}",
				Abbreviation = t.Identity.Abbreviation
			} )
			.ToList();

		return new PlayerDetailViewModel
		{
			Id = player.Id,
			Name = player.Identity.FullName,
			Age = player.Identity.Age,
			Position = displayPos,
			Overall = player.Ratings.Overall,
			Potential = player.Ratings.Potential,
			Health = player.Injury.Severity == InjurySeverity.None ? "Healthy" : player.Injury.Severity.ToString(),
			InjuryDetail = player.Injury.Description,
			Hometown = string.IsNullOrEmpty( player.Identity.Hometown ) ? "Unknown" : player.Identity.Hometown,
			College = player.Identity.College,
			Backstory = player.Identity.Backstory,
			Morale = player.Morale.Morale,
			TradeRequested = player.Morale.TradeRequested,
			DevelopmentPoints = player.DevelopmentPoints,
			ChampionshipRings = player.Career.ChampionshipRings,
			Attributes = player.Ratings.Attributes
				.OrderByDescending( kvp => kvp.Value )
				.Select( kvp => new PlayerAttributeRow { Key = FormatAttribute( kvp.Key ), RawKey = kvp.Key, Value = kvp.Value } )
				.ToList(),
			Traits = player.Traits.Select( t => t.ToString() ).ToList(),
			Personality = new[]
			{
				new PersonalityRow { Label = "Ambition", Value = player.Personality.Ambition },
				new PersonalityRow { Label = "Loyalty", Value = player.Personality.Loyalty },
				new PersonalityRow { Label = "Leadership", Value = player.Personality.Leadership },
				new PersonalityRow { Label = "Work Ethic", Value = player.Personality.WorkEthic },
				new PersonalityRow { Label = "Temperament", Value = player.Personality.Temperament },
				new PersonalityRow { Label = "Ego", Value = player.Personality.Ego },
				new PersonalityRow { Label = "Marketability", Value = player.Personality.Marketability }
			},
			SeasonStats = BuildStatRows( player.Career.SeasonStats, statKeys ),
			CareerStats = BuildStatRows( player.Career.CareerStats, statKeys ),
			SeasonHistory = player.Career.SeasonHistory
				.OrderByDescending( s => s.Season )
				.Select( s => new PlayerSeasonHistoryRow
				{
					Season = s.Season,
					Stats = BuildStatRows( s.Stats, statKeys )
				} )
				.ToList(),
			GameLogs = player.Career.GameLogs
				.Select( g => new PlayerGameLogRow
				{
					Season = g.Season,
					Week = g.Week,
					OpponentAbbreviation = g.OpponentAbbreviation,
					Result = g.Result,
					Line = FormatGameLogLine( g, statKeys )
				} )
				.ToList(),
			Awards = player.Career.Awards.OrderByDescending( a => a ).ToList(),
			Contract = new ContractSummary
			{
				YearsRemaining = player.Contract.YearsRemaining,
				AnnualSalary = player.Contract.AnnualSalary,
				GuaranteedMoney = player.Contract.GuaranteedMoney,
				SigningBonus = player.Contract.SigningBonus,
				IsFranchiseTagged = player.Contract.IsFranchiseTagged
			},
			ReleasePreview = new ReleasePreview
			{
				CapSavings = player.Contract.AnnualSalary,
				DeadCapHit = deadMoney
			},
			TradePartners = partners
		};
	}

	static List<StatRow> BuildStatRows( Dictionary<string, int> stats, string[] keys )
	{
		if ( stats == null || stats.Count == 0 )
			return new List<StatRow>();

		return keys
			.Where( k => stats.GetValueOrDefault( k ) != 0 )
			.Select( k => new StatRow { Label = PlayerStatKeys.FormatLabel( k ), Value = stats[k] } )
			.ToList();
	}

	static string FormatGameLogLine( PlayerGameStatEntry entry, string[] keys )
	{
		var parts = keys
			.Where( k => k != PlayerStatKeys.Games && entry.Stats.GetValueOrDefault( k ) != 0 )
			.Take( 4 )
			.Select( k => $"{PlayerStatKeys.FormatLabel( k )} {entry.Stats[k]}" );

		var statLine = string.Join( ", ", parts );
		return string.IsNullOrEmpty( statLine ) ? "—" : statLine;
	}

	static string FormatAttribute( string key )
		=> key.Replace( '_', ' ' );
}

public sealed class PlayerSeasonHistoryRow
{
	public int Season { get; init; }
	public IReadOnlyList<StatRow> Stats { get; init; } = Array.Empty<StatRow>();
}

public sealed class PlayerGameLogRow
{
	public int Season { get; init; }
	public int Week { get; init; }
	public string OpponentAbbreviation { get; init; } = "";
	public string Result { get; init; } = "";
	public string Line { get; init; } = "";
}

public sealed class PlayerAttributeRow
{
	public string Key { get; init; } = "";
	public string RawKey { get; init; } = "";
	public int Value { get; init; }
}

public sealed class PersonalityRow
{
	public string Label { get; init; } = "";
	public int Value { get; init; }
}

public sealed class StatRow
{
	public string Label { get; init; } = "";
	public int Value { get; init; }
}

public sealed class ContractSummary
{
	public int YearsRemaining { get; init; }
	public int AnnualSalary { get; init; }
	public int GuaranteedMoney { get; init; }
	public int SigningBonus { get; init; }
	public bool IsFranchiseTagged { get; init; }
}

public sealed class ReleasePreview
{
	public long CapSavings { get; init; }
	public long DeadCapHit { get; init; }
}

public sealed class TradePartnerRow
{
	public TeamId TeamId { get; init; }
	public string Label { get; init; } = "";
	public string Abbreviation { get; init; } = "";
	public string Record { get; init; } = "";
}
