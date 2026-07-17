namespace Offshore;

/// <summary>Dev-model primitives and world-size → LocalScale helpers.</summary>
public static class MeshPrimitives
{
	private static Model _box;
	private static Material _mat;

	public static Model Box => _box ??= Model.Load( "models/dev/box.vmdl" );
	public static Material Mat => _mat ??= Material.Load( "materials/default.vmat" );

	public static Vector3 BoxScale( Vector3 worldSize )
	{
		var size = Box.Bounds.Size;
		return new Vector3(
			size.x > 0.001f ? worldSize.x / size.x : 1f,
			size.y > 0.001f ? worldSize.y / size.y : 1f,
			size.z > 0.001f ? worldSize.z / size.z : 1f );
	}

	public static ModelRenderer CreateBox( GameObject parent, string name, Vector3 localPos, Vector3 worldSize, Color tint )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = BoxScale( worldSize );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Box;
		mr.MaterialOverride = Mat;
		mr.Tint = tint;
		return mr;
	}
}
