namespace DeepDive;

/// <summary>
/// Dave the Diver / Terraria-style ocean: open water column, boat on top,
/// seafloor along the bottom with gentle hills. Free left/right at every depth.
/// </summary>
public sealed class SeabedTerrain : Component
{
	public static SeabedTerrain Instance { get; private set; }

	public float LeftX { get; private set; } = -48f;
	public float RightX { get; private set; } = 48f;
	public float BaseFloorZ { get; private set; } = -188f;
	public float HillAmplitude { get; private set; } = 16f;
	public float SwimClearance { get; private set; } = 2.4f;
	public float BoatX { get; private set; }

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;
		LeftX = balance.SeabedLeftX;
		RightX = balance.SeabedRightX;
		BaseFloorZ = balance.SeabedBaseFloorZ;
		HillAmplitude = balance.SeabedHillAmplitude;
		SwimClearance = balance.SeabedSwimClearance;
		BoatX = balance.SurfaceSpawnX;

		BuildSeafloorVisuals( balance );
	}

	public float DeepFloorZ => BaseFloorZ - HillAmplitude;

	/// <summary>Seafloor Z at world X — mostly flat abyss with gentle hills (not a diagonal slope).</summary>
	public float FloorZAtX( float x )
	{
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;
		var minZ = balance.MinWorldZ + 4f;
		var hills =
			MathF.Sin( x * 0.11f ) * HillAmplitude * 0.55f +
			MathF.Sin( x * 0.047f + 1.3f ) * HillAmplitude * 0.45f +
			MathF.Sin( x * 0.23f + 0.4f ) * 3.5f;
		var z = BaseFloorZ + hills;
		if ( z < minZ ) z = minZ;
		return z;
	}

	/// <summary>Random-ish X across the play span (replaces old slope X-at-depth coupling).</summary>
	public float SampleX( float t )
	{
		if ( t < 0f ) t = 0f;
		if ( t > 1f ) t = 1f;
		return LeftX + 3f + (RightX - LeftX - 6f) * t;
	}

	public Vector3 GroundAnchor( float x, float hover = 0f ) =>
		new( x, 0f, FloorZAtX( x ) + hover );

	/// <summary>
	/// Place a ground sprite so its visual bottom sits on the seafloor.
	/// Uses content aspect so tall art does not float as a mid-water dirt slab.
	/// </summary>
	public Vector3 GroundSprite( float x, float worldHeight, float yLayer = 0f )
	{
		// Bottom of the sprite ≈ floor; center sits at ~half height.
		var hover = worldHeight * 0.48f;
		var a = GroundAnchor( x, hover );
		return new Vector3( a.x, yLayer, a.z );
	}

	public bool IsNearBoat( Vector3 pos )
	{
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;
		var dx = pos.x - BoatX;
		return MathF.Abs( dx ) <= balance.BoatReturnRadius
			&& pos.z >= balance.SurfaceZ - 8f;
	}

	/// <summary>Legacy name used by surface success — boat return, not a dock wall.</summary>
	public bool IsInDockZone( Vector3 pos ) => IsNearBoat( pos );

	public Vector3 ClampSwimPosition( Vector3 pos )
	{
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;

		if ( pos.x < LeftX + 1.5f ) pos.x = LeftX + 1.5f;
		if ( pos.x > RightX - 1.5f ) pos.x = RightX - 1.5f;

		var maxZ = balance.SurfaceSpawnZ + 0.5f;
		if ( pos.z > maxZ ) pos.z = maxZ;

		// Only the seafloor pushes you — never a mid-water "cave column" crush.
		var floor = FloorZAtX( pos.x ) + SwimClearance;
		if ( pos.z < floor ) pos.z = floor;

		pos.y = 0f;
		return pos;
	}

	private void BuildSeafloorVisuals( BalanceConfig balance )
	{
		var fill = DeepDivePixelArt.SeabedFill();
		var ridge = DeepDivePixelArt.SeabedRidge();
		var chunk = DeepDivePixelArt.SeabedChunk();
		var abyss = DeepDivePixelArt.AbyssSilhouette();
		var rocks = DeepDivePixelArt.Rocks();

		var yFar = balance.BackdropY - 6f;
		var yMid = balance.BackdropY - 2.5f;
		var yNear = balance.BackdropY + 1.4f;
		var yDress = balance.BackdropY + 2.1f;

		BuildAbyssParallax( abyss, rocks, yFar, balance );
		BuildSedimentMass( fill, yMid );
		BuildSandRidge( ridge, yNear );
		BuildSurfaceChunks( chunk, yDress );
	}

	/// <summary>Far darkened trench wall — depth read without grey boxes.</summary>
	private void BuildAbyssParallax( Texture abyss, Texture rocks, float y, BalanceConfig balance )
	{
		var span = RightX - LeftX;
		if ( abyss is not null && abyss.IsValid() && abyss != Texture.White )
		{
			const int plates = 3;
			for ( var i = 0; i < plates; i++ )
			{
				var t = (i + 0.5f) / plates;
				var x = LeftX + span * t;
				var height = 38f + (i % 2) * 6f;
				var floorZ = FloorZAtX( x );
				var root = new GameObject( GameObject, true, $"AbyssSilhouette_{i}" );
				root.WorldPosition = new Vector3( x, y, floorZ - height * 0.22f );
				var sr = DeepDiveSprites.SpawnTexture( root, abyss, height, name: "Sprite" );
				sr.Color = new Color( 0.35f, 0.45f, 0.7f, 0.85f );
				sr.FogStrength = 0.35f;
			}
		}

		if ( rocks is null || !rocks.IsValid() || rocks == Texture.White )
			return;

		for ( var i = 0; i < 5; i++ )
		{
			var t = i / 4f;
			var x = LeftX + 6f + (span - 12f) * t;
			var height = 14f + (i % 3) * 2.5f;
			var floorZ = FloorZAtX( x );
			var root = new GameObject( GameObject, true, $"AbyssRock_{i}" );
			// Sit behind / under the play floor so they read as trench wall, not floating shelves.
			root.WorldPosition = new Vector3( x, y + 0.8f, floorZ - height * 0.55f - 8f );
			var sr = DeepDiveSprites.SpawnTexture( root, rocks, height, name: "Sprite" );
			sr.Color = new Color( 0.25f, 0.3f, 0.45f, 0.85f );
		}
	}

	/// <summary>
	/// Continuous sedimentary under-mass. Keep tall aspect — never squash into short dirt tiles.
	/// </summary>
	private void BuildSedimentMass( Texture fill, float y )
	{
		if ( fill is null || !fill.IsValid() || fill == Texture.White )
		{
			Log.Warning( "[DeepDive] seabed_fill missing — seafloor under-mass will be thin." );
			return;
		}

		const int segments = 12;
		var span = RightX - LeftX + 10f;
		var left = LeftX - 5f;
		var aspect = fill.Width / (float)MathF.Max( 1, fill.Height );

		for ( var i = 0; i < segments; i++ )
		{
			var t = (i + 0.5f) / segments;
			var x = left + span * t;
			var floorZ = FloorZAtX( x );
			var height = 42f + MathF.Abs( MathF.Sin( i * 1.3f ) ) * 6f;
			var tileW = (span / segments) * 1.08f;
			var root = new GameObject( GameObject, true, $"SeafloorFill_{i}" );
			// Top of fill sits just under the swim floor.
			root.WorldPosition = new Vector3( x, y + (i % 2) * 0.1f, floorZ - height * 0.45f );
			var sr = DeepDiveSprites.SpawnTexture( root, fill, height, name: "Sprite" );
			sr.Size = new Vector2( tileW, height );
			_ = aspect;
			var deep = i / (float)MathF.Max( 1, segments - 1 );
			sr.Color = Color.Lerp(
				new Color( 0.88f, 0.86f, 0.84f ),
				new Color( 0.4f, 0.48f, 0.62f ),
				deep * 0.4f );
		}
	}

	/// <summary>Wavy sand-top strip that hides the fill seam at the swim floor.</summary>
	private void BuildSandRidge( Texture ridge, float y )
	{
		if ( ridge is null || !ridge.IsValid() || ridge == Texture.White )
			return;

		const int ridges = 10;
		var span = RightX - LeftX + 6f;
		var left = LeftX - 3f;

		for ( var i = 0; i < ridges; i++ )
		{
			var t = (i + 0.5f) / ridges;
			var x = left + span * t;
			var height = 6.5f + (i % 3) * 0.6f;
			var root = new GameObject( GameObject, true, $"SeafloorRidge_{i}" );
			root.WorldPosition = GroundSprite( x, height, y + (i % 2) * 0.15f );
			var sr = DeepDiveSprites.SpawnTexture( root, ridge, height, name: "Sprite" );
			var tileW = (span / ridges) * 1.12f;
			sr.Size = new Vector2( tileW, height );
		}
	}

	/// <summary>
	/// Sparse organic shelves — not a stair-step row of dirt blocks across the floor.
	/// </summary>
	private void BuildSurfaceChunks( Texture chunk, float y )
	{
		if ( chunk is null || !chunk.IsValid() || chunk == Texture.White )
		{
			Log.Warning( "[DeepDive] seabed_chunk missing — surface shelves skipped." );
			return;
		}

		// Fewer, larger, intentionally placed mounds instead of 28 overlapping tiles.
		float[] xs = [ -40f, -28f, -14f, -2f, 10f, 24f, 38f ];
		for ( var i = 0; i < xs.Length; i++ )
		{
			var x = xs[i] + MathF.Sin( i * 1.7f ) * 1.1f;
			var height = 11f + (i % 3) * 1.6f;
			var root = new GameObject( GameObject, true, $"SeafloorShelf_{i}" );
			root.WorldPosition = GroundSprite( x, height, y + (i % 2) * 0.12f );
			// Keep natural aspect from ApplyWorldScale — do not force-squash into rectangles.
			DeepDiveSprites.SpawnTexture( root, chunk, height, name: "Sprite" );
		}
	}
}
