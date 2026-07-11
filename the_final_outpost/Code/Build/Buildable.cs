namespace FinalOutpost;

public enum BuildableId
{
	GunTower,
	CannonTower,
	LongRangeTower,
	WallPiece,
	Barracks,
	Lab,
	Farm,
	Factory,
	Library,
	School,
	Hospital,
	Shop
}

public enum BuildingRole
{
	Defense,
	Wall,
	Management,
	Civic
}

public sealed class BuildableDef
{
	public BuildableId Id { get; init; }
	public string Key { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; }
	public BuildingRole Role { get; init; }
	public float BaseHp { get; init; }
	public float HpPerLevel { get; init; }
	public float BaseDamage { get; init; }
	public float DamagePerLevel { get; init; }
	public float BaseRange { get; init; }
	public float RangePerLevel { get; init; }
	public float FireInterval { get; init; }
	public int MaxLevel { get; init; } = 5;
	public Color Tint { get; init; }
	public Vector3 VisualSize { get; init; }

	/// <summary>Tighter XY footprint used for unit pathing (not placement overlap).</summary>
	public Vector3 CollisionFootprint => new(
		VisualSize.x * GameConstants.BuildingCollisionScale,
		VisualSize.y * GameConstants.BuildingCollisionScale,
		0f );

	public double PlaceCost => Role switch
	{
		BuildingRole.Defense => CombatEconomy.TowerPlaceCost( Dps(), BaseRange ),
		BuildingRole.Wall => CombatEconomy.WallPlaceCost( BaseHp ),
		BuildingRole.Management when Id == BuildableId.Lab => CombatEconomy.RoundCost( BaseHp * 2.5 ),
		BuildingRole.Management => CombatEconomy.BarracksPlaceCost( BaseHp, 0 ),
		BuildingRole.Civic => CombatEconomy.RoundCost( BaseHp * 2.0 ),
		_ => CombatEconomy.RoundCost( BaseHp * CombatEconomy.ScrapPerStructureHp )
	};
	public double UpgradeCost( int currentLevel ) => PlaceCost * Math.Pow( 1.65, currentLevel );
	public double RepairCost( float missingHp ) => missingHp * GameConstants.RepairCostPerHp;
	public float MaxHp( int level ) => BaseHp + (level - 1) * HpPerLevel;
	public float Damage( int level ) => BaseDamage + (level - 1) * DamagePerLevel;
	public float Range( int level ) => BaseRange + (level - 1) * RangePerLevel;
	public float Dps( int level = 1 ) => CombatEconomy.Dps( Damage( level ), FireInterval );
	public int UnlockNight => NightUnlocks.BuildingUnlockNight( this );

	public string GateLabel( SaveData save )
	{
		if ( GameCore.Instance?.IsCure == true )
		{
			if ( Role == BuildingRole.Civic )
				return TechTreeCatalog.GateLabelForBuilding( save, Key );
			return CureUnlocks.UnlockLabel( CureUnlocks.BuildingUnlockSeason( this ) );
		}

		return $"Night {UnlockNight}";
	}

	public string Subtitle => Role switch
	{
		BuildingRole.Defense => "Defense tower",
		BuildingRole.Wall => "Barricade",
		BuildingRole.Civic => "Civic building",
		_ => "Support building"
	};

