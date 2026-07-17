namespace FinalOutpost;

public static class MeshPrimitives
{
	private static Model _quad;
	private static Model _box;
	private static Model _cylinder;
	private static Model _pyramid;
	private static Material _mat;
	private static bool _loggedDevFallback;

	public static Model Quad => _quad ??= LoadOrBuildPlane();
	public static Model Box => _box ??= LoadOrBuildBox();
	public static Material Mat => _mat ??= LoadOrFlatMaterial();

	/// <summary>Unit cylinder (1x1x1 bounds, Z axis). Scale with <see cref="ScaleFor"/>.</summary>
	public static Model Cylinder => _cylinder ??= BuildCylinder( 20 );

	/// <summary>Unit square pyramid (1x1x1 bounds, apex +Z). Scale with <see cref="ScaleFor"/>.</summary>
	public static Model Pyramid => _pyramid ??= BuildPyramid();

	public static Vector3 QuadScale( float width, float height )
	{
		var size = Quad.Bounds.Size;
		return new Vector3(
			size.x > 0.001f ? width / size.x : 1f,
			size.y > 0.001f ? height / size.y : 1f,
			1f );
	}

	public static Vector3 BoxScale( Vector3 worldSize ) => ScaleFor( Box, worldSize );

	public static Vector3 ScaleFor( Model model, Vector3 worldSize )
	{
		var size = model.Bounds.Size;
		return new Vector3(
			size.x > 0.001f ? worldSize.x / size.x : 1f,
			size.y > 0.001f ? worldSize.y / size.y : 1f,
			size.z > 0.001f ? worldSize.z / size.z : 1f );
	}

	private static Model LoadOrBuildBox()
	{
		var loaded = AssetSafe.Model( "models/dev/box.vmdl" );
		if ( loaded is not null )
			return loaded;

		LogDevFallback( "box" );
		return BuildBox();
	}

	private static Model LoadOrBuildPlane()
	{
		var loaded = AssetSafe.Model( "models/dev/plane.vmdl" );
		if ( loaded is not null )
			return loaded;

		LogDevFallback( "plane" );
		return BuildPlane();
	}

	private static Material LoadOrFlatMaterial()
	{
		var loaded = AssetSafe.Material( "materials/default.vmat" );
		if ( loaded is not null )
			return loaded;

		Log.Warning( "[FinalOutpost] materials/default.vmat missing — procedural meshes may be untextured." );
		return null;
	}

	private static void LogDevFallback( string kind )
	{
		if ( _loggedDevFallback )
			return;

		_loggedDevFallback = true;
		Log.Info( $"[FinalOutpost] Engine models/dev/{kind} unavailable — using procedural mesh." );
	}

	// --- Procedural meshes (size 1 cube bounds, centered on origin) ---

	private static Model BuildBox()
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		// Front, back, left, right, top, bottom — each as two tris.
		AddFaceQuad( vb, new( -0.5f, -0.5f, -0.5f ), new( 0.5f, -0.5f, -0.5f ), new( 0.5f, -0.5f, 0.5f ), new( -0.5f, -0.5f, 0.5f ), Vector3.Backward );
		AddFaceQuad( vb, new( 0.5f, 0.5f, -0.5f ), new( -0.5f, 0.5f, -0.5f ), new( -0.5f, 0.5f, 0.5f ), new( 0.5f, 0.5f, 0.5f ), Vector3.Forward );
		AddFaceQuad( vb, new( -0.5f, 0.5f, -0.5f ), new( -0.5f, -0.5f, -0.5f ), new( -0.5f, -0.5f, 0.5f ), new( -0.5f, 0.5f, 0.5f ), Vector3.Left );
		AddFaceQuad( vb, new( 0.5f, -0.5f, -0.5f ), new( 0.5f, 0.5f, -0.5f ), new( 0.5f, 0.5f, 0.5f ), new( 0.5f, -0.5f, 0.5f ), Vector3.Right );
		AddFaceQuad( vb, new( -0.5f, -0.5f, 0.5f ), new( 0.5f, -0.5f, 0.5f ), new( 0.5f, 0.5f, 0.5f ), new( -0.5f, 0.5f, 0.5f ), Vector3.Up );
		AddFaceQuad( vb, new( -0.5f, 0.5f, -0.5f ), new( 0.5f, 0.5f, -0.5f ), new( 0.5f, -0.5f, -0.5f ), new( -0.5f, -0.5f, -0.5f ), Vector3.Down );

