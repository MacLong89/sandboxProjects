namespace FinalOutpost;

public enum CureTeamId
{
	None,
	Engineers,
	Medics,
	Soldiers,
	Scientists,
	Scouts,
	Settlers
}

public sealed class CureTeamDef
{
	public CureTeamId Id { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Description { get; init; }
}

/// <summary>Team-specific multipliers for Road to a Cure runs.</summary>
public static class TeamBonuses
{
	public static CureTeamId Team( GameCore core ) =>
		Enum.TryParse<CureTeamId>( core?.Save?.SelectedTeam, out var t ) ? t : CureTeamId.None;

	public static CureTeamDef Get( CureTeamId id ) =>
		CureTeamCatalog.All.FirstOrDefault( t => t.Id == id ) ?? CureTeamCatalog.All[0];

	public static float RepairSpeedMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Engineers => 1.25f,
		_ => 1f
	};

	public static float BuildCostMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Engineers => 0.85f,
		_ => 1f
	};

	public static float WorkerRepairMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Engineers => 1.2f,
		_ => 1f
	};

	public static float SicknessGainMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Medics => 0.6f,
		_ => 1f
	};

	public static float HealRateMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Medics => 1.3f,
		_ => 1f
	};

	public static float RecruitCostMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Medics => 0.85f,
		_ => 1f
	};

	public static float RecruitDamageMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Soldiers => 1.2f,
		_ => 1f
	};

	public static float RecruitHealthMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Soldiers => 1.25f,
		_ => 1f
	};

	public static float TurretDamageMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Soldiers => 1.1f,
		_ => 1f
	};

	public static float ResearchRateMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Scientists => 1.3f,
		_ => 1f
	};

	public static float ResearchCostMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Scientists => 0.8f,
		_ => 1f
	};

	public static float ExpeditionRewardMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Scouts => 1.25f,
		_ => 1f
	};

	public static float ExpeditionDurationMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Scouts => 0.8f,
		_ => 1f
	};

	public static float ForagerYieldMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Scouts => 1.2f,
		_ => 1f
	};

	public static float PlotClaimCostMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Settlers => 0.75f,
		_ => 1f
	};

	public static float PlotClearSpeedMult( GameCore core ) => Team( core ) switch
	{
		CureTeamId.Settlers => 1.3f,
		_ => 1f
	};

	public static void ApplyStartingBonuses( GameCore core, CureTeamId team )
	{
		if ( core?.Save is null || core.Resources is null ) return;

		core.Resources.Add( ResourceKind.Food, CureConstants.StartingFood );

		switch ( team )
		{
			case CureTeamId.Scientists:
				core.Resources.Add( ResourceKind.Specimens, 1 );
				break;
			case CureTeamId.Settlers:
				core.Resources.Add( ResourceKind.Wood, 25 );
				core.Resources.Add( ResourceKind.Stone, 25 );
				break;
		}
	}
}

public static class CureTeamCatalog
{
	public static readonly IReadOnlyList<CureTeamDef> All = new List<CureTeamDef>
	{
		new() { Id = CureTeamId.Engineers, Name = "Engineers", Icon = "construction", Description = "Faster repairs, cheaper buildings" },
		new() { Id = CureTeamId.Medics, Name = "Medics", Icon = "medical_services", Description = "Less sickness, faster heals, cheaper recruits" },
		new() { Id = CureTeamId.Soldiers, Name = "Soldiers", Icon = "shield", Description = "Stronger recruits and turrets" },
		new() { Id = CureTeamId.Scientists, Name = "Scientists", Icon = "science", Description = "Faster research, cheaper tiers, +1 Specimen" },
		new() { Id = CureTeamId.Scouts, Name = "Scouts", Icon = "explore", Description = "Better expeditions and forager yield" },
		new() { Id = CureTeamId.Settlers, Name = "Settlers", Icon = "home_work", Description = "Cheaper plots, faster clearing, bonus wood & stone" }
	};
}
