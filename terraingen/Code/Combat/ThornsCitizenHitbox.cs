namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Core;
using Terraingen.Player;

/// <summary>Vertical capsule hitbox for citizen players and bandits (72" tall by default).</summary>
public static class ThornsCitizenHitbox
{
	/// <summary>Top portion of the hitbox height that counts as a headshot zone.</summary>
	public const float HeadZoneHeightFraction = 0.2f;

	public const float HeadshotDamageMultiplier = 4f;

	/// <summary>Extra capsule slack so combat traces match the full citizen body when skeleton hitboxes run short.</summary>
	public const float CombatCapsuleHeightPadding = 10f;

	public const float CombatCapsuleRadiusPadding = 3f;

	public static bool IsCitizen( GameObject root )
	{
		if ( !root.IsValid() )
			return false;

		if ( root.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndParent ).IsValid() )
			return true;

		return root.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent ) is { IsValid: true };
	}

	public static bool TryGetExtents( GameObject root, out Vector3 feetWorld, out float height, out float radius )
	{
		feetWorld = default;
		height = ThornsPlayerFirstPersonRig.DefaultBodyHeight;
		radius = ThornsPlayerFirstPersonRig.DefaultBodyRadius;

		if ( !root.IsValid() )
			return false;

		feetWorld = root.WorldPosition;

		var controller = root.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( controller.IsValid() )
		{
			if ( controller.BodyHeight > 1f )
				height = controller.BodyHeight;
			if ( controller.BodyRadius > 0.5f )
				radius = controller.BodyRadius;
			return true;
		}

		var cc = root.Components.Get<CharacterController>( FindMode.EverythingInSelf );
		if ( cc.IsValid() )
		{
			if ( cc.Height > 1f )
				height = cc.Height;
			if ( cc.Radius > 0.5f )
				radius = cc.Radius;
			return true;
		}

		return true;
	}

	public static bool IsHeadshotAt( Vector3 hitWorld, GameObject victimRoot )
	{
		if ( !TryGetExtents( victimRoot, out var feet, out var height, out _ ) )
			return false;

		var headZoneMinZ = feet.z + height * (1f - HeadZoneHeightFraction);
		return hitWorld.z >= headZoneMinZ;
	}

	public static bool IsHeadshotFromTrace( SceneTraceResult trace, GameObject victimRoot )
	{
		if ( !IsCitizen( victimRoot ) || !trace.Hit )
			return false;

		if ( ThornsCombatTraceUtil.HasHeadHitboxTag( trace ) )
			return true;

		return IsHeadshotAt( trace.HitPosition, victimRoot );
	}

	/// <summary>Closest point on the citizen capsule along the shot ray (for head zone tests).</summary>
	public static bool TrySampleRayHit(
		GameObject victimRoot,
		Vector3 rayOrigin,
		Vector3 rayDirection,
		float maxRange,
		out Vector3 hitWorld )
	{
		hitWorld = default;
		if ( !TryGetExtents( victimRoot, out var feet, out var height, out var radius ) )
			return false;

		height += CombatCapsuleHeightPadding;
		radius += CombatCapsuleRadiusPadding;

		var dir = rayDirection.Normal;
		if ( dir.Length < 0.95f || maxRange <= 0f )
			return false;

		var axisBottom = feet + Vector3.Up * radius;
		var axisTop = feet + Vector3.Up * Math.Max( radius, height - radius );

		if ( !TryClosestPointRayToSegment( rayOrigin, dir, axisBottom, axisTop, out var tRay, out var pointOnRay, out var pointOnAxis ) )
			return false;

		if ( tRay < 0f || tRay > maxRange )
			return false;

		var radial = pointOnRay - pointOnAxis;
		var radialDist = new Vector2( radial.x, radial.y ).Length;
		if ( radialDist > radius + 0.5f )
			return false;

		hitWorld = pointOnRay;
		return true;
	}

	public static bool TryClassifyCitizenHit(
		GameObject victimRoot,
		Vector3 rayOrigin,
		Vector3 rayDirection,
		float maxRange,
		SceneTraceResult resolveTrace,
		out Vector3 hitWorld,
		out bool isHeadshot )
	{
		hitWorld = default;
		isHeadshot = false;

		if ( !IsCitizen( victimRoot ) )
			return false;

		if ( resolveTrace.Hit )
		{
			hitWorld = resolveTrace.HitPosition;
			isHeadshot = IsHeadshotFromTrace( resolveTrace, victimRoot );
			return true;
		}

		return TryClassifyHeadshot( victimRoot, rayOrigin, rayDirection, maxRange, out hitWorld, out isHeadshot );
	}

	/// <summary>Crosshair capsule pick for citizens when skeleton hitboxes miss (first valid intersection along the ray).</summary>
	public static bool TryPickCapsuleAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out GameObject victimRoot,
		out ThornsCombatDamage.VictimKind victimKind,
		out float hitDistance,
		out Vector3 hitWorld )
	{
		victimRoot = null;
		victimKind = ThornsCombatDamage.VictimKind.Unknown;
		hitDistance = float.MaxValue;
		hitWorld = default;

		if ( scene is null || !scene.IsValid() )
			return false;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f || maxRange <= 0f )
			return false;

		var bestDistance = float.MaxValue;
		GameObject bestRoot = null;
		var bestKind = ThornsCombatDamage.VictimKind.Unknown;
		Vector3 bestHitWorld = default;

		foreach ( var brain in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !brain.IsValid() || !brain.GameObject.IsValid() )
				continue;

			TryConsiderCapsuleCandidate(
				brain.GameObject,
				ThornsCombatDamage.VictimKind.Npc,
				origin,
				dir,
				maxRange,
				ignoreRoot,
				ref bestDistance,
				ref bestRoot,
				ref bestKind,
				ref bestHitWorld );
		}

		ThornsPlayerRootCache.RefreshIfStale( scene );
		foreach ( var root in ThornsPlayerRootCache.RootsReadOnly )
		{
			TryConsiderCapsuleCandidate(
				root,
				ThornsCombatDamage.VictimKind.Player,
				origin,
				dir,
				maxRange,
				ignoreRoot,
				ref bestDistance,
				ref bestRoot,
				ref bestKind,
				ref bestHitWorld );
		}

		if ( !bestRoot.IsValid() )
			return false;

		victimRoot = bestRoot;
		victimKind = bestKind;
		hitDistance = bestDistance;
		hitWorld = bestHitWorld;
		return true;
	}

	static void TryConsiderCapsuleCandidate(
		GameObject root,
		ThornsCombatDamage.VictimKind kind,
		Vector3 origin,
		Vector3 dir,
		float maxRange,
		GameObject ignoreRoot,
		ref float bestDistance,
		ref GameObject bestRoot,
		ref ThornsCombatDamage.VictimKind bestKind,
		ref Vector3 bestHitWorld )
	{
		if ( !root.IsValid() )
			return;

		if ( ignoreRoot.IsValid() && root == ignoreRoot )
			return;

		var rootPos = root.WorldPosition;
		var toRoot = rootPos - origin;
		var forward = Vector3.Dot( toRoot, dir );
		if ( forward < -64f || forward > maxRange + 64f )
			return;

		var lateralSq = (toRoot - dir * forward).LengthSquared;
		if ( lateralSq > maxRange * maxRange * 0.35f )
			return;

		if ( kind == ThornsCombatDamage.VictimKind.Npc )
		{
			var bandit = root.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
			if ( !bandit.IsValid() || bandit.IsDead )
				return;
		}
		else if ( kind == ThornsCombatDamage.VictimKind.Player )
		{
			var health = root.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndParent );
			if ( health is not null && health.IsValid() && !health.IsAlive )
				return;
		}
		else
		{
			return;
		}

		if ( !TrySampleRayHit( root, origin, dir, maxRange, out var sample ) )
			return;

		var dist = Vector3.Dot( sample - origin, dir );
		if ( dist < 0f || dist >= bestDistance )
			return;

		bestDistance = dist;
		bestHitWorld = sample;
		bestRoot = root;
		bestKind = kind;
	}

	public static bool TryClassifyHeadshot(
		GameObject victimRoot,
		Vector3 rayOrigin,
		Vector3 rayDirection,
		float maxRange,
		out Vector3 hitWorld,
		out bool isHeadshot )
	{
		hitWorld = default;
		isHeadshot = false;

		if ( !IsCitizen( victimRoot ) )
			return false;

		if ( !TrySampleRayHit( victimRoot, rayOrigin, rayDirection, maxRange, out hitWorld ) )
		{
			if ( !TryGetExtents( victimRoot, out var feet, out var height, out _ ) )
				return false;

			hitWorld = feet + Vector3.Up * (height * 0.5f);
		}

		isHeadshot = IsHeadshotAt( hitWorld, victimRoot );
		return true;
	}

	static bool TryClosestPointRayToSegment(
		Vector3 rayOrigin,
		Vector3 rayDir,
		Vector3 segA,
		Vector3 segB,
		out float tRay,
		out Vector3 pointOnRay,
		out Vector3 pointOnSegment )
	{
		tRay = 0f;
		pointOnRay = rayOrigin;
		pointOnSegment = segA;

		var ab = segB - segA;
		var abLenSq = ab.LengthSquared;
		if ( abLenSq < 1e-6f )
		{
			var w = segA - rayOrigin;
			tRay = Math.Max( 0f, Vector3.Dot( w, rayDir ) );
			pointOnRay = rayOrigin + rayDir * tRay;
			pointOnSegment = segA;
			return true;
		}

		var u = rayOrigin - segA;
		var tSeg = Math.Clamp( Vector3.Dot( -u, ab ) / abLenSq, 0f, 1f );
		pointOnSegment = segA + ab * tSeg;

		var wSeg = pointOnSegment - rayOrigin;
		tRay = Vector3.Dot( wSeg, rayDir );
		pointOnRay = rayOrigin + rayDir * tRay;
		return true;
	}
}
