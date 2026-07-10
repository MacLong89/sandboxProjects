namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Interest-filtered delivery of short-lived bullet tracers for nearby clients.</summary>
[Category( "Thorns/Combat" )]
public sealed class ThornsCombatTracerWorldService : Component
{
	public static ThornsCombatTracerWorldService Instance { get; private set; }

	protected override void OnStart() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public static void EnsureForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		if ( scene.GetAllComponents<ThornsCombatTracerWorldService>().FirstOrDefault() is not null )
			return;

		var go = scene.CreateObject();
		go.Name = "ThornsCombatTracerWorld";
		go.Components.Create<ThornsCombatTracerWorldService>();
	}

	public static void HostBroadcastShot(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		ThornsCombatTracerSource source )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( !ThornsCombatTracerResolve.TryResolveEnd( scene, origin, direction, maxRange, ignoreRoot, out var end ) )
			return;

		HostBroadcastSegment( scene, origin, end, source );
	}

	public static void HostBroadcastShotFromAttacker(
		Scene scene,
		GameObject attackerRoot,
		Vector3 aimOrigin,
		Vector3 aimDirection,
		float maxRange,
		ThornsCombatTracerSource source,
		string combatWeaponDefinitionId = "" )
	{
		if ( scene is null || !scene.IsValid() || !attackerRoot.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		var muzzle = ThornsCombatMuzzleResolve.ResolveTracerOrigin(
			attackerRoot,
			aimDirection,
			source,
			combatWeaponDefinitionId,
			preferFirstPersonViewmodel: source == ThornsCombatTracerSource.Player
			                            && ThornsLocalPlayer.IsLocallyControlledPawn( attackerRoot ) );

		if ( !ThornsCombatTracerResolve.TryResolveSegmentTowardAim(
			     scene,
			     muzzle,
			     aimOrigin,
			     aimDirection,
			     maxRange,
			     attackerRoot,
			     out var end ) )
			return;

		HostBroadcastSegment( scene, muzzle, end, source );
	}

	public static void HostBroadcastSegment(
		Scene scene,
		Vector3 start,
		Vector3 end,
		ThornsCombatTracerSource source )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( (end - start).Length < 1f )
			return;

		if ( !Networking.IsActive )
		{
			SpawnLocal( scene, start, end, source );
			return;
		}

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var inst = ResolveInstance( scene );
		if ( inst is null )
			return;

		var mid = (start + end) * 0.5f;
		var src = (byte)source;
		ThornsNetInterest.HostBroadcastNear( mid, () => inst.RpcTracer( start, end, src ) );
	}

	/// <summary>Immediate local tracer for the firing player (client prediction).</summary>
	public static void SpawnPredictedLocal(
		Scene scene,
		GameObject pawnRoot,
		Vector3 aimOrigin,
		Vector3 aimDirection,
		float maxRange,
		string combatWeaponDefinitionId = "" )
	{
		if ( scene is null || !scene.IsValid() || !pawnRoot.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		var muzzle = ThornsCombatMuzzleResolve.ResolvePlayerTracerOrigin(
			pawnRoot,
			aimDirection,
			combatWeaponDefinitionId,
			preferFirstPersonViewmodel: true );

		if ( !ThornsCombatTracerResolve.TryResolveEnd(
			     scene,
			     muzzle,
			     aimDirection,
			     maxRange,
			     pawnRoot,
			     out var end ) )
			return;

		if ( (end - muzzle).Length < 1f )
			return;

		SpawnLocal( scene, muzzle, end, ThornsCombatTracerSource.Player );
	}

	[Rpc.Broadcast]
	void RpcTracer( Vector3 start, Vector3 end, byte source )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		SpawnLocal( scene, start, end, (ThornsCombatTracerSource)source );
	}

	static void SpawnLocal( Scene scene, Vector3 start, Vector3 end, ThornsCombatTracerSource source )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		EnsureForScene( scene );

		var go = scene.CreateObject();
		go.Name = "TracerFx";
		var fx = go.Components.Create<ThornsCombatTracerFx>();
		fx.Init( start, end, source );
	}

	static ThornsCombatTracerWorldService ResolveInstance( Scene scene )
	{
		var inst = Instance;
		if ( inst is not null && inst.IsValid() )
			return inst;

		EnsureForScene( scene );
		inst = Instance;
		return inst is not null && inst.IsValid() ? inst : null;
	}
}