		return Finish( vb, "final_outpost/box" );
	}

	private static Model BuildPlane()
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		// XY plane, facing +Z (matches typical models/dev/plane usage).
		AddFaceQuad( vb,
			new( -0.5f, -0.5f, 0f ),
			new( 0.5f, -0.5f, 0f ),
			new( 0.5f, 0.5f, 0f ),
			new( -0.5f, 0.5f, 0f ),
			Vector3.Up );

		return Finish( vb, "final_outpost/plane" );
	}

	private static void AddFaceQuad( VertexBuffer vb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n )
	{
		AddVert( vb, a, n, new Vector2( 0f, 1f ) );
		AddVert( vb, b, n, new Vector2( 1f, 1f ) );
		AddVert( vb, c, n, new Vector2( 1f, 0f ) );

		AddVert( vb, a, n, new Vector2( 0f, 1f ) );
		AddVert( vb, c, n, new Vector2( 1f, 0f ) );
		AddVert( vb, d, n, new Vector2( 0f, 0f ) );
	}

	private static Model BuildCylinder( int segments )
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		const float r = 0.5f;
		const float zTop = 0.5f;
		const float zBot = -0.5f;

		for ( var i = 0; i < segments; i++ )
		{
			var a0 = i / (float)segments * MathF.PI * 2f;
			var a1 = (i + 1) / (float)segments * MathF.PI * 2f;

			var c0 = new Vector2( MathF.Cos( a0 ), MathF.Sin( a0 ) );
			var c1 = new Vector2( MathF.Cos( a1 ), MathF.Sin( a1 ) );

			var p0b = new Vector3( c0.x * r, c0.y * r, zBot );
			var p0t = new Vector3( c0.x * r, c0.y * r, zTop );
			var p1b = new Vector3( c1.x * r, c1.y * r, zBot );
			var p1t = new Vector3( c1.x * r, c1.y * r, zTop );

			var n0 = new Vector3( c0.x, c0.y, 0f );
			var n1 = new Vector3( c1.x, c1.y, 0f );

			var u0 = i / (float)segments;
			var u1 = (i + 1) / (float)segments;

			AddVert( vb, p0b, n0, new Vector2( u0, 1f ) );
			AddVert( vb, p1b, n1, new Vector2( u1, 1f ) );
			AddVert( vb, p1t, n1, new Vector2( u1, 0f ) );

			AddVert( vb, p0b, n0, new Vector2( u0, 1f ) );
			AddVert( vb, p1t, n1, new Vector2( u1, 0f ) );
			AddVert( vb, p0t, n0, new Vector2( u0, 0f ) );

			// Caps
			AddVert( vb, new Vector3( 0f, 0f, zTop ), Vector3.Up, new Vector2( 0.5f, 0.5f ) );
			AddVert( vb, p0t, Vector3.Up, new Vector2( c0.x * 0.5f + 0.5f, c0.y * 0.5f + 0.5f ) );
			AddVert( vb, p1t, Vector3.Up, new Vector2( c1.x * 0.5f + 0.5f, c1.y * 0.5f + 0.5f ) );

			AddVert( vb, new Vector3( 0f, 0f, zBot ), Vector3.Down, new Vector2( 0.5f, 0.5f ) );
			AddVert( vb, p1b, Vector3.Down, new Vector2( c1.x * 0.5f + 0.5f, c1.y * 0.5f + 0.5f ) );
			AddVert( vb, p0b, Vector3.Down, new Vector2( c0.x * 0.5f + 0.5f, c0.y * 0.5f + 0.5f ) );
		}

		return Finish( vb, "final_outpost/cylinder" );
	}

	private static Model BuildPyramid()
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var apex = new Vector3( 0f, 0f, 0.5f );
		var b0 = new Vector3( -0.5f, -0.5f, -0.5f );
		var b1 = new Vector3( 0.5f, -0.5f, -0.5f );
		var b2 = new Vector3( 0.5f, 0.5f, -0.5f );
		var b3 = new Vector3( -0.5f, 0.5f, -0.5f );

		AddFaceTri( vb, b0, b1, apex );
		AddFaceTri( vb, b1, b2, apex );
		AddFaceTri( vb, b2, b3, apex );
		AddFaceTri( vb, b3, b0, apex );

		// Base (facing down)
		AddFaceTri( vb, b2, b1, b0 );
		AddFaceTri( vb, b3, b2, b0 );

		return Finish( vb, "final_outpost/pyramid" );
	}

	private static void AddFaceTri( VertexBuffer vb, Vector3 a, Vector3 b, Vector3 c )
	{
		var n = Vector3.Cross( b - a, c - a ).Normal;
		AddVert( vb, a, n, new Vector2( 0f, 1f ) );
		AddVert( vb, b, n, new Vector2( 1f, 1f ) );
		AddVert( vb, c, n, new Vector2( 0.5f, 0f ) );
	}

	private static void AddVert( VertexBuffer vb, Vector3 pos, Vector3 normal, Vector2 uv )
	{
		var tan = MathF.Abs( Vector3.Dot( normal, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: Vector3.Cross( Vector3.Up, normal ).Normal;
		vb.Add( new Vertex( pos, normal, tan, new Vector4( uv.x, uv.y, 0f, 0f ) ) );
	}

	private static Model Finish( VertexBuffer vb, string name )
	{
		var mesh = new Mesh( Mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( name );
		mb.AddMesh( mesh );
		return mb.Create();
	}
}
