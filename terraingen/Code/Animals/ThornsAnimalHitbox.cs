namespace Terraingen.Animals;

using Terraingen.Physics;

/// <summary>Planar hitbox sizing from skinned model bounds (matches debug ModelCollider hull).</summary>
public static class ThornsAnimalHitbox
{
	public static float GetPlanarRadius( Model model, float uniformScale )
	{
		var scale = MathF.Max( uniformScale, 0.01f );
		if ( !model.IsValid() )
			return MathF.Max( ThornsAnimalManager.BaseAgentRadius * scale, 4f );

		var bounds = TerraingenAnchoredPhysics.GetTightModelBounds( model );
		var horizontal = MathF.Max( bounds.Size.x, bounds.Size.y ) * scale;
		return MathF.Max( horizontal * 0.5f, 4f );
	}

	public static float GetBodyHeight( Model model, float uniformScale )
	{
		var scale = MathF.Max( uniformScale, 0.01f );
		if ( !model.IsValid() )
			return ThornsAnimalManager.BaseAgentHeight * scale;

		return MathF.Max( TerraingenAnchoredPhysics.GetTightModelBounds( model ).Size.z * scale, 8f );
	}
}
