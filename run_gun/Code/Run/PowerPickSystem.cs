namespace RunGun;

public enum PowerPickId
{
	MolotovAmmo,
	AdrenalineRush,
	LootFever,
	PiercingRounds,
	RiotShield,
	OverdriveSpike,
}

public sealed class PowerPickDef
{
	public PowerPickId Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; }
	public Color Tint { get; init; }
}

/// <summary>
/// Boss-kill rewards only. Mid-run freebies diluted tension — these are earned spikes.
/// </summary>
public sealed class PowerPickSystem
{
	public static readonly IReadOnlyList<PowerPickDef> Catalog = new List<PowerPickDef>
	{
		new()
		{
			Id = PowerPickId.MolotovAmmo,
			Name = "MOLOTOV ROUNDS",
			Description = "+5 damage this run",
			Icon = "local_fire_department",
			Tint = new Color( 1f, 0.45f, 0.25f ),
		},
		new()
		{
			Id = PowerPickId.AdrenalineRush,
			Name = "ADRENALINE",
			Description = "+40% fire rate",
			Icon = "bolt",
			Tint = new Color( 0.4f, 0.9f, 1f ),
		},
		new()
		{
			Id = PowerPickId.LootFever,
			Name = "LOOT FEVER",
			Description = "+50% coins this run",
			Icon = "payments",
			Tint = new Color( 1f, 0.85f, 0.25f ),
		},
		new()
		{
			Id = PowerPickId.PiercingRounds,
			Name = "ARMOR PIERCE",
			Description = "+1 pierce + 10% crit",
			Icon = "arrow_forward",
			Tint = new Color( 1f, 0.4f, 0.75f ),
		},
		new()
		{
			Id = PowerPickId.RiotShield,
			Name = "RIOT SHIELD",
			Description = "+18 shield (absorbs hits)",
			Icon = "shield",
			Tint = new Color( 0.45f, 0.75f, 1f ),
		},
		new()
		{
			Id = PowerPickId.OverdriveSpike,
			Name = "SURGE READY",
			Description = "Fill Riot Surge meter",
			Icon = "flash_on",
			Tint = new Color( 1f, 0.85f, 0.3f ),
		},
	};

	public bool IsOpen { get; private set; }
	public IReadOnlyList<PowerPickDef> Choices { get; private set; } = Array.Empty<PowerPickDef>();
	public int PicksTaken { get; private set; }

	public void Reset()
	{
		IsOpen = false;
		Choices = Array.Empty<PowerPickDef>();
		PicksTaken = 0;
	}

	public void OfferBossReward()
	{
		if ( IsOpen ) return;
		IsOpen = true;
		Choices = RollThree();
	}

	public void Choose( PowerPickId id, RunState run )
	{
		if ( !IsOpen || run is null ) return;
		Apply( id, run );
		PicksTaken++;
		IsOpen = false;
		Choices = Array.Empty<PowerPickDef>();
		Sfx.Play( Sfx.Purchase );
	}

	private static IReadOnlyList<PowerPickDef> RollThree()
	{
		var pool = Catalog.ToList();
		var picks = new List<PowerPickDef>( 3 );
		for ( var i = 0; i < 3 && pool.Count > 0; i++ )
		{
			var idx = Game.Random.Int( 0, pool.Count - 1 );
			picks.Add( pool[idx] );
			pool.RemoveAt( idx );
		}
		return picks;
	}

	private static void Apply( PowerPickId id, RunState run )
	{
		switch ( id )
		{
			case PowerPickId.MolotovAmmo:
				run.Damage += 5f;
				break;
			case PowerPickId.AdrenalineRush:
				run.FireRateMult *= 1.4f;
				break;
			case PowerPickId.LootFever:
				run.RunCoinMult *= 1.5f;
				break;
			case PowerPickId.PiercingRounds:
				run.Pierce = Math.Clamp( run.Pierce + 1, 0, GameConstants.MaxPierce );
				run.CritChance = MathF.Min( GameConstants.CritChanceCap, run.CritChance + 0.1f );
				break;
			case PowerPickId.RiotShield:
				run.Shield += 18f;
				break;
			case PowerPickId.OverdriveSpike:
				run.OverdriveMeter = GameConstants.OverdriveMax;
				break;
		}
	}
}
