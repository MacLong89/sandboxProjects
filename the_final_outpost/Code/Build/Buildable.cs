namespace FinalOutpost;

public enum BuildableId
{
	GunTower,
	CannonTower,
	LongRangeTower,
	Spotlight,
	Minefield,
	OilSlick,
	Artillery,
	AmmoDepot,
	Hardpoint,
	RadioMast,
	WallPiece,
	Barracks,
	Lab,
	Farm,
	Factory,
	Library,
	School,
	Hospital,
	Shop,
	Observatory,
	University
}

public enum BuildingRole
{
	Defense,
	Support,
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
	/// <summary>Design-unit range. Physical reach is <see cref="Range"/>.</summary>
	public float BaseRange { get; init; }
	public float RangePerLevel { get; init; }
	public float FireInterval { get; init; }
	public int MaxLevel { get; init; } = 5;
	public Color Tint { get; init; }
	/// <summary>Mesh/ghost size. XY should match <see cref="GameConstants.CellSize"/> for 1×1 placeables.</summary>
	public Vector3 VisualSize { get; init; }

	/// <summary>Placement overlap — same XY as the visual / occupied cell.</summary>
	public Vector3 CollisionFootprint => new( VisualSize.x, VisualSize.y, 0f );

	/// <summary>
	/// Melee approach volume — matches the 1×1 placement cell / visual XY so zombies stop
	/// on the face of what they path around (same idea as wall segment footprints).
	/// </summary>
	public Vector3 ZombieCollisionFootprint => new( VisualSize.x, VisualSize.y, 0f );

	/// <summary>Scrap for the first copy — fixed table, not derived from stats.</summary>
	public double BaseCost => Id switch
	{
		BuildableId.GunTower => 75,
		BuildableId.CannonTower => 75,
		BuildableId.LongRangeTower => 100,
		BuildableId.Spotlight => 220,
		BuildableId.Minefield => 35,
		BuildableId.OilSlick => 180,
		BuildableId.Artillery => 360,
		BuildableId.AmmoDepot => 160,
		BuildableId.Hardpoint => 150,
		BuildableId.RadioMast => 170,
		BuildableId.WallPiece => 25,
		BuildableId.Barracks => 70,
		BuildableId.Lab => 200,
		BuildableId.Farm => 100,
		BuildableId.Factory => 150,
		BuildableId.Library => 120,
		BuildableId.School => 140,
		BuildableId.Hospital => 160,
		BuildableId.Shop => 120,
		BuildableId.Observatory => 130,
		BuildableId.University => 180,
		_ => 50
	};

	/// <summary>Extra scrap added for each copy you already own.</summary>
	public double CostBump => Id switch
	{
		BuildableId.GunTower or BuildableId.CannonTower => 10,
		BuildableId.LongRangeTower => 15,
		BuildableId.Spotlight => 45,
		BuildableId.Minefield => 8,
		BuildableId.OilSlick => 35,
		BuildableId.Artillery => 70,
		BuildableId.AmmoDepot or BuildableId.Hardpoint or BuildableId.RadioMast => 30,
		BuildableId.WallPiece => 5,
		BuildableId.Barracks => 25,
		BuildableId.Lab => 40,
		BuildableId.Farm or BuildableId.Library or BuildableId.School or BuildableId.Shop
			or BuildableId.Observatory => 15,
		BuildableId.Factory or BuildableId.Hospital or BuildableId.University => 20,
		_ => CombatEconomy.DefaultRepeatBump
	};

	/// <summary>First-copy price (no escalation). Live buy price is <see cref="BuildManager.PlaceCost"/>.</summary>
	public double PlaceCost => BaseCost;

