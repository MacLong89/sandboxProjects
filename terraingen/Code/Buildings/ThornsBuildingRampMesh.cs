namespace Terraingen.Buildings;

/// <summary>Procedural <c>wood_ramp</c> mesh matching thorns <c>ThornsBuildingVisuals.BuildRampModel</c> dimensions.</summary>
public static class ThornsBuildingRampMesh
{
	const int RampCollisionVersion = 2;

	static Model _cached;
	static int _builtVersion;

	public static Model GetOrCreate( string materialSlug = "wood" )
	{
		if ( _cached.IsValid() && !_cached.IsError && _builtVersion == RampCollisionVersion )
			return _cached;

		_cached = BuildRampModel( materialSlug );
		_builtVersion = RampCollisionVersion;
		return _cached;
	}

	static Model BuildRampModel( string materialSlug )
	{
		var mat = Material.Load( $"{ThornsProcBuildingMaterialCatalog.PathPrefix}{materialSlug}.vmat" );
		if ( mat is null || !mat.IsValid() )
			mat = Material.Load( "materials/default/default.vmat" );

		var cs = ThornsBuildingModule.Cell;
		var wh = ThornsBuildingModule.WallHeight;
		var ft = ThornsBuildingModule.FloorThickness;
		var t = ThornsBuildingModule.WallThickness;

		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var x0 = -cs * 0.5f;
		var x1 = cs * 0.5f;
		var y0 = -cs * 0.5f;
		var y1 = cs * 0.5f;
		var z0 = -wh * 0.5f;
		var z1 = wh * 0.5f;

		var a = new Vector3( x0, y0, z0 );
		var c = new Vector3( x1, y0, z1 );
		var d = new Vector3( x0, y1, z0 );
		var f = new Vector3( x1, y1, z1 );

		var rise = c - a;
		var run = d - a;
		var deckNormal = Vector3.Cross( rise, run );
		if ( deckNormal.LengthSquared < 1e-8f )
			deckNormal = Vector3.Up;
		else
			deckNormal = deckNormal.Normal;
		if ( deckNormal.y < 0f )
			deckNormal = -deckNormal;

		var thickness = MathF.Max( ft, 4f );
		var inset = deckNormal * thickness;
		var a2 = a - inset;
		var c2 = c - inset;
		var d2 = d - inset;
		var f2 = f - inset;

		AddQuad( vb, collVerts, collIdx, a, c, f, d, addVisual: false, addCollision: false );
		AddQuad( vb, collVerts, collIdx, a2, d2, f2, c2 );
		AddQuad( vb, collVerts, collIdx, a, d, d2, a2 );
		AddQuad( vb, collVerts, collIdx, c, f, f2, c2 );
		AddQuad( vb, collVerts, collIdx, a, c, c2, a2 );
		AddQuad( vb, collVerts, collIdx, d, f, f2, d2 );
		AppendRampStairVisuals( vb, collVerts, collIdx, x0, x1, y0, y1, z0, z1, ft, t );

		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( ThornsProcModelNaming.Sanitize( $"wood_ramp_{materialSlug}" ) );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );
		return mb.Create();
	}

	static void AppendRampStairVisuals(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		float x0,
		float x1,
		float y0,
		float y1,
		float z0,
		float z1,
		float ft,
		float wallThickness )
	{
		var rise = z1 - z0;
		var run = x1 - x0;
		if ( rise < 1f || run < 1f )
			return;

		var stepCount = Math.Clamp( (int)MathF.Round( rise / 11f ), 7, 12 );
		var risePerStep = rise / stepCount;
		var treadDepth = run / stepCount;
		var treadThickness = MathF.Max( ft * 0.85f, 4f );
		var riserThickness = MathF.Max( wallThickness * 0.65f, 3f );
		var treadSpanY = ( y1 - y0 ) * 0.94f;
		var yMid = ( y0 + y1 ) * 0.5f;

		for ( var i = 0; i < stepCount; i++ )
		{
			var zBottom = z0 + i * risePerStep;
			var zTop = z0 + ( i + 1 ) * risePerStep;
			var xFront = x0 + i * treadDepth;
			var xBack = x0 + ( i + 1 ) * treadDepth;
			var xMid = ( xFront + xBack ) * 0.5f;

			AddBox(
				vb,
				collVerts,
				collIdx,
				new Vector3( xMid, yMid, zTop - treadThickness * 0.5f ),
				new Vector3( treadDepth * 0.96f, treadSpanY, treadThickness ),
				addCollision: true );

			if ( i > 0 )
			{
				AddBox(
					vb,
					collVerts,
					collIdx,
					new Vector3( xFront, yMid, ( zBottom + zTop ) * 0.5f ),
					new Vector3( riserThickness, treadSpanY, risePerStep * 0.98f ),
					addCollision: true );
			}
		}

		AddBox(
			vb,
			collVerts,
			collIdx,
			new Vector3( x0 + riserThickness * 0.5f, yMid, z0 + risePerStep * 0.45f ),
			new Vector3( riserThickness, treadSpanY, risePerStep * 0.9f ),
			addCollision: true );
	}

	static void AddBox(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 center,
		Vector3 size,
		bool addCollision )
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

		AddQuad( vb, collVerts, collIdx, p000, p001, p011, p010, addCollision: addCollision );
		AddQuad( vb, collVerts, collIdx, p100, p110, p111, p101, addCollision: addCollision );
		AddQuad( vb, collVerts, collIdx, p000, p100, p101, p001, addCollision: addCollision );
		AddQuad( vb, collVerts, collIdx, p010, p011, p111, p110, addCollision: addCollision );
		AddQuad( vb, collVerts, collIdx, p001, p101, p111, p011, addCollision: addCollision );
		AddQuad( vb, collVerts, collIdx, p000, p010, p110, p100, addCollision: addCollision );
	}

	static void AddQuad(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 d,
		bool addVisual = true,
		bool addCollision = true )
	{
		AddTri( vb, collVerts, collIdx, a, b, c, addVisual, addCollision );
		AddTri( vb, collVerts, collIdx, a, c, d, addVisual, addCollision );
	}

	static void AddTri(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		bool addVisual,
		bool addCollision )
	{
		var n = Vector3.Cross( b - a, c - a );
		if ( n.LengthSquared < 1e-14f )
			n = Vector3.Up;
		else
			n = n.Normal;

		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: Vector3.Cross( Vector3.Up, n ).Normal;

		if ( addVisual && vb is not null )
		{
			vb.Add( new Vertex( a, n, tan, Vector4.One ) );
			vb.Add( new Vertex( b, n, tan, Vector4.One ) );
			vb.Add( new Vertex( c, n, tan, Vector4.One ) );
		}

		if ( !addCollision || collVerts is null || collIdx is null )
			return;

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}
}
