// Drop into a game's Code/ folder and change `SceneKit` namespace to your game's namespace
// (or keep SceneKit and `using SceneKit;`). Safe starting point when the game has no kit helpers.
namespace SceneKit;

/// <summary>
/// Dev-model primitives with Bounds-derived LocalScale helpers.
/// Note: models/dev/* may be editor-only — replace with a procedural cube for publish builds if needed.
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

	public static Vector3 QuadScale( float width, float height )
	{
		var size = Quad.Bounds.Size;
		var sx = size.x > 0.001f ? width / size.x : 1f;
		var sy = size.y > 0.001f ? height / size.y : 1f;
		return new Vector3( sx, sy, 1f );
	}

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
}
