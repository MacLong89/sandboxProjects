using System;

namespace Sandbox;

/// <summary>
/// Server-side helpers for THORNS_EVERYTHING_DOCUMENT §3 origin sanity and validated aim vs camera/head.
/// </summary>
public static class ThornsCombatAuthority
{
	static bool NameLooksLikeHead( string n )
	{
		if ( string.IsNullOrWhiteSpace( n ) )
			return false;

		return n.Contains( "head", StringComparison.OrdinalIgnoreCase )
		       || n.Contains( "neck", StringComparison.OrdinalIgnoreCase )
		       || n.Contains( "skull", StringComparison.OrdinalIgnoreCase )
		       || n.Contains( "face", StringComparison.OrdinalIgnoreCase );
	}

	public static GameObject FindChild( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.Name == name )
				return c;
		}

		return default;
	}

	/// <summary>Eye position + rotation used for traces and aim validation (matches <see cref="ThornsPawnCamera"/> layout).</summary>
	public static bool TryGetAuthoritativeEye( GameObject pawnRoot, out Vector3 eyeWorldPos, out Rotation eyeWorldRot )
	{
		eyeWorldPos = default;
		eyeWorldRot = Rotation.Identity;

		var viewGo = FindChild( pawnRoot, "View" );
		var vitalsEye = pawnRoot.Components.Get<ThornsVitals>();
		var camEye = viewGo.IsValid()
			? viewGo.Components.Get<ThornsPawnCamera>()
			: default;
		var mountIx = pawnRoot.Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty && viewGo.IsValid() )
		{
			eyeWorldPos = viewGo.WorldPosition;
			eyeWorldRot = viewGo.WorldRotation;
			return true;
		}

		var pitch = pawnRoot.Components.Get<ThornsPawnMovement>()?.LookAngles.pitch ?? 0f;
		var pitchQ = Rotation.FromAxis( Vector3.Right, pitch );
		// Bandits / NPCs have a View child but no ThornsPawnCamera — Get<> is null; never dereference .IsValid on a null component.
		var eyeLocal = ThornsPawnCamera.ComposeEyeOffsetLocal( camEye is { IsValid: true } ? camEye : null, vitalsEye );
		eyeWorldPos = pawnRoot.WorldPosition + pawnRoot.WorldRotation * eyeLocal;
		eyeWorldRot = pawnRoot.WorldRotation * pitchQ;
		return true;
	}

	public static bool IsDirectionWithinAimTolerance( Vector3 directionNormalized, Rotation eyeWorldRot, float minDot )
	{
		var f = eyeWorldRot.Forward;
		return Vector3.Dot( directionNormalized, f ) >= minDot;
	}

	/// <summary>Upper fraction of the humanoid capsule (measured from feet) counted as head when no separate head collision exists.</summary>
	public const float HumanoidHeadshotBandTopFraction = 0.2f;

	/// <summary>
	/// True when <paramref name="hitWorld"/>'s Z lies in the top <see cref="HumanoidHeadshotBandTopFraction"/> of the humanoid
	/// capsule (<see cref="PlayerController"/> or <see cref="CharacterController"/> on the victim root).
	/// </summary>
	public static bool HumanoidTopHeadBandContainsWorldHit( Vector3 hitWorld, ThornsHealth victimHealth )
	{
		if ( !victimHealth.IsValid() )
			return false;

		var rootGo = victimHealth.GameObject;
		var height = ThornsPawnLocomotion.TryGetHumanoidHeight( rootGo );
		var pc = rootGo.Components.GetInAncestorsOrSelf<PlayerController>( true );
		var cc = rootGo.Components.GetInAncestorsOrSelf<CharacterController>( true );
		var anchorGo = pc.IsValid() ? pc.GameObject : cc.IsValid() ? cc.GameObject : rootGo;
		var feetZ = anchorGo.WorldPosition.z;
		var minHeadZ = feetZ + height * (1f - HumanoidHeadshotBandTopFraction);
		return hitWorld.z >= minHeadZ;
	}

	/// <summary>
	/// Humanoid headshots: top 20% of the controller height using either trace or analytic hit world position (NPC hit meshes often miss → analytic).
	/// Wildlife: legacy name-based strike surface only when a real trace hit (unchanged).
	/// </summary>
	public static bool TryHeadshotForWeaponHit(
		bool usedAnalyticFallback,
		SceneTraceResult tr,
		Vector3 analyticHitWorld,
		ThornsHealth victimHealth )
	{
		if ( !victimHealth.IsValid() )
			return false;

		var root = victimHealth.GameObject;
		var humanoid = root.Components.GetInAncestorsOrSelf<ThornsPawn>( true ).IsValid()
		               || root.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid();

		if ( humanoid )
		{
			var hitWorld = usedAnalyticFallback ? analyticHitWorld : tr.HitPosition;
			if ( !usedAnalyticFallback && !tr.Hit )
				return false;

			return HumanoidTopHeadBandContainsWorldHit( hitWorld, victimHealth );
		}

		if ( usedAnalyticFallback || !tr.Hit )
			return false;

		return tr.GameObject.IsValid() && NameLooksLikeHead( tr.GameObject.Name );
	}

	/// <summary>Trace-only head test — use <see cref="TryHeadshotForWeaponHit"/> when analytic sphere fallback may apply.</summary>
	public static bool TryHeadshotFromTrace( SceneTraceResult tr, ThornsHealth victimHealth )
	{
		if ( !tr.Hit || !victimHealth.IsValid() )
			return false;

		return TryHeadshotForWeaponHit( false, tr, default, victimHealth );
	}

	/// <summary>Convenience when the caller already has a <see cref="ThornsPawn"/> reference.</summary>
	public static bool TryHeadshotFromTrace( SceneTraceResult tr, ThornsPawn hitPawn )
	{
		if ( !hitPawn.IsValid() )
			return false;

		var vh = hitPawn.Components.Get<ThornsHealth>();
		return vh.IsValid() && TryHeadshotFromTrace( tr, vh );
	}

	/// <summary>Players, wildlife, and humanoid bandits — targets that can receive rolled crits from player weapons.</summary>
	public static bool HostDamageReceiverEligibleForPlayerCrit( GameObject victimRootWithHealth )
	{
		if ( victimRootWithHealth is null || !victimRootWithHealth.IsValid() )
			return false;

		return victimRootWithHealth.Components.GetInAncestorsOrSelf<ThornsPawn>( true ).IsValid()
		       || victimRootWithHealth.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true ).IsValid()
		       || victimRootWithHealth.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid();
	}

	/// <summary>Host-only RNG — caller should skip when <see cref="TryHeadshotFromTrace"/> already true so the bonus multiplier applies once.</summary>
	public static bool HostTryRollPlayerWeaponCriticalHit( ThornsWeaponDefinitions.WeaponDefinition def, string authoritativeCombatId, GameObject victimHealthRoot )
	{
		if ( !Networking.IsHost || def is null || victimHealthRoot is null || !victimHealthRoot.IsValid() )
			return false;

		if ( !HostDamageReceiverEligibleForPlayerCrit( victimHealthRoot ) )
			return false;

		var p = ThornsWeaponDefinitions.ResolveCriticalHitChance( def, authoritativeCombatId );
		if ( p <= 0f )
			return false;

		return Random.Shared.NextDouble() < p;
	}
}
