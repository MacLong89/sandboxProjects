namespace FinalOutpost;

/// <summary>
/// Builds a small low-poly heightfield the player can actually stand on. Gentle rolling
/// hills come from layered value noise; the mesh carries a collision mesh so the physics
/// controller has ground to walk on (the old flat quad had no collider — hence the fall).
/// Height is centred on z=0 and flattened toward the middle so the command post, walls and
/// turrets still sit level.
/// </summary>
public sealed class OutpostTerrain : Component
{
	// Covers the whole plot field plus the enemy spawn ring with a margin.
	private static float Extent => GameConstants.ActiveTerrainHalfExtent;
	private static float CellSize => GameConstants.TerrainCellSize;
	private const float Amplitude = GameConstants.TerrainAmplitude;
	// Keep the entire home base (walls + build grid) flat; hills only start past it.
	private const float FlatRadius = GameConstants.ArenaHalf;         // fully flat under the base
	private const float FlatFalloff = GameConstants.ArenaHalf + 320f; // blend to full hills past this

	public void Build()
	{
		BuildSeaLevel();

		var model = BuildModel();

		var grass = StylizedMaterials.Grass;
		var useTexture = grass is not null && grass.IsValid() && grass != MeshPrimitives.Mat;

		var renderer = Components.GetOrCreate<ModelRenderer>();
		renderer.Model = model;
		renderer.MaterialOverride = grass;
		renderer.Tint = useTexture ? Color.White : new Color( 0.45f, 0.6f, 0.32f );

		var collider = Components.GetOrCreate<ModelCollider>();
		collider.Model = model;
		collider.Static = true;
	}

	/// <summary>One flat stylized water sheet under the whole map — replaces per-plot pond props.</summary>
	private void BuildSeaLevel()
	{
		var span = Extent * 2f + GameConstants.SeaSheetOvershoot;

		var go = new GameObject( GameObject, true, "SeaLevel" );
		go.WorldPosition = new Vector3( 0f, 0f, GameConstants.SeaLevel );
		go.LocalScale = MeshPrimitives.QuadScale( span, span );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Quad;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 0.22f, 0.48f, 0.78f );
	}

	/// <summary>World-space ground height at the given XY. Used for mesh gen and safe spawns.</summary>
	public static float SampleHeight( float x, float y )
	{
		var n = ValueNoise( x * 0.0055f, y * 0.0055f ) * 0.65f
			+ ValueNoise( x * 0.013f + 41.7f, y * 0.013f + 17.3f ) * 0.35f;

		var h = (n - 0.5f) * 2f * Amplitude;

		// Flatten toward the centre so structures stay level.
		var dist = MathF.Sqrt( x * x + y * y );
		var flat = MathF.Min( 1f, MathF.Max( 0f, (dist - FlatRadius) / (FlatFalloff - FlatRadius) ) );
		h *= flat;

		// Dip terrain toward sea level only on the outer rim so the global water plane peeks through at the coast.
		var rim = Extent - GameConstants.SeaRimBlendWidth;
		var absMax = MathF.Max( MathF.Abs( x ), MathF.Abs( y ) );
		if ( absMax > rim )
		{
			var blend = GameConstants.SeaRimBlendWidth;
			var t = Math.Clamp( (absMax - rim) / blend, 0f, 1f );
			var shore = GameConstants.SeaLevel + 4f;
			h = shore + (h - shore) * (1f - t);
		}

		return h;
	}

	private Model BuildModel()
	{
		var steps = (int)MathF.Ceiling( (Extent * 2f) / CellSize );
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		for ( var iy = 0; iy < steps; iy++ )
		for ( var ix = 0; ix < steps; ix++ )
		{
			var x0 = -Extent + ix * CellSize;
			var y0 = -Extent + iy * CellSize;
			var x1 = x0 + CellSize;
			var y1 = y0 + CellSize;

			var a = new Vector3( x0, y0, SampleHeight( x0, y0 ) );
			var b = new Vector3( x1, y0, SampleHeight( x1, y0 ) );
			var c = new Vector3( x1, y1, SampleHeight( x1, y1 ) );
			var d = new Vector3( x0, y1, SampleHeight( x0, y1 ) );

			AddTri( vb, collVerts, collIdx, a, b, c );
			AddTri( vb, collVerts, collIdx, a, c, d );
		}

		var mesh = new Mesh( MeshPrimitives.Mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( "final_outpost/terrain" );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		if ( collVerts.Count > 0 && collIdx.Count > 0 )
			mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );

		return mb.Create();
	}

	// --- Deterministic value noise (no asset/API dependency) ---
	private static float ValueNoise( float x, float y )
	{
		var xi = (int)MathF.Floor( x );
		var yi = (int)MathF.Floor( y );
		var xf = x - xi;
		var yf = y - yi;

		var u = xf * xf * (3f - 2f * xf);
		var v = yf * yf * (3f - 2f * yf);

		var a = Hash( xi, yi );
		var b = Hash( xi + 1, yi );
		var c = Hash( xi, yi + 1 );
		var d = Hash( xi + 1, yi + 1 );

		return Lerp( Lerp( a, b, u ), Lerp( c, d, u ), v );
	}

	private static float Lerp( float a, float b, float t ) => a + (b - a) * t;

	private static float Hash( int x, int y )
	{
		unchecked
		{
			var h = x * 374761393 + y * 668265263;
			h = (h ^ (h >> 13)) * 1274126177;
			h ^= h >> 16;
			return (h & 0x7fffffff) / (float)0x7fffffff;
		}
	}

	private static void AddTri( VertexBuffer vb, List<Vector3> collVerts, List<int> collIdx, Vector3 a, Vector3 b, Vector3 c )
	{
		var n = Vector3.Cross( b - a, c - a ).Normal;
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: Vector3.Cross( Vector3.Up, n ).Normal;

		vb.Add( new Vertex( a, n, tan, new Vector4( a.x / 128f, a.y / 128f, 0f, 0f ) ) );
		vb.Add( new Vertex( b, n, tan, new Vector4( b.x / 128f, b.y / 128f, 0f, 0f ) ) );
		vb.Add( new Vertex( c, n, tan, new Vector4( c.x / 128f, c.y / 128f, 0f, 0f ) ) );

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}
}
