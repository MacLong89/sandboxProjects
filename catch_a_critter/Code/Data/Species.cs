namespace CatchACritter;

public enum Biome { Meadow, Forest, Beach, Cavern, Tundra, Volcano, Mythwood }
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }
public enum BodyShape { Round, Tall, Long, Chunky }
public enum EarStyle { None, Pointy, Round, Long, Horn, Antenna }
public enum TailStyle { None, Nub, Bushy, Long, Curl, Fin }

public sealed record SpeciesDef(
	string Id,
	string Name,
	Biome Biome,
	Rarity Rarity,
	BodyShape Body,
	EarStyle Ears,
	TailStyle Tail,
	string PrimaryHex,
	string SecondaryHex,
	float Size )
{
	public Color Primary => Color.Parse( PrimaryHex ) ?? Color.White;
	public Color Secondary => Color.Parse( SecondaryHex ) ?? Color.Gray;

	/// <summary>Coins awarded when sold (before multipliers).</summary>
	public double BaseValue => BiomeCatalog.Get( Biome ).BaseValue * Balance.RarityValue[(int)Rarity];

	/// <summary>
	/// Net power needed to catch. A biome's commons are catchable with the net
	/// tier whose cost roughly matches the biome's gate price; epics and up in
	/// each biome ask for one or two tiers more.
	/// </summary>
	public int RequiredNetPower => Math.Clamp(
		(int)MathF.Floor( (int)Biome * 1.4f )
		+ (Rarity >= Rarity.Epic ? 1 : 0)
		+ (Rarity >= Rarity.Legendary ? 1 : 0),
		0, 11 );

	/// <summary>How far away this critter starts fleeing from a running player.</summary>
	public float FleeRadius => 110f + (int)Rarity * 42f;
	public float FleeSpeed => 120f + (int)Rarity * 34f;
}

public sealed record BiomeDef(
	Biome Id,
	string Name,
	string Blurb,
	double GateCost,
	double BaseValue,
	string GroundHex,
	string AccentHex,
	string PropHex,
	Vector2 Center,
	float Radius )
{
	public Color Ground => Color.Parse( GroundHex ) ?? Color.Green;
	public Color Accent => Color.Parse( AccentHex ) ?? Color.White;
	public Color Prop => Color.Parse( PropHex ) ?? Color.Gray;
}

public static class BiomeCatalog
{
	// Island fans out from the hub at origin. Angles chosen so gates ring the hub plaza.
	public static readonly BiomeDef[] All =
	{
		// Island discs must never overlap each other or the hub (radius 620) —
		// intersecting pads cut through each other visually.
		new( Biome.Meadow, "Sunny Meadow", "Where every keeper begins.", 0, 5, "#5fc23d", "#95e85c", "#3da32e", new Vector2( 0, 2000 ), 1250f ),
		new( Biome.Forest, "Whisperwood", "Old trees, quick tails.", 1_500, 14, "#3d9138", "#63c94e", "#2a6b28", new Vector2( -2100, 1000 ), 1050f ),
		new( Biome.Beach, "Shellshore", "Sandy naps and salty snacks.", 12_000, 40, "#f5d478", "#ffeeb0", "#e0a94f", new Vector2( 2100, 1000 ), 1050f ),
		new( Biome.Cavern, "Glowdeep Cavern", "Something sparkles in the dark.", 90_000, 120, "#5c5287", "#8a7cc2", "#423a61", new Vector2( -2600, -1100 ), 1000f ),
		new( Biome.Tundra, "Frostfell", "Bring a scarf. Critters included.", 600_000, 360, "#cfeaf7", "#f0fbff", "#8fc9e8", new Vector2( 2600, -1100 ), 1000f ),
		new( Biome.Volcano, "Emberpeak", "Warm feet, warmer friends.", 4_000_000, 1_100, "#9e4a26", "#d96a33", "#6b2f17", new Vector2( -1000, -2350 ), 950f ),
		new( Biome.Mythwood, "The Mythwood", "Legends live here.", 25_000_000, 3_400, "#6d4aa3", "#a578e0", "#4a3170", new Vector2( 1000, -2350 ), 950f ),
	};

	public static BiomeDef Get( Biome b ) => All[(int)b];
}

public static class SpeciesCatalog
{
	// NOTE: tools/generate_assets.py parses these S(...) lines to draw codex icons.
	// Keep one species per line, fields in order.
	static SpeciesDef S( string id, string name, Biome b, Rarity r, BodyShape body, EarStyle ears, TailStyle tail, string c1, string c2, float size )
		=> new( id, name, b, r, body, ears, tail, c1, c2, size );

