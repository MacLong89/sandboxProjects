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
	public double WaterCost { get; init; }
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
			LabPointsRequired = 15, ScrapCost = 80, WoodCost = 20, StoneCost = 15, WaterCost = 0, SpecimensCost = 0,
			MinSeason = 1
		},
		new()
		{
			Tier = 2, Name = "Synthesis", Description = "Combine samples into viable compounds.",
			LabPointsRequired = 35, ScrapCost = 150, WoodCost = 30, StoneCost = 25, WaterCost = 0, SpecimensCost = 2,
			MinSeason = 3
		},
		new()
		{
			Tier = 3, Name = "Patient Zero", Description = "Isolate the source strain from expedition finds.",
			LabPointsRequired = 60, ScrapCost = 220, WoodCost = 40, StoneCost = 35, WaterCost = 0, SpecimensCost = 5,
			MinSeason = 5
		},
		new()
		{
			Tier = 4, Name = "The Cure", Description = "Finalize and deploy the antidote.",
			LabPointsRequired = 90, ScrapCost = 300, WoodCost = 50, StoneCost = 45, WaterCost = 0, SpecimensCost = 8,
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

		var rate = labs * CureConstants.LabPointsPerSeasonBase / CureConstants.RealSecondsPerDay
			+ labLevelSum * CureConstants.LabPointsPerLevel / CureConstants.RealSecondsPerDay
			+ craftsmen * CureConstants.LabPointsPerScholar / CureConstants.RealSecondsPerDay;

		var sickness = core.Save.ColonySickness / CureConstants.MaxSickness;
		rate *= MathF.Max( 0.35f, 1f - sickness * 0.4f );

		// AUDIT FIX M5: Advanced Synthesis claimed "+25% lab output" but only knowledge paths used it.
		if ( TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) )
			rate *= 1.25;

		return rate * TeamBonuses.ResearchRateMult( core );
	}

	public static void TickLabPoints( GameCore core, float dt )
	{
		if ( core is null || !core.IsCure || dt <= 0 ) return;
		core.Save.CureLabPoints += LabOutputPerSec( core ) * dt;
	}

	public static void ApplySeasonLabBonus( GameCore core )
	{
		if ( core is null || !core.IsCure ) return;

		var labs = BuildManager.Instance?.Buildings.Count( b => !b.IsDestroyed && b.Type == BuildableId.Lab ) ?? 0;
		if ( labs <= 0 ) return;

		// Note (audit): continuous TickLabPoints already accrues toward the "per season base"
		// constant over RealSecondsPerDay*DaysPerSeason. This lump is an extra end-of-season
		// award — intentional or revisit later; synthesis mult kept consistent with LabOutputPerSec.
		var amount = CureConstants.LabPointsPerSeasonBase * labs * TeamBonuses.ResearchRateMult( core );
		if ( TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) )
			amount *= 1.25;
		core.Save.CureLabPoints += amount;
	}

	public static bool CanUnlockTier( GameCore core, int tier )
	{
		if ( core is null || !core.IsCure ) return false;

		var def = GetTier( tier );
		if ( def is null ) return false;
		if ( core.Save.CureResearchTier >= tier ) return false;
		if ( core.Save.CureResearchTier < tier - 1 ) return false;
		if ( !CureObjectives.ObjectivesMetForTier( core.Save, tier ) ) return false;
		if ( core.Save.CureLabPoints < def.LabPointsRequired ) return false;

		var mult = TeamBonuses.ResearchCostMult( core );
		if ( core.Wallet.Scrap < def.ScrapCost * mult ) return false;
		if ( core.Resources.Get( ResourceKind.Wood ) < def.WoodCost * mult ) return false;
		if ( core.Resources.Get( ResourceKind.Stone ) < def.StoneCost * mult ) return false;
		if ( core.Resources.Get( ResourceKind.Specimens ) < def.SpecimensCost * mult ) return false;

		return true;
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

		core.Save.CureResearchTier = tier;
		core.SaveManagerTouch();

		if ( tier >= CureConstants.ResearchTierCount )
			core.OnCureComplete();

		return true;
	}

	public static int LabCount( GameCore core ) =>
		BuildManager.Instance?.Buildings.Count( b => !b.IsDestroyed && b.Type == BuildableId.Lab ) ?? 0;
}
