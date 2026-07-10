using Dynasty.Core.Identifiers;
using Dynasty.Systems.Roster;

namespace Dynasty.UI.ViewModels;

public enum PlayerProfileKind
{
	Roster,
	Prospect
}

public sealed class PlayerProfileViewModel
{
	public PlayerProfileKind Kind { get; init; }
	public PlayerId Id { get; init; }
	public TeamId TeamId { get; init; }
	public string Name { get; init; } = "";
	public string Position { get; init; } = "";
	public string Age { get; init; } = "";
	public string Overall { get; init; } = "";
	public string Potential { get; init; } = "";
	public string Health { get; init; } = "";
	public string InjuryDetail { get; init; } = "";
	public string Hometown { get; init; } = "";
	public string College { get; init; } = "";
	public string Backstory { get; init; } = "";
	public string Morale { get; init; } = "";
	public bool TradeRequested { get; init; }
	public int ChampionshipRings { get; init; }
	public int? ConsensusRank { get; init; }
	public int? ScoutConfidence { get; init; }
	public bool HasHiddenTraits { get; init; }
	public string HiddenTraitHint { get; init; } = "";
	public string PersonalityNote { get; init; } = "";
	public IReadOnlyList<PersonalityRow> Personality { get; init; } = Array.Empty<PersonalityRow>();
	public IReadOnlyList<string> Awards { get; init; } = Array.Empty<string>();
	public ContractSummary Contract { get; init; } = new();
	public IReadOnlyList<PlayerProfileAttributeRow> Attributes { get; init; } = Array.Empty<PlayerProfileAttributeRow>();
	public IReadOnlyList<string> Traits { get; init; } = Array.Empty<string>();
	public int DevelopmentPoints { get; init; }
	public int AttributeTrainingCost { get; init; } = RosterManagementSystem.AttributeTrainingCost;
	public int TraitUnlockCost { get; init; } = RosterManagementSystem.TraitUnlockCost;
	public int NumericPotential { get; init; }
	public IReadOnlyList<StatRow> SeasonStats { get; init; } = Array.Empty<StatRow>();
	public IReadOnlyList<StatRow> CareerStats { get; init; } = Array.Empty<StatRow>();
	public IReadOnlyList<PlayerSeasonHistoryRow> SeasonHistory { get; init; } = Array.Empty<PlayerSeasonHistoryRow>();
	public IReadOnlyList<PlayerGameLogRow> GameLogs { get; init; } = Array.Empty<PlayerGameLogRow>();
	public bool ShowContractActions { get; init; }
	public bool ShowDraftAction { get; init; }
	public bool IsUserTeam { get; init; }

	public static PlayerProfileViewModel FromRoster( PlayerDetailViewModel detail, TeamId teamId, bool isUserTeam )
	{
		if ( detail == null )
			return null;

		return new PlayerProfileViewModel
		{
			Kind = PlayerProfileKind.Roster,
			Id = detail.Id,
			TeamId = teamId,
			Name = detail.Name,
			Position = detail.Position,
			Age = detail.Age.ToString(),
			Overall = detail.Overall.ToString(),
			Potential = detail.Potential.ToString(),
			NumericPotential = detail.Potential,
			Health = detail.Health,
			InjuryDetail = detail.InjuryDetail,
			Hometown = detail.Hometown,
			College = detail.College,
			Backstory = detail.Backstory,
			Morale = detail.Morale.ToString(),
			TradeRequested = detail.TradeRequested,
			ChampionshipRings = detail.ChampionshipRings,
			Personality = detail.Personality,
			Awards = detail.Awards,
			Contract = detail.Contract,
			Attributes = detail.Attributes
				.Select( a => new PlayerProfileAttributeRow
				{
					Key = a.Key,
					RawKey = a.RawKey,
					DisplayValue = a.Value.ToString(),
					NumericValue = a.Value
				} )
				.ToList(),
			Traits = detail.Traits,
			DevelopmentPoints = detail.DevelopmentPoints,
			AttributeTrainingCost = detail.AttributeTrainingCost,
			TraitUnlockCost = detail.TraitUnlockCost,
			SeasonStats = detail.SeasonStats,
			CareerStats = detail.CareerStats,
			SeasonHistory = detail.SeasonHistory,
			GameLogs = detail.GameLogs,
			ShowContractActions = isUserTeam,
			IsUserTeam = isUserTeam
		};
	}

	public static PlayerProfileViewModel FromProspect( ProspectDetailViewModel detail, bool showDraftAction )
	{
		if ( detail == null )
			return null;

		int.TryParse( detail.Potential, out var numericPotential );

		return new PlayerProfileViewModel
		{
			Kind = PlayerProfileKind.Prospect,
			Id = detail.Id,
			Name = detail.Name,
			Position = detail.Position,
			Age = detail.Age.ToString(),
			Overall = detail.Overall,
			Potential = detail.Potential,
			NumericPotential = numericPotential,
			Health = detail.Health,
			Hometown = detail.Hometown,
			College = detail.College,
			Backstory = detail.Backstory,
			Morale = detail.Morale,
			ConsensusRank = detail.ConsensusRank,
			ScoutConfidence = detail.ScoutConfidence,
			HasHiddenTraits = detail.HasHiddenTraits,
			HiddenTraitHint = detail.HiddenTraitHint,
			Personality = detail.Personality,
			PersonalityNote = detail.PersonalityNote,
			Attributes = detail.Attributes
				.Select( a => new PlayerProfileAttributeRow
				{
					Key = a.Key,
					RawKey = a.RawKey,
					DisplayValue = a.Value,
					IsHidden = a.Value == "??"
				} )
				.ToList(),
			Traits = detail.Traits,
			ShowDraftAction = showDraftAction
		};
	}
}

public sealed class PlayerProfileAttributeRow
{
	public string Key { get; init; } = "";
	public string RawKey { get; init; } = "";
	public string DisplayValue { get; init; } = "";
	public int NumericValue { get; init; }
	public bool IsHidden { get; init; }
}