	public static readonly SpeciesDef[] All =
	{
		// -------- Sunny Meadow --------
		S( "bunbun", "Bunbun", Biome.Meadow, Rarity.Common, BodyShape.Round, EarStyle.Long, TailStyle.Nub, "#e8d5b5", "#f7efe0", 0.85f ),
		S( "peep", "Peep", Biome.Meadow, Rarity.Common, BodyShape.Round, EarStyle.None, TailStyle.Nub, "#f6d44c", "#f2a53a", 0.7f ),
		S( "squibble", "Squibble", Biome.Meadow, Rarity.Uncommon, BodyShape.Long, EarStyle.Pointy, TailStyle.Bushy, "#b5713f", "#e0b184", 0.8f ),
		S( "honeypuff", "Honeypuff", Biome.Meadow, Rarity.Rare, BodyShape.Round, EarStyle.Antenna, TailStyle.None, "#f0b429", "#3a3126", 0.75f ),
		S( "dapple", "Dapple", Biome.Meadow, Rarity.Epic, BodyShape.Tall, EarStyle.Pointy, TailStyle.Nub, "#c98f5a", "#f2e3ce", 1.1f ),
		S( "sunwhisker", "Sunwhisker", Biome.Meadow, Rarity.Legendary, BodyShape.Long, EarStyle.Pointy, TailStyle.Bushy, "#e88f3c", "#fff3da", 1.0f ),

		// -------- Whisperwood --------
		S( "mossling", "Mossling", Biome.Forest, Rarity.Common, BodyShape.Round, EarStyle.Round, TailStyle.Nub, "#7d9c5b", "#a9c383", 0.8f ),
		S( "acornby", "Acornby", Biome.Forest, Rarity.Common, BodyShape.Chunky, EarStyle.None, TailStyle.Nub, "#8a5a33", "#c9995e", 0.75f ),
		S( "shroomkin", "Shroomkin", Biome.Forest, Rarity.Uncommon, BodyShape.Tall, EarStyle.None, TailStyle.None, "#d9534f", "#f5e8d8", 0.85f ),
		S( "twigget", "Twigget", Biome.Forest, Rarity.Rare, BodyShape.Long, EarStyle.Antenna, TailStyle.Long, "#6f5637", "#94bf6a", 0.9f ),
		S( "owlbert", "Owlbert", Biome.Forest, Rarity.Epic, BodyShape.Chunky, EarStyle.Horn, TailStyle.None, "#7a6455", "#d8c6a2", 1.0f ),
		S( "eldergrove", "Eldergrove", Biome.Forest, Rarity.Legendary, BodyShape.Tall, EarStyle.Horn, TailStyle.Long, "#4c6b3c", "#a3d977", 1.35f ),

		// -------- Shellshore --------
		S( "crabbit", "Crabbit", Biome.Beach, Rarity.Common, BodyShape.Chunky, EarStyle.Antenna, TailStyle.None, "#e2704a", "#f5c9a8", 0.75f ),
		S( "sandpip", "Sandpip", Biome.Beach, Rarity.Common, BodyShape.Round, EarStyle.None, TailStyle.Fin, "#ded1ac", "#faf3dd", 0.7f ),
		S( "bubblet", "Bubblet", Biome.Beach, Rarity.Uncommon, BodyShape.Round, EarStyle.None, TailStyle.Fin, "#6cc3d5", "#c3ecf2", 0.8f ),
		S( "shellby", "Shellby", Biome.Beach, Rarity.Rare, BodyShape.Chunky, EarStyle.None, TailStyle.Curl, "#b08fc9", "#efe0f7", 0.9f ),
		S( "pearlfin", "Pearlfin", Biome.Beach, Rarity.Epic, BodyShape.Long, EarStyle.Round, TailStyle.Fin, "#e8e3f2", "#9fd8e8", 1.0f ),
		S( "tidelord", "Tidelord", Biome.Beach, Rarity.Legendary, BodyShape.Tall, EarStyle.Horn, TailStyle.Fin, "#2e7fa8", "#9fe3f5", 1.3f ),

		// -------- Glowdeep Cavern --------
		S( "pebblit", "Pebblit", Biome.Cavern, Rarity.Common, BodyShape.Chunky, EarStyle.None, TailStyle.Nub, "#8d8798", "#b8b2c4", 0.75f ),
		S( "glowmite", "Glowmite", Biome.Cavern, Rarity.Common, BodyShape.Round, EarStyle.Antenna, TailStyle.None, "#5cd6b4", "#274f43", 0.7f ),
		S( "batkin", "Batkin", Biome.Cavern, Rarity.Uncommon, BodyShape.Round, EarStyle.Pointy, TailStyle.None, "#6c5a80", "#c4aede", 0.8f ),
		S( "crystallop", "Crystallop", Biome.Cavern, Rarity.Rare, BodyShape.Round, EarStyle.Long, TailStyle.Nub, "#a48fe0", "#e3d9ff", 0.9f ),
		S( "echoxol", "Echoxol", Biome.Cavern, Rarity.Epic, BodyShape.Long, EarStyle.Antenna, TailStyle.Long, "#e88bb8", "#ffd9ec", 1.0f ),
		S( "deepmaw", "Deepmaw", Biome.Cavern, Rarity.Legendary, BodyShape.Chunky, EarStyle.Horn, TailStyle.Long, "#3d3752", "#8de0d0", 1.35f ),

		// -------- Frostfell --------
		S( "snowpuff", "Snowpuff", Biome.Tundra, Rarity.Common, BodyShape.Round, EarStyle.Round, TailStyle.Nub, "#f2f5f9", "#cfdeeb", 0.8f ),
		S( "chillchick", "Chillchick", Biome.Tundra, Rarity.Common, BodyShape.Round, EarStyle.None, TailStyle.Nub, "#bcd9ea", "#f0f7fc", 0.7f ),
		S( "frostfox", "Frostfox", Biome.Tundra, Rarity.Uncommon, BodyShape.Long, EarStyle.Pointy, TailStyle.Bushy, "#dfe9f2", "#aecbdd", 0.9f ),
		S( "iceling", "Iceling", Biome.Tundra, Rarity.Rare, BodyShape.Tall, EarStyle.Horn, TailStyle.None, "#9fd0e8", "#e8f7ff", 0.85f ),
		S( "auroraowl", "Aurora Owl", Biome.Tundra, Rarity.Epic, BodyShape.Chunky, EarStyle.Horn, TailStyle.None, "#7f9fd0", "#d9f2e8", 1.05f ),
		S( "glacielk", "Glacielk", Biome.Tundra, Rarity.Legendary, BodyShape.Tall, EarStyle.Horn, TailStyle.Nub, "#c3d8e8", "#7fb3d0", 1.45f ),

		// -------- Emberpeak --------
		S( "cinderpup", "Cinderpup", Biome.Volcano, Rarity.Common, BodyShape.Round, EarStyle.Pointy, TailStyle.Bushy, "#c95f3f", "#f2a860", 0.8f ),
		S( "coalby", "Coalby", Biome.Volcano, Rarity.Common, BodyShape.Chunky, EarStyle.Round, TailStyle.Nub, "#4a3f3d", "#e86a3a", 0.75f ),
		S( "sparklit", "Sparklit", Biome.Volcano, Rarity.Uncommon, BodyShape.Round, EarStyle.Antenna, TailStyle.Long, "#f2b13a", "#e85a2a", 0.7f ),
		S( "magmalop", "Magmalop", Biome.Volcano, Rarity.Rare, BodyShape.Round, EarStyle.Long, TailStyle.Nub, "#a83a2a", "#f2c53a", 0.9f ),
		S( "ashwing", "Ashwing", Biome.Volcano, Rarity.Epic, BodyShape.Long, EarStyle.Pointy, TailStyle.Long, "#5d4a48", "#e88f3c", 1.05f ),
		S( "pyrelord", "Pyrelord", Biome.Volcano, Rarity.Legendary, BodyShape.Tall, EarStyle.Horn, TailStyle.Long, "#8f2f22", "#ffcf4a", 1.4f ),

		// -------- The Mythwood --------
		S( "wisp", "Wisp", Biome.Mythwood, Rarity.Rare, BodyShape.Round, EarStyle.None, TailStyle.None, "#bfe8ff", "#7fa8f2", 0.7f ),
		S( "moonmoth", "Moonmoth", Biome.Mythwood, Rarity.Rare, BodyShape.Round, EarStyle.Antenna, TailStyle.None, "#d9d0f5", "#8f7fd0", 0.85f ),
		S( "sylphid", "Sylphid", Biome.Mythwood, Rarity.Epic, BodyShape.Tall, EarStyle.Long, TailStyle.Long, "#a8e8c3", "#5fa87f", 1.0f ),
		S( "runestag", "Runestag", Biome.Mythwood, Rarity.Epic, BodyShape.Tall, EarStyle.Horn, TailStyle.Nub, "#7f6bb0", "#d9c3ff", 1.3f ),
		S( "starlynx", "Starlynx", Biome.Mythwood, Rarity.Legendary, BodyShape.Long, EarStyle.Pointy, TailStyle.Long, "#3d3566", "#ffe08a", 1.15f ),
		S( "faelume", "Faelume", Biome.Mythwood, Rarity.Mythic, BodyShape.Tall, EarStyle.Horn, TailStyle.Curl, "#f2e3ff", "#c37fe8", 1.5f ),
	};

