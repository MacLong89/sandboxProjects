namespace PawnShop;

/// <summary>
/// A concrete item in the world. Fully serializable — this is what gets saved.
/// Hidden truth (real authenticity, undiscovered defects) lives here; what the player
/// KNOWS is tracked by the Discovered* fields.
/// </summary>
public sealed class ItemInstance
{
	public int Id { get; set; }
	public string DefId { get; set; }

	// --- Truth ---
	public Condition Condition { get; set; } = Condition.Good;
	public Authenticity TrueAuthenticity { get; set; } = Authenticity.Genuine;
	public Rarity Rarity { get; set; } = Rarity.Common;
	public LegalStatus LegalStatus { get; set; } = LegalStatus.Clean;
	public List<string> Defects { get; set; } = new();
	/// <summary>0..1 grime level.</summary>
	public float Dirtiness { get; set; }
	public int AgeYears { get; set; }

	// --- Player knowledge ---
	public List<string> DiscoveredDefects { get; set; } = new();
	public bool AuthenticityKnown { get; set; }
	public bool RarityKnown { get; set; }
	public bool LegalStatusKnown { get; set; }
	public bool Researched { get; set; }
	/// <summary>Inspection hotspots already checked (index → tool used).</summary>
	public List<int> CheckedSpots { get; set; } = new();

	// --- Commerce ---
	public ItemLocation Location { get; set; } = ItemLocation.CustomerOwned;
	public int PurchasePrice { get; set; }
	public int RepairInvested { get; set; }
	public int SalePrice { get; set; }
	public bool NotForSale { get; set; }
	public int DayAcquired { get; set; }
	public int DisplaySlot { get; set; } = -1;
	public bool Cleaned { get; set; }
	public int RepairsDone { get; set; }

	public ItemDef Def => ItemCatalog.Get( DefId );

	public string Name => Def?.Name ?? "Unknown Item";

	public int TotalInvested => PurchasePrice + RepairInvested;

	public bool HasUndiscoveredDefects => Defects.Any( d => !DiscoveredDefects.Contains( d ) );

	public IEnumerable<DefectDef> DiscoveredDefectDefs =>
		DiscoveredDefects.Select( DefectCatalog.Get ).Where( d => d is not null );

	public bool KnownCounterfeit => AuthenticityKnown && TrueAuthenticity is Authenticity.Counterfeit or Authenticity.Replica or Authenticity.Altered;
	public bool KnownStolen => LegalStatusKnown && LegalStatus == LegalStatus.Stolen;

	/// <summary>Displayed authenticity — Unknown until proven.</summary>
	public Authenticity KnownAuthenticity => AuthenticityKnown ? TrueAuthenticity : Authenticity.Unknown;

	public bool CanRepair => Defects.Any( d => DefectCatalog.Get( d )?.Repairable == true && DiscoveredDefects.Contains( d ) )
		|| Condition < Condition.Good;

	public void DiscoverDefect( string defectId )
	{
		if ( !DiscoveredDefects.Contains( defectId ) )
			DiscoveredDefects.Add( defectId );

		var def = DefectCatalog.Get( defectId );
		if ( def is null ) return;
		if ( def.CounterfeitSign ) AuthenticityKnown = true;
		if ( def.StolenSign ) LegalStatusKnown = true;
	}
}

/// <summary>Rolls new item instances for customers, with archetype and difficulty modifiers.</summary>
public static class ItemFactory
{
	/// <summary>Create a random item for a customer of the given archetype on the given day.</summary>
	public static ItemInstance Roll( int id, ArchetypeDef archetype, int day, float scamMult, ItemCategory[] favorites = null )
	{
		ItemDef def;
		if ( favorites is { Length: > 0 } && Game.Random.Float() < 0.6f )
		{
			var cat = favorites[Game.Random.Int( 0, favorites.Length - 1 )];
			def = ItemCatalog.Random( d => d.Category == cat );
		}
		else
		{
			// Later days skew toward pricier stock.
			var minValue = day >= 8 ? 250 : day >= 4 ? 150 : 0;
			def = ItemCatalog.Random( d => d.BaseValue >= minValue || Game.Random.Float() < 0.35f );
		}

		return RollFromDef( id, def, archetype, day, scamMult );
	}

