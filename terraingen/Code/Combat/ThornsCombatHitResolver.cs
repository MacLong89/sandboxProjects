namespace Terraingen.Combat;

using Terraingen.Core;
using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Player;

/// <summary>Host-side combat target resolution for weapon traces.</summary>
public static class ThornsCombatHitResolver
{
	public static bool TryResolveVictimAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject attackerRoot,
		out GameObject victimRoot,
		out ThornsCombatDamage.VictimKind victimKind,
		out SceneTraceResult resolveTrace )
	{
		victimRoot = null;
		victimKind = ThornsCombatDamage.VictimKind.Unknown;
		resolveTrace = default;

		if ( scene is null || !scene.IsValid() )
			return false;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f || maxRange <= 0f )
			return false;

		var end = origin + dir * maxRange;
		var blockDistance = ResolveSolidBlockDistance( scene, origin, end, attackerRoot, maxRange );

		var hitboxTrace = scene.Trace
			.Ray( origin, end )
			.UseHitboxes( true )
			.IgnoreGameObjectHierarchy( attackerRoot )
			.Run();

		if ( hitboxTrace.Hit
		     && hitboxTrace.GameObject.IsValid()
		     && hitboxTrace.Distance <= blockDistance + 0.5f
		     && TryResolveVictimFromHit( hitboxTrace.GameObject, attackerRoot, out victimRoot, out victimKind )
		     && !IsBlockedBySolidWorld( scene, origin, hitboxTrace.HitPosition, attackerRoot, victimRoot ) )
		{
			resolveTrace = hitboxTrace;
			var ok = FinalizeVictim( scene, attackerRoot, ref victimRoot, ref victimKind );
			LogResolveDebug( scene, origin, dir, maxRange, attackerRoot, ok, victimRoot, victimKind, blockDistance, "hitbox" );
			return ok;
		}

		if ( TryPickFallbackVictimAlongRay(
			     scene,
			     origin,
			     dir,
			     maxRange,
			     attackerRoot,
			     blockDistance,
			     out victimRoot,
			     out victimKind,
			     out resolveTrace,
			     out var fallbackSource )
		     && !IsBlockedBySolidWorld( scene, origin, resolveTrace.HitPosition, attackerRoot, victimRoot ) )
		{
			var ok = FinalizeVictim( scene, attackerRoot, ref victimRoot, ref victimKind );
			LogResolveDebug( scene, origin, dir, maxRange, attackerRoot, ok, victimRoot, victimKind, blockDistance, fallbackSource );
			return ok;
		}

		LogResolveDebug( scene, origin, dir, maxRange, attackerRoot, false, null, ThornsCombatDamage.VictimKind.Unknown, blockDistance, "miss" );
		return false;
	}

	static bool TryPickFallbackVictimAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 dir,
		float maxRange,
		GameObject attackerRoot,
		float blockDistance,
		out GameObject victimRoot,
		out ThornsCombatDamage.VictimKind victimKind,
		out SceneTraceResult resolveTrace,
		out string source )
	{
		victimRoot = null;
		victimKind = ThornsCombatDamage.VictimKind.Unknown;
		resolveTrace = default;
		source = "miss";

		var bestDistance = float.MaxValue;
		GameObject bestRoot = null;
		var bestKind = ThornsCombatDamage.VictimKind.Unknown;
		SceneTraceResult bestTrace = default;
		var bestSource = "miss";

		if ( ThornsAnimalHitUtil.TryPickBrainAlongRay(
			     scene,
			     origin,
			     dir,
			     maxRange,
			     attackerRoot,
			     out var animalBrain,
			     out var animalDistance,
			     out var animalTrace )
		     && animalDistance <= blockDistance + 0.5f
		     && animalDistance < bestDistance )
		{
			bestDistance = animalDistance;
			bestRoot = animalBrain.GameObject;
			bestKind = ThornsCombatDamage.VictimKind.Animal;
			bestTrace = animalTrace;
			bestSource = "animal";
		}

		if ( ThornsCitizenHitbox.TryPickCapsuleAlongRay(
			     scene,
			     origin,
			     dir,
			     maxRange,
			     attackerRoot,
			     out var citizenRoot,
			     out var citizenKind,
			     out var citizenDistance,
			     out var citizenHitWorld )
		     && citizenDistance <= blockDistance + 0.5f
		     && citizenDistance < bestDistance )
		{
			bestDistance = citizenDistance;
			bestRoot = citizenRoot;
			bestKind = citizenKind;
			bestTrace = ThornsCombatTraceUtil.SyntheticHit( citizenRoot, citizenHitWorld );
			bestSource = "citizen-capsule";
		}

		if ( !bestRoot.IsValid() )
			return false;

		victimRoot = bestRoot;
		victimKind = bestKind;
		resolveTrace = bestTrace;
		source = bestSource;
		return true;
	}

	static void LogResolveDebug(
		Scene scene,
		Vector3 origin,
		Vector3 dir,
		float maxRange,
		GameObject attacker,
		bool resolved,
		GameObject victim,
		ThornsCombatDamage.VictimKind kind,
		float blockDistance,
		string source )
	{
		ThornsCombatHitscanDebug.LogResolveAlongRay(
			scene,
			origin,
			dir,
			maxRange,
			attacker,
			resolved,
			victim,
			kind,
			blockDistance,
			source );
	}

	public static bool TryResolveVictimAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject attackerRoot,
		out GameObject victimRoot,
		out ThornsCombatDamage.VictimKind victimKind )
		=> TryResolveVictimAlongRay( scene, origin, direction, maxRange, attackerRoot, out victimRoot, out victimKind, out _ );

	/// <summary>World impact point along the authoritative camera/combat ray (for tracers + debug).</summary>
	public static Vector3 SampleImpactPointOnRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject attackerRoot,
		GameObject victimRoot,
		SceneTraceResult resolveTrace )
	{
		if ( scene is null || !scene.IsValid() )
			return origin;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f || maxRange <= 0f )
			return origin;

		if ( resolveTrace.Hit )
			return resolveTrace.HitPosition;

		var traceEnd = origin + dir * maxRange;
		var worldTrace = scene.Trace
			.Ray( origin, traceEnd )
			.IgnoreGameObjectHierarchy( attackerRoot )
			.Run();

		return worldTrace.Hit ? worldTrace.HitPosition : traceEnd;
	}

	static float ResolveSolidBlockDistance( Scene scene, Vector3 origin, Vector3 end, GameObject attackerRoot, float maxRange )
	{
		var trace = scene.Trace
			.Ray( origin, end )
			.IgnoreGameObjectHierarchy( attackerRoot )
			.Run();

		if ( !trace.Hit || !trace.GameObject.IsValid() || !IsSolidWorldObstacle( trace ) )
			return maxRange;

		return Vector3.DistanceBetween( origin, trace.HitPosition );
	}

	static bool IsSolidWorldObstacle( SceneTraceResult trace )
	{
		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return false;

		if ( trace.Collider.IsValid() && trace.Collider.IsTrigger )
			return false;

		return true;
	}

	static bool FinalizeVictim(
		Scene scene,
		GameObject attackerRoot,
		ref GameObject victimRoot,
		ref ThornsCombatDamage.VictimKind victimKind )
	{
		if ( !victimRoot.IsValid() )
			return false;

		if ( victimKind == ThornsCombatDamage.VictimKind.Animal )
		{
			var brain = victimRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
			if ( brain.IsValid()
			     && TryResolveMountedRiderVictim( scene, brain, out var riderRoot )
			     && ( ThornsCombatFactions.ResolveFaction( attackerRoot ) != ThornsCombatFactions.FactionKind.Player
			          || ThornsCombatSettings.EnablePvPDamage ) )
			{
				victimRoot = riderRoot;
				victimKind = ThornsCombatDamage.VictimKind.Player;
				return true;
			}
		}

		if ( victimKind == ThornsCombatDamage.VictimKind.Player
		     && ThornsCombatFactions.ResolveFaction( attackerRoot ) == ThornsCombatFactions.FactionKind.Player
		     && !ThornsCombatSettings.EnablePvPDamage )
			return false;

		return true;
	}

	static bool TryResolveVictimFromHit(
		GameObject hit,
		GameObject attackerRoot,
		out GameObject victimRoot,
		out ThornsCombatDamage.VictimKind victimKind )
	{
		victimRoot = null;
		victimKind = ThornsCombatDamage.VictimKind.Unknown;

		if ( !hit.IsValid() )
			return false;

		if ( TryResolveAnimalBrainFromHit( hit, out var brain ) )
		{
			victimRoot = brain.GameObject;
			victimKind = ThornsCombatDamage.VictimKind.Animal;
			return true;
		}

		var bandit = hit.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( bandit.IsValid() && !bandit.IsDead )
		{
			victimRoot = bandit.GameObject;
			victimKind = ThornsCombatDamage.VictimKind.Npc;
			return true;
		}

		var player = hit.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndParent );
		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return false;

		if ( attackerRoot.IsValid() && player.GameObject == attackerRoot )
			return false;

		var health = player.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndParent );
		if ( health is not null && health.IsValid() && !health.IsAlive )
			return false;

		victimRoot = player.GameObject;
		victimKind = ThornsCombatDamage.VictimKind.Player;
		return true;
	}

	static bool TryResolveAnimalBrainFromHit( GameObject hit, out ThornsAnimalBrain brain )
	{
		brain = null;
		if ( !hit.IsValid() )
			return false;

		brain = hit.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return brain.IsValid() && !brain.IsDead;
	}

	static bool TryResolveMountedRiderVictim( Scene scene, ThornsAnimalBrain mountBrain, out GameObject riderRoot )
	{
		riderRoot = null;
		if ( mountBrain is null || !mountBrain.IsValid() || !mountBrain.IsMounted || mountBrain.MountedRiderId == Guid.Empty )
			return false;

		riderRoot = ThornsAnimalManager.TryGetPlayerByObjectId( scene, mountBrain.MountedRiderId );
		if ( !riderRoot.IsValid() )
			return false;

		var health = riderRoot.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndParent );
		return health is not null && health.IsValid() && health.IsAlive && !health.IsDeadState;
	}

	internal static bool IsBlockedBySolidWorld(
		Scene scene,
		Vector3 origin,
		Vector3 targetPoint,
		GameObject ignoreRoot,
		GameObject targetRoot )
	{
		var delta = targetPoint - origin;
		var dist = delta.Length;
		if ( dist < 1f )
			return false;

		var trace = scene.Trace
			.Ray( origin, targetPoint - delta.Normal * 2f )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.IgnoreGameObjectHierarchy( targetRoot )
			.Run();

		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return false;

		if ( trace.Collider.IsValid() && trace.Collider.IsTrigger )
			return false;

		if ( targetRoot.IsValid() && IsDescendantOf( targetRoot, trace.GameObject ) )
			return false;

		return trace.Distance < dist - 4f;
	}

	static bool IsDescendantOf( GameObject root, GameObject candidate )
	{
		if ( !root.IsValid() || !candidate.IsValid() )
			return false;

		for ( var node = candidate; node.IsValid(); node = node.Parent )
		{
			if ( node == root )
				return true;
		}

		return false;
	}
}