	static Dictionary<string, SpeciesDef> _byId;
	public static SpeciesDef Get( string id )
	{
		_byId ??= All.ToDictionary( s => s.Id );
		return _byId.GetValueOrDefault( id );
	}

	/// <summary>
	/// Rigged animal models (scratch-v6 library copied from scene_lab/shared_assets)
	/// for species with a real-animal silhouette. Species without an entry keep the
	/// procedural chibi body. Sequences are named "{SeqPrefix}_idle/walk/gallop".
	/// HeightMeters is the authored height used to normalize in-game scale.
	/// </summary>
	public sealed record ModelSkin( string ModelPath, string SeqPrefix, float HeightMeters );

	public static readonly Dictionary<string, ModelSkin> ModelSkins = new()
	{
		// Meadow
		["squibble"] = new( "models/shiba_inu_scratch_v6_realistic/shiba_inu_scratch_v6_realistic.vmdl", "shiba_inu", 0.55f ),
		["dapple"] = new( "models/deer_scratch_v6_realistic/deer_scratch_v6_realistic.vmdl", "deer", 1.50f ),
		["sunwhisker"] = new( "models/fox_scratch_v6_realistic/fox_scratch_v6_realistic.vmdl", "fox", 0.65f ),
		// Forest
		["eldergrove"] = new( "models/moose_scratch_v6_realistic/moose_scratch_v6_realistic.vmdl", "moose", 2.20f ),
		// Tundra
		["frostfox"] = new( "models/fox_scratch_v6_realistic/fox_scratch_v6_realistic.vmdl", "fox", 0.65f ),
		["iceling"] = new( "models/ram_scratch_v6_realistic/ram_scratch_v6_realistic.vmdl", "ram", 1.35f ),
		["glacielk"] = new( "models/elk_scratch_v6_realistic/elk_scratch_v6_realistic.vmdl", "elk", 1.90f ),
		// Volcano
		["cinderpup"] = new( "models/husky_scratch_v6_realistic/husky_scratch_v6_realistic.vmdl", "husky", 0.85f ),
		["pyrelord"] = new( "models/bull_scratch_v6_realistic/bull_scratch_v6_realistic.vmdl", "bull", 1.65f ),
		// Cavern
		["deepmaw"] = new( "models/buffalo_scratch_v6_realistic/buffalo_scratch_v6_realistic.vmdl", "buffalo", 1.75f ),
		// Mythwood
		["runestag"] = new( "models/stag_scratch_v6_realistic/stag_scratch_v6_realistic.vmdl", "stag", 1.70f ),
		["starlynx"] = new( "models/tripo_panther_scratch_v6_realistic/tripo_panther_scratch_v6_realistic.vmdl", "panther", 1.10f ),
		["faelume"] = new( "models/horse_scratch_v6_realistic/horse_scratch_v6_realistic.vmdl", "horse", 1.80f ),
	};