	public static ItemInstance RollFromDef( int id, ItemDef def, ArchetypeDef archetype, int day, float scamMult )
	{
		var item = new ItemInstance { Id = id, DefId = def.Id };

		// Condition: bell around Good, worse for desperate sellers.
		var roll = Game.Random.Float();
		item.Condition = roll switch
		{
			< 0.06f => Condition.Broken,
			< 0.18f => Condition.Poor,
			< 0.42f => Condition.Fair,
			< 0.78f => Condition.Good,
			< 0.94f => Condition.Excellent,
			_ => Condition.Mint,
		};

		// Rarity, capped by the def.
		var rRoll = Game.Random.Float();
		var rarity = rRoll switch
		{
			< 0.62f => Rarity.Common,
			< 0.85f => Rarity.Uncommon,
			< 0.955f => Rarity.Rare,
			< 0.99f => Rarity.VeryRare,
			_ => Rarity.Legendary,
		};
		if ( rarity > def.MaxRarity ) rarity = def.MaxRarity;
		item.Rarity = rarity;

		// Authenticity. Counterfeits get more convincing (no change to chance, but
		// scammer archetypes multiply base chance).
		var fakeChance = Math.Clamp( def.CounterfeitChance * archetype.CounterfeitMult * scamMult, 0f, 0.85f );
		if ( Game.Random.Float() < fakeChance )
		{
			item.TrueAuthenticity = Game.Random.Float() < 0.7f ? Authenticity.Counterfeit
				: Game.Random.Float() < 0.5f ? Authenticity.Replica : Authenticity.Altered;
			item.Rarity = Rarity.Common;

			// Guarantee at least one counterfeit sign is present so fakes are always detectable.
			var signs = def.DefectPool.Where( p => DefectCatalog.Get( p )?.CounterfeitSign == true ).ToList();
			if ( signs.Count == 0 )
				signs = new List<string> { def.HasMetal ? "wrong_alloy" : def.HasElectronics ? "gutted" : "fake_print" };
			item.Defects.Add( signs[Game.Random.Int( 0, signs.Count - 1 )] );
		}

		// Stolen?
		if ( Game.Random.Float() < archetype.StolenChance )
		{
			item.LegalStatus = LegalStatus.Stolen;
			if ( !item.Defects.Contains( "scratched_serial" ) && !item.Defects.Contains( "security_mark" ) && !item.Defects.Contains( "flagged_serial" ) )
				item.Defects.Add( Game.Random.Float() < 0.5f ? "scratched_serial" : Game.Random.Float() < 0.5f ? "security_mark" : "flagged_serial" );
		}
		else if ( archetype.Id == Archetype.SuspiciousSeller )
		{
			item.LegalStatus = LegalStatus.Suspicious;
		}

		// Regular defects: 0-3 rolls from the pool, more likely on worse condition.
		var defectChance = item.Condition switch
		{
			Condition.Broken => 0.9f,
			Condition.Poor => 0.7f,
			Condition.Fair => 0.5f,
			Condition.Good => 0.32f,
			Condition.Excellent => 0.18f,
			_ => 0.08f,
		};

		var pool = def.DefectPool.Where( p =>
		{
			var d = DefectCatalog.Get( p );
			return d is not null && !d.CounterfeitSign && !d.StolenSign && !item.Defects.Contains( p );
		} ).ToList();

		for ( var i = 0; i < 3 && pool.Count > 0; i++ )
		{
			if ( Game.Random.Float() >= defectChance ) break;
			var pick = pool[Game.Random.Int( 0, pool.Count - 1 )];
			pool.Remove( pick );

			var d = DefectCatalog.Get( pick );
			// Positive traits are rarer and skip fakes.
			if ( d.IsPositive && (item.TrueAuthenticity != Authenticity.Genuine || Game.Random.Float() > 0.35f) )
				continue;

			item.Defects.Add( pick );
			defectChance *= 0.55f;
		}

		item.Dirtiness = item.Condition <= Condition.Fair
			? Game.Random.Float( 0.3f, 1f )
			: Game.Random.Float( 0f, 0.5f );

		item.AgeYears = def.Category is ItemCategory.Antiques or ItemCategory.Art ? Game.Random.Int( 40, 140 )
			: def.Category is ItemCategory.Collectibles or ItemCategory.Memorabilia ? Game.Random.Int( 10, 60 )
			: Game.Random.Int( 0, 12 );

		return item;
	}
}
