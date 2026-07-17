namespace Offshore;

public enum UpgradeCategory
{
	Rod,
	Reel,
	Line,
	Hook,
	Cooler,
	FishFinder,
	Boat
}

public sealed class UpgradeDefinition
{
	public string Id { get; set; }
	public string DisplayName { get; set; }
	public string Description { get; set; }
	public UpgradeCategory Category { get; set; }
	public int MaxLevel { get; set; } = 5;
	public float BaseCost { get; set; } = 25f;
	public float GrowthRate { get; set; } = 1.55f;
}

public static class UpgradeCatalog
{
	public static IReadOnlyList<UpgradeDefinition> All { get; } =
	[
		new() { Id = "rod", DisplayName = "Fishing Rod", Description = "+Cast distance & charge speed", Category = UpgradeCategory.Rod, BaseCost = 25f },
		new() { Id = "reel", DisplayName = "Reel", Description = "+Reel speed & progress", Category = UpgradeCategory.Reel, BaseCost = 30f },
		new() { Id = "line", DisplayName = "Fishing Line", Description = "+Max tension before snap", Category = UpgradeCategory.Line, BaseCost = 28f },
		new() { Id = "hook", DisplayName = "Hook", Description = "+Bite window & hook chance", Category = UpgradeCategory.Hook, BaseCost = 22f },
		new() { Id = "cooler", DisplayName = "Cooler", Description = "+Cooler capacity", Category = UpgradeCategory.Cooler, BaseCost = 35f },
		new() { Id = "finder", DisplayName = "Fish Finder", Description = "+Rare fish chance hints", Category = UpgradeCategory.FishFinder, BaseCost = 40f },
		new() { Id = "boat_prog", DisplayName = "Boat Fund", Description = "Progress toward better boats", Category = UpgradeCategory.Boat, BaseCost = 80f, MaxLevel = 3, GrowthRate = 2.1f },
	];

	public static UpgradeDefinition Get( string id )
	{
		foreach ( var u in All )
			if ( string.Equals( u.Id, id, StringComparison.OrdinalIgnoreCase ) )
				return u;
		return null;
	}
}

public sealed class UpgradeSystem
{
	private bool _purchaseLatched;

	public int GetLevel( PlayerProgressionData p, string id ) =>
		p.UpgradeLevels.TryGetValue( id, out var lvl ) ? lvl : 0;

	public float GetCost( UpgradeDefinition def, int currentLevel ) =>
		MathF.Round( def.BaseCost * MathF.Pow( def.GrowthRate, currentLevel ) );

	public bool TryPurchase( OffshoreGameController game, string upgradeId )
	{
		if ( _purchaseLatched || game is null )
			return false;

		var def = UpgradeCatalog.Get( upgradeId );
		if ( def is null )
			return false;

		var level = GetLevel( game.Progression, upgradeId );
		if ( level >= def.MaxLevel )
			return false;

		var cost = GetCost( def, level );
		if ( game.Progression.Money < cost )
			return false;

		_purchaseLatched = true;
		game.Progression.Money -= cost;
		game.Progression.LifetimeMoneySpent += cost;
		game.Progression.UpgradeLevels[upgradeId] = level + 1;
		ApplyAll( game );
		OffshoreSaveSystem.Save( game.Progression );
		_purchaseLatched = false;
		game.SetStatus( $"Upgraded {def.DisplayName} to Lv {level + 1}" );
		return true;
	}

	public void ApplyAll( OffshoreGameController game )
	{
		if ( game is null )
			return;

		var b = game.Balance;
		var p = game.Progression;
		var defaults = BalanceConfig.Defaults;

		var rod = GetLevel( p, "rod" );
		var reel = GetLevel( p, "reel" );
		var line = GetLevel( p, "line" );
		var hook = GetLevel( p, "hook" );
		var cooler = GetLevel( p, "cooler" );
		var finder = GetLevel( p, "finder" );

		b.MaxCastDistance = defaults.MaxCastDistance + rod * 4f;
		b.ChargeRate = defaults.ChargeRate + rod * 0.08f;
		b.MinCastDistance = defaults.MinCastDistance;
		b.ReelProgressPerSecond = defaults.ReelProgressPerSecond + reel * 0.06f;
		b.FishStaminaDrainWhileReeling = defaults.FishStaminaDrainWhileReeling + reel * 0.03f;
		b.LineBreakTension = Math.Clamp( defaults.LineBreakTension + line * 0.025f, 0.85f, 1.15f );
		b.BiteReactionSeconds = defaults.BiteReactionSeconds + hook * 0.12f;
		b.MinBiteSeconds = MathF.Max( 0.8f, defaults.MinBiteSeconds - hook * 0.12f );
		b.RareFishBonus = finder * 0.08f;
		_ = cooler; // capacity applied with boat bonus below

		game.Fishing?.Cast?.ApplyBalance( b );
		BoatSystem.ApplyCapacity( game );
	}

	public void ClearPurchaseLatch() => _purchaseLatched = false;
}