	public static ModelSkin SkinFor( string id ) => ModelSkins.GetValueOrDefault( id );

	public static IEnumerable<SpeciesDef> InBiome( Biome b ) => All.Where( s => s.Biome == b );

	/// <summary>Weighted random pick for a biome spawn. Luck skews toward higher rarity.</summary>
	public static SpeciesDef Roll( Biome biome, float luck01 )
	{
		var pool = All.Where( s => s.Biome == biome ).ToList();
		var weights = new float[pool.Count];
		float total = 0f;
		for ( int i = 0; i < pool.Count; i++ )
		{
			// Common 100, Uncommon 34, Rare 10, Epic 2.6, Legendary 0.55, Mythic 0.1
			var baseW = (int)pool[i].Rarity switch
			{
				0 => 100f, 1 => 34f, 2 => 10f, 3 => 2.6f, 4 => 0.55f, _ => 0.1f
			};
			// Luck flattens the curve toward rares.
			weights[i] = baseW * MathF.Pow( 1f + luck01 * 0.9f, (int)pool[i].Rarity );
			total += weights[i];
		}

		var roll = Game.Random.Float( 0f, total );
		for ( int i = 0; i < pool.Count; i++ )
		{
			roll -= weights[i];
			if ( roll <= 0f ) return pool[i];
		}
		return pool[^1];
	}

	public static string RarityName( Rarity r ) => r.ToString();

	public static Color RarityColor( Rarity r ) => r switch
	{
		Rarity.Common => Color.Parse( "#b8c2b9" ) ?? Color.White,
		Rarity.Uncommon => Color.Parse( "#6fd06f" ) ?? Color.Green,
		Rarity.Rare => Color.Parse( "#5aa8f2" ) ?? Color.Blue,
		Rarity.Epic => Color.Parse( "#b06bf2" ) ?? Color.Magenta,
		Rarity.Legendary => Color.Parse( "#f2a83a" ) ?? Color.Orange,
		_ => Color.Parse( "#f25a8f" ) ?? Color.Red,
	};
}
