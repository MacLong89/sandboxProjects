namespace FinalOutpost;

/// <summary>Horizontal track in the left-to-right tech tree.</summary>
public enum TechTrack
{
	Defense,
	Colony,
	Science
}

public sealed class TechNodeDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Description { get; init; }
	public string[] Prerequisites { get; init; } = Array.Empty<string>();
	public double KnowledgeCost { get; init; }
	public string[] UnlocksBuildings { get; init; } = Array.Empty<string>();
	/// <summary>0 = Foundations … 3 = Outreach. Cost and section gates use this.</summary>
	public int Column { get; init; }
	/// <summary>Which parallel path this node sits on.</summary>
	public TechTrack Track { get; init; }
	/// <summary>Vertical order within the same track + column (top → bottom).</summary>
	public int TrackOrder { get; init; }
}

public static class TechTreeCatalog
{
	/// <summary>Knowledge cost by column: Foundations 5 → Expansion 10 → Industry 20 → Outreach 40.</summary>
	public static double BaseCostForColumn( int column ) => column switch
	{
		0 => 5,
		1 => 10,
		2 => 20,
		_ => 40
	};

	public static readonly IReadOnlyList<TechNodeDef> All = new List<TechNodeDef>
	{
		// --- Defense track ---
		new()
		{
			Id = "tactics", Name = "Field Tactics", Icon = "military_tech",
			Description = "Cannons, SMGs, Assault Rifles, and short scout trips.",
			KnowledgeCost = BaseCostForColumn( 0 ), UnlocksBuildings = new[] { "cannon" },
			Column = 0, Track = TechTrack.Defense, TrackOrder = 0
		},
		new()
		{
			Id = "marksmanship", Name = "Marksmanship", Icon = "radar",
			Description = "Long Range towers, Shotguns, and Snipers.",
			Prerequisites = new[] { "tactics" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "sniper" },
			Column = 1, Track = TechTrack.Defense, TrackOrder = 0
		},
		new()
		{
			Id = "logistics", Name = "Logistics", Icon = "inventory_2",
			Description = "Ammo Depots — +25% fire rate on that plot.",
			Prerequisites = new[] { "tactics" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "ammo_depot" },
			Column = 1, Track = TechTrack.Defense, TrackOrder = 1
		},
		new()
		{
			Id = "optics", Name = "Optics", Icon = "lightbulb",
			Description = "Spotlight supports — +50% range on that plot.",
			Prerequisites = new[] { "tactics" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "spotlight" },
			Column = 1, Track = TechTrack.Defense, TrackOrder = 2
		},
		new()
		{
			Id = "fortification", Name = "Fortification", Icon = "security",
			Description = "Hardpoints — 20% less damage taken on that plot.",
			Prerequisites = new[] { "tactics" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "hardpoint" },
			Column = 1, Track = TechTrack.Defense, TrackOrder = 3
		},
		new()
		{
			Id = "signals", Name = "Signals", Icon = "cell_tower",
			Description = "Radio Masts — faster recruits and quicker acquire.",
			Prerequisites = new[] { "tactics" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "radio_mast" },
			Column = 1, Track = TechTrack.Defense, TrackOrder = 4
		},
		new()
		{
			Id = "siege", Name = "Siege Engineering", Icon = "rocket_launch",
			Description = "Artillery — massive splash howitzers.",
			Prerequisites = new[] { "marksmanship", "industry" }, KnowledgeCost = BaseCostForColumn( 2 ),
			UnlocksBuildings = new[] { "artillery" },
			Column = 2, Track = TechTrack.Defense, TrackOrder = 0
		},

		// --- Colony track ---
		new()
		{
			Id = "agriculture", Name = "Agriculture", Icon = "agriculture",
			Description = $"Farms (+{CureConstants.FarmFoodPerSec:0.#} food/s), Farmers, and Foragers.",
			KnowledgeCost = BaseCostForColumn( 0 ), UnlocksBuildings = new[] { "farm" },
			Column = 0, Track = TechTrack.Colony, TrackOrder = 0
		},
		new()
		{
			Id = "industry", Name = "Industry", Icon = "factory",
			Description = "Factories and Operators.",
			Prerequisites = new[] { "agriculture" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "factory" },
			Column = 1, Track = TechTrack.Colony, TrackOrder = 0
		},
		new()
		{
			Id = "medicine", Name = "Medicine", Icon = "local_hospital",
			Description = "Hospitals and Medics.",
			Prerequisites = new[] { "agriculture" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "hospital" },
			Column = 1, Track = TechTrack.Colony, TrackOrder = 1
		},
		new()
		{
			Id = "commerce", Name = "Commerce", Icon = "storefront",
			Description = "Shops and Merchants — scrap to cover upkeep.",
			Prerequisites = new[] { "industry" }, KnowledgeCost = BaseCostForColumn( 2 ),
			UnlocksBuildings = new[] { "shop" },
			Column = 2, Track = TechTrack.Colony, TrackOrder = 0
		},
		new()
		{
			Id = "demolitions", Name = "Demolitions", Icon = "crisis_alert",
			Description = "Minefields — cheap zombie-only splash.",
			Prerequisites = new[] { "industry" }, KnowledgeCost = BaseCostForColumn( 2 ),
			UnlocksBuildings = new[] { "mines" },
			Column = 2, Track = TechTrack.Colony, TrackOrder = 1
		},
		new()
		{
			Id = "petroleum", Name = "Petroleum", Icon = "water_drop",
			Description = "Oil Slick supports that slow the horde.",
			Prerequisites = new[] { "industry" }, KnowledgeCost = BaseCostForColumn( 2 ),
			UnlocksBuildings = new[] { "oil_slick" },
			Column = 2, Track = TechTrack.Colony, TrackOrder = 2
		},
		new()
		{
			Id = "diplomacy", Name = "Diplomacy", Icon = "handshake",
			Description = "Better civ trades and long expeditions.",
			Prerequisites = new[] { "commerce" }, KnowledgeCost = BaseCostForColumn( 3 ),
			Column = 3, Track = TechTrack.Colony, TrackOrder = 0
		},

		// --- Science track (one node per column; path gates left→right within this track) ---
		new()
		{
			Id = "literacy", Name = "Literacy", Icon = "menu_book",
			Description = "Libraries, Schools, Research Labs, and Scholars.",
			KnowledgeCost = BaseCostForColumn( 0 ), UnlocksBuildings = new[] { "library", "school", "lab" },
			Column = 0, Track = TechTrack.Science, TrackOrder = 0
		},
		new()
		{
			Id = "surveying", Name = "Surveying", Icon = "travel_explore",
			Description = "Observatories — steady knowledge from skywatching.",
			Prerequisites = new[] { "literacy" }, KnowledgeCost = BaseCostForColumn( 1 ),
			UnlocksBuildings = new[] { "observatory" },
			Column = 1, Track = TechTrack.Science, TrackOrder = 0
		},
		new()
		{
			Id = "academia", Name = "Academia", Icon = "account_balance",
			Description = "Universities — strong knowledge income and better lab output.",
			Prerequisites = new[] { "surveying" }, KnowledgeCost = BaseCostForColumn( 2 ),
			UnlocksBuildings = new[] { "university" },
			Column = 2, Track = TechTrack.Science, TrackOrder = 0
		},
		new()
		{
			Id = "synthesis", Name = "Advanced Synthesis", Icon = "science",
			Description = "+25% knowledge income and Research Lab output.",
			Prerequisites = new[] { "academia", "medicine" }, KnowledgeCost = BaseCostForColumn( 3 ),
			Column = 3, Track = TechTrack.Science, TrackOrder = 0
		}
	};

