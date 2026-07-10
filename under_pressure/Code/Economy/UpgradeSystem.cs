namespace UnderPressure;

public enum UpgradeId
{
	// Append new values at the end only. Inserting mid-enum shifts every later value's
	// integer, which breaks s&box hot-load reconciliation of any live enum-by-int state.
	// Display order is driven by the All list below, not by this enum.
	Pressure,
	Nozzle,
	CashMultiplier,
	MoveSpeed,
	Tank,
	AutoHelper,
	Van,
	Range,
	BrushPower,
	BrushWidth,
	SqueegeePower,
	SqueegeeWidth,
	Stamina,
	BrushReach,
	SqueegeeReach
}

/// <summary>Which tool / area of the business an upgrade improves. Drives the grouped UI so
/// it's obvious at a glance what each upgrade actually affects.</summary>
public enum UpgradeGroup
{
	PressureWasher,
	ScrubBrush,
	Squeegee,
	Endurance,
	Business,
}

/// <summary>Static definition of a purchasable upgrade line.</summary>
public sealed class UpgradeDef
{
	public UpgradeId Id { get; init; }
	public string Key { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; }
	public UpgradeGroup Group { get; init; }
	public int MaxLevel { get; init; }
	public double BaseCost { get; init; }
	public double CostGrowth { get; init; }

	public double CostForLevel( int level ) => BaseCost * Math.Pow( CostGrowth, level );
}

/// <summary>Owns upgrade levels and exposes the derived gameplay modifiers they grant.</summary>
public sealed class UpgradeSystem
{
	private readonly SaveData _save;

	public UpgradeSystem( SaveData save ) => _save = save;

	public event Action Changed;

	public static readonly IReadOnlyList<UpgradeDef> All = new List<UpgradeDef>
	{
		new() { Id = UpgradeId.Pressure, Key = "pressure", Name = "Pressure (PSI)", Description = "Blast dirt away faster.", Icon = "bolt", Group = UpgradeGroup.PressureWasher, MaxLevel = 25, BaseCost = 40, CostGrowth = 1.62 },
		new() { Id = UpgradeId.Nozzle, Key = "nozzle", Name = "Nozzle Width", Description = "Wider spray covers more area.", Icon = "grain", Group = UpgradeGroup.PressureWasher, MaxLevel = 15, BaseCost = 90, CostGrowth = 1.82 },
		new() { Id = UpgradeId.Range, Key = "range", Name = "Hose Reach", Description = "Clean surfaces from further away.", Icon = "open_in_full", Group = UpgradeGroup.PressureWasher, MaxLevel = 12, BaseCost = 90, CostGrowth = 1.6 },
		new() { Id = UpgradeId.Tank, Key = "tank", Name = "Water Tank", Description = "Spray longer before it refills.", Icon = "water_drop", Group = UpgradeGroup.PressureWasher, MaxLevel = 12, BaseCost = 120, CostGrowth = 1.7 },
		new() { Id = UpgradeId.BrushPower, Key = "brush_power", Name = "Bristle Stiffness", Description = "Scrub brush strips caked-on moss faster.", Icon = "cleaning_services", Group = UpgradeGroup.ScrubBrush, MaxLevel = 15, BaseCost = 70, CostGrowth = 1.6 },
		new() { Id = UpgradeId.BrushWidth, Key = "brush_width", Name = "Brush Size", Description = "A wider brush head scrubs more at once.", Icon = "aspect_ratio", Group = UpgradeGroup.ScrubBrush, MaxLevel = 12, BaseCost = 90, CostGrowth = 1.65 },
		new() { Id = UpgradeId.BrushReach, Key = "brush_reach", Name = "Extension Pole", Description = "Reach higher, farther spots with the brush.", Icon = "open_in_full", Group = UpgradeGroup.ScrubBrush, MaxLevel = 10, BaseCost = 85, CostGrowth = 1.6 },
		new() { Id = UpgradeId.SqueegeePower, Key = "sq_power", Name = "Blade Edge", Description = "A sharper squeegee wipes glass faster.", Icon = "wash", Group = UpgradeGroup.Squeegee, MaxLevel = 15, BaseCost = 80, CostGrowth = 1.6 },
		new() { Id = UpgradeId.SqueegeeWidth, Key = "sq_width", Name = "Blade Width", Description = "A wider squeegee clears more glass per stroke.", Icon = "crop_landscape", Group = UpgradeGroup.Squeegee, MaxLevel = 12, BaseCost = 100, CostGrowth = 1.65 },
		new() { Id = UpgradeId.SqueegeeReach, Key = "sq_reach", Name = "Extension Pole", Description = "Reach higher, farther windows with the squeegee.", Icon = "open_in_full", Group = UpgradeGroup.Squeegee, MaxLevel = 10, BaseCost = 110, CostGrowth = 1.6 },
		new() { Id = UpgradeId.Stamina, Key = "stamina", Name = "Energy Drinks", Description = "Bigger stamina bar — scrub & squeegee longer before resting.", Icon = "local_drink", Group = UpgradeGroup.Endurance, MaxLevel = 15, BaseCost = 80, CostGrowth = 1.6 },
		new() { Id = UpgradeId.CashMultiplier, Key = "cash", Name = "Contract Value", Description = "Earn more for every spot cleaned.", Icon = "payments", Group = UpgradeGroup.Business, MaxLevel = 30, BaseCost = 100, CostGrowth = 1.6 },
		new() { Id = UpgradeId.MoveSpeed, Key = "speed", Name = "Boot Speed", Description = "Move between spots faster.", Icon = "directions_run", Group = UpgradeGroup.Business, MaxLevel = 12, BaseCost = 80, CostGrowth = 1.65 },
		new() { Id = UpgradeId.AutoHelper, Key = "helper", Name = "Hire Helper", Description = "A crew member earns cash passively while you work.", Icon = "engineering", Group = UpgradeGroup.Business, MaxLevel = 20, BaseCost = 10000, CostGrowth = 1.9 },
		new() { Id = UpgradeId.Van, Key = "van", Name = "Van Class", Description = "A better rig lands bigger contracts. +50% earnings per tier.", Icon = "local_shipping", Group = UpgradeGroup.Business, MaxLevel = 4, BaseCost = 1200, CostGrowth = 3.2 },
	};

