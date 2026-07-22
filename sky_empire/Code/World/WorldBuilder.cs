namespace SkyEmpire;

/// <summary>
/// Builds the shared sky world deterministically on every client: a central hub
/// island, twelve empty player plots in a ring, bridges, and drifting clouds.
/// Plot contents (the actual tycoons) are rendered per-player by PlotVisual.
/// </summary>
public static class WorldBuilder
{
	public const float BridgeWidth = 150f;

	static readonly Color HubColor = Color.Parse( "#cbb68c" ) ?? Color.White;
	static readonly Color WoodColor = Color.Parse( "#9c7b4f" ) ?? Color.Orange;

	/// <summary>Center of a plot pad (top surface ≈ z+2).</summary>
	public static Vector3 PlotCenter( int index )
	{
		var ang = index / (float)Balance.PlotCount * MathF.Tau;
		return new Vector3( MathF.Cos( ang ) * Balance.PlotRingRadius, MathF.Sin( ang ) * Balance.PlotRingRadius, 0f );
	}

	/// <summary>Yaw so the plot's local +Y axis points at the hub.</summary>
	public static float PlotYaw( int index )
	{
		var c = PlotCenter( index );
		return MathF.Atan2( -c.y, -c.x ).RadianToDegree() - 90f;
	}

	/// <summary>Where the plot owner spawns/respawns (world space).</summary>
	public static Vector3 PlotSpawn( int index )
	{
		var rot = Rotation.FromYaw( PlotYaw( index ) );
		return PlotCenter( index ) + rot * new Vector3( 0f, 320f, 10f );
	}

	public static GameObject Build( Scene scene )
	{
		var root = new GameObject( true, "SkyWorld" );
		var rng = new Random( 20260721 );

		BuildHub( root, rng );

		for ( int i = 0; i < Balance.PlotCount; i++ )
		{
			BuildPlotPad( root, i );
			BuildBridge( root, i );
		}

		BuildClouds( root, rng );
		return root;
	}

	static void BuildFloatingPad( GameObject root, string name, Vector3 center, float radius, Color grass, Color cliff )
	{
		// Grassy top disc (flattened sphere, top ≈ z+2 like the walk plane).
		Kit.Sphere( root, $"{name}_Top", center + new Vector3( 0, 0, -22f ), new Vector3( radius * 2f, radius * 2f, 48f ), grass );

		// Rocky underside taper so the island reads as floating.
		Kit.Sphere( root, $"{name}_Under1", center + new Vector3( 0, 0, -95f ), new Vector3( radius * 1.55f, radius * 1.55f, 190f ), cliff );
		Kit.Sphere( root, $"{name}_Under2", center + new Vector3( 0, 0, -240f ), new Vector3( radius * 0.85f, radius * 0.85f, 200f ), cliff.Darken( 0.12f ) );
		Kit.Sphere( root, $"{name}_Under3", center + new Vector3( 0, 0, -360f ), new Vector3( radius * 0.35f, radius * 0.35f, 130f ), cliff.Darken( 0.22f ) );

		// Walkable square collider inset inside the disc (top at z=0).
		var half = radius * 0.72f;
		var col = new GameObject( true, $"{name}_Floor" );
		col.SetParent( root );
		col.LocalPosition = center + new Vector3( 0, 0, -25f );
		var box = col.Components.Create<BoxCollider>();
		box.Scale = new Vector3( half * 2f, half * 2f, 50f );
		box.Static = true;
	}

