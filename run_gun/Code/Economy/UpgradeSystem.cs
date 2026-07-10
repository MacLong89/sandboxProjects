namespace RunGun;

public enum UpgradeId
{
	StartPower,
	Damage,
	FireRate,
	MoveSpeed,
	MaxHealth,
	CoinBonus,
	CritChance,
	Pierce,
	Lifesteal,
	OverdriveCharge,
	GateLuck,
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
		new() { Id = UpgradeId.StartPower, Key = "startpower", Name = "Head Start", Description = "Begin every run with more damage.", Icon = "rocket_launch", MaxLevel = 30, BaseCost = 60, CostGrowth = 1.55 },
		new() { Id = UpgradeId.Damage, Key = "damage", Name = "Hollow Points", Description = "Every bullet hits harder.", Icon = "whatshot", MaxLevel = 30, BaseCost = 80, CostGrowth = 1.6 },
		new() { Id = UpgradeId.FireRate, Key = "firerate", Name = "Trigger Finger", Description = "Shoot faster at any power.", Icon = "bolt", MaxLevel = 25, BaseCost = 100, CostGrowth = 1.6 },
		new() { Id = UpgradeId.MoveSpeed, Key = "movespeed", Name = "Light Feet", Description = "Strafe across lanes quicker.", Icon = "directions_run", MaxLevel = 15, BaseCost = 70, CostGrowth = 1.6 },
		new() { Id = UpgradeId.MaxHealth, Key = "health", Name = "Recruits", Description = "Start every run with a bigger crew.", Icon = "groups", MaxLevel = 20, BaseCost = 90, CostGrowth = 1.65 },
		new() { Id = UpgradeId.CoinBonus, Key = "coins", Name = "Bounty", Description = "Earn more cash from every run.", Icon = "payments", MaxLevel = 30, BaseCost = 120, CostGrowth = 1.6 },
		new() { Id = UpgradeId.CritChance, Key = "crit", Name = "Deadeye", Description = "Higher crit chance every run.", Icon = "stars", MaxLevel = 20, BaseCost = 150, CostGrowth = 1.65 },
		new() { Id = UpgradeId.Pierce, Key = "pierce", Name = "Penetrator", Description = "Bullets punch through more targets.", Icon = "arrow_forward", MaxLevel = 15, BaseCost = 140, CostGrowth = 1.65 },
		new() { Id = UpgradeId.Lifesteal, Key = "lifesteal", Name = "Recruiter", Description = "Chance to draft a runner into your crew on kill.", Icon = "person_add", MaxLevel = 12, BaseCost = 180, CostGrowth = 1.7 },
		new() { Id = UpgradeId.OverdriveCharge, Key = "overdrive", Name = "Adrenaline", Description = "Overdrive charges faster.", Icon = "flash_on", MaxLevel = 15, BaseCost = 160, CostGrowth = 1.65 },
		new() { Id = UpgradeId.GateLuck, Key = "gateluck", Name = "Gate Sense", Description = "Better gate values and rarer stats.", Icon = "auto_awesome", MaxLevel = 20, BaseCost = 130, CostGrowth = 1.6 },
	};

	public static UpgradeDef Def( UpgradeId id ) => All.First( u => u.Id == id );

	public int Level( UpgradeId id ) => _save.GetUpgrade( Def( id ).Key );

	public double NextCost( UpgradeId id )
	{
		var def = Def( id );
		var lvl = Level( id );
		return lvl >= def.MaxLevel ? double.PositiveInfinity : def.CostForLevel( lvl );
	}

	public bool IsMaxed( UpgradeId id ) => Level( id ) >= Def( id ).MaxLevel;

	public bool TryPurchase( UpgradeId id, PlayerWallet wallet )
	{
		if ( IsMaxed( id ) ) return false;
		var cost = NextCost( id );
		if ( !wallet.TrySpend( cost ) ) return false;

		_save.SetUpgrade( Def( id ).Key, Level( id ) + 1 );
		Changed?.Invoke();
		return true;
	}

	public float StartDamageBonus => Level( UpgradeId.StartPower ) * GameConstants.StartDamagePerLevel;
	public float DamageMult => 1f + Level( UpgradeId.Damage ) * GameConstants.DamageMultPerLevel;
	public float FireRateMult => 1f + Level( UpgradeId.FireRate ) * GameConstants.FireRateMultPerLevel;
	public float StrafeSpeed => GameConstants.StrafeSpeedBase + Level( UpgradeId.MoveSpeed ) * GameConstants.StrafeSpeedPerLevel;
	public int StartSquadBonus => Level( UpgradeId.MaxHealth ) * GameConstants.SquadPerRecruitLevel;
	public double CoinMult => (1.0 + Level( UpgradeId.CoinBonus ) * GameConstants.CoinMultPerLevel) * (1.0 + _save.PrestigeLevel * GameConstants.PrestigeCoinMultPerLevel);
	public float CritChance => Level( UpgradeId.CritChance ) * GameConstants.CritPerLevel;
	public int PierceBonus => (int)MathF.Round( Level( UpgradeId.Pierce ) * GameConstants.PiercePerLevel );
	public float LifestealFraction => Level( UpgradeId.Lifesteal ) * GameConstants.LifestealPerLevel;
	public float OverdriveChargeMult => 1f + Level( UpgradeId.OverdriveCharge ) * GameConstants.OverdriveChargePerLevel;
	public float GateLuck => Level( UpgradeId.GateLuck ) * GameConstants.GateLuckPerLevel;

	public void ResetForPrestige()
	{
		foreach ( var def in All )
			_save.SetUpgrade( def.Key, 0 );
		Changed?.Invoke();
	}
}