	public double UpgradeCost( int currentLevel ) => BaseCost * Math.Pow( 1.65, currentLevel );
	public double RepairCost( float missingHp ) => missingHp * GameConstants.RepairCostPerHp;
	public float MaxHp( int level ) => BaseHp + (level - 1) * HpPerLevel;
	public float Damage( int level ) => BaseDamage + (level - 1) * DamagePerLevel;
	/// <summary>World-space engagement range (design × <see cref="GameConstants.RangeScale"/>).</summary>
	public float Range( int level ) =>
		(BaseRange + (level - 1) * RangePerLevel) * GameConstants.RangeScale;
	public float Dps( int level = 1 ) => CombatEconomy.Dps( Damage( level ), FireInterval );
	public int UnlockNight => NightUnlocks.BuildingUnlockNight( this );

	public string GateLabel( SaveData save )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.BuildingUnlockLabel( save, Id );

		return $"Night {UnlockNight}";
	}

	public string Subtitle => Role switch
	{
		BuildingRole.Defense => "Defense tower",
		BuildingRole.Support => "Support pad",
		BuildingRole.Wall => "Barricade",
		BuildingRole.Civic => "Civic building",
		BuildingRole.Management => "Support building",
		_ => "Building"
	};

	/// <summary>Level-1 stats shown while the player is placing this building.</summary>
	public IReadOnlyList<StatLine> PlacementStats()
	{
		var cost = BuildManager.Instance?.PlaceCost( this ) ?? BaseCost;
		var list = new List<StatLine>
		{
			new( "Cost", SelectHelp.Cost( cost ) ),
			new( "Health", $"{MaxHp( 1 ):0}" )
		};

		var up = GameCore.Instance?.Upgrades;
		if ( Role == BuildingRole.Support )
		{
			foreach ( var line in SupportPlacementStats( Id, up ) )
				list.Add( line );
		}
		else if ( Role == BuildingRole.Defense )
		{
			var dmg = Damage( 1 ) + (up?.TurretDamageBonus ?? 0);
			var dps = FireInterval > 0 ? dmg / FireInterval : 0f;
			list.Add( new StatLine( "Damage", $"{dmg:0}" ) );
			if ( Id is BuildableId.Minefield or BuildableId.Artillery or BuildableId.CannonTower )
				list.Add( new StatLine( "Splash", "Yes" ) );
			list.Add( new StatLine( "DPS", $"{dps:0}" ) );
			list.Add( new StatLine( "Range", $"{Range( 1 ) + (up?.TurretRangeBonus ?? 0):0}" ) );
			if ( FireInterval > 0f )
				list.Add( new StatLine( Id == BuildableId.Minefield ? "Rearm" : "Fire Rate",
					Id == BuildableId.Minefield
						? $"{FireInterval:0.0}s"
						: $"{1f / FireInterval:0.0}/s" ) );
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

	public static IEnumerable<StatLine> SupportPlacementStats( BuildableId id, UpgradeSystem up ) => id switch
	{
		BuildableId.Spotlight => new[]
		{
			new StatLine( "Plot Buff", "+50% range" ),
			new StatLine( "Affects", "Towers & recruits" ),
			new StatLine( "Limit", "1 per plot" )
		},
		BuildableId.OilSlick => new[]
		{
			new StatLine( "Slow", "60% in radius" ),
			new StatLine( "Range", $"{BuildableCatalog.Get( id ).Range( 1 ) + (up?.TurretRangeBonus ?? 0):0}" ),
			new StatLine( "Limit", "1 per plot" )
		},
		BuildableId.AmmoDepot => new[]
		{
			new StatLine( "Plot Buff", $"+{(int)MathF.Round( (DefenseEffects.AmmoDepotFireRateMult - 1f) * 100f )}% fire rate" ),
			new StatLine( "Affects", "Towers & recruits" ),
			new StatLine( "Limit", "1 per plot" )
		},
		BuildableId.Hardpoint => new[]
		{
			new StatLine( "Plot Buff", $"-{(int)MathF.Round( (1f - DefenseEffects.HardpointDamageTakenMult) * 100f )}% damage taken" ),
			new StatLine( "Affects", "Buildings & walls" ),
			new StatLine( "Limit", "1 per plot" )
		},
		BuildableId.RadioMast => new[]
		{
			new StatLine( "Plot Buff", $"+{(int)MathF.Round( (DefenseEffects.RadioMastMoveMult - 1f) * 100f )}% recruit speed" ),
			new StatLine( "Acquire", $"+{(int)MathF.Round( (DefenseEffects.RadioMastAcquireMult - 1f) * 100f )}% range" ),
			new StatLine( "Limit", "1 per plot" )
		},
		_ => Array.Empty<StatLine>()
	};

	public static IEnumerable<StatLine> CivicOutputStats( BuildableId id ) => id switch
	{
		BuildableId.Farm => new[] { new StatLine( "Food", $"+{CureConstants.FarmFoodPerSec:0.0}/s" ) },
		BuildableId.Factory => new[]
		{
			new StatLine( "Supplies", $"+{CureConstants.FactorySuppliesPerSec:0.0}/s" ),
			new StatLine( "Food", $"+{CureConstants.FactoryFoodPerSec:0.0}/s" ),
			new StatLine( "Repairs", "Speeds repair jobs" )
		},
		BuildableId.Library => new[]
		{
			new StatLine( "Knowledge", $"+{CureConstants.LibraryKnowledgePerSec:0.0}/s" ),
			new StatLine( "Tech costs", $"-{(int)MathF.Round( (1f - CureConstants.LibraryTechCostMultPer) * 100f )}% each" )
		},
		BuildableId.School => new[] { new StatLine( "Knowledge", $"+{CureConstants.SchoolKnowledgePerSec:0.0}/s" ) },
		BuildableId.Hospital => new[]
		{
			new StatLine( "Recruit Heal", $"+{CureConstants.HospitalRecruitHealPerSec:0.0} HP/s" ),
			new StatLine( "Sickness", "Reduces colony sickness" )
		},
		BuildableId.Shop => new[] { new StatLine( "Scrap", $"+{CureConstants.ShopScrapPerSec:0.0}/s" ) },
		BuildableId.Observatory => new[] { new StatLine( "Knowledge", $"+{CureConstants.ObservatoryKnowledgePerSec:0.0}/s" ) },
		BuildableId.University => new[]
		{
			new StatLine( "Knowledge", $"+{CureConstants.UniversityKnowledgePerSec:0.0}/s" ),
			new StatLine( "Lab output", $"+{(int)MathF.Round( (CureConstants.UniversityLabOutputMult - 1f) * 100f )}% each" )
		},
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
			BaseDamage = 10, DamagePerLevel = 4, BaseRange = 340f, RangePerLevel = 25f,
			FireInterval = 0.32f, Tint = new Color( 0.55f, 0.7f, 0.9f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 72f ) )
		},
		new()
		{
			Id = BuildableId.CannonTower, Key = "cannon", Name = "Cannon", Icon = "local_fire_department",
			Description = "Slow but devastating splash damage.",
			Role = BuildingRole.Defense, BaseHp = 160, HpPerLevel = 50,
			BaseDamage = 28, DamagePerLevel = 8, BaseRange = 280f, RangePerLevel = 15f,
			FireInterval = 0.9f, Tint = new Color( 0.85f, 0.5f, 0.35f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 64f ) )
		},
		new()
		{
			Id = BuildableId.LongRangeTower, Key = "sniper", Name = "Long Range", Icon = "radar",
			Description = "Picks off targets from afar.",
			Role = BuildingRole.Defense, BaseHp = 90, HpPerLevel = 30,
			BaseDamage = 18, DamagePerLevel = 5, BaseRange = 520f, RangePerLevel = 35f,
			FireInterval = 0.55f, Tint = new Color( 0.65f, 0.85f, 0.55f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 90f ) )
		},
		new()
		{
			Id = BuildableId.Spotlight, Key = "spotlight", Name = "Spotlight", Icon = "lightbulb",
			Description = "Floodlights this plot — towers and recruits here gain +50% range. One per plot.",
			Role = BuildingRole.Support, BaseHp = 110, HpPerLevel = 30,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.95f, 0.9f, 0.55f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 96f ) )
		},
		new()
		{
			Id = BuildableId.Minefield, Key = "mines", Name = "Minefield", Icon = "crisis_alert",
			Description = "Cheap proximity mines — splash the pack, never friendly fire. Rearms after each blast.",
			Role = BuildingRole.Defense, BaseHp = 55, HpPerLevel = 18,
			BaseDamage = 48, DamagePerLevel = 14, BaseRange = 95f, RangePerLevel = 8f,
			FireInterval = 2.4f, Tint = new Color( 0.55f, 0.35f, 0.28f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 28f ) )
		},
		new()
		{
			Id = BuildableId.OilSlick, Key = "oil_slick", Name = "Oil Slick", Icon = "water_drop",
			Description = "Pours a sticky slick — zombies in the pool move at 40% speed. One per plot.",
			Role = BuildingRole.Support, BaseHp = 120, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 240f, RangePerLevel = 18f,
			FireInterval = 0.2f, Tint = new Color( 0.25f, 0.22f, 0.2f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 40f ) )
		},
		new()
		{
			Id = BuildableId.Artillery, Key = "artillery", Name = "Artillery", Icon = "rocket_launch",
			Description = "Heavy howitzer — slow fire, massive splash that flattens packs.",
			Role = BuildingRole.Defense, BaseHp = 150, HpPerLevel = 45,
			BaseDamage = 95, DamagePerLevel = 22, BaseRange = 500f, RangePerLevel = 30f,
			FireInterval = 2.75f, Tint = new Color( 0.45f, 0.5f, 0.38f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 70f ) )
		},
		new()
		{
			Id = BuildableId.AmmoDepot, Key = "ammo_depot", Name = "Ammo Depot", Icon = "inventory_2",
			Description = "Stockpiles rounds — towers and recruits on this plot fire 25% faster. One per plot.",
			Role = BuildingRole.Support, BaseHp = 130, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.72f, 0.55f, 0.32f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 52f ) )
		},
		new()
		{
			Id = BuildableId.Hardpoint, Key = "hardpoint", Name = "Hardpoint", Icon = "security",
			Description = "Sandbagged fighting position — buildings and walls on this plot take 20% less damage. One per plot.",
			Role = BuildingRole.Support, BaseHp = 160, HpPerLevel = 45,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.52f, 0.45f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 44f ) )
		},
		new()
		{
			Id = BuildableId.RadioMast, Key = "radio_mast", Name = "Radio Mast", Icon = "cell_tower",
			Description = "Coordinates the squad — recruits on this plot move faster and acquire threats sooner. One per plot.",
			Role = BuildingRole.Support, BaseHp = 100, HpPerLevel = 28,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.45f, 0.7f, 0.85f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 110f ) )
		},
		new()
		{
			Id = BuildableId.WallPiece, Key = "wall", Name = "Wall Segment", Icon = "foundation",
			Description = "Timber scaffolding and iron rails — blocks the horde, open enough to fire through.",
			Role = BuildingRole.Wall, BaseHp = 100, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.42f, 0.28f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.WallHeight )
		},
		new()
		{
			Id = BuildableId.Barracks, Key = "barracks", Name = "Barracks", Icon = "groups",
			Description = "Required for recruits — houses 3 soldiers. Cost rises for each one you build.",
			Role = BuildingRole.Management, BaseHp = 200, HpPerLevel = 60,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.7f, 0.6f, 0.45f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 56f ) )
		},
		new()
		{
			Id = BuildableId.Lab, Key = "lab", Name = "Research Lab", Icon = "science",
			Description = "Produces cure lab points (not Knowledge). Unlock with Literacy in the Tech Tree.",
			Role = BuildingRole.Management, BaseHp = 180, HpPerLevel = 50,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.75f, 0.95f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 64f ) )
		},
		new()
		{
			Id = BuildableId.Farm, Key = "farm", Name = "Farm", Icon = "agriculture",
			Description = $"Produces +{CureConstants.FarmFoodPerSec:0.#} food/s. Keep food above zero — unlock with Agriculture.",
			Role = BuildingRole.Civic, BaseHp = 120, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.45f, 0.82f, 0.38f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 48f ) )
		},
		new()
		{
			Id = BuildableId.Factory, Key = "factory", Name = "Factory", Icon = "factory",
			Description = "Boosts supplies, food, and repair throughput. Unlock with Industry in the Tech Tree.",
			Role = BuildingRole.Civic, BaseHp = 150, HpPerLevel = 40,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.72f, 0.58f, 0.42f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 56f ) )
		},
		new()
		{
			Id = BuildableId.Library, Key = "library", Name = "Library", Icon = "menu_book",
			Description = "Modest knowledge income and cheaper tech research. Unlock with Literacy.",
			Role = BuildingRole.Civic, BaseHp = 140, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.55f, 0.72f, 0.95f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 58f ) )
		},
		new()
		{
			Id = BuildableId.School, Key = "school", Name = "School", Icon = "school",
			Description = "Best pure knowledge income. Unlock with Literacy.",
			Role = BuildingRole.Civic, BaseHp = 160, HpPerLevel = 40,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.62f, 0.78f, 0.55f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 60f ) )
		},
		new()
		{
			Id = BuildableId.Hospital, Key = "hospital", Name = "Hospital", Icon = "local_hospital",
			Description = "Heals injured recruits and reduces colony sickness. Unlock with Medicine.",
			Role = BuildingRole.Civic, BaseHp = 180, HpPerLevel = 45,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.95f, 0.95f, 0.98f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 62f ) )
		},
		new()
		{
			Id = BuildableId.Shop, Key = "shop", Name = "Shop", Icon = "storefront",
			Description = $"Earns +{CureConstants.ShopScrapPerSec:0.#} scrap/s. Needed once upkeep outpaces the Command Post.",
			Role = BuildingRole.Civic, BaseHp = 130, HpPerLevel = 30,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.85f, 0.62f, 0.32f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 52f ) )
		},
		new()
		{
			Id = BuildableId.Observatory, Key = "observatory", Name = "Observatory", Icon = "travel_explore",
			Description = $"Skywatch dome — +{CureConstants.ObservatoryKnowledgePerSec:0.#} knowledge/s. Unlock with Surveying.",
			Role = BuildingRole.Civic, BaseHp = 140, HpPerLevel = 35,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.45f, 0.55f, 0.85f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 70f ) )
		},
		new()
		{
			Id = BuildableId.University, Key = "university", Name = "University", Icon = "account_balance",
			Description = $"Campus hall — +{CureConstants.UniversityKnowledgePerSec:0.#} knowledge/s and boosts Research Lab output. Unlock with Academia.",
			Role = BuildingRole.Civic, BaseHp = 200, HpPerLevel = 50,
			BaseDamage = 0, DamagePerLevel = 0, BaseRange = 0, RangePerLevel = 0,
			FireInterval = 0f, Tint = new Color( 0.7f, 0.78f, 0.92f ),
			VisualSize = new Vector3( GameConstants.CellSize, GameConstants.CellSize, GameConstants.H( 68f ) )
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

	public static bool TryGet( BuildableId id, out BuildableDef def )
	{
		def = All.FirstOrDefault( b => b.Id == id );
		return def is not null;
	}

	public static bool TryGet( string key, out BuildableDef def )
	{
		def = null;
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		def = All.FirstOrDefault( b => b.Key == key );
		if ( def is not null )
			return true;

		// Legacy / hot-reload: enum name instead of Key (e.g. "GunTower" vs "gun_tower").
		if ( Enum.TryParse<BuildableId>( key, true, out var id ) )
			return TryGet( id, out def );

		return false;
	}

	public static BuildableDef Get( BuildableId id )
	{
		if ( TryGet( id, out var def ) )
			return def;

		Log.Warning( $"[FinalOutpost] Unknown buildable id {id} — falling back to {All[0].Key}." );
		return All[0];
	}

	public static BuildableDef Get( string key )
	{
		if ( TryGet( key, out var def ) )
			return def;

		Log.Warning( $"[FinalOutpost] Unknown buildable key '{key}' — falling back to {All[0].Key}." );
		return All[0];
	}
}
