namespace ThinkDrink.Studio;

/// <summary>Shared primitive builders — single dev box model, minimal draw calls.</summary>
public static class StudioPrimitives
{
	static Model _boxModel;
	static Model _sphereModel;

	static Model BoxModel => _boxModel ??= Model.Load( "models/dev/box.vmdl" );
	static Model SphereModel => _sphereModel ??= Model.Load( "models/dev/sphere.vmdl" );

	public static GameObject CreateGroup( GameObject parent, string name )
	{
		var go = new GameObject( parent, true, name );
		go.Tags.Add( "studio" );
		return go;
	}

	public static GameObject CreateVisualBox(
		GameObject parent,
		string name,
		Vector3 localPos,
		Vector3 scale,
		Color tint )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = scale;
		var r = go.AddComponent<ModelRenderer>();
		r.Model = BoxModel;
		r.Tint = tint;
		return go;
	}

	public static GameObject CreateSolidBox(
		GameObject parent,
		string name,
		Vector3 localPos,
		Vector3 scale,
		Color tint )
	{
		var go = CreateVisualBox( parent, name, localPos, scale, tint );
		go.Tags.Add( "solid" );
		go.Tags.Add( "world" );
		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = new Vector3( 50f, 50f, 50f );
		collider.Static = true;
		return go;
	}

	public static GameObject CreateVisualSphere(
		GameObject parent,
		string name,
		Vector3 localPos,
		Vector3 scale,
		Color tint )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = scale;
		var r = go.AddComponent<ModelRenderer>();
		r.Model = SphereModel.IsValid() ? SphereModel : BoxModel;
		r.Tint = tint;
		return go;
	}

	public static GameObject CreateNeonStrip(
		GameObject parent,
		string name,
		Vector3 localPos,
		Vector3 scale,
		Color color )
	{
		return CreateVisualBox( parent, name, localPos, scale, color.WithAlpha( 0.95f ) );
	}
}
