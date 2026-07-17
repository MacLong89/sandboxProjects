namespace RunGun;

public sealed class CharacterDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; }
	public double UnlockCost { get; init; }
	public float DamageBonus { get; init; }
	public float FireRateMult { get; init; } = 1f;
	public int MultishotBonus { get; init; }
	public int PierceBonus { get; init; }
	public float CritBonus { get; init; }
	public float CoinMultBonus { get; init; }
	public BuildStat GateAffinity { get; init; } = BuildStat.Damage;
}

/// <summary>Unlockable runners with distinct starting stat blocks and gate affinities.</summary>
public sealed class CharacterSystem
{
	private readonly SaveData _save;

	public CharacterSystem( SaveData save ) => _save = save;

	public event Action Changed;

	public static readonly IReadOnlyList<CharacterDef> All = new List<CharacterDef>
	{
		new()
		{
			Id = "runner",
			Name = "Spark",
			Description = "Balanced riot starter. No frills.",
			Icon = "local_fire_department",
			UnlockCost = 0,
			DamageBonus = 0f,
			GateAffinity = BuildStat.Damage,
		},
		new()
		{
			Id = "sprinter",
			Name = "Sprinter",
			Description = "Fast fire. Loves rate gates.",
			Icon = "bolt",
			UnlockCost = 400,
			FireRateMult = 1.15f,
			GateAffinity = BuildStat.FireRate,
		},
		new()
		{
			Id = "sniper",
			Name = "Marks",
			Description = "High damage and crit. Pierce specialist.",
			Icon = "gps_fixed",
			UnlockCost = 900,
			DamageBonus = 2f,
			PierceBonus = 1,
			CritBonus = 0.05f,
			GateAffinity = BuildStat.CritChance,
		},
		new()
		{
			Id = "bulwark",
			Name = "Bulwark",
			Description = "Shield affinity. Hold the line.",
			Icon = "shield",
			UnlockCost = 750,
			DamageBonus = -0.5f,
			GateAffinity = BuildStat.Shield,
		},
		new()
		{
			Id = "scavenger",
			Name = "Looter",
			Description = "Coin bonus and denser spray.",
			Icon = "payments",
			UnlockCost = 1100,
			MultishotBonus = 2,
			CoinMultBonus = 0.15f,
			GateAffinity = BuildStat.CoinMult,
		},
	};

	public static CharacterDef Def( string id ) => All.First( c => c.Id == id );

	public CharacterDef ActiveDef => Def( string.IsNullOrEmpty( _save.SelectedCharacter ) ? "runner" : _save.SelectedCharacter );

	public bool IsUnlocked( string id ) => id == "runner" || _save.UnlockedCharacters.Contains( id );

	public bool TryUnlock( string id, PlayerWallet wallet )
	{
		if ( IsUnlocked( id ) ) return false;
		var def = Def( id );
		if ( !wallet.TrySpend( def.UnlockCost ) ) return false;
		_save.UnlockedCharacters.Add( id );
		Changed?.Invoke();
		return true;
	}

	public bool Select( string id )
	{
		if ( !IsUnlocked( id ) ) return false;
		_save.SelectedCharacter = id;
		Changed?.Invoke();
		return true;
	}
}
