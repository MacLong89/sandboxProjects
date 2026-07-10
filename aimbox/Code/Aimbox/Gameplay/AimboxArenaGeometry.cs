namespace Sandbox;

/// <summary>Shared procedural arena primitives with Terraingen-style material overrides and collision-safe boxes.</summary>
public static class AimboxArenaGeometry
{
	const float ArenaUvTileWorldUnits = 96f;

	static readonly Dictionary<string, Model> BoxCache = new( StringComparer.OrdinalIgnoreCase );

	public static void ResetCaches( string reason )
	{
		BoxCache.Clear();
		Log.Info( $"[Aimbox MatDbg] Arena geometry cache reset ({reason})." );
	}

	public static Vector3 OnGround( Vector3 position, Vector3 size ) =>
		new( position.x, position.y, size.z * 0.5f );

	public static Vector3 OnTopOf( Vector3 position, Vector3 size, float elevation ) =>
		OnGround( position, size ) + Vector3.Up * elevation;

	public static void AddBlock(
		GameObject parent,
		string name,
		Vector3 center,
		Vector3 size,
		AimboxArenaSurface surface,
		Color tint = default,
		Rotation rotation = default )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.WorldPosition = center;
		go.WorldRotation = rotation;
		var tag = go.Components.Create<AimboxArenaMaterialTag>();
		tag.Surface = surface;
		tag.Slug = surface.MaterialSlug() ?? "";
		tag.MaterialPath = AimboxArenaMaterials.MaterialPath( surface ) ?? AimboxArenaMaterials.DefaultMaterialPath;
		tag.Size = size;

		var material = AimboxArenaMaterials.Get( surface );
		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = GetBoxModel( size );
		if ( material is not null && material.IsValid() )
			renderer.MaterialOverride = material;
		renderer.Tint = tint == default ? Color.White : tint;

		renderer.RenderOptions.Game = true;
		renderer.Enabled = true;

		var collider = go.Components.Create<BoxCollider>();
		collider.Scale = size;
		collider.Static = true;

		AimboxArenaMaterialDebug.LogRendererState( "create", go, renderer );
		_ = ApplyGameRenderLayerAsync( renderer );
	}

	static Model GetBoxModel( Vector3 size )
	{
		var key = $"{size.x:F1}|{size.y:F1}|{size.z:F1}";
		if ( BoxCache.TryGetValue( key, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		var material = AimboxArenaMaterials.Get( AimboxArenaSurface.Concrete );
		if ( material is null || !material.IsValid() )
			material = Material.Load( AimboxArenaMaterials.DefaultMaterialPath );

		cached = BuildBoxModel( SanitizeModelName( $"aimbox/arena/block_{key}" ), size, material );
		BoxCache[key] = cached;
		return cached;
	}

	static Model BuildBoxModel( string name, Vector3 size, Material material )
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();
		AddBox( vb, collVerts, collIdx, Vector3.Zero, size );

		var mesh = new Mesh( material, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( name );
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
		AddTri( vb, collVerts, collIdx, a, b, c, UvBox( a, n ), UvBox( b, n ), UvBox( c, n ) );
		AddTri( vb, collVerts, collIdx, a, c, d, UvBox( a, n ), UvBox( c, n ), UvBox( d, n ) );
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
		Vector2 uvc )
	{
		var n = UnitNormal( b - a, c - a );
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: UnitNormal( Vector3.Up, n );

		vb.Add( new Vertex( a, n, tan, new Vector4( uva.x, uva.y, 0f, 0f ) ) );
		vb.Add( new Vertex( b, n, tan, new Vector4( uvb.x, uvb.y, 0f, 0f ) ) );
		vb.Add( new Vertex( c, n, tan, new Vector4( uvc.x, uvc.y, 0f, 0f ) ) );

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}

	static Vector2 UvBox( Vector3 p, Vector3 n )
	{
		var nx = MathF.Abs( n.x );
		var ny = MathF.Abs( n.y );
		var nz = MathF.Abs( n.z );
		if ( nz >= nx && nz >= ny )
			return new Vector2( p.x / ArenaUvTileWorldUnits, p.y / ArenaUvTileWorldUnits );
		if ( nx >= ny && nx >= nz )
			return new Vector2( p.y / ArenaUvTileWorldUnits, p.z / ArenaUvTileWorldUnits );
		return new Vector2( p.x / ArenaUvTileWorldUnits, p.z / ArenaUvTileWorldUnits );
	}

	static Vector3 UnitNormal( Vector3 edgeA, Vector3 edgeB )
	{
		var cross = Vector3.Cross( edgeA, edgeB );
		var lenSq = cross.LengthSquared;
		return lenSq < 1e-14f ? Vector3.Up : cross / MathF.Sqrt( lenSq );
	}

	static string SanitizeModelName( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			return "aimbox_arena_block";

		var chars = name.ToCharArray();
		for ( var i = 0; i < chars.Length; i++ )
		{
			var c = chars[i];
			if ( char.IsLetterOrDigit( c ) || c == '_' || c == '-' )
				continue;

			chars[i] = '_';
		}

		return new string( chars );
	}

	static async Task ApplyGameRenderLayerAsync( ModelRenderer renderer )
	{
		if ( !renderer.IsValid() )
			return;

		var deadline = DateTime.UtcNow.AddSeconds( 2 );
		while ( renderer.IsValid() && renderer.SceneObject is null && DateTime.UtcNow < deadline )
			await Task.Delay( 16 );

		if ( !renderer.IsValid() )
			return;

		renderer.RenderOptions.Game = true;
		if ( renderer.SceneObject is not null )
			renderer.RenderOptions.Apply( renderer.SceneObject );

		AimboxArenaMaterialDebug.LogRendererState( "post-render-layer", renderer.GameObject, renderer );
	}
}
