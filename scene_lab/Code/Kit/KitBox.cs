namespace SceneLab;

/// <summary>Shared tinted primitive spawn. Wall/body tints are forced opaque (alpha 1).</summary>
public static class KitBox
{
	/// <summary>Scale RGB only; always opaque. Use for walls/roofs — not for intentional glass.</summary>
	public static Color Solid( Color c, float rgbScale = 1f )
	{
		var s = c * rgbScale;
		return new Color( s.r, s.g, s.b, 1f );
	}

	public static GameObject Box( GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Angles rot = default, bool opaque = true )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalRotation = rot.ToRotation();
		go.LocalScale = MeshPrimitives.BoxScale( size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = opaque ? MeshPrimitives.MatOpaque : MeshPrimitives.MatGlass;
		mr.Tint = opaque ? Solid( color ) : color;
		if ( !opaque )
			mr.RenderType = ModelRenderer.ShadowRenderType.Off;
		return go;
	}

	/// <summary>
	/// Visual box plus a static collider on a sibling (no LocalScale) so Scale matches world size.
	/// </summary>
	public static GameObject CollidingBox( GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Angles rot = default, bool opaque = true )
	{
		var go = Box( parent, name, localPos, size, color, rot, opaque );
		var colGo = new GameObject( parent, true, name + "_col" );
		colGo.LocalPosition = localPos;
		colGo.LocalRotation = rot.ToRotation();
		var col = colGo.Components.Create<BoxCollider>();
		col.Scale = size;
		col.Center = Vector3.Zero;
		col.Static = true;
		return go;
	}

	public static GameObject Sphere( GameObject parent, string name, Vector3 localPos, float diameter, Color color, bool opaque = true )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalScale = MeshPrimitives.SphereScale( diameter );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Sphere;
		mr.MaterialOverride = MeshPrimitives.MatOpaque;
		mr.Tint = opaque ? Solid( color ) : color;
		return go;
	}

	public static GameObject Cylinder( GameObject parent, string name, Vector3 localPos, float diameter, float length, Color color, Angles rot = default, bool opaque = true )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalRotation = rot.ToRotation();
		go.LocalScale = MeshPrimitives.CylinderScale( diameter, length );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Cylinder;
		mr.MaterialOverride = MeshPrimitives.MatOpaque;
		mr.Tint = opaque ? Solid( color ) : color;
		return go;
	}
}
