namespace PawnShop;

/// <summary>
/// THE single authority on item value. Everything (offers, estimates, buyer budgets,
/// pawn loans) routes through here so numbers never disagree.
/// </summary>
public static class ItemValue
{
	/// <summary>The real market value of the item, all truth included.</summary>
	public static int TrueValue( ItemInstance item, GameManager game = null )
	{
		var def = item.Def;
		if ( def is null ) return 10;

		double v = def.BaseValue;
		v *= item.Condition.ConditionMult();
		v *= item.Rarity.RarityMult();

		v *= item.TrueAuthenticity switch
		{
			Authenticity.Counterfeit => 0.06,
			Authenticity.Replica => 0.22,
			Authenticity.Altered => 0.50,
			_ => 1.0,
		};

		// Defect penalties (positives raise value).
		var penalty = 0f;
		foreach ( var id in item.Defects )
		{
			var d = DefectCatalog.Get( id );
			if ( d is not null ) penalty += d.ValuePenalty;
		}
		v *= Math.Clamp( 1f - penalty, 0.2f, 2.2f );

		// Dirt knocks up to 15% off until cleaned.
		v *= 1f - item.Dirtiness * 0.15f;

		// Today's demand for the category.
		v *= game?.Events.DemandFor( def.Category ) ?? 1f;

		return Math.Max( 5, (int)Math.Round( v / 5.0 ) * 5 );
	}

	/// <summary>
	/// What the player can currently estimate: value using only discovered knowledge.
	/// Undiscovered problems make this estimate optimistic — that's the game.
	/// </summary>
	public static int KnownValue( ItemInstance item, GameManager game = null )
	{
		var def = item.Def;
		if ( def is null ) return 10;

		double v = def.BaseValue;
		v *= item.Condition.ConditionMult();
		v *= (item.RarityKnown ? item.Rarity : Rarity.Common).RarityMult();

		if ( item.AuthenticityKnown )
		{
			v *= item.TrueAuthenticity switch
			{
				Authenticity.Counterfeit => 0.06,
				Authenticity.Replica => 0.22,
				Authenticity.Altered => 0.50,
				_ => 1.0,
			};
		}

		var penalty = 0f;
		foreach ( var id in item.DiscoveredDefects )
		{
			var d = DefectCatalog.Get( id );
			if ( d is not null ) penalty += d.ValuePenalty;
		}
		v *= Math.Clamp( 1f - penalty, 0.2f, 2.2f );

		v *= 1f - item.Dirtiness * 0.15f;
		v *= game?.Events.DemandFor( def.Category ) ?? 1f;

		return Math.Max( 5, (int)Math.Round( v / 5.0 ) * 5 );
	}

	/// <summary>0..1 — how thoroughly the item has been inspected.</summary>
	public static float InspectionCoverage( ItemInstance item )
	{
		var spots = InspectionModel.SpotsFor( item );
		if ( spots.Count == 0 ) return 1f;
		return (float)item.CheckedSpots.Count( i => i >= 0 && i < spots.Count ) / spots.Count;
	}

	/// <summary>Estimate range shown to the player. Narrows as inspection coverage rises.</summary>
	public static (int Low, int High, string Confidence) Estimate( ItemInstance item, GameManager game = null )
	{
		var known = KnownValue( item, game );
		var coverage = InspectionCoverage( item );
		if ( item.Researched ) coverage = Math.Max( coverage, 0.9f );

		var spread = 0.42f + (0.06f - 0.42f) * coverage;
		var low = (int)(known * (1f - spread));
		var high = (int)(known * (1f + spread));

		var confidence = coverage switch
		{
			>= 0.99f => "High",
			>= 0.6f => "Medium",
			>= 0.3f => "Low",
			_ => "Very Low",
		};

		low = Math.Max( 5, low / 5 * 5 );
		high = Math.Max( low + 5, high / 5 * 5 );
		return (low, high, confidence);
	}

	/// <summary>Suggested sticker price for a healthy margin.</summary>
	public static int SuggestedPrice( ItemInstance item, GameManager game = null )
	{
		var v = TrueValueIfKnownElseKnown( item, game );
		return Math.Max( 5, (int)Math.Round( v * 1.15 / 5.0 ) * 5 );
	}

	/// <summary>Value basis for pricing: use full truth only for fully-inspected/researched items.</summary>
	private static int TrueValueIfKnownElseKnown( ItemInstance item, GameManager game )
	{
		return InspectionCoverage( item ) >= 0.99f || item.Researched
			? TrueValue( item, game )
			: KnownValue( item, game );
	}
}

