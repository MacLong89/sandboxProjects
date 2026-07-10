namespace Sandbox;

/// <summary>Local delivery of short-lived bullet tracers.</summary>
[Category( "Aimbox" )]
public sealed class AimboxCombatTracerService : Component
{
	public static AimboxCombatTracerService Instance { get; private set; }

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

		if ( scene.GetAllComponents<AimboxCombatTracerService>().FirstOrDefault() is not null )
			return;

		var go = scene.CreateObject();
		go.Name = "AimboxCombatTracerWorld";
		go.Components.Create<AimboxCombatTracerService>();
	}

	public static void SpawnLocalShot(
		IAimboxCombatActor attacker,
		AimboxWeaponRuntime weapon,
		Vector3 aimOrigin,
		Vector3 aimDirection,
		Vector3 knownTracerEnd,
		SkinnedModelRenderer viewModelRenderer,
		GameObject viewModelRoot,
		CameraComponent camera,
		ModelRenderer suppressorRenderer = null )
	{
		if ( attacker is null || weapon is null || weapon.Definition.IsMelee )
			return;

		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		var scene = attacker.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		Vector3 muzzle;
		if ( attacker.ShowThirdPersonBody || attacker is AimboxBotController )
		{
			if ( !AimboxCombatMuzzleResolve.TryResolveThirdPersonMuzzleWorld( attacker, weapon.Definition.Id, aimDirection, out muzzle ) )
				muzzle = AimboxCombatMuzzleResolve.ResolveThirdPersonTracerFallback( attacker, aimDirection );
		}
		else if ( attacker is AimboxPlayerController player )
		{
			muzzle = AimboxCombatMuzzleResolve.ResolvePlayerTracerOrigin(
				player,
				aimDirection,
				weapon.Definition.Id,
				viewModelRenderer,
				viewModelRoot,
				camera,
				suppressorRenderer );
		}
		else
		{
			muzzle = AimboxCombatMuzzleResolve.ResolveThirdPersonTracerFallback( attacker, aimDirection );
		}

		var end = knownTracerEnd;
		if ( (end - muzzle).Length < 1f
		     && !AimboxCombatTracerResolve.TryResolveSegmentTowardAim(
			     scene,
			     muzzle,
			     aimOrigin,
			     aimDirection,
			     weapon.Definition.Range,
			     attacker.GameObject,
			     out end ) )
			return;

		SpawnLocal( scene, muzzle, end, AimboxCombatTracerSource.LocalPlayer );
	}

	static void SpawnLocal( Scene scene, Vector3 start, Vector3 end, AimboxCombatTracerSource source )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( (end - start).Length < 1f )
			return;

		EnsureForScene( scene );

		var go = scene.CreateObject();
		go.Name = "TracerFx";
		var fx = go.Components.Create<AimboxCombatTracerFx>();
		fx.Init( start, end, source );
	}
}
