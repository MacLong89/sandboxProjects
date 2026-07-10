using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.Domain.League;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Formation;

namespace Dynasty.UI.ViewModels;

public sealed class FormationRosterViewModel
{
	public string TeamName { get; init; } = "";
	public int Season { get; init; }
	public int Week { get; init; }
	public FormationSide ActiveSide { get; init; } = FormationSide.Offense;
	public FormationType ActiveFormationType { get; init; } = FormationType.Offense11;
	public string FormationName { get; init; } = "";
	public IReadOnlyList<FormationOptionRow> FormationOptions { get; init; } = Array.Empty<FormationOptionRow>();
	public IReadOnlyList<FormationSlotCardViewModel> Slots { get; init; } = Array.Empty<FormationSlotCardViewModel>();
	public ulong StateRevision { get; init; }

	public int StartersFilled { get; init; }
	public int StartersTotal { get; init; }
	public string CompletionLabel { get; init; } = "";

	public static FormationRosterViewModel From(
		LeagueState state,
		TeamId teamId,
		FormationSide side,
		FormationType? formationOverride = null )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return new FormationRosterViewModel();

		var formationType = formationOverride ?? ( side switch
		{
			FormationSide.Offense => team.ActiveOffenseFormation,
			FormationSide.Defense => team.ActiveDefenseFormation,
			FormationSide.SpecialTeams => FormationType.SpecialTeams,
			_ => team.ActiveOffenseFormation
		} );

		var layout = FormationLayoutRegistry.Get( formationType );
		IReadOnlyList<FormationOptionRow> options = side == FormationSide.SpecialTeams
			? Array.Empty<FormationOptionRow>()
			: FormationLayoutRegistry.GetForSide( side )
				.Select( f => new FormationOptionRow
				{
					Type = f.Type,
					Label = f.DisplayName,
					IsSelected = f.Type == formationType
				} )
				.ToList();

		var slots = layout.Slots
			.Where( slot => !slot.IsOptional || !DepthChart.GetStarter( team.DepthChart, slot.SlotKey ).IsEmpty )
			.Select( slot => FormationSlotCardViewModel.From( state, team, slot ) )
			.ToList();

		var (filled, total) = DepthChartSystem.GetStarterCompletion( state, teamId );

		return new FormationRosterViewModel
		{
			TeamName = $"{team.Identity.City} {team.Identity.Name}",
			Season = state.CurrentSeason,
			Week = state.CurrentWeek,
			ActiveSide = side,
			ActiveFormationType = formationType,
			FormationName = layout.DisplayName,
			FormationOptions = options,
			Slots = slots,
			StateRevision = state.StateRevision,
			StartersFilled = filled,
			StartersTotal = total,
			CompletionLabel = total > 0 ? $"{filled}/{total} starters set" : ""
		};
	}
}

public sealed class FormationOptionRow
{
	public FormationType Type { get; init; }
	public string Label { get; init; } = "";
	public bool IsSelected { get; init; }
}

public sealed class FormationSlotCardViewModel
{
	public string SlotKey { get; init; } = "";
	public string DisplayLabel { get; init; } = "";
	public float NormalizedX { get; init; }
	public float NormalizedY { get; init; }
	public bool IsEmpty { get; init; }
	public PlayerId PlayerId { get; init; }
	public string LastName { get; init; } = "";
	public int Overall { get; init; }
	public int Age { get; init; }
	public string TraitIcon { get; init; } = "";
	public string TraitColor { get; init; } = "";
	public string StatusIndicator { get; init; } = "";
	public string StatusClass { get; init; } = "";

