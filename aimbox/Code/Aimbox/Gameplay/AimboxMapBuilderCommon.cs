namespace Sandbox;

/// <summary>Shared floor, perimeter, spawn alcoves, and cover helpers for AIMBOX maps.</summary>
public static class AimboxMapBuilderCommon
{
	const float LooseCoverScale = 2f;

	public static float WalkZ => AimboxMapDesignRules.FloorWalkZ;
	public static Vector3 LooseCoverSize( Vector3 size ) => size * LooseCoverScale;

	public static void BuildFloorSlab( GameObject root, AimboxMapLayout cfg, float floorThickness, AimboxArenaSurface floor )
	{
		AimboxArenaGeometry.AddBlock(
			root,
			"Floor Slab",
			AimboxArenaGeometry.OnGround( Vector3.Zero, new Vector3( cfg.ArenaHalfWidth * 2.04f, cfg.ArenaHalfLength * 2.04f, floorThickness ) ),
			new Vector3( cfg.ArenaHalfWidth * 2.04f, cfg.ArenaHalfLength * 2.04f, floorThickness ),
			floor );
	}

	public static void BuildPerimeter( GameObject root, AimboxMapLayout cfg, AimboxArenaSurface wall )
	{
		var h = cfg.WallHeight;
		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth + t * 0.5f;
		var hl = cfg.ArenaHalfLength + t * 0.5f;

		AddBlock( root, "Perimeter N", OnGround( new Vector3( 0, hl, 0 ), new Vector3( hw * 2f, t, h ) ), new Vector3( hw * 2f, t, h ), wall );
		AddBlock( root, "Perimeter S", OnGround( new Vector3( 0, -hl, 0 ), new Vector3( hw * 2f, t, h ) ), new Vector3( hw * 2f, t, h ), wall );
		AddBlock( root, "Perimeter E", OnGround( new Vector3( hw, 0, 0 ), new Vector3( t, hl * 2f, h ) ), new Vector3( t, hl * 2f, h ), wall );
		AddBlock( root, "Perimeter W", OnGround( new Vector3( -hw, 0, 0 ), new Vector3( t, hl * 2f, h ) ), new Vector3( t, hl * 2f, h ), wall );
	}

	/// <summary>Spawn wing walls and sightline baffles for the shared spawn set.</summary>
	public static void BuildSpawnAlcoves( GameObject root, AimboxMapLayout cfg, AimboxArenaSurface surface = AimboxArenaPalette.Barrier )
	{
		var t = cfg.WallThickness;
		var h = cfg.LaneDividerHeight;
		var spread = cfg.SpawnSpreadY;
		var sideX = cfg.ArenaHalfWidth - cfg.SpawnInset - t * 2.25f;
		var blind = new Vector3( t * 0.85f, t * 4.4f, h );
		var wing = new Vector3( t * 2.35f, t * 1.05f, h );

		foreach ( var sign in new[] { -1f, 1f } )
		{
			var label = sign < 0 ? "Red" : "Blue";
			var x = sign * sideX;
			AddBlock( root, $"{label} Spawn Blind Mid", OnGround( new Vector3( x, 0, 0 ), blind ), blind, surface );
			AddBlock( root, $"{label} Spawn Blind N", OnGround( new Vector3( x, spread * 0.43f, 0 ), blind ), blind, surface );
			AddBlock( root, $"{label} Spawn Blind S", OnGround( new Vector3( x, -spread * 0.43f, 0 ), blind ), blind, surface );
			AddBlock( root, $"{label} Spawn Exit N", OnGround( new Vector3( x - sign * t * 1.45f, spread * 0.36f, 0 ), wing ), wing, surface );
			AddBlock( root, $"{label} Spawn Exit S", OnGround( new Vector3( x - sign * t * 1.45f, -spread * 0.36f, 0 ), wing ), wing, surface );
		}

		BuildFfaSpawnBaffles( root, cfg, surface );
	}

