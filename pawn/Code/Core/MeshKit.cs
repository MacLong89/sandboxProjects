namespace PawnShop;

/// <summary>
/// Shared dev-model primitives plus builders that convert desired world sizes into
/// LocalScale. All shop geometry is composed from these tinted primitives.
/// </summary>
public static class MeshKit
{
	private static Model _box;
	private static Model _sphere;
	private static Material _mat;

	public static Model Box => _box ??= Model.Load( "models/dev/box.vmdl" );
	public static Model Sphere => _sphere ??= Model.Load( "models/dev/sphere.vmdl" );
	public static Material Mat => _mat ??= Material.Load( "materials/default.vmat" );

	public static Vector3 BoxScale( Vector3 worldSize )
	{
		var size = Box.Bounds.Size;
		return new Vector3(
			size.x > 0.001f ? worldSize.x / size.x : 1f,
			size.y > 0.001f ? worldSize.y / size.y : 1f,
			size.z > 0.001f ? worldSize.z / size.z : 1f );
	}

	public static Vector3 SphereScale( float diameter )
	{
		var size = Sphere.Bounds.Size;
		var d = size.x > 0.001f ? diameter / size.x : 1f;
		return new Vector3( d, d, d );
	}

	/// <summary>Spawn a tinted box. Position is the box center (lift by z*0.5 to sit on floor).</summary>
	public static GameObject Spawn( GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Angles rot = default, bool collide = false )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalRotation = rot.ToRotation();
		go.LocalScale = BoxScale( size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Box;
		mr.MaterialOverride = Mat;
		mr.Tint = color;

		if ( collide )
		{
			// Collider on a child sized in world units so the render scale doesn't distort it.
			var col = go.Components.Create<BoxCollider>();
			col.Scale = Box.Bounds.Size;
			col.Static = true;
		}

		return go;
	}

	public static GameObject SpawnSphere( GameObject parent, string name, Vector3 localPos, float diameter, Color color )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = SphereScale( diameter );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Sphere;
		mr.MaterialOverride = Mat;
		mr.Tint = color;
		return go;
	}
}
