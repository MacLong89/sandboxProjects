namespace Sandbox;

/// <summary>
/// Terrain-aligned Z for <c>wood_foundation</c> slab-on-ray placement — keeps preview + commits from sitting under the ground
/// when the aim ray bumps along an awkward normal (<see cref="ThornsBuildingSnap.BumpFromTrace"/>).
/// </summary>
public static class ThornsBuildingTerrainSurface
{
	const float MinUpNormalDot = 0.42f;
	const float DownRayStartAboveAim = 720f;
	const float DownRayLength = 4200f;

	/// <summary>
	/// World Z of the top/support surface under the slab XY — look ray when it hits an upward-facing surface, otherwise a tall down-probe.
	/// </summary>
	public static bool TryGetSupportWorldZ(
		Scene scene,
		GameObject ignoreRoot,
		in SceneTraceResult lookRay,
		Vector3 aimNearWorld,
		out float supportWorldZ )
	{
		supportWorldZ = default;

		if ( scene is null || !scene.IsValid() )
			return false;

		if ( lookRay.Hit && lookRay.Normal.Dot( Vector3.Up ) >= MinUpNormalDot )
		{
			supportWorldZ = lookRay.HitPosition.z;
			return true;
		}

		var startZ = aimNearWorld.z + DownRayStartAboveAim;
		if ( lookRay.Hit )
			startZ = Math.Max( startZ, lookRay.HitPosition.z + 120f );

		var origin = new Vector3( aimNearWorld.x, aimNearWorld.y, startZ );
		var down = ThornsTraceUtility.WithIgnoredRoots(
				ThornsTraceUtility.PrepareRay( scene, new Ray( origin, Vector3.Down ), DownRayLength, ThornsTraceProfile.BuildingTerrainSupportDown ),
				ignoreRoot,
				null )
			.Run();
		if ( !down.Hit )
			return false;

		supportWorldZ = down.HitPosition.z;
		return true;
	}

	/// <summary>
	/// Slab centre Z so the slab <b>bottom</b> sits on <paramref name="supportWorldZ"/> (terrain or hit surface) — continuous
	/// along slopes; socket-snapped foundations still use storey grid from their host pose.
	/// </summary>
	public static float FoundationSlabCentreZFromSupportWorldZ( float supportWorldZ )
	{
		var ft = ThornsBuildingModule.FloorThickness;
		return supportWorldZ + ft * 0.5f;
	}

	/// <summary>Repositions a terrain-seeded foundation suggestion so its underside clears world support at XY.</summary>
	public static void ClampFoundationTerrainSlabToSurface(
		Scene scene,
		GameObject ignorePlacementPawnRoot,
		in SceneTraceResult lookRay,
		Vector3 aimNearWorld,
		ref ThornsPlacementSuggestion snap )
	{
		if ( snap.TerrainKind != ThornsTerrainSeedKind.SlabOnRay || snap.UsesSocketSnap )
			return;

		if ( !TryGetSupportWorldZ( scene, ignorePlacementPawnRoot, in lookRay, aimNearWorld, out var zSurf ) )
			return;

		var cz = FoundationSlabCentreZFromSupportWorldZ( zSurf );
		var p = snap.ProposedWorldPosition;
		snap.ProposedWorldPosition = new Vector3( p.x, p.y, cz );
	}
}