	/// <summary>Sparse mid-map cover with deliberate gaps between pieces.</summary>
	public static void ScatterCover(
		GameObject root,
		AimboxMapLayout cfg,
		IReadOnlyList<Vector2> positions,
		Vector3 size,
		AimboxArenaSurface surface,
		string prefix = "Cover" )
	{
		var coverSize = LooseCoverSize( size );
		for ( var i = 0; i < positions.Count; i++ )
		{
			var pos = positions[i];
			var yaw = (i % 3 - 1) * 11f;
			AddBlock(
				root,
				$"{prefix} {i + 1}",
				OnGround( new Vector3( pos.x, pos.y, 0 ), coverSize ),
				coverSize,
				surface,
				Rotation.FromYaw( yaw ) );
		}
	}

	/// <summary>Offset cover row along Y with open gaps between each piece.</summary>
	public static void BuildOffsetLaneCover(
		GameObject root,
		AimboxMapLayout cfg,
		float centerX,
		float laneHalfSpan,
		int pieceCount,
		Vector3 size,
		AimboxArenaSurface surface,
		string prefix )
	{
		for ( var i = 0; i < pieceCount; i++ )
		{
			var laneT = (i - (pieceCount - 1) * 0.5f) / MathF.Max( 1, pieceCount - 1 );
			var y = laneT * laneHalfSpan;
			var x = centerX + (i % 2 == 0 ? 1f : -1f) * cfg.WallThickness * 1.4f;
			AddBlock( root, $"{prefix} {i + 1}", OnGround( new Vector3( x, y, 0 ), size ), size, surface, Rotation.FromYaw( i * 13f ) );
		}
	}

	public static void BuildSpawnAccents( GameObject root, AimboxMapLayout cfg )
	{
		var stripeDepth = cfg.WallThickness * 0.65f;
		var stripeHeight = cfg.WaistCoverHeight * 0.35f;
		var stripeLength = cfg.ArenaHalfLength * 1.28f;
		var x = cfg.ArenaHalfWidth - cfg.SpawnInset * 0.35f;

		AddAccent( root, "Red Spawn Stripe", OnGround( new Vector3( -x, 0, 0 ), new Vector3( stripeDepth, stripeLength, stripeHeight ) ), new Vector3( stripeDepth, stripeLength, stripeHeight ), AimboxArenaPalette.RedAccent );
		AddAccent( root, "Blue Spawn Stripe", OnGround( new Vector3( x, 0, 0 ), new Vector3( stripeDepth, stripeLength, stripeHeight ) ), new Vector3( stripeDepth, stripeLength, stripeHeight ), AimboxArenaPalette.BlueAccent );
	}

	/// <summary>
	/// Dresses the barren outer ring shared by every arena: cover clusters in the four corners,
	/// waist-high cover hugging the long north/south walls, and evenly spaced pilasters that break up
	/// the otherwise flat perimeter. Cover sits beyond the spawn spread so it never buries a spawn column.
	/// </summary>
	public static void BuildPerimeterDressing(
		GameObject root,
		AimboxMapLayout cfg,
		AimboxArenaSurface cornerSurface,
		AimboxArenaSurface edgeSurface,
		AimboxArenaSurface pilasterSurface,
		Color cornerTint = default,
		Color edgeTint = default )
	{
		BuildCornerClusters( root, cfg, cornerSurface, cornerTint );
		BuildWallEdgeCover( root, cfg, edgeSurface, edgeTint );
		BuildWallPilasters( root, cfg, pilasterSurface );
	}

	static void BuildCornerClusters( GameObject root, AimboxMapLayout cfg, AimboxArenaSurface surface, Color tint )
	{
		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;

		var stack = new Vector3( t * 3.4f, t * 3.4f, cfg.TallCoverHeight * 1.05f );
		var ledge = new Vector3( t * 2.2f, t * 2.2f, cfg.WaistCoverHeight * 0.9f );

		foreach ( var (sx, sy, label) in new[] { (-1f, 1f, "NW"), (1f, 1f, "NE"), (-1f, -1f, "SW"), (1f, -1f, "SE") } )
		{
			var stackPos = new Vector3( sx * (hw - stack.x * 0.5f - t * 0.6f), sy * (hl - stack.y * 0.5f - t * 0.6f), 0f );
			AddDressing( root, $"Corner Stack {label}", OnGround( stackPos, stack ), stack, surface, tint );

			var ledgePos = new Vector3( stackPos.x, stackPos.y - sy * (stack.y * 0.5f + ledge.y * 0.6f), 0f );
			AddDressing( root, $"Corner Ledge {label}", OnGround( ledgePos, ledge ), ledge, surface, tint, Rotation.FromYaw( sx * sy * 9f ) );
		}
	}

