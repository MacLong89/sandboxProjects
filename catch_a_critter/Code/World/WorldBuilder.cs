namespace CatchACritter;

public enum StationKind { Sell, Nest, Ascend, Board }

/// <summary>A hub interaction point — the player walks close and presses Use.</summary>
public sealed class Station : Component
{
	[Property] public StationKind Kind { get; set; }
	[Property] public float Range { get; set; } = 150f;

	public string Prompt => Kind switch
	{
		StationKind.Sell => "Sell your critters",
		StationKind.Nest => "Sanctuary & Breeding",
		StationKind.Ascend => "Ascend (Prestige)",
		_ => "Daily Quests",
	};
}

/// <summary>
/// Biome gate. World geometry is built identically on every client; this
/// collider is purely local — it opens for players who bought the zone.
/// </summary>
public sealed class GateController : Component
{
	[Property] public Biome Target { get; set; }

	BoxCollider _blocker;
	TextRenderer _label;
	GameObject _visualRoot;

	public bool IsOpen => PlayerProgress.Local?.IsZoneUnlocked( Target ) == true;

	public void Setup( BoxCollider blocker, TextRenderer label, GameObject visualRoot )
	{
		_blocker = blocker;
		_label = label;
		_visualRoot = visualRoot;
	}

	protected override void OnUpdate()
	{
		var progress = PlayerProgress.Local;
		if ( progress is null ) return;

		// Re-evaluated live so gates re-lock after an Ascend reset.
		var open = IsOpen;
		if ( _blocker.IsValid() )
		{
			_blocker.Enabled = !open;
			// Disable the whole blocker object — collider.Enabled alone can leave a solid ghost.
			if ( _blocker.GameObject.IsValid() )
				_blocker.GameObject.Enabled = !open;
		}
		if ( _visualRoot.IsValid() ) _visualRoot.Enabled = !open;

		if ( _label.IsValid() )
		{
			_label.GameObject.Enabled = !open;
			if ( !open )
			{
				var def = BiomeCatalog.Get( Target );
				var affordable = progress.Coins >= def.GateCost;
				_label.Text = $"{def.Name}\nLOCKED - {Balance.Fmt( def.GateCost )} coins\n{(affordable ? "Press E to unlock!" : "Keep catching!")}";
				_label.Color = affordable ? new Color( 0.55f, 1f, 0.55f ) : new Color( 1f, 0.92f, 0.6f );
			}
		}
	}

	/// <summary>Closest locked gate the local player could interact with.</summary>
	public static GateController Nearest( Vector3 pos, float range )
	{
		GateController best = null;
		var bestDist = range;
		var scene = Game.ActiveScene;
		if ( scene is null ) return null;
		foreach ( var gate in scene.GetAllComponents<GateController>() )
		{
			if ( gate.IsOpen ) continue;
			var d = Vector3.DistanceBetween( gate.WorldPosition, pos );
			if ( d < bestDist ) { bestDist = d; best = gate; }
		}
		return best;
	}
}

/// <summary>
/// Builds the whole island deterministically on every client — hub plaza,
/// seven biome discs, bridges, gates, and prop dressing. No networking needed.
/// </summary>
public static class WorldBuilder
{
	public const float HubRadius = 620f;
	public const float BridgeWidth = 170f;

	static readonly Color HubColor = Color.Parse( "#e0c383" ) ?? Color.White;
	static readonly Color SeaColor = Color.Parse( "#22b3dd" ) ?? Color.Blue;
	static readonly Color WoodColor = Color.Parse( "#a87c45" ) ?? Color.Orange;

	public static GameObject Build( Scene scene )
	{
		var root = new GameObject( true, "Island" );
		var rng = new Random( 20260720 );

		BuildSeaAndFloor( root );
		BuildHub( root, rng );

		foreach ( var biome in BiomeCatalog.All )
		{
			BuildBiomeDisc( root, biome, rng );
			BuildBridgeAndGate( root, biome );
			ScatterProps( root, biome, rng );
		}

		return root;
	}

