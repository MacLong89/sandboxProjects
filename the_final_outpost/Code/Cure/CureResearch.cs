namespace FinalOutpost;

public sealed class CureResearchTierDef
{
	public int Tier { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public double LabPointsRequired { get; init; }
	public double ScrapCost { get; init; }
	public double WoodCost { get; init; }
	public double StoneCost { get; init; }
	public double SpecimensCost { get; init; }
	public int MinSeason { get; init; }
}

public static class CureResearch
{
	public static readonly IReadOnlyList<CureResearchTierDef> Tiers = new List<CureResearchTierDef>
	{
		new()
		{
			Tier = 1, Name = "Field Lab", Description = "Establish basic research capacity.",
			LabPointsRequired = 15, ScrapCost = 80, WoodCost = 20, StoneCost = 15, SpecimensCost = 0,
			MinSeason = 1
		},
		new()
		{
			Tier = 2, Name = "Synthesis", Description = "Combine samples into viable compounds.",
			LabPointsRequired = 35, ScrapCost = 150, WoodCost = 30, StoneCost = 25, SpecimensCost = 4,
			MinSeason = 3
		},
		new()
		{
			Tier = 3, Name = "Patient Zero", Description = "Isolate the source strain from expedition finds.",
			LabPointsRequired = 60, ScrapCost = 220, WoodCost = 40, StoneCost = 35, SpecimensCost = 10,
			MinSeason = 5
		},
		new()
		{
			Tier = 4, Name = "The Cure", Description = "Finalize and deploy the antidote.",
			LabPointsRequired = 90, ScrapCost = 300, WoodCost = 50, StoneCost = 45, SpecimensCost = 18,
			MinSeason = 7
		}
	};

	public static CureResearchTierDef GetTier( int tier ) =>
		Tiers.FirstOrDefault( t => t.Tier == tier );

	public static double LabOutputPerSec( GameCore core )
	{
		if ( core is null || !core.IsCure ) return 0;

		var labs = BuildManager.Instance?.Buildings.Count( b => !b.IsDestroyed && b.Type == BuildableId.Lab ) ?? 0;
		if ( labs <= 0 ) return 0;

		var craftsmen = WorkerManager.Instance?.Units.Count( u => u.Role is WorkerRole.Craftsman or WorkerRole.Scholar ) ?? 0;
		var labLevelSum = BuildManager.Instance?.Buildings
			.Where( b => !b.IsDestroyed && b.Type == BuildableId.Lab )
			.Sum( b => b.Level ) ?? 0;

		var seasonSeconds = CureConstants.SeasonDurationSeconds;
		var rate = labs * CureConstants.LabPointsPerSeasonBase / seasonSeconds
			+ labLevelSum * CureConstants.LabPointsPerLevel / seasonSeconds
			+ craftsmen * CureConstants.LabPointsPerScholar / seasonSeconds;

		var sickness = core.Save.ColonySickness / CureConstants.MaxSickness;
		rate *= MathF.Max( 0.35f, 1f - sickness * 0.4f );

		var universities = BuildManager.Instance?.Buildings.Count( b => !b.IsDestroyed && b.Type == BuildableId.University ) ?? 0;
		if ( universities > 0 )
			rate *= MathF.Pow( CureConstants.UniversityLabOutputMult, universities );

		if ( TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) )
			rate *= 1.25;

		return rate * TeamBonuses.ResearchRateMult( core );
	}

	public static void TickLabPoints( GameCore core, float dt )
	{
		if ( core is null || !core.IsCure || dt <= 0 ) return;
		core.Save.CureLabPoints += LabOutputPerSec( core ) * dt;
	}

	public static bool CanUnlockTier( GameCore core, int tier )
	{
		if ( core is null || !core.IsCure ) return false;

		var def = GetTier( tier );
		if ( def is null ) return false;
		if ( core.Save.CureResearchTier >= tier ) return false;
		if ( core.Save.CureResearchTier < tier - 1 ) return false;
		if ( CureConstants.ProgressSeason( core.Save ) < def.MinSeason ) return false;
		if ( !CureObjectives.ObjectivesMetForTier( core.Save, tier ) ) return false;
		if ( core.Save.CureLabPoints < def.LabPointsRequired ) return false;

		var mult = TeamBonuses.ResearchCostMult( core );
		if ( core.Wallet.Scrap < def.ScrapCost * mult ) return false;
		if ( core.Resources.Get( ResourceKind.Wood ) < def.WoodCost * mult ) return false;
		if ( core.Resources.Get( ResourceKind.Stone ) < def.StoneCost * mult ) return false;
		if ( core.Resources.Get( ResourceKind.Specimens ) < def.SpecimensCost * mult ) return false;

		return true;
	}

	/// <summary>Why a research tier cannot be unlocked yet — for Cure Progress UI.</summary>
	public static string LockReason( GameCore core, int tier )
	{
		if ( core?.Save is null ) return "Locked";

		var def = GetTier( tier );
		if ( def is null ) return "Locked";
		if ( core.Save.CureResearchTier >= tier ) return "Complete";
		if ( core.Save.CureResearchTier < tier - 1 )
			return $"Unlock tier {tier - 1} first";

		if ( CureConstants.ProgressSeason( core.Save ) < def.MinSeason )
			return $"Reach season {def.MinSeason}";

		if ( !CureObjectives.ObjectivesMetForTier( core.Save, tier ) )
			return "Complete objectives";

		if ( core.Save.CureLabPoints < def.LabPointsRequired )
			return $"Need {def.LabPointsRequired:0} lab pts";

		var mult = TeamBonuses.ResearchCostMult( core );
		if ( core.Wallet.Scrap < def.ScrapCost * mult )
			return $"Need {def.ScrapCost * mult:0} scrap";
		if ( core.Resources.Get( ResourceKind.Wood ) < def.WoodCost * mult )
			return $"Need {def.WoodCost * mult:0} wood";
		if ( core.Resources.Get( ResourceKind.Stone ) < def.StoneCost * mult )
			return $"Need {def.StoneCost * mult:0} stone";
		if ( core.Resources.Get( ResourceKind.Specimens ) < def.SpecimensCost * mult )
			return $"Need {def.SpecimensCost * mult:0} specimens";

		return "Unlock";
	}

	public static bool TryUnlockTier( GameCore core, int tier )
	{
		if ( !CanUnlockTier( core, tier ) ) return false;

		var def = GetTier( tier );
		var mult = TeamBonuses.ResearchCostMult( core );

		core.Wallet.TrySpend( def.ScrapCost * mult );
		core.Resources.TrySpend( ResourceKind.Wood, def.WoodCost * mult );
		core.Resources.TrySpend( ResourceKind.Stone, def.StoneCost * mult );
		core.Resources.TrySpend( ResourceKind.Specimens, def.SpecimensCost * mult );
		// Lab points are cumulative milestones — not spent on unlock.

		core.Save.CureResearchTier = tier;
		core.SaveManagerTouch();

		if ( tier >= CureConstants.ResearchTierCount )
			core.OnCureComplete();

		return true;
	}

	public static int LabCount( GameCore core ) =>
		BuildManager.Instance?.Buildings.Count( b => !b.IsDestroyed && b.Type == BuildableId.Lab ) ?? 0;
}