	static void BuildWallEdgeCover( GameObject root, AimboxMapLayout cfg, AimboxArenaSurface surface, Color tint )
	{
		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;

		var crate = new Vector3( t * 2.6f, t * 2.0f, cfg.WaistCoverHeight * 0.95f );
		var offsets = new[] { -0.66f, -0.34f, 0.34f, 0.66f };

		foreach ( var side in new[] { 1f, -1f } )
		{
			var label = side > 0 ? "North" : "South";
			var y = side * (hl - crate.y * 0.5f - t * 0.9f);
			for ( var i = 0; i < offsets.Length; i++ )
			{
				var x = offsets[i] * hw;
				var yaw = (i % 2 == 0 ? 6f : -6f);
				AddDressing( root, $"{label} Edge Crate {i + 1}", OnGround( new Vector3( x, y, 0 ), crate ), crate, surface, tint, Rotation.FromYaw( yaw ) );
			}
		}
	}

	static void BuildWallPilasters( GameObject root, AimboxMapLayout cfg, AimboxArenaSurface surface )
	{
		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var pilaster = new Vector3( t * 1.5f, t * 0.8f, cfg.WallHeight * 0.72f );

		var count = 5;
		for ( var i = 0; i < count; i++ )
		{
			var fx = (i - (count - 1) * 0.5f) / (count - 1) * 1.6f;
			var x = fx * hw;
			var y = hl - pilaster.y * 0.5f;
			AddDressing( root, $"North Pilaster {i + 1}", OnGround( new Vector3( x, y, 0 ), pilaster ), pilaster, surface, default );
			AddDressing( root, $"South Pilaster {i + 1}", OnGround( new Vector3( x, -y, 0 ), pilaster ), pilaster, surface, default );
		}
	}

	static void AddDressing( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Color tint, Rotation rotation = default )
	{
		if ( tint == default )
			AddBlock( root, name, center, size, surface, rotation );
		else
			AimboxArenaGeometry.AddBlock( root, name, center, size, surface, tint, rotation );
	}

	public static Vector3 OnGround( Vector3 position, Vector3 size, float floorTopZ = -1f )
	{
		if ( floorTopZ < 0f )
			floorTopZ = WalkZ;

		return new Vector3( position.x, position.y, floorTopZ + size.z * 0.5f );
	}

	public static Vector3 OnTopOf( Vector3 position, Vector3 size, float elevationAboveWalk, float floorTopZ = -1f ) =>
		OnGround( position, size, floorTopZ ) + Vector3.Up * elevationAboveWalk;

	public static void AddBlock( GameObject parent, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Rotation rotation = default ) =>
		AimboxArenaGeometry.AddBlock( parent, name, center, size, surface, rotation: rotation );

	public static void AddAccent( GameObject parent, string name, Vector3 center, Vector3 size, Color tint, Rotation rotation = default ) =>
		AimboxArenaGeometry.AddBlock( parent, name, center, size, AimboxArenaSurface.Solid, tint, rotation );

	static void BuildFfaSpawnBaffles( GameObject root, AimboxMapLayout cfg, AimboxArenaSurface surface )
	{
		var positions = AimboxMapDesignRules.CreateFfaSpawnPositions( cfg );

		var screen = new Vector3( cfg.WallThickness * 0.75f, cfg.WallThickness * 4.4f, cfg.LaneDividerHeight );
		for ( var i = 0; i < positions.Count; i++ )
		{
			var p = new Vector3( positions[i].x, positions[i].y, 0f );
			var toCenter = -p.WithZ( 0f );
			if ( toCenter.LengthSquared < 1f )
				continue;

			var dir = toCenter.Normal;
			var center = p + dir * (cfg.WallThickness * 3.1f);
			var yaw = MathF.Atan2( dir.y, dir.x ) * 180f / MathF.PI;
			AddBlock(
				root,
				$"FFA Spawn Baffle {i + 1}",
				OnGround( center, screen ),
				screen,
				surface,
				Rotation.FromYaw( yaw ) );
		}
	}
}
