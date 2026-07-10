namespace Terraingen.Buildings;

/// <summary>Procedural peaked roof meshes (ridge runs N–S; door faces north gable end).</summary>
public static class ThornsBuildingRoofMesh
{
	const float RoofUvTileWorldUnits = 32f;
	const float DefaultThickness = 6f;

	static readonly Dictionary<string, Model> Cache = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Full gable roof: ridge on Y, slopes on east/west, solid triangular prisms.</summary>
	public static Model GetPeakedRoof(
		string materialSlug,
		float widthWorld,
		float depthWorld,
		float riseWorld,
		float thicknessWorld = DefaultThickness )
	{
		var key = $"peak|{materialSlug}|{widthWorld:F1}|{depthWorld:F1}|{riseWorld:F1}|{thicknessWorld:F1}";
		if ( Cache.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		cached = BuildPeakedRoof( key, materialSlug, widthWorld, depthWorld, riseWorld, thicknessWorld );
		Cache[key] = cached;
		return cached;
	}

	/// <summary>Single-slope roof: low at south, high at north (door side).</summary>
	public static Model GetShedRoof(
		string materialSlug,
		float widthWorld,
		float depthWorld,
		float riseWorld,
		float thicknessWorld = DefaultThickness )
	{
		var key = $"shed|{materialSlug}|{widthWorld:F1}|{depthWorld:F1}|{riseWorld:F1}|{thicknessWorld:F1}";
		if ( Cache.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		cached = BuildShedRoof( key, materialSlug, widthWorld, depthWorld, riseWorld, thicknessWorld );
		Cache[key] = cached;
		return cached;
	}

	public static Model GetSawtoothRoof(
		string materialSlug,
		float widthWorld,
		float depthWorld,
		float riseWorld,
		int segments,
		float thicknessWorld = DefaultThickness )
	{
		segments = Math.Clamp( segments, 2, 5 );
		var key = $"sawroof3|{materialSlug}|{widthWorld:F1}|{depthWorld:F1}|{riseWorld:F1}|{segments}|{thicknessWorld:F1}";
		if ( Cache.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		cached = BuildSawtoothRoof( key, materialSlug, widthWorld, depthWorld, riseWorld, segments, thicknessWorld );
		Cache[key] = cached;
		return cached;
	}

	[Obsolete( "Use GetSawtoothRoof." )]
	public static Model GetSawtoothSegment( string materialSlug, float widthWorld, float depthWorld, float riseWorld, float thicknessWorld = DefaultThickness )
		=> GetSawtoothRoof(
			materialSlug,
			widthWorld,
			depthWorld,
			riseWorld,
			Math.Clamp( (int)MathF.Round( widthWorld / MathF.Max( 1f, depthWorld ) ), 2, 5 ),
			thicknessWorld );

	static Model BuildSawtoothRoof(
		string name,
		string materialSlug,
		float width,
		float depth,
		float rise,
		int segments,
		float thickness )
	{
		var mat = LoadMaterial( materialSlug );
		var vb = NewBuffer();
		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var halfW = width * 0.5f;
		var halfD = depth * 0.5f;
		var segmentWidth = width / segments;

		// Teeth run east–west (X); each tooth slopes up toward north (+Y, door side).
		for ( var i = 0; i < segments; i++ )
		{
			var x0 = -halfW + segmentWidth * i;
			var x1 = -halfW + segmentWidth * (i + 1 );
			AddSawtoothTooth(
				vb,
				collVerts,
				collIdx,
				x0,
				x1,
				-halfD,
				halfD,
				rise,
				thickness );
		}

		return FinishModel( name, vb, collVerts, collIdx, mat );
	}

	static void AddSawtoothTooth(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		float x0,
		float x1,
		float ySouth,
		float yNorth,
		float rise,
		float thickness )
	{
		var southWest = new Vector3( x0, ySouth, 0f );
		var southEast = new Vector3( x1, ySouth, 0f );
		var northWestBase = new Vector3( x0, yNorth, 0f );
		var northEastBase = new Vector3( x1, yNorth, 0f );
		var northWestTop = new Vector3( x0, yNorth, rise );
		var northEastTop = new Vector3( x1, yNorth, rise );
		var extrude = MathF.Max( thickness, MathF.Max( rise * 0.28f, 24f ) );

		AddSawtoothWedgePrism(
			vb,
			collVerts,
			collIdx,
			southWest,
			southEast,
			northWestBase,
			northEastBase,
			northWestTop,
			northEastTop,
			extrude );
	}

	/// <summary>Closed sawtooth bay: sloped south roof slab + vertical north clerestory + solid end caps.</summary>
	static void AddSawtoothWedgePrism(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 southWest,
		Vector3 southEast,
		Vector3 northWestBase,
		Vector3 northEastBase,
		Vector3 northWestTop,
		Vector3 northEastTop,
		float extrudeDepth )
	{
		var down = Vector3.Down * extrudeDepth;

		// Sloped south roof (top).
		AddDoubleSidedQuad( vb, collVerts, collIdx, southWest, southEast, northEastTop, northWestTop );

		// Parallel sloped underside — turns the ramp into a thick prism slab.
		AddDoubleSidedQuad(
			vb,
			collVerts,
			collIdx,
			southWest + down,
			northWestTop + down,
			northEastTop + down,
			southEast + down );

		// Vertical north clerestory above deck.
		AddDoubleSidedQuad( vb, collVerts, collIdx, northWestBase, northEastBase, northEastTop, northWestTop );

		// North wall foot down to the prism base.
		AddDoubleSidedQuad(
			vb,
			collVerts,
			collIdx,
			northWestBase + down,
			northEastBase + down,
			northEastBase,
			northWestBase );

		// South eave fascia below the slope line.
		AddDoubleSidedQuad( vb, collVerts, collIdx, southWest, southEast, southEast + down, southWest + down );

		// West / east edges of the sloped slab.
		AddDoubleSidedQuad( vb, collVerts, collIdx, southWest, southWest + down, northWestTop + down, northWestTop );
		AddDoubleSidedQuad( vb, collVerts, collIdx, southEast, northEastTop, northEastTop + down, southEast + down );

		AddSawtoothEndCap(
			vb,
			collVerts,
			collIdx,
			southWest,
			northWestBase,
			northWestTop,
			down );
		AddSawtoothEndCap(
			vb,
			collVerts,
			collIdx,
			southEast,
			northEastBase,
			northEastTop,
			down );
	}

	static void AddSawtoothEndCap(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 south,
		Vector3 northBase,
		Vector3 northTop,
		Vector3 down )
	{
		// Upper slope profile triangle.
		AddDoubleSidedTri( vb, collVerts, collIdx, south, northBase, northTop );

		// North clerestory thickness strip on the end cap.
		AddDoubleSidedQuad( vb, collVerts, collIdx, northBase, northTop, northTop + down, northBase + down );

		// South deck band down to the prism base.
		AddDoubleSidedQuad( vb, collVerts, collIdx, south, northBase, northBase + down, south + down );
	}

	static Model BuildPeakedRoof( string name, string materialSlug, float width, float depth, float rise, float thickness )
	{
		var mat = LoadMaterial( materialSlug );
		var vb = NewBuffer();
		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var halfW = width * 0.5f;
		var halfD = depth * 0.5f;

		// Ridge runs north–south (Y). Door is on the north wall (+Y gable end).
		var swEave = new Vector3( -halfW, -halfD, 0f );
		var seEave = new Vector3( halfW, -halfD, 0f );
		var nwEave = new Vector3( -halfW, halfD, 0f );
		var neEave = new Vector3( halfW, halfD, 0f );
		var southRidge = new Vector3( 0f, -halfD, rise );
		var northRidge = new Vector3( 0f, halfD, rise );

		// West slope (top + soffit + outer wall + gable ends).
		AddSlopePrism(
			vb,
			collVerts,
			collIdx,
			swEave,
			nwEave,
			northRidge,
			southRidge,
			new Vector3( -1f, 0f, 0f ),
			thickness );

		// East slope.
		AddSlopePrism(
			vb,
			collVerts,
			collIdx,
			seEave,
			neEave,
			northRidge,
			southRidge,
			new Vector3( 1f, 0f, 0f ),
			thickness );

		// Ridge cap strip along Y.
		AddDoubleSidedQuad(
			vb,
			collVerts,
			collIdx,
			southRidge + new Vector3( -3f, 0f, 0f ),
			northRidge + new Vector3( -3f, 0f, 0f ),
			northRidge + new Vector3( 3f, 0f, 0f ),
			southRidge + new Vector3( 3f, 0f, 0f ) );

		// North and south gable-end triangles (door faces north gable).
		AddDoubleSidedTri( vb, collVerts, collIdx, swEave, seEave, southRidge );
		AddDoubleSidedTri( vb, collVerts, collIdx, nwEave, neEave, northRidge );

		return FinishModel( name, vb, collVerts, collIdx, mat );
	}

	static Model BuildShedRoof( string name, string materialSlug, float width, float depth, float rise, float thickness )
	{
		var mat = LoadMaterial( materialSlug );
		var vb = NewBuffer();
		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var halfW = width * 0.5f;
		var halfD = depth * 0.5f;
		var lowSouthWest = new Vector3( -halfW, -halfD, 0f );
		var lowSouthEast = new Vector3( halfW, -halfD, 0f );
		var highNorthEast = new Vector3( halfW, halfD, rise );
		var highNorthWest = new Vector3( -halfW, halfD, rise );
		var down = Vector3.Down * thickness;

		AddDoubleSidedQuad( vb, collVerts, collIdx, lowSouthWest, lowSouthEast, highNorthEast, highNorthWest );
		AddDoubleSidedQuad( vb, collVerts, collIdx, lowSouthWest + down, highNorthWest + down, highNorthEast + down, lowSouthEast + down );
		AddDoubleSidedQuad( vb, collVerts, collIdx, lowSouthWest, lowSouthWest + down, highNorthWest + down, highNorthWest );
		AddDoubleSidedQuad( vb, collVerts, collIdx, lowSouthEast, highNorthEast, highNorthEast + down, lowSouthEast + down );
		AddDoubleSidedQuad( vb, collVerts, collIdx, lowSouthWest, highNorthWest, highNorthEast, lowSouthEast );

		return FinishModel( name, vb, collVerts, collIdx, mat );
	}

	/// <summary>Extruded slope panel: top quad + bottom/soffit + outer wall.</summary>
	static void AddSlopePrism(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 eaveA,
		Vector3 eaveB,
		Vector3 ridgeB,
		Vector3 ridgeA,
		Vector3 outward,
		float thickness )
	{
		AddDoubleSidedQuad( vb, collVerts, collIdx, eaveA, eaveB, ridgeB, ridgeA );

		var down = Vector3.Down * thickness;
		AddDoubleSidedQuad( vb, collVerts, collIdx, eaveA + down, ridgeA + down, ridgeB + down, eaveB + down );

		var push = outward.Normal * thickness;
		if ( push.LengthSquared > 1e-6f )
			AddDoubleSidedQuad( vb, collVerts, collIdx, eaveA, eaveA + down, ridgeA + down, ridgeA );
		if ( push.LengthSquared > 1e-6f )
			AddDoubleSidedQuad( vb, collVerts, collIdx, eaveB, ridgeB, ridgeB + down, eaveB + down );
	}

	static VertexBuffer NewBuffer()
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;
		return vb;
	}

	static Model FinishModel( string name, VertexBuffer vb, List<Vector3> collVerts, List<int> collIdx, Material mat )
	{
		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( ThornsProcModelNaming.Sanitize( $"terraingen/building/roof_{name}" ) );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		if ( collVerts.Count > 0 && collIdx.Count > 0 )
			mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );
		return mb.Create();
	}

	static Material LoadMaterial( string materialSlug )
	{
		var mat = Material.Load( $"{ThornsProcBuildingMaterialCatalog.PathPrefix}{materialSlug}.vmat" );
		return mat is not null && mat.IsValid() ? mat : Material.Load( "materials/default/default.vmat" );
	}

	static void AddDoubleSidedQuad(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 d )
	{
		if ( IsDegenerate( a, b, c, d ) )
			return;

		var n = FaceNormal( a, b, c );
		AddTri( vb, a, b, c, n );
		AddTri( vb, a, c, d, n );
		AddTri( vb, a, c, b, -n );
		AddTri( vb, a, d, c, -n );
		AddCollisionQuad( collVerts, collIdx, a, b, c, d );
	}

	static void AddDoubleSidedTri(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c )
	{
		if ( (b - a).LengthSquared < 1e-4f || (c - a).LengthSquared < 1e-4f )
			return;

		var n = FaceNormal( a, b, c );
		AddTri( vb, a, b, c, n );
		AddTri( vb, a, c, b, -n );

		var baseIdx = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( baseIdx );
		collIdx.Add( baseIdx + 1 );
		collIdx.Add( baseIdx + 2 );
	}

	static void AddCollisionQuad(
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 d )
	{
		var baseIdx = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collVerts.Add( d );
		collIdx.Add( baseIdx );
		collIdx.Add( baseIdx + 1 );
		collIdx.Add( baseIdx + 2 );
		collIdx.Add( baseIdx );
		collIdx.Add( baseIdx + 2 );
		collIdx.Add( baseIdx + 3 );
	}

	static bool IsDegenerate( Vector3 a, Vector3 b, Vector3 c, Vector3 d )
	{
		if ( (a - b).LengthSquared < 1e-4f || (a - c).LengthSquared < 1e-4f )
			return true;
		if ( (b - c).LengthSquared < 1e-4f || (c - d).LengthSquared < 1e-4f )
			return true;
		return FaceNormal( a, b, c ).LengthSquared < 1e-8f;
	}

	static Vector3 FaceNormal( Vector3 a, Vector3 b, Vector3 c )
	{
		var n = Vector3.Cross( b - a, c - a );
		return n.LengthSquared < 1e-8f ? Vector3.Up : n.Normal;
	}

	static void AddTri( VertexBuffer vb, Vector3 a, Vector3 b, Vector3 c, Vector3 n )
	{
		var def = vb.Default;
		def.Position = a;
		def.Normal = n;
		def.Tangent = new Vector4( 1f, 0f, 0f, 1f );
		def.TexCoord0 = Uv( a );
		vb.Add( def );

		def.Position = b;
		def.TexCoord0 = Uv( b );
		vb.Add( def );

		def.Position = c;
		def.TexCoord0 = Uv( c );
		vb.Add( def );
	}

	static Vector2 Uv( Vector3 p ) =>
		new( p.x / RoofUvTileWorldUnits, (p.y + p.z) / RoofUvTileWorldUnits );
}