/// <summary>One clickable inspection point on an item.</summary>
public sealed class InspectionSpot
{
	public string Label { get; init; }
	public string Icon { get; init; }
	/// <summary>Defect revealed when checked with the right tool, or null for nothing.</summary>
	public string DefectId { get; init; }
	/// <summary>Tool needed to get a conclusive read on this spot.</summary>
	public InspectTool Tool { get; init; } = InspectTool.Eyes;
	/// <summary>Position (percent) on the inspection card.</summary>
	public float X { get; init; }
	public float Y { get; init; }
}

/// <summary>
/// Builds the deterministic inspection layout for an item instance: every actual defect
/// gets a spot (hidden ones demand their reveal tool), padded with decoy spots.
/// </summary>
public static class InspectionModel
{
	private static readonly (string Label, string Icon)[] Decoys =
	{
		("Surface", "texture"), ("Label", "label"), ("Underside", "flip_to_back"),
		("Edges", "crop_square"), ("Fittings", "handyman"), ("Markings", "fingerprint"),
	};

	public static List<InspectionSpot> SpotsFor( ItemInstance item )
	{
		// Deterministic per instance so the layout never reshuffles.
		var rng = new Random( item.Id * 7919 + 13 );
		var spots = new List<InspectionSpot>();

		foreach ( var defectId in item.Defects )
		{
			var d = DefectCatalog.Get( defectId );
			if ( d is null ) continue;
			spots.Add( new InspectionSpot
			{
				Label = SpotLabel( d, rng ),
				Icon = d.Icon,
				DefectId = defectId,
				Tool = d.RevealTool,
			} );
		}

		// Pad with decoys up to 4-6 spots.
		var target = Math.Clamp( spots.Count + 2, 4, 6 );
		var decoyPool = Decoys.OrderBy( _ => rng.Next() ).ToList();
		while ( spots.Count < target && decoyPool.Count > 0 )
		{
			var (label, icon) = decoyPool[0];
			decoyPool.RemoveAt( 0 );
			var tools = ToolsForItem( item );
			spots.Add( new InspectionSpot
			{
				Label = label,
				Icon = icon,
				DefectId = null,
				Tool = tools[rng.Next( tools.Count )],
			} );
		}

		// Stable shuffle + positions scattered over the card.
		spots = spots.OrderBy( _ => rng.Next() ).ToList();
		var positioned = new List<InspectionSpot>();
		for ( var i = 0; i < spots.Count; i++ )
		{
			var col = i % 3;
			var row = i / 3;
			positioned.Add( new InspectionSpot
			{
				Label = spots[i].Label,
				Icon = spots[i].Icon,
				DefectId = spots[i].DefectId,
				Tool = spots[i].Tool,
				X = 16f + col * 30f + rng.Next( -5, 6 ),
				Y = 24f + row * 34f + rng.Next( -6, 7 ),
			} );
		}

		return positioned;
	}

	private static List<InspectTool> ToolsForItem( ItemInstance item )
	{
		var def = item.Def;
		var list = new List<InspectTool> { InspectTool.Eyes, InspectTool.Magnifier, InspectTool.UvLight };
		if ( def?.HasElectronics == true ) list.Add( InspectTool.ElectronicsTester );
		if ( def?.HasMetal == true ) list.Add( InspectTool.MetalTester );
		if ( def?.HasGem == true ) list.Add( InspectTool.GemTester );
		return list;
	}

	private static string SpotLabel( DefectDef d, Random rng )
	{
		return d.RevealTool switch
		{
			InspectTool.Eyes => new[] { "Body", "Surface", "Corner", "Front Face" }[rng.Next( 4 )],
			InspectTool.Magnifier => new[] { "Fine Detail", "Engraving", "Serial Plate", "Seams" }[rng.Next( 4 )],
			InspectTool.ElectronicsTester => new[] { "Power Port", "Internals", "Battery Bay" }[rng.Next( 3 )],
			InspectTool.MetalTester => new[] { "Metal Body", "Clasp", "Band" }[rng.Next( 3 )],
			InspectTool.GemTester => new[] { "Main Stone", "Setting", "Side Stones" }[rng.Next( 3 )],
			InspectTool.UvLight => new[] { "Under UV", "Coating", "Back Panel" }[rng.Next( 3 )],
			InspectTool.Database => new[] { "Serial Number", "Model Number" }[rng.Next( 2 )],
			_ => "Detail",
		};
	}
}
