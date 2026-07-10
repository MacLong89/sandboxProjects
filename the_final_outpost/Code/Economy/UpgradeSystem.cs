namespace FinalOutpost;

public enum UpgradeId
{
	WallArmor,
	TurretPower,
	TurretRange,
	ScrapBonus,
	FortifyCore,
	DefenderTraining
}

public sealed class UpgradeDef
{
	public UpgradeId Id { get; init; }
	public string Key { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; }
	public int MaxLevel { get; init; }
	public double BaseCost { get; init; }
	public double CostGrowth { get; init; }

	public double CostForLevel( int level ) => BaseCost * Math.Pow( CostGrowth, level );
}

public sealed class UpgradeSystem
{
	private readonly SaveData _save;

	public UpgradeSystem( SaveData save ) => _save = save;

	public event Action Changed;

	public static readonly IReadOnlyList<UpgradeDef> All = new List<UpgradeDef>
	{
		new() { Id = UpgradeId.WallArmor, Key = "walls", Name = "Reinforced Walls", Description = "+35 max HP on every wall segment per level (starts at 80 HP).", Icon = "foundation", MaxLevel = 25, BaseCost = 40, CostGrowth = 1.5 },
		new() { Id = UpgradeId.TurretPower, Key = "turret_dmg", Name = "Turret Caliber", Description = "+3 tower damage per level; recruits +0.75 damage per shot per level.", Icon = "adjust", MaxLevel = 30, BaseCost = 55, CostGrowth = 1.55 },
		new() { Id = UpgradeId.TurretRange, Key = "turret_rng", Name = "Turret Optics", Description = "+35 tower range per level; recruits +17 range per level.", Icon = "radar", MaxLevel = 20, BaseCost = 70, CostGrowth = 1.6 },
		new() { Id = UpgradeId.ScrapBonus, Key = "scrap", Name = "Salvage Crew", Description = "+10% scrap earned from zombie kills per level.", Icon = "recycling", MaxLevel = 25, BaseCost = 80, CostGrowth = 1.6 },
		new() { Id = UpgradeId.FortifyCore, Key = "core", Name = "Fortify Command", Description = "+120 command post max HP per level (base 500 HP).", Icon = "shield", MaxLevel = 20, BaseCost = 100, CostGrowth = 1.6 },
		new() { Id = UpgradeId.DefenderTraining, Key = "def_train", Name = "Combat Drills", Description = "Boosts damage from Recruit Train upgrades.", Icon = "fitness_center", MaxLevel = 15, BaseCost = 90, CostGrowth = 1.65 },
	};

	public static UpgradeDef Def( UpgradeId id ) => All.First( u => u.Id == id );

	/// <summary>Plain per-level effect shown in the upgrade menu.</summary>
	public static string PerLevelEffect( UpgradeId id ) => id switch
	{
		UpgradeId.WallArmor => "+35 max HP on each wall segment per level",
		UpgradeId.TurretPower => "+3 tower damage and +0.75 recruit shot damage per level",
		UpgradeId.TurretRange => "+35 tower range and +17 recruit range per level",
		UpgradeId.ScrapBonus => "+10% scrap from zombie kills per level",
		UpgradeId.FortifyCore => "+120 command post max HP per level",
		UpgradeId.DefenderTraining =>
			"+10% per level to damage from Recruit Train (Train button in Recruits menu)",
		_ => ""
	};

	/// <summary>What this upgrade is doing right now at its current level.</summary>
	public string CurrentBonus( UpgradeId id )
	{
		var lvl = Level( id );
		if ( lvl <= 0 ) return "Current bonus: none";

		return id switch
		{
			UpgradeId.WallArmor => $"Current bonus: walls at {WallMaxHp:0} HP",
			UpgradeId.TurretPower =>
				$"Current bonus: +{TurretDamageBonus:0} tower dmg, +{TurretDamageBonus * 0.25f:0.#} recruit dmg",
			UpgradeId.TurretRange =>
				$"Current bonus: +{TurretRangeBonus:0} tower range, +{TurretRangeBonus * 0.5f:0} recruit range",
			UpgradeId.ScrapBonus => $"Current bonus: {ScrapMult * 100f - 100f:0}% more kill scrap",
			UpgradeId.FortifyCore => $"Current bonus: command post at {CoreMaxHp:0} HP",
			UpgradeId.DefenderTraining =>
				$"Current bonus: Recruit Train damage +{DefenderTrainBonus * 100f:0}%",
			_ => ""
		};
	}

	public int Level( UpgradeId id ) => _save.GetUpgrade( Def( id ).Key );

	public double NextCost( UpgradeId id )
	{
		var def = Def( id );
		return Level( id ) >= def.MaxLevel ? double.PositiveInfinity : def.CostForLevel( Level( id ) );
	}

	public bool IsMaxed( UpgradeId id ) => Level( id ) >= Def( id ).MaxLevel;

	public bool TryPurchase( UpgradeId id, PlayerWallet wallet )
	{
		if ( IsMaxed( id ) ) return false;
		if ( !wallet.TrySpend( NextCost( id ) ) ) return false;

		_save.SetUpgrade( Def( id ).Key, Level( id ) + 1 );
		Changed?.Invoke();
		return true;
	}

	public float WallMaxHp => 80f + Level( UpgradeId.WallArmor ) * 35f;
	public float TurretDamageBonus => Level( UpgradeId.TurretPower ) * GameConstants.TurretDamagePerLevel;
	public float TurretRangeBonus => Level( UpgradeId.TurretRange ) * GameConstants.TurretRangePerLevel;
	public double ScrapMult => 1.0 + Level( UpgradeId.ScrapBonus ) * 0.10;
	public float CoreMaxHp => GameConstants.CoreBaseHp + Level( UpgradeId.FortifyCore ) * GameConstants.CoreHpPerFortify;
	public float DefenderTrainBonus => Level( UpgradeId.DefenderTraining ) * 0.1f;
}