	/// <summary>Display order for the grouped Upgrades tab.</summary>
	public static readonly IReadOnlyList<UpgradeGroup> GroupOrder = new[]
	{
		UpgradeGroup.Business,
		UpgradeGroup.PressureWasher,
		UpgradeGroup.ScrubBrush,
		UpgradeGroup.Squeegee,
		UpgradeGroup.Endurance,
	};

	/// <summary>Heading label + icon for an upgrade group (tells the player which tool it's for).</summary>
	public static (string Name, string Icon) GroupInfo( UpgradeGroup group ) => group switch
	{
		UpgradeGroup.PressureWasher => ("Pressure Washer", "shower"),
		UpgradeGroup.ScrubBrush => ("Scrub Brush", "cleaning_services"),
		UpgradeGroup.Squeegee => ("Squeegee", "wash"),
		UpgradeGroup.Endurance => ("Stamina (hand tools)", "local_drink"),
		_ => ("Business & Crew", "storefront"),
	};

	/// <summary>Gameplay upgrades belonging to a group, in list order.</summary>
	public static IEnumerable<UpgradeDef> InGroup( UpgradeGroup group ) => Gameplay.Where( u => u.Group == group );

	/// <summary>Upgrades shown in the general Upgrades tab (everything except the Van).</summary>
	public static readonly IReadOnlyList<UpgradeDef> Gameplay = All.Where( u => u.Id != UpgradeId.Van ).ToList();

	public static readonly UpgradeDef VanDef = Def( UpgradeId.Van );

	public static UpgradeDef Def( UpgradeId id ) => All.First( u => u.Id == id );

	public int Level( UpgradeId id ) => _save.GetUpgrade( Def( id ).Key );

