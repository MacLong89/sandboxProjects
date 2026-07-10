using System;

namespace Sandbox;

/// <summary>
/// Server-authored skill trees — upgrade points only; synced for persistence/UI.
/// </summary>
[Title( "Thorns — Player upgrades" )]
[Category( "Thorns" )]
[Icon( "auto_awesome" )]
[Order( 46 )]
public sealed class ThornsPlayerUpgrades : Component
{
	[Sync( SyncFlags.FromHost )] public int UnspentUpgradePoints { get; set; }

	[Sync( SyncFlags.FromHost )] public int HydrationRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int IronGutRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int StrongStomachRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int WeatheredRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int ThickHideRank { get; set; }

	[Sync( SyncFlags.FromHost )] public int EnduranceRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int GhostRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int BeastmasterRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int HardenedRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int LuckyChamberRank { get; set; }

	[Sync( SyncFlags.FromHost )] public int LumberjackRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int MinerRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int ScavengerRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int ReinforcedRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int TechnicianRank { get; set; }

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		RequestVitalsCapRefreshHost();
	}

	public void HostGrantUpgradePointsForLevelsGained( int levelsGained )
	{
		if ( !Networking.IsHost || levelsGained <= 0 )
			return;

		var n = levelsGained * ThornsXpBalance.UpgradePointsPerLevelGained;
		UnspentUpgradePoints += n;
		Log.Info( $"[Thorns] Upgrade points +{n} (Δlevels={levelsGained}) → unspent={UnspentUpgradePoints} pawn='{GameObject.Name}'" );
	}

	public float GetHarvestYieldMultiplier( ThornsResourceKind kind ) =>
		kind is ThornsResourceKind.Wood or ThornsResourceKind.Fiber
			? 1f + LumberjackRank * ThornsUpgradeBalance.HarvestYieldBonusPerLumberjackRank
			: 1f + MinerRank * ThornsUpgradeBalance.HarvestYieldBonusPerMinerRank;

	public int GetEffectiveCraftingTier() =>
		Math.Min(
			ThornsUpgradeBalance.CraftingTierBaseline + TechnicianRank,
			ThornsUpgradeBalance.CraftingTierBaseline + ThornsUpgradeBalance.RankCapTechnician );

	public float GetTamingHealthFractionThreshold()
	{
		var t = ThornsUpgradeBalance.TamingThresholdBaseHpFraction
		        + BeastmasterRank * ThornsUpgradeBalance.TamingThresholdBonusPerRank;
		return Math.Min( ThornsUpgradeBalance.TamingThresholdCapHpFraction, t );
	}

	public float GetGhostWildlifeDetectionRadiusMultiplier()
	{
		var shrink = GhostRank * ThornsUpgradeBalance.GhostDetectionRadiusShrinkPerRank;
		return Math.Max( 0.55f, 1f - Math.Min( shrink, 0.45f ) );
	}

	public float GetReinforcedDurabilityLossMultiplier() =>
		Math.Max( 0.25f, 1f - ReinforcedRank * ThornsUpgradeBalance.ReinforcedDurabilityLossReductionPerRank );

	public float GetLuckyChamberProcChance() =>
		Math.Min( 0.22f, LuckyChamberRank * ThornsUpgradeBalance.LuckyChamberFreeShotChancePerRank );

	public int GetRank( ThornsUpgradeCategory category ) => category switch
	{
		ThornsUpgradeCategory.Hydration => HydrationRank,
		ThornsUpgradeCategory.IronGut => IronGutRank,
		ThornsUpgradeCategory.StrongStomach => StrongStomachRank,
		ThornsUpgradeCategory.Weathered => WeatheredRank,
		ThornsUpgradeCategory.ThickHide => ThickHideRank,
		ThornsUpgradeCategory.Endurance => EnduranceRank,
		ThornsUpgradeCategory.Ghost => GhostRank,
		ThornsUpgradeCategory.Beastmaster => BeastmasterRank,
		ThornsUpgradeCategory.Hardened => HardenedRank,
		ThornsUpgradeCategory.LuckyChamber => LuckyChamberRank,
		ThornsUpgradeCategory.Lumberjack => LumberjackRank,
		ThornsUpgradeCategory.Miner => MinerRank,
		ThornsUpgradeCategory.Scavenger => ScavengerRank,
		ThornsUpgradeCategory.Reinforced => ReinforcedRank,
		ThornsUpgradeCategory.Technician => TechnicianRank,
		_ => 0
	};

	public int GetMaxRank( ThornsUpgradeCategory category ) =>
		ThornsUpgradeBalance.RankCapFor( category );

	public int GetBaseCost( ThornsUpgradeCategory category ) =>
		ThornsUpgradeBalance.DefaultTreeCostBase( category );

	[Rpc.Host]
	public void RpcRequestPurchaseUpgrade( int categoryOrdinal )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
		{
			Log.Warning( "[Thorns] Upgrade purchase rejected: caller does not own pawn" );
			return;
		}

		if ( categoryOrdinal < 0 || categoryOrdinal > (int)ThornsUpgradeCategory.Technician )
		{
			Log.Warning( $"[Thorns] Upgrade purchase rejected: invalid category ordinal={categoryOrdinal}" );
			return;
		}

		HostTryPurchaseUpgrade( (ThornsUpgradeCategory)categoryOrdinal, out _ );
	}

	public bool HostTryPurchaseUpgrade( ThornsUpgradeCategory category, out string rejectReason )
	{
		rejectReason = "";
		if ( !Networking.IsHost )
		{
			rejectReason = "not_host";
			return false;
		}

		var rank = GetRank( category );
		var cap = GetMaxRank( category );
		if ( rank >= cap )
		{
			rejectReason = "max_rank";
			return false;
		}

		var cost = ThornsUpgradeBalance.NextPurchaseUpgradePointCost( GetBaseCost( category ), rank );
		if ( UnspentUpgradePoints < cost )
		{
			rejectReason = "insufficient_points";
			return false;
		}

		UnspentUpgradePoints -= cost;
		SetRankHost( category, rank + 1 );
		Log.Info( $"[Thorns] Skill purchased cat={category} rank→{rank + 1} cost={cost} unspent={UnspentUpgradePoints} pawn='{GameObject.Name}'" );

		RequestVitalsCapRefreshHost();
		RpcPlaySkillUpgradeStinger();
		return true;
	}

	[Rpc.Owner]
	void RpcPlaySkillUpgradeStinger()
	{
		if ( !Game.IsPlaying )
			return;

		ThornsGameplaySfx.PlayAtPawnEar( GameObject, ThornsGameplaySfx.SkillUpgrade );
	}

	void SetRankHost( ThornsUpgradeCategory category, int value )
	{
		switch ( category )
		{
			case ThornsUpgradeCategory.Hydration: HydrationRank = value; break;
			case ThornsUpgradeCategory.IronGut: IronGutRank = value; break;
			case ThornsUpgradeCategory.StrongStomach: StrongStomachRank = value; break;
			case ThornsUpgradeCategory.Weathered: WeatheredRank = value; break;
			case ThornsUpgradeCategory.ThickHide: ThickHideRank = value; break;
			case ThornsUpgradeCategory.Endurance: EnduranceRank = value; break;
			case ThornsUpgradeCategory.Ghost: GhostRank = value; break;
			case ThornsUpgradeCategory.Beastmaster: BeastmasterRank = value; break;
			case ThornsUpgradeCategory.Hardened: HardenedRank = value; break;
			case ThornsUpgradeCategory.LuckyChamber: LuckyChamberRank = value; break;
			case ThornsUpgradeCategory.Lumberjack: LumberjackRank = value; break;
			case ThornsUpgradeCategory.Miner: MinerRank = value; break;
			case ThornsUpgradeCategory.Scavenger: ScavengerRank = value; break;
			case ThornsUpgradeCategory.Reinforced: ReinforcedRank = value; break;
			case ThornsUpgradeCategory.Technician: TechnicianRank = value; break;
		}
	}

	void RequestVitalsCapRefreshHost()
	{
		var v = Components.Get<ThornsVitals>();
		if ( v.IsValid() )
			v.HostRefreshSurvivalCapsFromUpgrades();
	}

	/// <summary>Incoming weapon damage after armor — scales vs wildlife attackers.</summary>
	public static float HostGetDamageMultiplierAfterArmor( ThornsPlayerUpgrades victimUps, DamageContext context )
	{
		if ( victimUps is null || !victimUps.IsValid() || context.AttackerRoot is null || !context.AttackerRoot.IsValid() )
			return 1f;

		var atkRoot = context.AttackerRoot;
		var wildlife = atkRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true ).IsValid();
		if ( wildlife || (context.Kind?.StartsWith( "wildlife", StringComparison.OrdinalIgnoreCase ) ?? false) )
		{
			var r = victimUps.ThickHideRank * ThornsUpgradeBalance.ThickHideWildlifeDamageReductionPerRank;
			return Math.Max( 0.5f, 1f - Math.Min( r, 0.45f ) );
		}

		var bandit = atkRoot.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid();
		if ( bandit || string.Equals( context.Kind, "bandit_hitscan", StringComparison.Ordinal ) )
		{
			var r = victimUps.HardenedRank * ThornsUpgradeBalance.HardenedHumanNpcDamageReductionPerRank;
			return Math.Max( 0.55f, 1f - Math.Min( r, 0.45f ) );
		}

		return 1f;
	}
}
