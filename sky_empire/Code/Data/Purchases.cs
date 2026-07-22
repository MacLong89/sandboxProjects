namespace SkyEmpire;

public enum PurchaseKind
{
	/// <summary>Spawns orbs onto the belt. Value + Interval used.</summary>
	Dropper,
	/// <summary>Arch over the belt; multiplies orbs passing under it. Mult used.</summary>
	Upgrader,
	/// <summary>Belt motor: faster orbs + small income multiplier.</summary>
	Belt,
	/// <summary>Island decoration with a flat income multiplier.</summary>
	Decor,
	/// <summary>Improves golden orb odds.</summary>
	Charm,
	/// <summary>Flat +X% orb value on every dropper.</summary>
	Value,
	/// <summary>Unlocks the next floor of the sky tower (visual + gating).</summary>
	Floor,
}

/// <summary>One button on the island. The whole tycoon is data in this file.</summary>
public sealed record PurchaseDef(
	string Id,
	string Name,
	PurchaseKind Kind,
	double Cost,
	string Requires,          // id that must be owned first ("" = available at start)
	int Floor,                // which tower floor it belongs to (1..4), for visuals
	double Value = 0,         // dropper: orb value | upgrader: mult | decor/belt/value: +income fraction
	float Interval = 0,       // dropper: seconds between orbs
	float BeltX = 0,          // dropper drop point / upgrader arch position
	string Blurb = "" );

