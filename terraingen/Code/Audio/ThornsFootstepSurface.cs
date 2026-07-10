namespace Sandbox;

using Terraingen.Buildings;

/// <summary>Resolves outdoor vs indoor footstep surfaces for players and NPCs.</summary>
public static class ThornsFootstepSurface
{
	/// <summary>Outdoor grass uses the base step distance cadence (multiplier 1 = no stretch).</summary>
	public const float OutdoorStepDistanceMultiplier = 1f;

	/// <summary>True when the pawn is outside proc building footprints (wilderness, roads, building exteriors).</summary>
	public static bool ShouldUseOutdoorGrassFootstep( GameObject root )
	{
		if ( !root.IsValid() )
			return true;

		var pos = root.WorldPosition;
		return !ThornsProcBuildingFootprintRegistry.ContainsWorldPoint( pos.x, pos.y );
	}

	public static float ScaleDistancePerStep( float baseDistance, GameObject root ) =>
		ShouldUseOutdoorGrassFootstep( root )
			? baseDistance * OutdoorStepDistanceMultiplier
			: baseDistance;
}