	public double NextCost( UpgradeId id )
	{
		var def = Def( id );
		var lvl = Level( id );
		return lvl >= def.MaxLevel ? double.PositiveInfinity : def.CostForLevel( lvl );
	}

	public bool IsMaxed( UpgradeId id ) => Level( id ) >= Def( id ).MaxLevel;

	/// <summary>Try to buy the next level, spending from the wallet.</summary>
	public bool TryPurchase( UpgradeId id, PlayerWallet wallet )
	{
		if ( IsMaxed( id ) ) return false;
		var cost = NextCost( id );
		if ( !wallet.TrySpend( cost ) ) return false;

		_save.SetUpgrade( Def( id ).Key, Level( id ) + 1 );
		Changed?.Invoke();
		return true;
	}

	// --- Derived modifiers ---
	public float CleanPower => GameConstants.CleanPowerBase + Level( UpgradeId.Pressure ) * GameConstants.CleanPowerPerLevel;
	public float NozzleRadius => GameConstants.NozzleRadiusBase + Level( UpgradeId.Nozzle ) * GameConstants.NozzleRadiusPerLevel;
	public float SprayRange => GameConstants.SprayRange + Level( UpgradeId.Range ) * GameConstants.SprayRangePerLevel;
	public double CashMultiplier => 1.0 + Level( UpgradeId.CashMultiplier ) * 0.22;
	public float MoveSpeed => GameConstants.WalkSpeedBase + Level( UpgradeId.MoveSpeed ) * GameConstants.WalkSpeedPerLevel;
	public float TankMax => GameConstants.TankBase + Level( UpgradeId.Tank ) * GameConstants.TankPerLevel;
	public float StaminaMax => GameConstants.StaminaBase + Level( UpgradeId.Stamina ) * GameConstants.StaminaPerLevel;
	public double AutoIncomePerSecond => Level( UpgradeId.AutoHelper ) * GameConstants.AutoIncomePerLevel;
	public int VanTier => Level( UpgradeId.Van );
	public double VanMultiplier => 1.0 + VanTier * 0.5;

	// --- Per-tool effective stats ---
	// Each tool has its own upgrade line(s); the pressure washer is fully upgrade-driven,
	// while hand tools start from their ToolDef base and gain from their own upgrades.

	/// <summary>Cleaning power for the given tool, including its upgrades.</summary>
	public float PowerFor( ToolDef tool ) => tool.Type switch
	{
		ToolType.PressureWasher => CleanPower,
		ToolType.ScrubBrush => tool.Power + Level( UpgradeId.BrushPower ) * GameConstants.BrushPowerPerLevel,
		ToolType.Squeegee => tool.Power + Level( UpgradeId.SqueegeePower ) * GameConstants.SqueegeePowerPerLevel,
		ToolType.Gun => tool.Power,
		_ => tool.Power,
	};

	/// <summary>Brush/contact footprint for the given tool, including its upgrades.</summary>
	public float RadiusFor( ToolDef tool ) => tool.Type switch
	{
		ToolType.PressureWasher => NozzleRadius,
		ToolType.ScrubBrush => tool.Radius + Level( UpgradeId.BrushWidth ) * GameConstants.BrushRadiusPerLevel,
		ToolType.Squeegee => tool.Radius + Level( UpgradeId.SqueegeeWidth ) * GameConstants.SqueegeeRadiusPerLevel,
		ToolType.Gun => tool.Radius,
		_ => tool.Radius,
	};

	/// <summary>Effective reach for the given tool, including its reach upgrade.</summary>
	public float RangeFor( ToolDef tool ) => tool.Type switch
	{
		ToolType.PressureWasher => SprayRange,
		ToolType.ScrubBrush => tool.Range + Level( UpgradeId.BrushReach ) * GameConstants.BrushRangePerLevel,
		ToolType.Squeegee => tool.Range + Level( UpgradeId.SqueegeeReach ) * GameConstants.SqueegeeRangePerLevel,
		ToolType.Gun => tool.Range,
		_ => tool.Range,
	};
}
