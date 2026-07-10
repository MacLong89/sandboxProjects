namespace Sandbox;

/// <summary>
/// Authoritative hit endpoints are packed by the host and mirrored with <see cref="ThornsWeapon.RpcFireOutcome"/> — clients decode hit positions only (THORNS §3 host-validated combat).
/// </summary>
public enum ThornsWeaponImpactSurfaceKind
{
	None = 0,
	Terrain = 1,
	Metal = 2,
	Player = 3
}

/// <summary>Network helpers + optional client FX hooks (tracers/impact puffs disabled).</summary>
public static class ThornsWeaponCombatFeedback
{
	public static Vector3 UnpackHitMm( int xMm, int yMm, int zMm ) =>
		new Vector3( xMm / 1000f, yMm / 1000f, zMm / 1000f );

	public static void PackHitMm( Vector3 worldPos, out int xMm, out int yMm, out int zMm )
	{
		xMm = (int)MathF.Round( worldPos.x * 1000f );
		yMm = (int)MathF.Round( worldPos.y * 1000f );
		zMm = (int)MathF.Round( worldPos.z * 1000f );
	}

	public static void SpawnGunTracerAndImpactLocal(
		Vector3 traceStartWorld,
		Vector3 traceEndWorld,
		ThornsWeaponImpactSurfaceKind surfaceKind,
		bool damageAppliedToTarget )
	{
		_ = traceStartWorld;
		_ = traceEndWorld;
		_ = surfaceKind;
		_ = damageAppliedToTarget;
	}

	public static void SpawnImpactOnlyLocal( Vector3 hitWorld, ThornsWeaponImpactSurfaceKind surfaceKind )
	{
		_ = hitWorld;
		_ = surfaceKind;
	}
}
