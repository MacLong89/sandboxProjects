namespace Terraingen.Buildings;

/// <summary>Packaged runtime cube used by procedural buildings instead of editor-only dev models.</summary>
public static class ThornsProcBoxMesh
{
	static Model _cached;

	public static Model GetOrCreate()
	{
		if ( _cached.IsValid() && !_cached.IsError )
			return _cached;

		var mat = Material.Load( "materials/default/default.vmat" );
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var verts = new List<Vector3>();
		var indices = new List<int>();
		AddBox( vb, verts, indices, Vector3.Zero, Vector3.One * 50f );

		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( "thorns_proc_box" );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		mb.AddCollisionMesh( verts.ToArray(), indices.ToArray() );
		_cached = mb.Create();
		return _cached;
	}

	static void AddBox( VertexBuffer vb, List<Vector3> collVerts, List<int> collIdx, Vector3 center, Vector3 size )
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

		AddQuad( vb, collVerts, collIdx, p000, p001, p011, p010 );
		AddQuad( vb, collVerts, collIdx, p100, p110, p111, p101 );
		AddQuad( vb, collVerts, collIdx, p000, p100, p101, p001 );
		AddQuad( vb, collVerts, collIdx, p010, p011, p111, p110 );
		AddQuad( vb, collVerts, collIdx, p001, p101, p111, p011 );
		AddQuad( vb, collVerts, collIdx, p000, p010, p110, p100 );
	}

	static void AddQuad( VertexBuffer vb, List<Vector3> collVerts, List<int> collIdx, Vector3 a, Vector3 b, Vector3 c, Vector3 d )
	{
		AddTri( vb, collVerts, collIdx, a, b, c );
		AddTri( vb, collVerts, collIdx, a, c, d );
	}

	static void AddTri( VertexBuffer vb, List<Vector3> collVerts, List<int> collIdx, Vector3 a, Vector3 b, Vector3 c )
	{
		var n = Vector3.Cross( b - a, c - a );
		n = n.LengthSquared < 1e-14f ? Vector3.Up : n.Normal;
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: Vector3.Cross( Vector3.Up, n ).Normal;

		AddVertex( vb, a, n, tan );
		AddVertex( vb, b, n, tan );
		AddVertex( vb, c, n, tan );

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}

	static void AddVertex( VertexBuffer vb, Vector3 pos, Vector3 normal, Vector3 tangent )
	{
		vb.Add( new Vertex( pos, normal, tangent, Vector4.One ) );
	}
}
