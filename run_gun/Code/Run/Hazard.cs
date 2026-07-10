namespace RunGun;

/// <summary>
/// An unshootable lane obstacle. You must strafe out of its Y-span before crossing it or eat a
/// big hit. Hazards are the skill threat that raw firepower can't delete, so dodging stays
/// relevant even when your build has outscaled the enemies — and they escalate to guarantee
/// every run eventually ends.
/// </summary>
public sealed class Hazard : Component
{
	public bool Triggered { get; set; }
	public float X => WorldPosition.x;
	public float MinY { get; private set; }
	public float MaxY { get; private set; }

	private ModelRenderer _slab;

	public void Setup( float minY, float maxY )
	{
		MinY = minY;
		MaxY = maxY;

		var centerY = (minY + maxY) * 0.5f;
		var width = MathF.Max( 20f, maxY - minY );
		WorldPosition = WorldPosition.WithY( centerY );

		var slab = new GameObject( GameObject, true, "HazardSlab" );
		slab.LocalPosition = new Vector3( 0f, 0f, GameConstants.HazardHeight * 0.5f );
		slab.LocalScale = MeshPrimitives.BoxScale( new Vector3( 44f, width, GameConstants.HazardHeight ) );
		_slab = slab.Components.Create<ModelRenderer>();
		_slab.Model = MeshPrimitives.Box;
		_slab.MaterialOverride = MeshPrimitives.Mat;
		_slab.Tint = new Color( 1f, 0.16f, 0.12f, 0.8f );
	}

	public bool Contains( float y ) => y >= MinY && y <= MaxY;
}
