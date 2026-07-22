namespace FinalOutpost;

/// <summary>Rare plot boosts — claimed when the player clears the plot.</summary>
public enum PlotBoostKind
{
	None,
	FertileSoil,
	ScrapForge,
	Archive,
	IronRich,
	HealingSpring
}

/// <summary>Boss encounters on plots — clearing triggers an amplified threat wave.</summary>
public enum BossKind
{
	None,
	Giant,
	MutantBeast,
	MilitaryConvoy,
	InfectedHive
}

public sealed class PlotBoostDef
{
	public PlotBoostKind Kind { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Description { get; init; }
	public Color Tint { get; init; }
	public float FoodPerSec { get; init; }
	public float ScrapPerSec { get; init; }
	public float KnowledgePerSec { get; init; }
	public float ForagerMult { get; init; }
	public float SicknessHealPerSec { get; init; }
}

public sealed class BossDef
{
	public BossKind Kind { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Description { get; init; }
	public Color Tint { get; init; }
	public float ThreatMult { get; init; }
}

/// <summary>Deterministic per-plot rolls for Cure world map content.</summary>
public static class PlotWorldRolls
{
	// Lower divisor = higher chance (h % mod == 0). Tuned ~20% denser than original 17/19/31.
	const int BossRollMod = 14;
	const int BoostRollMod = 16;
	const int RivalSeedRollMod = 26;

	public static readonly IReadOnlyList<PlotBoostDef> Boosts = new List<PlotBoostDef>
	{
		new()
		{
			Kind = PlotBoostKind.FertileSoil, Name = "Fertile Soil", Icon = "grass",
			Description = "+0.4 food/s when claimed.",
			Tint = new Color( 0.55f, 0.88f, 0.35f ), FoodPerSec = 0.4f
		},
		new()
		{
			Kind = PlotBoostKind.ScrapForge, Name = "Scrap Forge", Icon = "construction",
			Description = "+0.35 scrap/s when claimed.",
			Tint = new Color( 0.95f, 0.62f, 0.22f ), ScrapPerSec = 0.35f
		},
		new()
		{
			Kind = PlotBoostKind.Archive, Name = "Ancient Archive", Icon = "auto_stories",
			Description = "+0.5 knowledge/s when claimed.",
			Tint = new Color( 0.45f, 0.72f, 0.98f ), KnowledgePerSec = 0.5f
		},
		new()
		{
			Kind = PlotBoostKind.IronRich, Name = "Iron-Rich Vein", Icon = "landscape",
			Description = "+25% forager yield when claimed.",
			Tint = new Color( 0.62f, 0.64f, 0.7f ), ForagerMult = 0.25f
		},
		new()
		{
			Kind = PlotBoostKind.HealingSpring, Name = "Healing Spring", Icon = "spa",
			Description = "Reduces colony sickness when claimed.",
			Tint = new Color( 0.35f, 0.88f, 0.82f ), SicknessHealPerSec = 0.04f
		}
	};

	public static readonly IReadOnlyList<BossDef> Bosses = new List<BossDef>
	{
		new()
		{
			Kind = BossKind.Giant, Name = "Wasteland Giant", Icon = "accessibility_new",
			Description = "A towering mutant — expect a huge wave.",
			Tint = new Color( 0.72f, 0.42f, 0.32f ), ThreatMult = 1.55f
		},
		new()
		{
			Kind = BossKind.MutantBeast, Name = "Mutant Beast", Icon = "pets",
			Description = "Feral pack leader — fast, vicious swarm.",
			Tint = new Color( 0.38f, 0.78f, 0.28f ), ThreatMult = 1.35f
		},
		new()
		{
			Kind = BossKind.MilitaryConvoy, Name = "Military Convoy", Icon = "local_shipping",
			Description = "Infected soldiers and armor — heavy assault.",
			Tint = new Color( 0.55f, 0.58f, 0.62f ), ThreatMult = 1.45f
		},
		new()
		{
			Kind = BossKind.InfectedHive, Name = "Infected Hive", Icon = "coronavirus",
			Description = "Breached nest — largest threat multiplier.",
			Tint = new Color( 0.92f, 0.28f, 0.38f ), ThreatMult = 1.7f
		}
	};

	public static PlotBoostDef GetBoost( PlotBoostKind kind ) =>
		Boosts.FirstOrDefault( b => b.Kind == kind ) ?? Boosts[0];

	public static BossDef GetBoss( BossKind kind ) =>
		Bosses.FirstOrDefault( b => b.Kind == kind ) ?? Bosses[0];

	public static PlotBoostKind BoostAt( int x, int y )
	{
		if ( PlotGrid.IsHome( x, y ) || GameCore.Instance?.IsCure != true ) return PlotBoostKind.None;
		if ( BossAt( x, y ) != BossKind.None ) return PlotBoostKind.None;
		if ( PlotFeatureGrid.KindAt( x, y ) != PlotKind.Standard ) return PlotBoostKind.None;
		if ( RivalCivManager.IsSeedPlot( x, y ) ) return PlotBoostKind.None;

		var h = Hash( x, y );
		if ( h % BoostRollMod != 0 ) return PlotBoostKind.None;
		return Boosts[h % Boosts.Count].Kind;
	}

	public static BossKind BossAt( int x, int y )
	{
		if ( PlotGrid.IsHome( x, y ) || GameCore.Instance?.IsCure != true ) return BossKind.None;
		if ( PlotFeatureGrid.KindAt( x, y ) != PlotKind.Standard ) return BossKind.None;
		if ( RivalCivManager.IsSeedPlot( x, y ) ) return BossKind.None;

		var h = Hash( x, y );
		if ( h % BossRollMod != 0 || PlotGrid.Ring( x, y ) < 1 ) return BossKind.None;
		return Bosses[(h / BossRollMod) % Bosses.Count].Kind;
	}

	public static bool IsRivalSeedCandidate( int x, int y )
	{
		if ( PlotGrid.IsHome( x, y ) || GameCore.Instance?.IsCure != true ) return false;
		if ( PlotGrid.Ring( x, y ) < 3 ) return false;
		if ( BossAt( x, y ) != BossKind.None ) return false;
		if ( PlotFeatureGrid.KindAt( x, y ) != PlotKind.Standard ) return false;
		return Hash( x, y ) % RivalSeedRollMod == 0;
	}

	internal static int Hash( int x, int y )
	{
		unchecked
		{
			var h = (x * 73856093) ^ (y * 19349663) ^ 0x5a7c;
			return h & 0x7fffffff;
		}
	}
}