/// <summary>The full build-out of one sky plot, in purchase order.</summary>
public static class PurchaseCatalog
{
	public static readonly PurchaseDef[] All =
	{
		// ---------------- Floor 1 — Meadow Deck ----------------
		new( "d1", "Cloud Dropper", PurchaseKind.Dropper, 0, "", 1, 1, 2.4f, -400f,
			"Your first dropper. It squeezes little value orbs out of clouds." ),
		new( "b1", "Belt Motor I", PurchaseKind.Belt, 20, "d1", 1, 0.08 ),
		new( "d2", "Rain Dropper", PurchaseKind.Dropper, 45, "d1", 1, 3, 2.2f, -330f ),
		new( "u1", "Polisher Arch", PurchaseKind.Upgrader, 120, "d2", 1, 1.5, 0, -160f,
			"Orbs passing under get polished: ×1.5 value." ),
		new( "dec1", "Sky Garden", PurchaseKind.Decor, 220, "u1", 1, 0.05 ),
		new( "d3", "Hail Dropper", PurchaseKind.Dropper, 380, "d2", 1, 7, 2.0f, -260f ),
		new( "u2", "Ionizer Arch", PurchaseKind.Upgrader, 750, "u1", 1, 1.75, 0, -60f ),
		new( "b2", "Belt Motor II", PurchaseKind.Belt, 950, "b1", 1, 0.08 ),
		new( "d4", "Thunder Dropper", PurchaseKind.Dropper, 1_500, "d3", 1, 16, 1.9f, -190f ),
		new( "f2", "FLOOR 2 — Storm Deck", PurchaseKind.Floor, 3_200, "d4", 2,
			Blurb: "Raise your tower into the storm layer. Unlocks a new tier of machines." ),

		// ---------------- Floor 2 — Storm Deck ----------------
		new( "d5", "Storm Dropper", PurchaseKind.Dropper, 4_800, "f2", 2, 42, 1.8f, -120f ),
		new( "u3", "Charger Arch", PurchaseKind.Upgrader, 7_500, "f2", 2, 2.0, 0, 40f ),
		new( "dec2", "Crystal Fountain", PurchaseKind.Decor, 10_000, "u3", 2, 0.08 ),
		new( "b3", "Belt Motor III", PurchaseKind.Belt, 13_000, "b2", 2, 0.08 ),
		new( "d6", "Tempest Dropper", PurchaseKind.Dropper, 19_000, "d5", 2, 105, 1.7f, -50f ),
		new( "u4", "Amplifier Arch", PurchaseKind.Upgrader, 32_000, "u3", 2, 2.5, 0, 140f ),
		new( "g1", "Golden Charm", PurchaseKind.Charm, 48_000, "u4", 2, 1,
			Blurb: "Golden orbs (×20 value) appear more often." ),
		new( "d7", "Cyclone Dropper", PurchaseKind.Dropper, 75_000, "d6", 2, 270, 1.6f, 20f ),
		new( "u5", "Resonator Arch", PurchaseKind.Upgrader, 115_000, "u4", 2, 3.0, 0, 240f ),
		new( "f3", "FLOOR 3 — Aurora Spire", PurchaseKind.Floor, 190_000, "u5", 3,
			Blurb: "Pierce the aurora. The big machines live up here." ),

		// ---------------- Floor 3 — Aurora Spire ----------------
		new( "d8", "Aurora Dropper", PurchaseKind.Dropper, 280_000, "f3", 3, 700, 1.5f, 90f ),
		new( "u6", "Starforge Arch", PurchaseKind.Upgrader, 430_000, "f3", 3, 4.0, 0, 340f ),
		new( "dec3", "Star Beacon", PurchaseKind.Decor, 580_000, "u6", 3, 0.10 ),
		new( "b4", "Belt Motor IV", PurchaseKind.Belt, 740_000, "b3", 3, 0.08 ),
		new( "g2", "Golden Idol", PurchaseKind.Charm, 950_000, "g1", 3, 1 ),
		new( "v1", "Orb Enrichment", PurchaseKind.Value, 1_300_000, "d8", 3, 0.5,
			Blurb: "+50% value on every orb, from every dropper." ),
		new( "dec4", "Cloud Whale", PurchaseKind.Decor, 1_700_000, "dec3", 3, 0.12,
			Blurb: "A gentle giant circles your island. Purely majestic. +12% income." ),
		new( "f4", "FLOOR 4 — Cosmic Crown", PurchaseKind.Floor, 2_400_000, "v1", 4,
			Blurb: "The top of the sky. Almost ready to rebirth." ),

		// ---------------- Floor 4 — Cosmic Crown ----------------
		new( "v2", "Void Infusion", PurchaseKind.Value, 3_200_000, "f4", 4, 0.75 ),
		new( "g3", "Golden Colossus", PurchaseKind.Charm, 4_300_000, "g2", 4, 1 ),
		new( "dec5", "Aurora Ring", PurchaseKind.Decor, 5_800_000, "f4", 4, 0.15 ),
		new( "v3", "Stellar Core", PurchaseKind.Value, 7_800_000, "v2", 4, 1.0 ),
		new( "dec6", "Comet Fountain", PurchaseKind.Decor, 10_500_000, "dec5", 4, 0.20 ),
		new( "crown", "SKY CROWN", PurchaseKind.Value, 15_000_000, "v3", 4, 0.25,
			Blurb: "The island is complete. Wear the crown — then rebirth for +50% forever." ),
	};

	static readonly Dictionary<string, PurchaseDef> _byId = All.ToDictionary( p => p.Id );
	public static PurchaseDef Get( string id ) => _byId.GetValueOrDefault( id );

	/// <summary>Buttons currently visible: not owned, prerequisite owned.</summary>
	public static IEnumerable<PurchaseDef> Available( ICollection<string> owned ) =>
		All.Where( p => !owned.Contains( p.Id )
			&& (p.Requires == "" || owned.Contains( p.Requires )) );

	/// <summary>Golden orb chance including charms.</summary>
	public static double GoldenChance( ICollection<string> owned )
	{
		var charms = All.Count( p => p.Kind == PurchaseKind.Charm && owned.Contains( p.Id ) );
		return charms switch
		{
			0 => Balance.GoldenBaseChance,
			1 => 1.0 / 45.0,
			2 => 1.0 / 30.0,
			_ => 1.0 / 18.0,
		};
	}

	/// <summary>Product of arch multipliers a given drop point passes under.</summary>
	public static double ArchMultFor( float dropX, ICollection<string> owned )
	{
		double mult = 1.0;
		foreach ( var p in All )
			if ( p.Kind == PurchaseKind.Upgrader && owned.Contains( p.Id ) && p.BeltX > dropX )
				mult *= p.Value;
		return mult;
	}

