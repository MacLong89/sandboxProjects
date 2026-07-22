namespace FinalOutpost;

/// <summary>Deterministic rival garrison layout for a plot (recruits + buildings).</summary>
public readonly struct RivalGarrisonSlot
{
	public RecruitWeaponType Weapon { get; init; }
	public Vector3 LocalOffset { get; init; }
}

public readonly struct RivalBuildingSlot
{
	public BuildableId Id { get; init; }
	public Vector3 LocalOffset { get; init; }
}

public readonly struct RivalGarrisonLayout
{
	public int PlotX { get; init; }
	public int PlotY { get; init; }
	public IReadOnlyList<RivalGarrisonSlot> Recruits { get; init; }
	public IReadOnlyList<RivalBuildingSlot> Buildings { get; init; }
	/// <summary>Defense building pads that fire during assault.</summary>
	public IReadOnlyList<Vector3> TowerOffsets { get; init; }
}

public static class RivalGarrison
{
	public const int MaxRecruits = 4;

	static readonly RecruitWeaponType[] WeaponPool =
	{
		RecruitWeaponType.Pistol,
		RecruitWeaponType.Smg,
		RecruitWeaponType.AssaultRifle,
		RecruitWeaponType.Shotgun,
		RecruitWeaponType.Sniper
	};

	/// <summary>Buildings rivals may place inside the wall ring (no wall pieces — perimeter is fixed).</summary>
	static readonly BuildableId[] BuildingPool =
	{
		BuildableId.GunTower,
		BuildableId.CannonTower,
		BuildableId.LongRangeTower,
		BuildableId.Spotlight,
		BuildableId.OilSlick,
		BuildableId.AmmoDepot,
		BuildableId.Hardpoint,
		BuildableId.RadioMast,
		BuildableId.Artillery,
		BuildableId.Barracks,
		BuildableId.Farm,
		BuildableId.Factory,
		BuildableId.Lab,
		BuildableId.Library,
		BuildableId.School,
		BuildableId.Hospital,
		BuildableId.Shop,
		BuildableId.Observatory,
		BuildableId.University
	};

	public static RivalGarrisonLayout Build( int plotX, int plotY )
	{
		var rng = new Random( Hash( plotX, plotY ) );
		var recruitCount = 1 + rng.Next( MaxRecruits ); // 1–4
		var buildingCount = 2 + rng.Next( 5 ); // 2–6
		var half = GameConstants.ArenaHalf;
		var cell = GameConstants.CellSize;
		// Stay inside the wall ring and clear of the 2×2 command post.
		var maxCell = (int)MathF.Floor( (half - cell * 1.5f) / cell );

		var used = new HashSet<(int, int)>();
		var buildings = new List<RivalBuildingSlot>( buildingCount );
		for ( var i = 0; i < buildingCount; i++ )
		{
			if ( !TryPickCell( rng, maxCell, used, out var lx, out var ly ) )
				break;
			used.Add( (lx, ly) );
			buildings.Add( new RivalBuildingSlot
			{
				Id = BuildingPool[rng.Next( BuildingPool.Length )],
				LocalOffset = new Vector3( (lx + 0.5f) * cell, (ly + 0.5f) * cell, 0f )
			} );
		}

		var recruits = new List<RivalGarrisonSlot>( recruitCount );
		for ( var i = 0; i < recruitCount; i++ )
		{
			var ang = (i / (float)recruitCount) * MathF.PI * 2f + (float)rng.NextDouble() * 0.35f;
			var dist = cell * 1.5f + (float)rng.NextDouble() * (half * 0.55f);
			recruits.Add( new RivalGarrisonSlot
			{
				Weapon = WeaponPool[rng.Next( WeaponPool.Length )],
				LocalOffset = new Vector3( MathF.Cos( ang ) * dist, MathF.Sin( ang ) * dist, 0f )
			} );
		}

		var towers = new List<Vector3>();
		foreach ( var b in buildings )
		{
			if ( BuildableCatalog.TryGet( b.Id, out var def ) && def.Role == BuildingRole.Defense )
				towers.Add( b.LocalOffset );
		}

		return new RivalGarrisonLayout
		{
			PlotX = plotX,
			PlotY = plotY,
			Recruits = recruits,
			Buildings = buildings,
			TowerOffsets = towers
		};
	}

	static bool TryPickCell( Random rng, int maxCell, HashSet<(int, int)> used, out int lx, out int ly )
	{
		lx = 0;
		ly = 0;
		for ( var attempt = 0; attempt < 40; attempt++ )
		{
			lx = rng.Next( -maxCell, maxCell );
			ly = rng.Next( -maxCell, maxCell );
			// Command post occupies local cells (-1|0, -1|0) relative to plot center.
			if ( (lx == -1 || lx == 0) && (ly == -1 || ly == 0) )
				continue;
			if ( used.Contains( (lx, ly) ) )
				continue;
			return true;
		}
		return false;
	}

	public static int Hash( int x, int y )
	{
		unchecked
		{
			return (x * 73856093) ^ (y * 19349663) ^ 0x51a7;
		}
	}

	/// <summary>Chance a Cure timed threat becomes a rival outpost attack.</summary>
	public const float CureRivalAttackChance = 0.28f;
	/// <summary>Chance a Survival night becomes a rival outpost attack (from night 3+).</summary>
	public const float SurvivalRivalAttackChance = 0.08f;
	public const int SurvivalRivalAttackMinNight = 3;

	public static int BaseAttackCount( int progressionNight )
	{
		var n = Math.Max( 1, progressionNight );
		return Math.Clamp( 5 + n / 2, 5, 14 );
	}
}
