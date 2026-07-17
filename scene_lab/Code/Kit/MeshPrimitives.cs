namespace SceneLab;

/// <summary>
/// Primitives with Bounds-derived LocalScale. Cylinder is procedural (Z-axis unit).
/// Prefer <see cref="MatOpaque"/> for kits — materials/default.vmat dithers when volumes overlap.
/// </summary>
public static class MeshPrimitives
{
	private static Model _quad;
	private static Model _box;
	private static Model _sphere;
	private static Model _cylinder;
	private static Material _mat;
	private static Material _matOpaque;
	private static Material _matGlass;

	public static Model Quad => _quad ??= Model.Load( "models/dev/plane.vmdl" );
	public static Model Box => _box ??= Model.Load( "models/dev/box.vmdl" );
	public static Model Sphere => _sphere ??= Model.Load( "models/dev/sphere.vmdl" );
	public static Model Cylinder => _cylinder ??= BuildCylinder( 18 );

	/// <summary>Legacy default — may dither. Prefer <see cref="MatOpaque"/>.</summary>
	public static Material Mat => _mat ??= Material.Load( "materials/default.vmat" );

	public static Material MatOpaque =>
		_matOpaque ??= Material.Load( "materials/kit_opaque.vmat" ) ?? Mat;

	/// <summary>Tint alpha see-through panes (windows). Falls back to default if missing.</summary>
	public static Material MatGlass =>
		_matGlass ??= Material.Load( "materials/kit_glass.vmat" ) ?? Mat;

	public static Vector3 BoxScale( Vector3 worldSize ) => ScaleFor( Box, worldSize );

	public static Vector3 SphereScale( float diameter )
	{
		var size = Sphere.Bounds.Size;
		var d = size.x > 0.001f ? diameter / size.x : 1f;
		return new Vector3( d, d, d );
	}

	public static Vector3 CylinderScale( float diameter, float lengthAlongAxis )
		=> ScaleFor( Cylinder, new Vector3( diameter, diameter, lengthAlongAxis ) );

	public static Vector3 ScaleFor( Model model, Vector3 worldSize )
	{
		var size = model.Bounds.Size;
		return new Vector3(
			size.x > 0.001f ? worldSize.x / size.x : 1f,
			size.y > 0.001f ? worldSize.y / size.y : 1f,
			size.z > 0.001f ? worldSize.z / size.z : 1f );
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

			AddVert( vb, new Vector3( 0f, 0f, zTop ), Vector3.Up, new Vector2( 0.5f, 0.5f ) );
			AddVert( vb, p0t, Vector3.Up, new Vector2( c0.x * 0.5f + 0.5f, c0.y * 0.5f + 0.5f ) );
			AddVert( vb, p1t, Vector3.Up, new Vector2( c1.x * 0.5f + 0.5f, c1.y * 0.5f + 0.5f ) );

			AddVert( vb, new Vector3( 0f, 0f, zBot ), Vector3.Down, new Vector2( 0.5f, 0.5f ) );
			AddVert( vb, p1b, Vector3.Down, new Vector2( c1.x * 0.5f + 0.5f, c1.y * 0.5f + 0.5f ) );
			AddVert( vb, p0b, Vector3.Down, new Vector2( c0.x * 0.5f + 0.5f, c0.y * 0.5f + 0.5f ) );
		}

		var mesh = new Mesh( MatOpaque, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );
		var mb = new ModelBuilder();
		mb.WithName( "scene_lab/cylinder" );
		mb.AddMesh( mesh );
		return mb.Create();
	}

	private static void AddVert( VertexBuffer vb, Vector3 pos, Vector3 normal, Vector2 uv )
	{
		var tan = MathF.Abs( Vector3.Dot( normal, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: Vector3.Cross( Vector3.Up, normal ).Normal;
		vb.Add( new Vertex( pos, normal, tan, new Vector4( uv.x, uv.y, 0f, 0f ) ) );
	}
}
