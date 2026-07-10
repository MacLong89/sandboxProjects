namespace Terraingen.Buildings;

/// <summary>
/// Procedural wall / window / doorframe meshes with world-space facade UVs (matches thorns
/// <c>ThornsBuildingVisuals.BuildFrameWallContinuousBackingModel</c> — one UV frame across window jambs).
/// </summary>
public static class ThornsBuildingWallMesh
{
	const float WallUvTileWorldUnits = 25f;

	const float WindowSillHeight = 34f;

	static readonly Dictionary<string, Model> SolidBySlug = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, Model> WindowBySlug = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, Model> DoorFrameBySlug = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, Model> DoorPanelBySlug = new( StringComparer.OrdinalIgnoreCase );

	public static float ProcWallRunWorld => ThornsBuildingModule.Cell + ThornsBuildingModule.WallThickness;

	public static Model GetSolidWall( string materialSlug, float runWorld = -1f ) =>
		GetSolidWallCached( materialSlug, ResolveRunWorld( runWorld ) );

	public static Model GetWindowWall( string materialSlug, float runWorld = -1f ) =>
		GetWindowWallCached( materialSlug, ResolveRunWorld( runWorld ) );

	public static Model GetDoorFrameWall( string materialSlug, float runWorld = -1f ) =>
		GetDoorFrameWallCached( materialSlug, ResolveRunWorld( runWorld ) );

	public static Model GetDoorPanel( string materialSlug ) => GetDoorPanelCached( materialSlug );

	static float ResolveRunWorld( float runWorld ) =>
		runWorld > 0f ? runWorld : ProcWallRunWorld;

	static string CacheKey( string materialSlug, float runWorld ) => $"{materialSlug}|{runWorld:F1}";