	static void BuildHub( GameObject root, Random rng )
	{
		BuildFloatingPad( root, "Hub", Vector3.Zero, Balance.HubRadius, HubColor, new Color( 0.5f, 0.42f, 0.33f ) );

		// Welcome totem in the middle.
		var totem = new GameObject( true, "HubTotem" );
		totem.SetParent( root );
		totem.LocalPosition = new Vector3( 0, 0, 2f );
		Kit.Box( totem, "Base", Vector3.Zero, new Vector3( 90f, 90f, 26f ), new Color( 0.72f, 0.68f, 0.6f ), collider: true );
		Kit.Box( totem, "Pillar", new Vector3( 0, 0, 26f ), new Vector3( 34f, 34f, 170f ), WoodColor );
		Kit.Sphere( totem, "Orb", new Vector3( 0, 0, 230f ), new Vector3( 60f, 60f, 60f ), new Color( 1f, 0.85f, 0.35f ) );
		Kit.Label( totem, "Sign", new Vector3( 0, 0, 300f ), "SKY EMPIRE", new Color( 1f, 0.9f, 0.5f ), 0.3f, 48 );
		Kit.Label( totem, "Sub", new Vector3( 0, 0, 268f ), "cross a bridge to visit a friend — you BOTH earn +25%", new Color( 0.85f, 0.95f, 1f ), 0.14f, 30 );

		// Lamp ring.
		for ( int i = 0; i < 6; i++ )
		{
			var ang = i / 6f * MathF.Tau + 0.26f;
			var pos = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0 ) * (Balance.HubRadius - 110f);
			var lamp = new GameObject( true, "HubLamp" );
			lamp.SetParent( root );
			lamp.LocalPosition = pos.WithZ( 2f );
			Kit.Box( lamp, "Post", Vector3.Zero, new Vector3( 8f, 8f, 95f ), WoodColor.Darken( 0.2f ) );
			Kit.Sphere( lamp, "Bulb", new Vector3( 0, 0, 105f ), new Vector3( 22f, 22f, 22f ), new Color( 1f, 0.92f, 0.6f ) );
		}
	}

	static void BuildPlotPad( GameObject root, int index )
	{
		// Neutral empty pad; PlotVisual re-tints per-owner rebirth palette on claim.
		var tier = RebirthCatalog.Tiers[0];
		BuildFloatingPad( root, $"Plot{index}", PlotCenter( index ), Balance.PlotRadius, tier.Grass, tier.Cliff );
	}

	static void BuildBridge( GameObject root, int index )
	{
		var center = PlotCenter( index );
		var dir = (-center).Normal;
		var a = center + dir * (Balance.PlotRadius - 60f);   // plot edge, hub side
		var b = -dir * (Balance.HubRadius - 60f);            // hub edge, plot side
		var mid = (a + b) * 0.5f;
		var length = (b - a).Length;
		var yaw = MathF.Atan2( dir.y, dir.x ).RadianToDegree();

		var bridge = new GameObject( true, $"Bridge{index}" );
		bridge.SetParent( root );
		bridge.LocalPosition = mid;
		bridge.LocalRotation = Rotation.FromYaw( yaw );

		// Deck top lands at z=0 so it lines up with pad tops.
		Kit.Box( bridge, "Deck", new Vector3( 0, 0, -10f ), new Vector3( length + 40f, BridgeWidth, 10f ), WoodColor, collider: true );
		Kit.Box( bridge, "RailL", new Vector3( 0, -BridgeWidth * 0.5f, 0f ), new Vector3( length + 40f, 8f, 30f ), WoodColor.Darken( 0.12f ) );
		Kit.Box( bridge, "RailR", new Vector3( 0, BridgeWidth * 0.5f, 0f ), new Vector3( length + 40f, 8f, 30f ), WoodColor.Darken( 0.12f ) );
	}

	static void BuildClouds( GameObject root, Random rng )
	{
		for ( int i = 0; i < 26; i++ )
		{
			var ang = (float)rng.NextDouble() * MathF.Tau;
			var dist = 900f + (float)rng.NextDouble() * 4200f;
			var z = -650f + (float)rng.NextDouble() * 260f;
			if ( i % 3 == 0 ) z = 500f + (float)rng.NextDouble() * 700f;
			Kit.Cloud( root, new Vector3( MathF.Cos( ang ) * dist, MathF.Sin( ang ) * dist, z ), 1.2f + (float)rng.NextDouble() * 2.2f, rng );
		}
	}
}
