namespace SceneKit;

/// <summary>
/// Minimal kit prop builders (tree / bush / crate). Parent root should sit on the ground plane.
/// </summary>
public static class KitProps
{
	public static GameObject Box( GameObject parent, string name, Vector3 localPos, Vector3 size, Color color )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = MeshPrimitives.BoxScale( size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = color;
		return go;
	}

	public static GameObject Sphere( GameObject parent, string name, Vector3 localPos, float diameter, Color color )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = MeshPrimitives.SphereScale( diameter );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Sphere;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = color;
		return go;
	}

	/// <summary>Spawn a low-poly tree. <paramref name="height"/> is total height in game units.</summary>
	public static GameObject Tree( GameObject parent, string name, Vector3 worldPos, float height, Color? trunk = null, Color? leafA = null, Color? leafB = null )
	{
		var root = new GameObject( parent, true, name );
		root.WorldPosition = worldPos.WithZ( worldPos.z < 0.01f ? Depth.SitOnGround : worldPos.z );

		var trunkColor = trunk ?? new Color( 0.62f, 0.38f, 0.14f );
		var a = leafA ?? new Color( 0.38f, 0.86f, 0.08f );
		var b = leafB ?? new Color( 0.28f, 0.78f, 0.06f );

		var trunkH = height * 0.45f;
		var trunkW = MathF.Max( height * 0.06f, 4f );
		Box( root, "Trunk", new Vector3( 0f, 0f, Depth.CenterPivotLift( trunkH ) ), new Vector3( trunkW, trunkW, trunkH ), trunkColor );

		var canopyH = height * 0.55f;
		var baseZ = trunkH * 0.85f;
		for ( var i = 0; i < 3; i++ )
		{
			var t = 1f - i * 0.22f;
			var w = height * 0.34f * t;
			var h = canopyH * 0.34f;
			var z = baseZ + h * 0.5f + i * h * 0.72f;
			var col = i % 2 == 0 ? a : b;
			Box( root, $"Canopy_{i}", new Vector3( 0f, 0f, z ), new Vector3( w, w, h ), col );
		}

		return root;
	}

	public static GameObject Bush( GameObject parent, string name, Vector3 worldPos, float height, Color? leaf = null )
	{
		var root = new GameObject( parent, true, name );
		root.WorldPosition = worldPos.WithZ( worldPos.z < 0.01f ? Depth.SitOnGround : worldPos.z );
		var c = leaf ?? new Color( 0.25f, 0.7f, 0.12f );
		Sphere( root, "A", new Vector3( -height * 0.15f, 0f, height * 0.4f ), height * 0.9f, c );
		Sphere( root, "B", new Vector3( height * 0.18f, height * 0.08f, height * 0.35f ), height * 0.75f, c * 0.9f );
		return root;
	}

	public static GameObject Crate( GameObject parent, string name, Vector3 worldPos, float size, Color? body = null, Color? lid = null )
	{
		var root = new GameObject( parent, true, name );
		root.WorldPosition = worldPos.WithZ( worldPos.z < 0.01f ? Depth.SitOnGround : worldPos.z );
		var bodyC = body ?? new Color( 0.77f, 0.60f, 0.42f );
		var lidC = lid ?? new Color( 0.65f, 0.49f, 0.32f );
		var bodyH = size * 0.9f;
		Box( root, "Body", new Vector3( 0f, 0f, Depth.CenterPivotLift( bodyH ) ), new Vector3( size, size, bodyH ), bodyC );
		var lidH = size * 0.12f;
		Box( root, "Lid", new Vector3( 0f, 0f, bodyH + Depth.Step + lidH * 0.5f ), new Vector3( size * 1.02f, size * 1.02f, lidH ), lidC );
		return root;
	}
}