	static Model GetSolidWallCached( string materialSlug, float runWorld )
	{
		var key = CacheKey( materialSlug, runWorld );
		if ( SolidBySlug.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		var mat = LoadFacadeMaterial( materialSlug );
		var size = new Vector3( ThornsBuildingModule.WallThickness, runWorld, ThornsBuildingModule.WallHeight );
		cached = BuildBoxModel( ThornsProcModelNaming.Sanitize( $"terraingen/building/wall_{key}" ), size, mat );
		SolidBySlug[key] = cached;
		return cached;
	}

	static Model GetWindowWallCached( string materialSlug, float runWorld )
	{
		var key = CacheKey( materialSlug, runWorld );
		if ( WindowBySlug.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		var halfW = runWorld * 0.5f;
		var halfH = ThornsBuildingModule.WallHeight * 0.5f;
		var hole = ThornsBuildingModule.WindowHoleSize;
		var halfHoleW = MathF.Min( hole * 0.5f, halfW - 4f );
		var holeBottom = -halfH + WindowSillHeight;
		var holeTop = MathF.Min( halfH, holeBottom + hole );
		cached = BuildFrameWallModel(
			ThornsProcModelNaming.Sanitize( $"terraingen/building/window_{key}" ),
			halfW,
			halfH,
			halfHoleW,
			holeBottom,
			holeTop,
			LoadFacadeMaterial( materialSlug ) );
		WindowBySlug[key] = cached;
		return cached;
	}

	static Model GetDoorPanelCached( string materialSlug )
	{
		if ( DoorPanelBySlug.TryGetValue( materialSlug, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		var size = new Vector3(
			ThornsBuildingModule.WallThickness,
			ThornsBuildingModule.DoorWidth,
			ThornsBuildingModule.DoorHeight );
		cached = BuildBoxModel(
			ThornsProcModelNaming.Sanitize( $"terraingen/building/door_panel_{materialSlug}" ),
			size,
			LoadFacadeMaterial( materialSlug ) );
		DoorPanelBySlug[materialSlug] = cached;
		return cached;
	}

	static Model GetDoorFrameWallCached( string materialSlug, float runWorld )
	{
		var key = CacheKey( materialSlug, runWorld );
		if ( DoorFrameBySlug.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		var halfW = runWorld * 0.5f;
		var halfH = ThornsBuildingModule.WallHeight * 0.5f;
		var halfHoleW = MathF.Min( ThornsBuildingModule.DoorWidth * 0.5f, halfW - 4f );
		var holeBottom = -halfH;
		var holeTop = MathF.Min( halfH, holeBottom + ThornsBuildingModule.DoorHeight );
		cached = BuildFrameWallModel(
			ThornsProcModelNaming.Sanitize( $"terraingen/building/doorframe_{key}" ),
			halfW,
			halfH,
			halfHoleW,
			holeBottom,
			holeTop,
			LoadFacadeMaterial( materialSlug ),
			omitHoleCeilingCollision: true );
		DoorFrameBySlug[key] = cached;
		return cached;
	}

	/// <summary>Yaw so mesh thickness (local X) aligns with building-local axes used by <see cref="ThornsProcBuildingShellSpawner"/>.</summary>
	public static Rotation RotationForSide( int side ) =>
		side switch
		{
			0 => Rotation.FromYaw( 90f ),
			2 => Rotation.FromYaw( -90f ),
			3 => Rotation.FromYaw( 180f ),
			_ => Rotation.Identity
		};

	static Material LoadFacadeMaterial( string materialSlug )
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
		AddBox( vb, collVerts, collIdx, Vector3.Zero, size, wallUv: true );

		return FinishModel( name, vb, collVerts, collIdx, mat );
	}

	static Model BuildFrameWallModel(
		string name,
		float halfW,
		float halfH,
		float halfHoleW,
		float holeBottom,
		float holeTop,
		Material mat,
		bool omitHoleCeilingCollision = false )
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var xf = ThornsBuildingModule.WallThickness * 0.5f;
		var xb = -xf;
		var holeTopLintelCollision = !omitHoleCeilingCollision;

		void AddWallQuad( Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool outwardPositiveX, bool addCollision = true )
		{
			var n = UnitNormal( b - a, c - a );
			var uva = UvWall( a, n );
			var uvb = UvWall( b, n );
			var uvc = UvWall( c, n );
			var uvd = UvWall( d, n );
			if ( outwardPositiveX )
			{
				AddTri( vb, collVerts, collIdx, a, b, c, uva, uvb, uvc, addCollision );
				AddTri( vb, collVerts, collIdx, a, c, d, uva, uvc, uvd, addCollision );
			}
			else
			{
				AddTri( vb, collVerts, collIdx, a, c, b, uva, uvc, uvb, addCollision );
				AddTri( vb, collVerts, collIdx, a, d, c, uva, uvd, uvc, addCollision );
			}
		}

		void AddMappedQuad( Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool addCollision = true )
		{
			var n = UnitNormal( b - a, c - a );
			AddTri( vb, collVerts, collIdx, a, b, c, UvWall( a, n ), UvWall( b, n ), UvWall( c, n ), addCollision );
			AddTri( vb, collVerts, collIdx, a, c, d, UvWall( a, n ), UvWall( c, n ), UvWall( d, n ), addCollision );
		}

		// Primary ±X faces — shared (y,z) UV frame across all backing strips.
		AddWallQuad( new Vector3( xf, -halfW, -halfH ), new Vector3( xf, -halfHoleW, -halfH ), new Vector3( xf, -halfHoleW, halfH ), new Vector3( xf, -halfW, halfH ), true );
		AddWallQuad( new Vector3( xf, halfHoleW, -halfH ), new Vector3( xf, halfW, -halfH ), new Vector3( xf, halfW, halfH ), new Vector3( xf, halfHoleW, halfH ), true );
		AddWallQuad( new Vector3( xf, -halfHoleW, -halfH ), new Vector3( xf, halfHoleW, -halfH ), new Vector3( xf, halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeBottom ), true );
		AddWallQuad( new Vector3( xf, -halfHoleW, holeTop ), new Vector3( xf, halfHoleW, holeTop ), new Vector3( xf, halfHoleW, halfH ), new Vector3( xf, -halfHoleW, halfH ), true, holeTopLintelCollision );

		AddWallQuad( new Vector3( xb, -halfW, -halfH ), new Vector3( xb, -halfHoleW, -halfH ), new Vector3( xb, -halfHoleW, halfH ), new Vector3( xb, -halfW, halfH ), false );
		AddWallQuad( new Vector3( xb, halfHoleW, -halfH ), new Vector3( xb, halfW, -halfH ), new Vector3( xb, halfW, halfH ), new Vector3( xb, halfHoleW, halfH ), false );
		AddWallQuad( new Vector3( xb, -halfHoleW, -halfH ), new Vector3( xb, halfHoleW, -halfH ), new Vector3( xb, halfHoleW, holeBottom ), new Vector3( xb, -halfHoleW, holeBottom ), false );
		AddWallQuad( new Vector3( xb, -halfHoleW, holeTop ), new Vector3( xb, halfHoleW, holeTop ), new Vector3( xb, halfHoleW, halfH ), new Vector3( xb, -halfHoleW, halfH ), false, holeTopLintelCollision );

		AddMappedQuad( new Vector3( xb, -halfHoleW, -halfH ), new Vector3( xf, -halfHoleW, -halfH ), new Vector3( xf, halfHoleW, -halfH ), new Vector3( xb, halfHoleW, -halfH ) );
		AddMappedQuad( new Vector3( xb, -halfHoleW, halfH ), new Vector3( xf, -halfHoleW, halfH ), new Vector3( xf, halfHoleW, halfH ), new Vector3( xb, halfHoleW, halfH ) );
		AddMappedQuad( new Vector3( xb, -halfW, -halfH ), new Vector3( xf, -halfW, -halfH ), new Vector3( xf, -halfW, halfH ), new Vector3( xb, -halfW, halfH ) );
		AddMappedQuad( new Vector3( xb, halfW, -halfH ), new Vector3( xf, halfW, -halfH ), new Vector3( xf, halfW, halfH ), new Vector3( xb, halfW, halfH ) );
		AddMappedQuad( new Vector3( xb, -halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeBottom ), new Vector3( xf, halfHoleW, holeBottom ), new Vector3( xb, halfHoleW, holeBottom ) );
		AddMappedQuad( new Vector3( xb, -halfHoleW, holeTop ), new Vector3( xf, -halfHoleW, holeTop ), new Vector3( xf, halfHoleW, holeTop ), new Vector3( xb, halfHoleW, holeTop ), holeTopLintelCollision );
		AddMappedQuad( new Vector3( xb, -halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeTop ), new Vector3( xb, -halfHoleW, holeTop ) );
		AddMappedQuad( new Vector3( xb, halfHoleW, holeBottom ), new Vector3( xf, halfHoleW, holeBottom ), new Vector3( xf, halfHoleW, holeTop ), new Vector3( xb, halfHoleW, holeTop ) );

		return FinishModel( name, vb, collVerts, collIdx, mat );
	}

	static Model FinishModel( string name, VertexBuffer vb, List<Vector3> collVerts, List<int> collIdx, Material mat )
	{
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
		Vector3 size,
		bool wallUv )
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
			AddQuad( vb, collVerts, collIdx, a, b, c, d, wallUv );

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
		Vector3 d,
		bool wallUv )
	{
		var n = UnitNormal( b - a, c - a );
		if ( wallUv )
		{
			AddTri( vb, collVerts, collIdx, a, b, c, UvWall( a, n ), UvWall( b, n ), UvWall( c, n ) );
			AddTri( vb, collVerts, collIdx, a, c, d, UvWall( a, n ), UvWall( c, n ), UvWall( d, n ) );
		}
		else
		{
			AddTri( vb, collVerts, collIdx, a, b, c );
			AddTri( vb, collVerts, collIdx, a, c, d );
		}
	}

	static void AddTri(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector2? uva = null,
		Vector2? uvb = null,
		Vector2? uvc = null,
		bool addCollision = true )
	{
		var n = UnitNormal( b - a, c - a );
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: UnitNormal( Vector3.Up, n );

		if ( uva.HasValue )
		{
			vb.Add( new Vertex( a, n, tan, new Vector4( uva.Value.x, uva.Value.y, 0f, 0f ) ) );
			vb.Add( new Vertex( b, n, tan, new Vector4( uvb!.Value.x, uvb.Value.y, 0f, 0f ) ) );
			vb.Add( new Vertex( c, n, tan, new Vector4( uvc!.Value.x, uvc.Value.y, 0f, 0f ) ) );
		}
		else
		{
			vb.Add( new Vertex( a, n, tan, Vector4.One ) );
			vb.Add( new Vertex( b, n, tan, Vector4.One ) );
			vb.Add( new Vertex( c, n, tan, Vector4.One ) );
		}

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

	static Vector2 UvWall( Vector3 p, Vector3 n )
	{
		var nx = MathF.Abs( n.x );
		var ny = MathF.Abs( n.y );
		var nz = MathF.Abs( n.z );
		if ( nx >= ny && nx >= nz )
			return new Vector2( p.y / WallUvTileWorldUnits, p.z / WallUvTileWorldUnits );
		if ( ny >= nx && ny >= nz )
			return new Vector2( p.x / WallUvTileWorldUnits, p.z / WallUvTileWorldUnits );
		return new Vector2( p.x / WallUvTileWorldUnits, p.y / WallUvTileWorldUnits );
	}

	static Vector3 UnitNormal( Vector3 edgeA, Vector3 edgeB )
	{
		var cross = Vector3.Cross( edgeA, edgeB );
		var lenSq = cross.LengthSquared;
		return lenSq < 1e-14f ? Vector3.Up : cross / MathF.Sqrt( lenSq );
	}
}
