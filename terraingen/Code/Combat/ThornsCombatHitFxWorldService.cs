namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Multiplayer;

/// <summary>Interest-filtered blood bursts when wildlife or humanoid NPCs take damage.</summary>
[Category( "Thorns/Combat" )]
public sealed class ThornsCombatHitFxWorldService : Component
{
	public static ThornsCombatHitFxWorldService Instance { get; private set; }

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

		if ( scene.GetAllComponents<ThornsCombatHitFxWorldService>().FirstOrDefault() is not null )
			return;

		var go = scene.CreateObject();
		go.Name = "ThornsCombatHitFxWorld";
		go.Components.Create<ThornsCombatHitFxWorldService>();
	}

	public static void HostNotifyDamageApplied( Scene scene, in ThornsCombatDamage.DamageInfo info, in ThornsCombatDamage.DamageResult result )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( !ThornsCombatHitFx.ShouldSpawnBlood( in info, in result ) )
			return;

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var position = ThornsCombatHitFx.ResolveImpactPoint( in info );
		var spray = ThornsCombatHitFx.ResolveSprayDirection( in info );
		var intensity = ThornsCombatHitFx.ResolveIntensity( in result );
		var heavy = result.Killed;

		// Listen host never receives its own filtered RPC — spawn blood locally on the host machine.
		if ( !Networking.IsActive || ThornsMultiplayer.IsHostOrOffline )
			SpawnLocal( scene, position, spray, intensity, heavy );

		if ( !Networking.IsActive )
			return;

		var inst = ResolveInstance( scene );
		if ( inst is null )
			return;

		ThornsNetInterest.HostBroadcastNear( position, () => inst.RpcBloodBurst( position, spray, intensity, heavy ) );
	}

	[Rpc.Broadcast]
	void RpcBloodBurst( Vector3 position, Vector3 sprayDirection, float intensity, bool heavyKillBurst )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		SpawnLocal( scene, position, sprayDirection, intensity, heavyKillBurst );
	}

	static void SpawnLocal( Scene scene, Vector3 position, Vector3 sprayDirection, float intensity, bool heavyKillBurst )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		EnsureForScene( scene );

		var go = scene.CreateObject();
		go.Name = "BloodSplatterFx";
		go.WorldPosition = position;
		var fx = go.Components.Create<ThornsBloodSplatterFx>();
		fx.Init( sprayDirection, intensity, heavyKillBurst );
	}

	static ThornsCombatHitFxWorldService ResolveInstance( Scene scene )
	{
		var inst = Instance;
		if ( inst is not null && inst.IsValid() )
			return inst;

		if ( scene is null || !scene.IsValid() )
			return null;

		inst = scene.GetAllComponents<ThornsCombatHitFxWorldService>().FirstOrDefault();
		if ( inst is not null && inst.IsValid() )
			return inst;

		EnsureForScene( scene );
		return scene.GetAllComponents<ThornsCombatHitFxWorldService>().FirstOrDefault();
	}
}
