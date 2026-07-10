namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Player;

/// <summary>Opt-in combat ray diagnostics — enable with <c>thorns_combat_hitscan_debug 1</c>.</summary>
public static class ThornsCombatHitscanDebug
{
	[ConVar( "thorns_combat_hitscan_debug" )]
	public static bool Enabled { get; set; }

	[ConVar( "thorns_combat_hitscan_debug_draw" )]
	public static bool DrawOverlay { get; set; } = true;

	const float OverlayDurationSeconds = 2.5f;

	public static void LogFireValidation(
		GameObject pawn,
		Vector3 clientOrigin,
		Vector3 clientDir,
		Vector3 serverOrigin,
		Vector3 serverDir,
		bool accepted,
		string rejectReason = "" )
	{
		if ( !Enabled )
			return;

		var angleDeg = AngleBetweenDegrees( clientDir, serverDir );
		var originErr = Vector3.DistanceBetween( clientOrigin, serverOrigin );
		var msg = accepted
			? $"[Thorns Combat Debug] FireValidation OK pawn={Label( pawn )} originErr={originErr:F1}\" angleErr={angleDeg:F2}° serverOrigin={serverOrigin:F0} serverDir={serverDir:F2}"
			: $"[Thorns Combat Debug] FireValidation REJECT pawn={Label( pawn )} reason={rejectReason} originErr={originErr:F1}\" angleErr={angleDeg:F2}° clientOrigin={clientOrigin:F0} serverOrigin={serverOrigin:F0}";

		Log.Info( msg );
	}

	public static void LogPlayerShot(
		GameObject attacker,
		string weaponId,
		Vector3 cameraOrigin,
		Vector3 muzzleOrigin,
		Vector3 aimDir,
		Vector3 pelletDir,
		float maxRange,
		bool hit,
		GameObject victim,
		ThornsCombatDamage.VictimKind victimKind,
		Vector3 impactPoint,
		float blockDistance,
		string detail = "" )
	{
		if ( !Enabled )
			return;

		var muzzleSep = Vector3.DistanceBetween( cameraOrigin, muzzleOrigin );
		var victimLabel = hit && victim.IsValid() ? Label( victim ) : "—";
		Log.Info(
			$"[Thorns Combat Debug] PlayerShot weapon={weaponId} hit={hit} kind={victimKind} victim={victimLabel} " +
			$"cam={cameraOrigin:F0} muzzle={muzzleOrigin:F0} sep={muzzleSep:F1}\" impact={impactPoint:F0} block={blockDistance:F0} " +
			$"aim={aimDir:F2} pellet={pelletDir:F2} range={maxRange:F0} {detail}" );

		if ( DrawOverlay && attacker.Scene.IsValid() )
			DrawShotOverlay( attacker.Scene, cameraOrigin, muzzleOrigin, impactPoint, hit );
	}

	public static void LogResolveAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject attacker,
		bool resolved,
		GameObject victim,
		ThornsCombatDamage.VictimKind kind,
		float blockDistance,
		string source )
	{
		if ( !Enabled )
			return;

		var end = origin + direction.Normal * maxRange;
		Log.Info(
			$"[Thorns Combat Debug] Resolve[{source}] hit={resolved} kind={kind} victim={( victim.IsValid() ? Label( victim ) : "—" )} " +
			$"origin={origin:F0} end={end:F0} block={blockDistance:F0} attacker={Label( attacker )}" );

		if ( DrawOverlay && scene.IsValid() )
		{
			var overlay = scene.DebugOverlay;
			if ( overlay is not null )
			{
				overlay.Line( origin, end, Color.Cyan, OverlayDurationSeconds );
				if ( resolved && victim.IsValid() )
					overlay.Sphere( new Sphere( victim.WorldPosition + Vector3.Up * 36f, 8f ), Color.Green, OverlayDurationSeconds );
			}
		}
	}

	static void DrawShotOverlay( Scene scene, Vector3 cameraOrigin, Vector3 muzzleOrigin, Vector3 impact, bool hit )
	{
		var overlay = scene.DebugOverlay;
		if ( overlay is null )
			return;

		var hitColor = hit ? Color.Green : Color.Red;
		overlay.Line( cameraOrigin, impact, hitColor, OverlayDurationSeconds );
		overlay.Line( muzzleOrigin, impact, Color.Yellow, OverlayDurationSeconds );
		overlay.Sphere( new Sphere( impact, 6f ), hitColor, OverlayDurationSeconds );
	}

	static string Label( GameObject go )
	{
		if ( !go.IsValid() )
			return "null";

		if ( go.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent ).IsValid() )
			return go.Name;

		if ( go.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent ).IsValid() )
			return go.Name;

		return go.Name;
	}

	static float AngleBetweenDegrees( Vector3 a, Vector3 b )
	{
		var dot = Math.Clamp( Vector3.Dot( a.Normal, b.Normal ), -1f, 1f );
		return MathF.Acos( dot ) * (180f / MathF.PI);
	}
}