	/// <summary>Level-1 stats shown while the player is placing this building.</summary>
	public IReadOnlyList<StatLine> PlacementStats()
	{
		var cost = Id == BuildableId.Barracks
			? CombatEconomy.BarracksPlaceCost( BaseHp, BuildManager.Instance?.BarracksCount ?? 0 )
			: PlaceCost;
		var list = new List<StatLine>
		{
			new( "Cost", SelectHelp.Cost( cost ) ),
			new( "Health", $"{MaxHp( 1 ):0}" )
		};

		var up = GameCore.Instance?.Upgrades;
		if ( Role == BuildingRole.Defense )
		{
			var dmg = Damage( 1 ) + (up?.TurretDamageBonus ?? 0);
			var dps = FireInterval > 0 ? dmg / FireInterval : 0f;
			list.Add( new StatLine( "Damage", $"{dmg:0}" ) );
			list.Add( new StatLine( "DPS", $"{dps:0}" ) );
			list.Add( new StatLine( "Range", $"{Range( 1 ) + (up?.TurretRangeBonus ?? 0):0}" ) );
			list.Add( new StatLine( "Fire Rate", $"{(FireInterval > 0 ? 1f / FireInterval : 0):0.0}/s" ) );
		}
		else if ( Role == BuildingRole.Management )
		{
			list.Add( new StatLine( "Squad Cap", $"{GameConstants.RecruitsPerBarracks} per Barracks" ) );
			list.Add( new StatLine( "Day Heal", $"{GameConstants.BarracksHealPerSec:0}/s" ) );
			list.Add( new StatLine( "Dawn Heal", "Full in range" ) );
			list.Add( new StatLine( "Heal Range", $"{GameConstants.BarracksHealRadius:0}" ) );
		}
		else if ( Role == BuildingRole.Civic )
		{
			foreach ( var line in CivicOutputStats( Id ) )
				list.Add( line );
		}

		return list;
	}

	public static IEnumerable<StatLine> CivicOutputStats( BuildableId id ) => id switch
	{
		BuildableId.Farm => new[] { new StatLine( "Food", $"+{CureConstants.FarmFoodPerSec:0.0}/s" ) },
		BuildableId.Factory => new[]
		{
			new StatLine( "Supplies", $"+{CureConstants.FactorySuppliesPerSec:0.0}/s" ),
			new StatLine( "Food", $"+{CureConstants.FactoryFoodPerSec:0.0}/s" ),
			new StatLine( "Repairs", "Speeds repair jobs" )
		},
		BuildableId.Library => new[] { new StatLine( "Knowledge", $"+{CureConstants.LibraryKnowledgePerSec:0.0}/s" ) },
		BuildableId.School => new[] { new StatLine( "Knowledge", $"+{CureConstants.SchoolKnowledgePerSec:0.0}/s" ) },
		BuildableId.Hospital => new[]
		{
			new StatLine( "Recruit Heal", $"+{CureConstants.HospitalRecruitHealPerSec:0.0} HP/s" ),
			new StatLine( "Sickness", "Reduces colony sickness" )
		},
		BuildableId.Shop => new[] { new StatLine( "Scrap", $"+{CureConstants.ShopScrapPerSec:0.0}/s" ) },
		_ => Array.Empty<StatLine>()
	};
}

