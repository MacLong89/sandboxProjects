using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class FacilitiesViewModel
{
	public string TeamName { get; init; } = "";
	public long Budget { get; init; }
	public IReadOnlyList<FacilityRowViewModel> Facilities { get; init; } = Array.Empty<FacilityRowViewModel>();

	public static FacilitiesViewModel From( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return new FacilitiesViewModel();

		var rows = Enum.GetValues<FacilityType>()
			.Select( type => FacilityRowViewModel.From( team, type ) )
			.ToList();

		return new FacilitiesViewModel
		{
			TeamName = $"{team.Identity.City} {team.Identity.Name}",
			Budget = team.Finances.Budget,
			Facilities = rows
		};
	}
}

public sealed class FacilityRowViewModel
{
	public FacilityType Type { get; init; }
	public string Label { get; init; } = "";
	public string Benefit { get; init; } = "";
	public int Level { get; init; }
	public int MaxLevel { get; init; } = 10;
	public long UpgradeCost { get; init; }
	public bool CanAfford { get; init; }
	public bool IsMaxed { get; init; }

	public static FacilityRowViewModel From( Domain.Teams.TeamState team, FacilityType type )
	{
		var level = team.Facilities.Levels.GetValueOrDefault( type, 1 );
		var cost = (level + 1) * 5_000_000L;
		return new FacilityRowViewModel
		{
			Type = type,
			Label = FormatLabel( type ),
			Benefit = FormatBenefit( type ),
			Level = level,
			UpgradeCost = cost,
			CanAfford = team.Finances.Budget >= cost,
			IsMaxed = level >= 10
		};
	}

	static string FormatLabel( FacilityType type ) => type switch
	{
		FacilityType.Stadium => "Stadium",
		FacilityType.TrainingFacility => "Training Facility",
		FacilityType.MedicalCenter => "Medical Center",
		FacilityType.ScoutingDepartment => "Scouting Department",
		FacilityType.FanAmenities => "Fan Amenities",
		_ => type.ToString()
	};

	static string FormatBenefit( FacilityType type ) => type switch
	{
		FacilityType.Stadium => "Fan attendance & popularity",
		FacilityType.TrainingFacility => "Player development speed",
		FacilityType.MedicalCenter => "Injury recovery (future)",
		FacilityType.ScoutingDepartment => "Draft scouting accuracy",
		FacilityType.FanAmenities => "Fan happiness between games",
		_ => ""
	};
}