	/// <summary>Flat island income multiplier from decor, belts, and value upgrades.</summary>
	public static double IslandMult( ICollection<string> owned )
	{
		double mult = 1.0;
		foreach ( var p in All )
		{
			if ( !owned.Contains( p.Id ) ) continue;
			if ( p.Kind is PurchaseKind.Decor or PurchaseKind.Belt or PurchaseKind.Value )
				mult *= 1.0 + p.Value;
		}
		return mult;
	}

	public static int BeltMotorCount( ICollection<string> owned ) =>
		All.Count( p => p.Kind == PurchaseKind.Belt && owned.Contains( p.Id ) );

	/// <summary>Average income per second for the island — used for offline pay and the HUD rate.</summary>
	public static double IncomePerSecond( ICollection<string> owned, double rebirthMult )
	{
		double total = 0;
		var golden = GoldenChance( owned );
		var goldenAvg = 1.0 + golden * (Balance.GoldenValueMult - 1.0);
		foreach ( var p in All )
		{
			if ( p.Kind != PurchaseKind.Dropper || !owned.Contains( p.Id ) ) continue;
			total += p.Value / p.Interval * ArchMultFor( p.BeltX, owned );
		}
		return total * IslandMult( owned ) * goldenAvg * rebirthMult;
	}
}

/// <summary>Rebirth tiers re-skin the whole island so progress is visible from across the sky.</summary>
public sealed record RebirthTierDef( string Name, Color Grass, Color Cliff, Color Accent, Color Flame );

public static class RebirthCatalog
{
	public static readonly RebirthTierDef[] Tiers =
	{
		new( "Meadow Sky", Color.Parse( "#6fbf5c" ) ?? Color.Green, Color.Parse( "#8a6f4d" ) ?? Color.Orange, Color.Parse( "#ffd24a" ) ?? Color.Yellow, Color.Parse( "#ff9d3c" ) ?? Color.Orange ),
		new( "Sunset Reach", Color.Parse( "#c98f4e" ) ?? Color.Orange, Color.Parse( "#7d4f38" ) ?? Color.Red, Color.Parse( "#ff7a5c" ) ?? Color.Red, Color.Parse( "#ff5c39" ) ?? Color.Red ),
		new( "Storm Shelf", Color.Parse( "#5f7d8f" ) ?? Color.Blue, Color.Parse( "#3d4b57" ) ?? Color.Gray, Color.Parse( "#8ae0ff" ) ?? Color.Cyan, Color.Parse( "#63c8ff" ) ?? Color.Cyan ),
		new( "Aurora Steppe", Color.Parse( "#4fae9c" ) ?? Color.Green, Color.Parse( "#3e5c66" ) ?? Color.Blue, Color.Parse( "#b48aff" ) ?? Color.Magenta, Color.Parse( "#9c6bff" ) ?? Color.Magenta ),
		new( "Cosmic Isle", Color.Parse( "#5c4a80" ) ?? Color.Magenta, Color.Parse( "#2e2640" ) ?? Color.Black, Color.Parse( "#ff8ae0" ) ?? Color.Magenta, Color.Parse( "#ff63d1" ) ?? Color.Magenta ),
		new( "Golden Empire", Color.Parse( "#c9a94e" ) ?? Color.Yellow, Color.Parse( "#8a6f2e" ) ?? Color.Orange, Color.Parse( "#fff1b0" ) ?? Color.White, Color.Parse( "#ffd24a" ) ?? Color.Yellow ),
	};

	public static RebirthTierDef Tier( int rebirths ) =>
		Tiers[Math.Clamp( rebirths, 0, Tiers.Length - 1 )];

	public static double Cost( int rebirths ) =>
		Balance.RebirthBaseCost * Math.Pow( Balance.RebirthCostGrowth, rebirths );

	public static double IncomeMult( int rebirths ) =>
		1.0 + rebirths * Balance.RebirthIncomeBonus;
}