public static class BuildableCatalog
{
	public static readonly IReadOnlyList<BuildableDef> All = new List<BuildableDef>
	{
		new()
		{
			Id = BuildableId.GunTower, Key = "gun_tower", Name = "Gun Tower", Icon = "adjust",
			Description = "Fast-firing perimeter turret.",
			Role = BuildingRole.Defense, BaseHp = 120, HpPerLevel = 40,
			BaseDamage = 10, DamagePerLevel = 4, BaseRange = 340, RangePerLevel = 25,
			FireInterval = 0.32f, Tint = new Color( 0.55f, 0.7f, 0.9f ),
			VisualSize = new Vector3( 48f, 48f, 72f )
		},
		new()
		{
			Id = BuildableId.CannonTower, Key = "cannon", Name = "Cannon", Icon = "local_fire_department",
			Description = "Slow but devastating splash damage.",
			Role = BuildingRole.Defense, BaseHp = 160, HpPerLevel = 50,
			BaseDamage = 28, DamagePerLevel = 8, BaseRange = 280, RangePerLevel = 15,
			FireInterval = 0.9f, Tint = new Color( 0.85f, 0.5f, 0.35f ),
			VisualSize = new Vector3( 56f, 56f, 64f )
		},
		new()
		{
			Id = BuildableId.LongRangeTower, Key = "sniper", Name = "Long Range", Icon = "radar",
			Description = "Picks off targets from afar.",
			Role = BuildingRole.Defense, BaseHp = 90, HpPerLevel = 30,
			BaseDamage = 18, DamagePerLevel = 5, BaseRange = 520, RangePerLevel = 35,
			FireInterval = 0.55f, Tint = new Color( 0.65f, 0.85f, 0.55f ),
			VisualSize = new Vector3( 40f, 40f, 90f )
		},
		new()
		{
			Id = BuildableId.WallPiece, Key = "wall", Name = "Wall Segment", Icon = "foundation",
			Description = "Matches your perimeter wall. Blocks the horde.",
			Role = BuildingRole.Wall, BaseHp = 100, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.58f, 0.62f ),
			VisualSize = new Vector3( 60f, 60f, GameConstants.WallHeight )
		},
		new()
		{
			Id = BuildableId.Barracks, Key = "barracks", Name = "Barracks", Icon = "groups",
			Description = "Required for recruits — houses 3 soldiers. Cost rises for each one you build.",
			Role = BuildingRole.Management, BaseHp = 200, HpPerLevel = 60,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.7f, 0.6f, 0.45f ),
			VisualSize = new Vector3( 64f, 64f, 56f )
		},
		new()
		{
			Id = BuildableId.Lab, Key = "lab", Name = "Research Lab", Icon = "science",
			Description = "Generates cure research. Assign scholars to boost output.",
			Role = BuildingRole.Management, BaseHp = 180, HpPerLevel = 50,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.75f, 0.95f ),
			VisualSize = new Vector3( 60f, 60f, 64f )
		},
		new()
		{
			Id = BuildableId.Farm, Key = "farm", Name = "Farm", Icon = "agriculture",
			Description = "Produces food for your colony.",
			Role = BuildingRole.Civic, BaseHp = 120, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.45f, 0.82f, 0.38f ),
			VisualSize = new Vector3( 64f, 64f, 48f )
		},
		new()
		{
			Id = BuildableId.Factory, Key = "factory", Name = "Factory", Icon = "factory",
			Description = "Boosts supplies, food, and repair throughput.",
			Role = BuildingRole.Civic, BaseHp = 150, HpPerLevel = 40,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.72f, 0.58f, 0.42f ),
			VisualSize = new Vector3( 68f, 68f, 56f )
		},
		new()
		{
			Id = BuildableId.Library, Key = "library", Name = "Library", Icon = "menu_book",
			Description = "Generates knowledge for the tech tree.",
			Role = BuildingRole.Civic, BaseHp = 140, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.72f, 0.95f ),
			VisualSize = new Vector3( 60f, 60f, 58f )
		},
		new()
		{
			Id = BuildableId.School, Key = "school", Name = "School", Icon = "school",
			Description = "Trains citizens — steady knowledge output.",
			Role = BuildingRole.Civic, BaseHp = 160, HpPerLevel = 40,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.62f, 0.78f, 0.55f ),
			VisualSize = new Vector3( 64f, 64f, 60f )
		},
		new()
		{
			Id = BuildableId.Hospital, Key = "hospital", Name = "Hospital", Icon = "local_hospital",
			Description = "Heals injured recruits and reduces colony sickness.",
			Role = BuildingRole.Civic, BaseHp = 180, HpPerLevel = 45,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.95f, 0.95f, 0.98f ),
			VisualSize = new Vector3( 66f, 66f, 62f )
		},
		new()
		{
			Id = BuildableId.Shop, Key = "shop", Name = "Shop", Icon = "storefront",
			Description = "Generates scrap income for your colony.",
			Role = BuildingRole.Civic, BaseHp = 130, HpPerLevel = 30,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.85f, 0.62f, 0.32f ),
			VisualSize = new Vector3( 58f, 58f, 52f )
		}
	};

	public static IEnumerable<BuildableDef> ForMode( GameModeId mode )
	{
		foreach ( var def in All )
		{
			if ( def.Id == BuildableId.Lab && mode != GameModeId.RoadToCure )
				continue;
			if ( def.Role == BuildingRole.Civic && mode != GameModeId.RoadToCure )
				continue;
			yield return def;
		}
	}

	public static BuildableDef Get( BuildableId id ) => All.First( b => b.Id == id );
	public static BuildableDef Get( string key ) => All.First( b => b.Key == key );
}
