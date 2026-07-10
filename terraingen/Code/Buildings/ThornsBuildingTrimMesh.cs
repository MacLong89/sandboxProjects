namespace Terraingen.Buildings;

/// <summary>
/// Perimeter trim / corner posts with world-space floor UVs (matches thorns
/// <c>ThornsBuildingVisuals.BuildPerimeterTrimPanelModel</c>).
/// </summary>
public static class ThornsBuildingTrimMesh
{
	const float FloorUvTileWorldUnits = 28f;

	static readonly Dictionary<string, Model> BoxByKey = new( StringComparer.OrdinalIgnoreCase );

	public static float ProcPerimeterRunWorld( int cellsAlongEdge ) =>
		cellsAlongEdge * ThornsBuildingModule.Cell + ThornsBuildingModule.WallThickness;

	public static Model GetTrimBox( string materialSlug, Vector3 worldSize )
	{
		var key = $"{materialSlug}|{worldSize.x:F1}|{worldSize.y:F1}|{worldSize.z:F1}";
		if ( BoxByKey.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		var mat = LoadTrimMaterial( materialSlug );
		cached = BuildBoxModel( ThornsProcModelNaming.Sanitize( $"terraingen/building/trim_{key}" ), worldSize, mat );
		BoxByKey[key] = cached;
		return cached;
	}

	static Material LoadTrimMaterial( string materialSlug )
	{
		var mat = Material.Load( $"{ThornsProcBuildingMaterialCatalog.PathPrefix}{materialSlug}.vmat" );
		return mat is not null && mat.IsValid() ? mat : Material.Load( "materials/default/default.vmat" );
	}

	static Model BuildBoxModel( string name, Vector3 size, Material mat )
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();
		AddBox( vb, collVerts, collIdx, Vector3.Zero, size );

		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( ThornsProcModelNaming.Sanitize( name ) );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		if ( collVerts.Count > 0 && collIdx.Count > 0 )
			mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );
		return mb.Create();
	}

	static void AddBox(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 center,
		Vector3 size )
	{
		var h = size * 0.5f;
		var x0 = center.x - h.x;
		var x1 = center.x + h.x;
		var y0 = center.y - h.y;
		var y1 = center.y + h.y;
		var z0 = center.z - h.z;
		var z1 = center.z + h.z;

		var p000 = new Vector3( x0, y0, z0 );
		var p001 = new Vector3( x0, y0, z1 );
		var p010 = new Vector3( x0, y1, z0 );
		var p011 = new Vector3( x0, y1, z1 );
		var p100 = new Vector3( x1, y0, z0 );
		var p101 = new Vector3( x1, y0, z1 );
		var p110 = new Vector3( x1, y1, z0 );
		var p111 = new Vector3( x1, y1, z1 );

		void Quad( Vector3 a, Vector3 b, Vector3 c, Vector3 d ) =>
			AddQuad( vb, collVerts, collIdx, a, b, c, d );

		Quad( p000, p001, p011, p010 );
		Quad( p100, p110, p111, p101 );
		Quad( p000, p100, p101, p001 );
		Quad( p010, p011, p111, p110 );
		Quad( p001, p101, p111, p011 );
		Quad( p000, p010, p110, p100 );
	}

	static void AddQuad(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 d )
	{
		var n = UnitNormal( b - a, c - a );
		AddTri( vb, collVerts, collIdx, a, b, c, UvFloor( a, n ), UvFloor( b, n ), UvFloor( c, n ) );
		AddTri( vb, collVerts, collIdx, a, c, d, UvFloor( a, n ), UvFloor( c, n ), UvFloor( d, n ) );
	}

	static void AddTri(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector2 uva,
		Vector2 uvb,
		Vector2 uvc,
		bool addCollision = true )
	{
		var n = UnitNormal( b - a, c - a );
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: UnitNormal( Vector3.Up, n );

		vb.Add( new Vertex( a, n, tan, new Vector4( uva.x, uva.y, 0f, 0f ) ) );
		vb.Add( new Vertex( b, n, tan, new Vector4( uvb.x, uvb.y, 0f, 0f ) ) );
		vb.Add( new Vertex( c, n, tan, new Vector4( uvc.x, uvc.y, 0f, 0f ) ) );

		if ( !addCollision )
			return;

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}

	static Vector2 UvFloor( Vector3 p, Vector3 n )
	{
		var nx = MathF.Abs( n.x );
		var ny = MathF.Abs( n.y );
		var nz = MathF.Abs( n.z );
		if ( nz >= nx && nz >= ny )
			return new Vector2( p.x / FloorUvTileWorldUnits, p.y / FloorUvTileWorldUnits );
		if ( nx >= ny && nx >= nz )
			return new Vector2( p.y / FloorUvTileWorldUnits, p.z / FloorUvTileWorldUnits );
		return new Vector2( p.x / FloorUvTileWorldUnits, p.z / FloorUvTileWorldUnits );
	}

	static Vector3 UnitNormal( Vector3 edgeA, Vector3 edgeB )
	{
		var cross = Vector3.Cross( edgeA, edgeB );
		var lenSq = cross.LengthSquared;
		return lenSq < 1e-14f ? Vector3.Up : cross / MathF.Sqrt( lenSq );
	}
}