	public static readonly IReadOnlyList<(TechTrack Track, string Label, string Icon)> Tracks = new List<(TechTrack, string, string)>
	{
		(TechTrack.Defense, "Defense", "military_tech"),
		(TechTrack.Colony, "Colony", "home"),
		(TechTrack.Science, "Science", "science")
	};

	public static readonly IReadOnlyList<string> ColumnLabels = new[]
	{
		"Foundations",
		"Expansion",
		"Industry",
		"Outreach"
	};

	public static int MaxColumn => All.Max( n => n.Column );

	public static IEnumerable<TechNodeDef> NodesIn( TechTrack track, int column ) =>
		All.Where( n => n.Track == track && n.Column == column ).OrderBy( n => n.TrackOrder );

	public static bool HasUnlockedInTrackColumn( SaveData save, TechTrack track, int column )
	{
		if ( save is null || column < 0 ) return false;
		foreach ( var n in All )
		{
			if ( n.Track != track || n.Column != column ) continue;
			if ( IsUnlocked( save, n.Id ) ) return true;
		}
		return false;
	}

	/// <summary>
	/// Within a track, column 0 is open; later columns need ≥1 unlocked tech
	/// in the same track one column to the left.
	/// </summary>
	public static bool TrackColumnUnlocked( SaveData save, TechTrack track, int column ) =>
		column <= 0 || HasUnlockedInTrackColumn( save, track, column - 1 );