	static void BuildSeaAndFloor( GameObject root )
	{
		// Sea visual, below all walkable surfaces.
		Kit.Box( root, "Sea", new Vector3( 0, 0, -26f ), new Vector3( 9000f, 9000f, 10f ), SeaColor );

		// One invisible walk floor across the entire play space (top at z=0).
		var floor = new GameObject( true, "Floor" );
		floor.SetParent( root );
		floor.LocalPosition = new Vector3( 0, 0, -25f );
		var col = floor.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 9000f, 9000f, 50f );
		col.Static = true;
	}

	static void BuildDisc( GameObject root, string name, Vector2 center, float radius, Color color )
	{
		// Flattened sphere reads as a soft island pad. Top surface ≈ z+2.
		Kit.Sphere( root, name, new Vector3( center.x, center.y, -22f ), new Vector3( radius * 2f, radius * 2f, 48f ), color );
	}

	static void BuildHub( GameObject root, Random rng )
	{
		BuildDisc( root, "HubPad", Vector2.Zero, HubRadius, HubColor );

		// --- Sell stand ---
		// The one place money happens, so it gets a beacon: gold light beam,
		// spinning-coin marker, and a sign you can read across the plaza.
		var gold = new Color( 1f, 0.85f, 0.32f );
		var sell = new GameObject( true, "SellStand" );
		sell.SetParent( root );
		sell.LocalPosition = new Vector3( 150f, 210f, 2f );
		Kit.Box( sell, "Counter", Vector3.Zero, new Vector3( 130f, 60f, 55f ), WoodColor, collider: true );
		Kit.Box( sell, "PoleL", new Vector3( -55f, -20f, 0 ), new Vector3( 10f, 10f, 120f ), WoodColor.Darken( 0.15f ) );
		Kit.Box( sell, "PoleR", new Vector3( 55f, -20f, 0 ), new Vector3( 10f, 10f, 120f ), WoodColor.Darken( 0.15f ) );
		Kit.Box( sell, "Awning", new Vector3( 0, -10f, 121f ), new Vector3( 150f, 90f, 10f ), new Color( 0.9f, 0.35f, 0.35f ) );
		Kit.Box( sell, "AwningStripe", new Vector3( 0, -10f, 122f ), new Vector3( 150f, 30f, 10f ), new Color( 0.98f, 0.92f, 0.85f ) );
		Kit.Box( sell, "Beacon", new Vector3( 0, 20f, 0 ), new Vector3( 26f, 26f, 950f ), gold.WithAlpha( 0.28f ) );
		Kit.Sphere( sell, "Coin", new Vector3( 0, 20f, 160f ), new Vector3( 46f, 14f, 46f ), gold );
		Kit.Label( sell, "Sign", new Vector3( 0, 0, 200f ), "SELL CRITTERS HERE", gold, 0.3f, 44 );
		var sellStation = sell.Components.Create<Station>();
		sellStation.Kind = StationKind.Sell;

		// --- Breeding nest ---
		var nest = new GameObject( true, "Nest" );
		nest.SetParent( root );
		nest.LocalPosition = new Vector3( -220f, 160f, 2f );
		Kit.Sphere( nest, "NestBase", new Vector3( 0, 0, 18f ), new Vector3( 150f, 150f, 55f ), WoodColor.Darken( 0.1f ) );
		Kit.Sphere( nest, "NestInner", new Vector3( 0, 0, 30f ), new Vector3( 110f, 110f, 40f ), new Color( 0.55f, 0.42f, 0.28f ) );
		Kit.Sphere( nest, "Egg1", new Vector3( -18f, 8f, 46f ), new Vector3( 34f, 34f, 44f ), new Color( 0.95f, 0.93f, 0.85f ) );
		Kit.Sphere( nest, "Egg2", new Vector3( 20f, -6f, 44f ), new Vector3( 28f, 28f, 38f ), new Color( 0.8f, 0.9f, 0.95f ) );
		Kit.Label( nest, "Sign", new Vector3( 0, 0, 130f ), "SANCTUARY", new Color( 0.75f, 0.95f, 0.7f ) );
		var nestStation = nest.Components.Create<Station>();
		nestStation.Kind = StationKind.Nest;

		// --- Ascend statue ---
		var statue = new GameObject( true, "AscendStatue" );
		statue.SetParent( root );
		statue.LocalPosition = new Vector3( 0f, -260f, 2f );
		Kit.Box( statue, "Pedestal", Vector3.Zero, new Vector3( 110f, 110f, 40f ), new Color( 0.75f, 0.72f, 0.68f ), collider: true );
		Kit.Box( statue, "Pedestal2", new Vector3( 0, 0, 41f ), new Vector3( 84f, 84f, 26f ), new Color( 0.68f, 0.65f, 0.6f ) );
		Kit.Sphere( statue, "Body", new Vector3( 0, 0, 105f ), new Vector3( 60f, 55f, 65f ), new Color( 0.95f, 0.8f, 0.3f ) );
		Kit.Sphere( statue, "Head", new Vector3( 0, 0, 152f ), new Vector3( 40f, 38f, 38f ), new Color( 0.95f, 0.8f, 0.3f ) );
		Kit.BoxCentered( statue, "Horn", new Vector3( 0, 0, 180f ), new Vector3( 10f, 10f, 30f ), new Color( 1f, 0.95f, 0.7f ) );
		Kit.Label( statue, "Sign", new Vector3( 0, 0, 225f ), "ASCEND", new Color( 1f, 0.85f, 0.4f ) );
		var statueStation = statue.Components.Create<Station>();
		statueStation.Kind = StationKind.Ascend;

		// --- Daily board ---
		var board = new GameObject( true, "DailyBoard" );
		board.SetParent( root );
		board.LocalPosition = new Vector3( 260f, -120f, 2f );
		Kit.Box( board, "PostL", new Vector3( -50f, 0, 0 ), new Vector3( 12f, 12f, 130f ), WoodColor.Darken( 0.2f ) );
		Kit.Box( board, "PostR", new Vector3( 50f, 0, 0 ), new Vector3( 12f, 12f, 130f ), WoodColor.Darken( 0.2f ) );
		Kit.Box( board, "Panel", new Vector3( 0, 0, 60f ), new Vector3( 130f, 8f, 80f ), WoodColor );
		Kit.Label( board, "Sign", new Vector3( 0, 0, 165f ), "DAILY QUESTS", new Color( 0.7f, 0.9f, 1f ) );
		var boardStation = board.Components.Create<Station>();
		boardStation.Kind = StationKind.Board;

		// Plaza dressing
		for ( int i = 0; i < 8; i++ )
		{
			var ang = i / 8f * MathF.Tau + 0.35f;
			var pos = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0 ) * (HubRadius - 90f);
			Kit.Flower( root, pos.WithZ( 2f ), new ColorHsv( rng.Next( 360 ), 0.55f, 0.95f ), rng );
		}
	}

	static void BuildBiomeDisc( GameObject root, BiomeDef biome, Random rng )
	{
		BuildDisc( root, $"Pad_{biome.Id}", biome.Center, biome.Radius, biome.Ground );

		// Accent patches for visual variety (+1 z-nudge above the pad top, per depth rules).
		for ( int i = 0; i < 7; i++ )
		{
			var ang = (float)rng.NextDouble() * MathF.Tau;
			var dist = (float)rng.NextDouble() * biome.Radius * 0.7f;
			var p = biome.Center + new Vector2( MathF.Cos( ang ), MathF.Sin( ang ) ) * dist;
			var size = 90f + (float)rng.NextDouble() * 160f;
			Kit.Sphere( root, "Patch", new Vector3( p.x, p.y, -21f ), new Vector3( size, size, 46f ), biome.Accent );
		}
	}

	static void BuildBridgeAndGate( GameObject root, BiomeDef biome )
	{
		if ( biome.Id == Biome.Meadow )
		{
			// Meadow is free — bridge with no gate, dressed as the obvious
			// "start here" exit so it never reads as one more locked barricade.
			BuildBridge( root, biome, out _ );
			BuildStarterEntrance( root, biome );
			return;
		}

		BuildBridge( root, biome, out var mid );

		var toBiome = (biome.Center - Vector2.Zero).Normal;
		var yaw = MathF.Atan2( toBiome.y, toBiome.x ).RadianToDegree();

		var gate = new GameObject( true, $"Gate_{biome.Id}" );
		gate.SetParent( root );
		gate.LocalPosition = new Vector3( mid.x, mid.y, 2f );
		gate.LocalRotation = Rotation.FromYaw( yaw );

		var visual = new GameObject( true, "GateVisual" );
		visual.SetParent( gate );
		visual.LocalPosition = Vector3.Zero;

		var pillar = biome.Prop;
		Kit.Box( visual, "PillarL", new Vector3( 0, -BridgeWidth * 0.55f, 0 ), new Vector3( 26f, 26f, 190f ), pillar );
		Kit.Box( visual, "PillarR", new Vector3( 0, BridgeWidth * 0.55f, 0 ), new Vector3( 26f, 26f, 190f ), pillar );
		Kit.Box( visual, "Crossbar", new Vector3( 0, 0, 190f ), new Vector3( 30f, BridgeWidth * 1.25f, 24f ), pillar.Darken( 0.1f ) );
		Kit.Box( visual, "DoorGlow", new Vector3( 0, 0, 4f ), new Vector3( 8f, BridgeWidth * 1.05f, 150f ), biome.Accent.WithAlpha( 0.85f ) );

		// Crossed planks so a locked gate reads "barricade" at a glance —
		// the starter arch has an open walkway by contrast.
		var plank = WoodColor.Darken( 0.25f );
		Kit.BoxCentered( visual, "PlankA", new Vector3( -6f, 0, 82f ), new Vector3( 12f, BridgeWidth * 1.18f, 16f ), plank, Rotation.From( 0, 0, 32f ) );
		Kit.BoxCentered( visual, "PlankB", new Vector3( -7f, 0, 82f ), new Vector3( 12f, BridgeWidth * 1.18f, 16f ), plank, Rotation.From( 0, 0, -32f ) );

		var blockerGo = new GameObject( true, "Blocker" );
		blockerGo.SetParent( gate );
		blockerGo.LocalPosition = new Vector3( 0, 0, 130f );
		var blocker = blockerGo.Components.Create<BoxCollider>();
		blocker.Scale = new Vector3( 40f, BridgeWidth * 1.3f, 260f );
		blocker.Static = true;

		var label = Kit.Label( gate, "LockLabel", new Vector3( 0, 0, 250f ), "", Color.White, 0.22f, 40 );
		var text = label.Components.Get<TextRenderer>();

		var controller = gate.Components.Create<GateController>();
		controller.Target = biome.Id;
		controller.Setup( blocker, text, visual );
	}

	/// <summary>
	/// Festive open arch + chevron trail marking the free Meadow exit. Pillars
	/// sit outside the bridge walls and there is no door panel, so the walkway
	/// visibly stays open — the opposite read of the locked biome gates.
	/// </summary>
	static void BuildStarterEntrance( GameObject root, BiomeDef biome )
	{
		var green = new Color( 0.45f, 0.9f, 0.4f );
		var dir = biome.Center.Normal;
		var yaw = MathF.Atan2( dir.y, dir.x ).RadianToDegree();
		var mouth = dir * (HubRadius - 40f);

		var arch = new GameObject( true, "StarterArch" );
		arch.SetParent( root );
		arch.LocalPosition = new Vector3( mouth.x, mouth.y, 2f );
		arch.LocalRotation = Rotation.FromYaw( yaw );

		Kit.Box( arch, "PillarL", new Vector3( 0, -BridgeWidth * 0.78f, 0 ), new Vector3( 24f, 24f, 210f ), green );
		Kit.Box( arch, "PillarR", new Vector3( 0, BridgeWidth * 0.78f, 0 ), new Vector3( 24f, 24f, 210f ), green );
		Kit.Box( arch, "Crossbar", new Vector3( 0, 0, 210f ), new Vector3( 26f, BridgeWidth * 1.72f, 20f ), green.Darken( 0.12f ) );

		// Pennant flags strung under the crossbar.
		for ( int i = -3; i <= 3; i++ )
		{
			var hue = new ColorHsv( (i + 3) * 52f, 0.6f, 0.98f );
			Kit.BoxCentered( arch, "Flag", new Vector3( 0, i * BridgeWidth * 0.22f, 197f ), new Vector3( 6f, 18f, 26f ), hue );
		}

		Kit.Label( arch, "Sign", new Vector3( 0, 0, 262f ), "SUNNY MEADOW - START HERE!", green, 0.3f, 44 );

		// Chevron trail on the plaza floor pointing from spawn to the bridge.
		for ( int i = 0; i < 4; i++ )
		{
			var p = dir * (190f + i * 95f);
			var chev = new GameObject( true, "StartTrail" );
			chev.SetParent( root );
			chev.LocalPosition = new Vector3( p.x, p.y, 2f );
			chev.LocalRotation = Rotation.FromYaw( yaw );
			Kit.BoxCentered( chev, "L", new Vector3( 0, 11f, 0 ), new Vector3( 34f, 10f, 3f ), green, Rotation.FromYaw( -45f ) );
			Kit.BoxCentered( chev, "R", new Vector3( 0, -11f, 0 ), new Vector3( 34f, 10f, 3f ), green, Rotation.FromYaw( 45f ) );
		}
	}

	static void BuildBridge( GameObject root, BiomeDef biome, out Vector2 mid )
	{
		var dir = biome.Center.Normal;
		var a = dir * (HubRadius - 40f);
		var b = biome.Center - dir * (biome.Radius - 40f);
		mid = (a + b) * 0.5f;
		var length = (b - a).Length;
		var yaw = MathF.Atan2( dir.y, dir.x ).RadianToDegree();

		var bridge = new GameObject( true, $"Bridge_{biome.Id}" );
		bridge.SetParent( root );
		bridge.LocalPosition = new Vector3( mid.x, mid.y, 0f );
		bridge.LocalRotation = Rotation.FromYaw( yaw );

		// Deck top lands at z=0 (the walk floor) so players don't sink into it.
		Kit.Box( bridge, "Deck", new Vector3( 0, 0, -8f ), new Vector3( length + 30f, BridgeWidth, 8f ), WoodColor );
		// Visual rails only — no invisible side colliders (those read as mystery walls).
		Kit.Box( bridge, "RailL", new Vector3( 0, -BridgeWidth * 0.5f, 0f ), new Vector3( length + 30f, 10f, 34f ), WoodColor.Darken( 0.12f ) );
		Kit.Box( bridge, "RailR", new Vector3( 0, BridgeWidth * 0.5f, 0f ), new Vector3( length + 30f, 10f, 34f ), WoodColor.Darken( 0.12f ) );
	}

	static void ScatterProps( GameObject root, BiomeDef biome, Random rng )
	{
		var count = 16 + rng.Next( 6 );
		for ( int i = 0; i < count; i++ )
		{
			var ang = (float)rng.NextDouble() * MathF.Tau;
			var dist = biome.Radius * (0.3f + 0.55f * (float)rng.NextDouble());
			var p = biome.Center + new Vector2( MathF.Cos( ang ), MathF.Sin( ang ) ) * dist;

			// Keep the bridge mouth clear.
			var toHub = (Vector2.Zero - biome.Center).Normal;
			var mouth = biome.Center + toHub * (biome.Radius - 120f);
			if ( (p - mouth).Length < 260f ) continue;

			var pos = new Vector3( p.x, p.y, 2f );
			switch ( biome.Id )
			{
				case Biome.Meadow:
					if ( i % 3 == 0 ) Kit.Tree( root, pos, WoodColor, biome.Accent, 1f, rng );
					else Kit.Flower( root, pos, new ColorHsv( rng.Next( 360 ), 0.6f, 0.95f ), rng );
					break;
				case Biome.Forest:
					if ( i % 4 == 3 ) Kit.Rock( root, pos, new Color( 0.5f, 0.52f, 0.5f ), 0.8f, rng );
					else Kit.PineTree( root, pos, WoodColor.Darken( 0.2f ), biome.Prop, 1.1f, rng );
					break;
				case Biome.Beach:
					if ( i % 3 == 0 ) Kit.Tree( root, pos, new Color( 0.72f, 0.55f, 0.35f ), new Color( 0.45f, 0.75f, 0.4f ), 1.15f, rng );
					else Kit.Rock( root, pos, new Color( 0.9f, 0.85f, 0.72f ), 0.5f, rng );
					break;
				case Biome.Cavern:
					if ( i % 2 == 0 ) Kit.Crystal( root, pos, new Color( 0.55f, 0.85f, 0.8f ), 1f, rng );
					else Kit.Rock( root, pos, biome.Prop, 1.1f, rng );
					break;
				case Biome.Tundra:
					if ( i % 3 == 0 ) Kit.Rock( root, pos, Color.White, 0.9f, rng );
					else Kit.PineTree( root, pos, WoodColor.Darken( 0.25f ), new Color( 0.85f, 0.93f, 0.95f ), 1f, rng );
					break;
				case Biome.Volcano:
					if ( i % 3 == 0 ) Kit.Crystal( root, pos, new Color( 0.95f, 0.55f, 0.2f ), 0.9f, rng );
					else Kit.Rock( root, pos, biome.Prop, 1.2f, rng );
					break;
				case Biome.Mythwood:
					if ( i % 3 == 0 ) Kit.Crystal( root, pos, new Color( 0.75f, 0.55f, 0.95f ), 1.1f, rng );
					else Kit.Tree( root, pos, new Color( 0.4f, 0.32f, 0.5f ), biome.Accent, 1.2f, rng );
					break;
			}
		}
	}

	/// <summary>Which zone contains this position? Null when out on a bridge or the sea.</summary>
	public static Biome? ZoneAt( Vector3 pos )
	{
		var p2 = new Vector2( pos.x, pos.y );
		foreach ( var biome in BiomeCatalog.All )
		{
			if ( (p2 - biome.Center).Length <= biome.Radius )
				return biome.Id;
		}
		return null;
	}

	public static bool InHub( Vector3 pos ) => new Vector2( pos.x, pos.y ).Length <= HubRadius;
}