	public static FormationSlotCardViewModel From( LeagueState state, Domain.Teams.TeamState team, FormationSlot slot )
	{
		var playerId = DepthChart.GetStarter( team.DepthChart, slot.SlotKey );
		if ( playerId.IsEmpty || !state.Players.TryGetValue( playerId, out var player ) )
		{
			return new FormationSlotCardViewModel
			{
				SlotKey = slot.SlotKey,
				DisplayLabel = slot.DisplayLabel,
				NormalizedX = slot.NormalizedX,
				NormalizedY = slot.NormalizedY,
				IsEmpty = true
			};
		}

		string traitIcon = "";
		string traitColor = "";
		if ( player.Traits.Count > 0 )
			(traitIcon, traitColor) = TraitVisuals.Get( player.Traits[0] );
		var statusClass = "";
		var status = "";

		if ( player.Injury.Severity != Core.Enums.InjurySeverity.None )
		{
			status = "INJ";
			statusClass = "injured";
		}
		else if ( player.Morale.Morale < 45 )
		{
			status = "LOW";
			statusClass = "morale-low";
		}

		return new FormationSlotCardViewModel
		{
			SlotKey = slot.SlotKey,
			DisplayLabel = slot.DisplayLabel,
			NormalizedX = slot.NormalizedX,
			NormalizedY = slot.NormalizedY,
			IsEmpty = false,
			PlayerId = player.Id,
			LastName = GetLastName( player.Identity.FullName ),
			Overall = player.Ratings.Overall,
			Age = player.Identity.Age,
			TraitIcon = traitIcon,
			TraitColor = traitColor,
			StatusIndicator = status,
			StatusClass = statusClass
		};
	}

	static string GetLastName( string fullName )
	{
		if ( string.IsNullOrWhiteSpace( fullName ) )
			return "—";

		var parts = fullName.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		return parts.Length > 0 ? parts[^1] : fullName;
	}
}

public sealed class DepthChartViewModel
{
	public string TeamName { get; init; } = "";
	public IReadOnlyList<DepthChartSlotRow> OffenseSlots { get; init; } = Array.Empty<DepthChartSlotRow>();
	public IReadOnlyList<DepthChartSlotRow> DefenseSlots { get; init; } = Array.Empty<DepthChartSlotRow>();
	public IReadOnlyList<DepthChartSlotRow> SpecialTeamsSlots { get; init; } = Array.Empty<DepthChartSlotRow>();
	public IReadOnlyList<DepthChartPositionGroupRow> PositionGroups { get; init; } = Array.Empty<DepthChartPositionGroupRow>();

	public static DepthChartViewModel From( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return new DepthChartViewModel();

		var offenseLayout = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		var defenseLayout = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );
		var specialLayout = FormationLayoutRegistry.GetSpecialTeams();

		return new DepthChartViewModel
		{
			TeamName = $"{team.Identity.City} {team.Identity.Name}",
			OffenseSlots = BuildSlotRows( state, team, offenseLayout ),
			DefenseSlots = BuildSlotRows( state, team, defenseLayout ),
			SpecialTeamsSlots = BuildSlotRows( state, team, specialLayout ),
			PositionGroups = BuildPositionGroupRows( state, team )
		};
	}

	static List<DepthChartPositionGroupRow> BuildPositionGroupRows( LeagueState state, Domain.Teams.TeamState team )
	{
		return team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.GroupBy( p => p.Identity.Position )
			.OrderBy( g => g.Key.ToString() )
			.Select( g => new DepthChartPositionGroupRow
			{
				Position = g.Key.ToString(),
				Count = g.Count(),
				Players = g.OrderByDescending( p => p.Ratings.Overall )
					.Select( p => $"{GetLastName( p.Identity.FullName )} ({p.Ratings.Overall})" )
					.ToList()
			} )
			.ToList();
	}

	static List<DepthChartSlotRow> BuildSlotRows( LeagueState state, Domain.Teams.TeamState team, FormationLayout layout )
	{
		return layout.Slots.Select( slot =>
		{
			var depth = DepthChart.GetDepth( team.DepthChart, slot.SlotKey );
			var names = depth
				.Select( id => state.Players.GetValueOrDefault( id ) )
				.Where( p => p != null )
				.Select( p => $"{GetLastName( p.Identity.FullName )} ({p.Ratings.Overall})" )
				.ToList();

			return new DepthChartSlotRow
			{
				SlotKey = slot.SlotKey,
				Label = slot.DisplayLabel,
				StarterName = names.FirstOrDefault() ?? "Empty",
				Backups = names.Skip( 1 ).ToList()
			};
		} ).ToList();
	}

	static string GetLastName( string fullName )
	{
		var parts = fullName.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		return parts.Length > 0 ? parts[^1] : fullName;
	}
}