	public static string TrackColumnGateLabel( TechTrack track, int column )
	{
		if ( column <= 0 ) return null;
		var prev = column - 1;
		var colName = prev < ColumnLabels.Count ? ColumnLabels[prev] : $"tier {prev + 1}";
		var trackName = Tracks.FirstOrDefault( t => t.Track == track ).Label ?? "this path";
		return $"Research {trackName} · {colName} first";
	}

	public static TechNodeDef Get( string id ) => All.FirstOrDefault( n => n.Id == id );

	public static bool IsUnlocked( SaveData save, string id ) =>
		save?.UnlockedTech?.Contains( id ) == true;

	public static bool CanResearch( GameCore core, TechNodeDef node )
	{
		if ( core?.Save is null || node is null ) return false;
		if ( IsUnlocked( core.Save, node.Id ) ) return false;
		if ( !TrackColumnUnlocked( core.Save, node.Track, node.Column ) ) return false;

		foreach ( var pre in node.Prerequisites )
			if ( !IsUnlocked( core.Save, pre ) ) return false;

		return core.Resources.Get( ResourceKind.Knowledge ) >= EffectiveKnowledgeCost( core, node );
	}

	public static bool TryUnlock( GameCore core, string id )
	{
		var node = Get( id );
		if ( node is null || !CanResearch( core, node ) ) return false;

		var cost = EffectiveKnowledgeCost( core, node );
		if ( cost > 0 && !core.Resources.TrySpend( ResourceKind.Knowledge, cost ) )
			return false;

		core.Save.UnlockedTech ??= new List<string>();
		if ( !core.Save.UnlockedTech.Contains( id ) )
			core.Save.UnlockedTech.Add( id );

		core.SaveManagerTouch();
		Sfx.Play( Sfx.Purchase, "TechUnlock" );
		return true;
	}

	public static bool BuildingUnlockedByTech( SaveData save, string buildingKey )
	{
		if ( save is null ) return false;
		foreach ( var node in All )
		{
			if ( !IsUnlocked( save, node.Id ) ) continue;
			if ( node.UnlocksBuildings.Contains( buildingKey ) ) return true;
		}
		return false;
	}

	public static string GateLabelForBuilding( SaveData save, string buildingKey )
	{
		if ( BuildingUnlockedByTech( save, buildingKey ) )
			return "Unlocked";

		var node = NodeForBuilding( buildingKey );
		if ( node is not null )
			return $"Tech: {node.Name}";

		return "Tech Locked";
	}

	/// <summary>Why a tech node cannot be researched yet — shown in the Tech Tree UI.</summary>
	public static string LockReason( GameCore core, TechNodeDef node )
	{
		if ( core?.Save is null || node is null ) return "Locked";
		if ( IsUnlocked( core.Save, node.Id ) ) return "Researched";

		if ( !TrackColumnUnlocked( core.Save, node.Track, node.Column ) )
			return TrackColumnGateLabel( node.Track, node.Column );

		var missing = new List<string>();
		foreach ( var pre in node.Prerequisites )
		{
			if ( IsUnlocked( core.Save, pre ) ) continue;
			var preNode = Get( pre );
			missing.Add( preNode?.Name ?? pre );
		}

		if ( missing.Count > 0 )
			return missing.Count == 1 ? $"Needs {missing[0]}" : $"Needs {string.Join( " + ", missing )}";

		var knowledge = core.Resources.Get( ResourceKind.Knowledge );
		var cost = EffectiveKnowledgeCost( core, node );
		if ( knowledge < cost )
			return $"Need {cost:0} Knowledge";

		return "Research";
	}

	public static TechNodeDef NodeForBuilding( string buildingKey ) =>
		All.FirstOrDefault( n => n.UnlocksBuildings.Contains( buildingKey ) );

	/// <summary>Knowledge cost after Library discounts (each Library ×0.9, floor 0.7).</summary>
	public static double EffectiveKnowledgeCost( GameCore core, TechNodeDef node )
	{
		if ( node is null ) return 0;
		var baseCost = BaseCostForColumn( node.Column );
		var mult = LibraryTechCostMult( core );
		return Math.Ceiling( baseCost * mult );
	}

	public static float LibraryTechCostMult( GameCore core )
	{
		if ( core?.Build is null ) return 1f;
		var libraries = 0;
		foreach ( var b in core.Build.Buildings )
		{
			if ( b?.Def?.Id == BuildableId.Library )
				libraries++;
		}

		if ( libraries <= 0 ) return 1f;

		var mult = MathF.Pow( CureConstants.LibraryTechCostMultPer, libraries );
		return MathF.Max( CureConstants.LibraryTechCostMultFloor, mult );
	}
}
