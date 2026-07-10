namespace RunGun;

public enum AchievementId
{
	FirstBlood,
	Distance500,
	Distance1000,
	Combo25,
	BossSlayer,
	EliteHunter,
	Prestige1,
	Rich,
}

public sealed class AchievementDef
{
	public AchievementId Id { get; init; }
	public string Key { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; }
	public double CashReward { get; init; }
}

/// <summary>Persistent milestones that grant one-time cash rewards.</summary>
public sealed class AchievementSystem
{
	private readonly SaveData _save;
	private readonly PlayerWallet _wallet;

	public AchievementSystem( SaveData save, PlayerWallet wallet )
	{
		_save = save;
		_wallet = wallet;
	}

	public event Action Changed;

	public static readonly IReadOnlyList<AchievementDef> All = new List<AchievementDef>
	{
		new() { Id = AchievementId.FirstBlood, Key = "first_blood", Name = "First Blood", Description = "Kill your first brute.", Icon = "whatshot", CashReward = 100 },
		new() { Id = AchievementId.Distance500, Key = "dist_500", Name = "Half K", Description = "Reach 500m in one run.", Icon = "flag", CashReward = 500 },
		new() { Id = AchievementId.Distance1000, Key = "dist_1000", Name = "Kilometer Club", Description = "Reach 1000m in one run.", Icon = "emoji_events", CashReward = 1500 },
		new() { Id = AchievementId.Combo25, Key = "combo_25", Name = "On Fire", Description = "Hit a 25 kill combo.", Icon = "local_fire_department", CashReward = 800 },
		new() { Id = AchievementId.BossSlayer, Key = "boss_slayer", Name = "Boss Slayer", Description = "Defeat a boss.", Icon = "skull", CashReward = 1000 },
		new() { Id = AchievementId.EliteHunter, Key = "elite_10", Name = "Elite Hunter", Description = "Kill 10 elites in one run.", Icon = "military_tech", CashReward = 1200 },
		new() { Id = AchievementId.Prestige1, Key = "prestige_1", Name = "Ascended", Description = "Prestige once.", Icon = "auto_awesome", CashReward = 2000 },
		new() { Id = AchievementId.Rich, Key = "rich", Name = "Loaded", Description = "Bank $50,000 total.", Icon = "savings", CashReward = 3000 },
	};

	public bool IsComplete( AchievementId id ) => _save.CompletedAchievements.Contains( Def( id ).Key );

	public static AchievementDef Def( AchievementId id ) => All.First( a => a.Id == id );

	public void CheckRunEnd( RunState run )
	{
		TryComplete( AchievementId.FirstBlood, run.KillCount >= 1 );
		TryComplete( AchievementId.Distance500, run.DistanceMeters >= 500f );
		TryComplete( AchievementId.Distance1000, run.DistanceMeters >= 1000f );
		TryComplete( AchievementId.Combo25, run.PeakCombo >= 25 );
		TryComplete( AchievementId.BossSlayer, run.BossesKilled >= 1 );
		TryComplete( AchievementId.EliteHunter, run.EliteKillCount >= 10 );
		TryComplete( AchievementId.Rich, _save.LifetimeEarned >= 50_000 );
	}

	public void CheckPrestige() => TryComplete( AchievementId.Prestige1, _save.PrestigeLevel >= 1 );

	private void TryComplete( AchievementId id, bool condition )
	{
		if ( !condition || IsComplete( id ) ) return;
		var def = Def( id );
		_save.CompletedAchievements.Add( def.Key );
		_wallet.Earn( def.CashReward );
		Changed?.Invoke();
	}
}