public sealed class DepthChartSlotRow
{
	public string SlotKey { get; init; } = "";
	public string Label { get; init; } = "";
	public string StarterName { get; init; } = "";
	public IReadOnlyList<string> Backups { get; init; } = Array.Empty<string>();
}

public sealed class DepthChartPositionGroupRow
{
	public string Position { get; init; } = "";
	public int Count { get; init; }
	public IReadOnlyList<string> Players { get; init; } = Array.Empty<string>();
}

public sealed class PlayerPickerViewModel
{
	public string SlotKey { get; init; } = "";
	public string SlotLabel { get; init; } = "";
	public IReadOnlyList<PlayerPickerRow> Candidates { get; init; } = Array.Empty<PlayerPickerRow>();

	public static PlayerPickerViewModel From( LeagueState state, TeamId teamId, string slotKey, string slotLabel )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return new PlayerPickerViewModel { SlotKey = slotKey, SlotLabel = slotLabel };

		var candidates = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.Where( p => DepthChartPositionRules.IsEligible( slotKey, p.Identity.Position ) )
			.OrderByDescending( p => p.Ratings.Overall )
			.ThenBy( p => p.Identity.FullName )
			.Select( p => new PlayerPickerRow
			{
				PlayerId = p.Id,
				Name = p.Identity.FullName,
				Position = p.Identity.Position.ToString(),
				Overall = p.Ratings.Overall,
				Age = p.Identity.Age,
				IsCurrentStarter = DepthChart.GetStarter( team.DepthChart, slotKey ).Value == p.Id.Value
			} )
			.ToList();

		return new PlayerPickerViewModel
		{
			SlotKey = slotKey,
			SlotLabel = slotLabel,
			Candidates = candidates
		};
	}
}

public sealed class PlayerPickerRow
{
	public PlayerId PlayerId { get; init; }
	public string Name { get; init; } = "";
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int Age { get; init; }
	public bool IsCurrentStarter { get; init; }
}

public sealed class TeamContractsViewModel
{
	public string TeamName { get; init; } = "";
	public IReadOnlyList<ContractPlayerRow> Players { get; init; } = Array.Empty<ContractPlayerRow>();

	public static TeamContractsViewModel From( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return new TeamContractsViewModel();

		var players = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.OrderByDescending( p => p.Contract.AnnualSalary )
			.Select( p => new ContractPlayerRow
			{
				PlayerId = p.Id,
				Name = p.Identity.FullName,
				Position = p.Identity.Position.ToString(),
				Overall = p.Ratings.Overall,
				YearsRemaining = p.Contract.YearsRemaining,
				AnnualSalary = p.Contract.AnnualSalary,
				GuaranteedMoney = p.Contract.GuaranteedMoney
			} )
			.ToList();

		return new TeamContractsViewModel
		{
			TeamName = $"{team.Identity.City} {team.Identity.Name}",
			Players = players
		};
	}
}

public sealed class ContractPlayerRow
{
	public PlayerId PlayerId { get; init; }
	public string Name { get; init; } = "";
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int YearsRemaining { get; init; }
	public int AnnualSalary { get; init; }
	public int GuaranteedMoney { get; init; }
}

static class TraitVisuals
{
	public static (string Icon, string Color) Get( Core.Enums.PlayerTrait trait )
	{
		return trait switch
		{
			Core.Enums.PlayerTrait.Clutch => ( "★", "#f5a623" ),
			Core.Enums.PlayerTrait.IronMan => ( "⛨", "#4fc3f7" ),
			Core.Enums.PlayerTrait.Leader => ( "C", "#7cb342" ),
			Core.Enums.PlayerTrait.TeamFriendly => ( "♥", "#e57373" ),
			_ => ( "", "" )
		};
	}
}
