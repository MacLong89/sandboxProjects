namespace UnderPressure;

/// <summary>
/// Shared dev-model primitives plus helpers that convert a desired world size into the
/// LocalScale needed for that model. Deriving scale from <see cref="Model.Bounds"/> keeps
/// geometry correct no matter what base dimensions the dev models ship with.
/// </summary>
public static class MeshPrimitives
{
	private static Model _quad;
	private static Model _box;
	private static Model _sphere;
	private static Material _mat;

	public static Model Quad => _quad ??= Model.Load( "models/dev/plane.vmdl" );
	public static Model Box => _box ??= Model.Load( "models/dev/box.vmdl" );
	public static Model Sphere => _sphere ??= Model.Load( "models/dev/sphere.vmdl" );
	public static Material Mat => _mat ??= Material.Load( "materials/default.vmat" );

	/// <summary>LocalScale to make the flat quad span width x height (in the XY plane).</summary>
	public static Vector3 QuadScale( float width, float height )
	{
		var size = Quad.Bounds.Size;
		var sx = size.x > 0.001f ? width / size.x : 1f;
		var sy = size.y > 0.001f ? height / size.y : 1f;
		return new Vector3( sx, sy, 1f );
	}

	/// <summary>LocalScale to make the box match the given world size on each axis.</summary>
	public static Vector3 BoxScale( Vector3 worldSize )
	{
		var size = Box.Bounds.Size;
		return new Vector3(
			size.x > 0.001f ? worldSize.x / size.x : 1f,
			size.y > 0.001f ? worldSize.y / size.y : 1f,
			size.z > 0.001f ? worldSize.z / size.z : 1f );
	}

	/// <summary>LocalScale to make the sphere a given world diameter.</summary>
	public static Vector3 SphereScale( float diameter )
	{
		var size = Sphere.Bounds.Size;
		var d = size.x > 0.001f ? diameter / size.x : 1f;
		return new Vector3( d, d, d );
	}
}
