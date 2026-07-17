namespace Deep;

public sealed class UpgradeSystem
{
	private readonly PlayerProgressionData _progression;
	private readonly BalanceConfig _balance;

	public UpgradeSystem( PlayerProgressionData progression, BalanceConfig balance )
	{
		_progression = progression;
		_balance = balance;
	}

	public int GetLevel( string upgradeId ) => _progression.GetUpgradeLevel( upgradeId );

	public float NextCost( string upgradeId )
	{
		var def = UpgradeCatalog.Get( upgradeId );
		if ( def is null ) return float.MaxValue;
		var level = GetLevel( upgradeId );
		if ( level >= def.MaxLevel ) return float.MaxValue;
		return UpgradeCatalog.CostForLevel( def, level );
	}

	public bool UsesShells( string upgradeId ) =>
		UpgradeCatalog.UsesShells( UpgradeCatalog.Get( upgradeId ) );

	public bool CanBuy( string upgradeId )
	{
		var def = UpgradeCatalog.Get( upgradeId );
		if ( def is null ) return false;
		var level = GetLevel( upgradeId );
		if ( level >= def.MaxLevel ) return false;
		var cost = NextCost( upgradeId );
		return UpgradeCatalog.UsesShells( def )
			? _progression.Shells + 0.001f >= cost
			: _progression.Money + 0.001f >= cost;
	}

	public bool TryBuy( string upgradeId )
	{
		var def = UpgradeCatalog.Get( upgradeId );
		if ( def is null ) return false;

		var level = GetLevel( upgradeId );
		if ( level >= def.MaxLevel ) return false;

		var cost = UpgradeCatalog.CostForLevel( def, level );
		var paid = UpgradeCatalog.UsesShells( def )
			? _progression.TrySpendShells( cost )
			: _progression.TrySpend( cost );
		if ( !paid )
			return false;

		_progression.SetUpgradeLevel( upgradeId, level + 1 );
		ApplyAllToBalance();
		return true;
	}

	public void ApplyAllToBalance()
	{
		var defaults = BalanceConfig.CreateDefaults();

		var o2 = GetLevel( "oxygen_tank" );
		var fins = GetLevel( "fins" );
		var bag = GetLevel( "dive_bag" );
		var suit = GetLevel( "pressure_suit" );
		var hp = GetLevel( "hull_health" );
		var light = GetLevel( "floodlight" );
		var sonar = GetLevel( "sonar_range" );

		var o2Def = UpgradeCatalog.Get( "oxygen_tank" );
		var finsDef = UpgradeCatalog.Get( "fins" );
		var bagDef = UpgradeCatalog.Get( "dive_bag" );
		var suitDef = UpgradeCatalog.Get( "pressure_suit" );
		var hpDef = UpgradeCatalog.Get( "hull_health" );
		var lightDef = UpgradeCatalog.Get( "floodlight" );
		var sonarDef = UpgradeCatalog.Get( "sonar_range" );

		_balance.MaxOxygenSeconds = defaults.MaxOxygenSeconds + o2 * (o2Def?.EffectPerLevel ?? 12f);
		_balance.SwimSpeed = defaults.SwimSpeed + fins * (finsDef?.EffectPerLevel ?? 2.5f);
		_balance.AscentSpeed = defaults.AscentSpeed + fins * (finsDef?.EffectPerLevel ?? 2.5f);
		_balance.DescentSpeed = defaults.DescentSpeed + fins * (finsDef?.EffectPerLevel ?? 2.5f);
		_balance.BaseHaulCapacity = defaults.BaseHaulCapacity + (int)(bag * (bagDef?.EffectPerLevel ?? 2f));
		_balance.SafeDepthMeters = defaults.SafeDepthMeters + suit * (suitDef?.EffectPerLevel ?? 30f);
		_balance.MaxHealth = defaults.MaxHealth + hp * (hpDef?.EffectPerLevel ?? 20f);
		_balance.VisibilityBonus = light * (lightDef?.EffectPerLevel ?? 0.12f);
		_balance.ScannerRangeBonus = sonar * (sonarDef?.EffectPerLevel ?? 6f);
	}
}
