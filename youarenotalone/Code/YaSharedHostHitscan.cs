using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host-only hitscan traces shared by <see cref="YaWeapon"/> and NPC combat (<see cref="ThornsBanditCombat"/>).
/// THORNS §3 — damage application stays on host; no client replication of traces.
/// </summary>
public static class YaSharedHostHitscan
{
	/// <summary>Static helpers must not bind <see cref="Scene.Trace"/> via the <see cref="Scene"/> type name.</summary>
	static Scene ResolvePhysicsScene( GameObject attackerRoot )
	{
		if ( attackerRoot.IsValid() )
		{
			var s = attackerRoot.Scene;
			if ( s is not null && s.IsValid() )
				return s;
		}

		var active = Game.ActiveScene;
		if ( active is not null && active.IsValid() )
			return active;

		return default;
	}

	public static Vector3 SamplePelletDirection( Vector3 forwardWorld, float halfAngleDegrees )
	{
		var f = forwardWorld.Normal;
		if ( halfAngleDegrees <= 0.001f )
			return f;

		var spreadRad = halfAngleDegrees * (MathF.PI / 180f);
		var cosMax = MathF.Cos( spreadRad );
		var cosTheta = 1f - Random.Shared.NextSingle() * (1f - cosMax );
		var sinTheta = MathF.Sqrt( MathF.Max( 0f, 1f - cosTheta * cosTheta ) );
		var phi = Random.Shared.NextSingle() * 2f * MathF.PI;

		var bitangent = Vector3.Cross( Vector3.Up, f );
		if ( bitangent.LengthSquared < 1e-6f )
			bitangent = Vector3.Cross( Vector3.Right, f );
		bitangent = bitangent.Normal;

		var up = Vector3.Cross( f, bitangent ).Normal;
		var pellet = f * cosTheta + bitangent * (sinTheta * MathF.Cos( phi )) + up * (sinTheta * MathF.Sin( phi ));
		return pellet.Normal;
	}

	public static SceneTraceResult TraceHitscanSegment(
		GameObject attackerRoot,
		Vector3 segmentStart,
		Vector3 dir,
		float segmentLen,
		List<GameObject> ignoredRoots )
	{
		var dirN = dir.Normal;
		if ( dirN.LengthSquared < 1e-8f )
			return default;

		var physicsScene = ResolvePhysicsScene( attackerRoot );
		if ( physicsScene is null || !physicsScene.IsValid() )
			return default;

		var ray = new Ray( segmentStart, dirN );
		var b = physicsScene.Trace.Ray( ray, segmentLen )
			.UseHitPosition( true )
			.UseHitboxes( true )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( attackerRoot );
		foreach ( var ig in ignoredRoots )
		{
			if ( ig.IsValid() )
				b = b.IgnoreGameObjectHierarchy( ig );
		}

		var tr = b.Run();
		if ( !tr.Hit )
		{
			b = physicsScene.Trace.Ray( ray, segmentLen )
				.UseHitPosition( true )
				.UseHitboxes( false )
				.UsePhysicsWorld( true )
				.IgnoreGameObjectHierarchy( attackerRoot );
			foreach ( var ig in ignoredRoots )
			{
				if ( ig.IsValid() )
					b = b.IgnoreGameObjectHierarchy( ig );
			}

			tr = b.Run();
		}

		return tr;
	}

	public static bool TryResolveHitscanDamageTarget(
		GameObject attackerRoot,
		Vector3 start,
		Vector3 dir,
		float range,
		out SceneTraceResult tr,
		out GameObject hitGo,
		out YaPawn victimPawn,
		out YaPlayerHealth victimHealth,
		out bool usedAnalyticFallback,
		out Vector3 analyticHitPosition )
	{
		tr = default;
		hitGo = default;
		victimPawn = default;
		victimHealth = default;
		usedAnalyticFallback = false;
		analyticHitPosition = default;

		var dirN = dir.Normal;
		if ( dirN.LengthSquared < 1e-8f )
			return false;

		var rayEnd = start + dirN * range;
		var cursor = start;
		var ignored = new List<GameObject>();
		const float penetrationSkin = 1.5f;

		for ( var iter = 0; iter < 24; iter++ )
		{
			var segLen = Vector3.Dot( rayEnd - cursor, dirN );
			if ( segLen <= 0.001f )
				break;

			var segmentTr = TraceHitscanSegment( attackerRoot, cursor, dirN, segLen, ignored );
			if ( !segmentTr.Hit )
			{
				tr = segmentTr;
				if ( TryAnalyticPawnAlongRay( attackerRoot, start, dirN, range, out hitGo, out victimPawn, out victimHealth, out analyticHitPosition ) )
				{
					if ( victimPawn.GameObject == attackerRoot )
					{
						Log.Warning( "[YA] Analytic hit rejected: self" );
						return false;
					}

					usedAnalyticFallback = true;
					Log.Info( $"[YA] Hitscan: physics trace miss — analytic pawn hit '{hitGo?.Name}' at {analyticHitPosition}" );
					return true;
				}

				Log.Info( "[YA] Hitscan: miss (no hit)" );
				return false;
			}

			tr = segmentTr;
			hitGo = segmentTr.GameObject;
			victimPawn = hitGo.Components.GetInAncestorsOrSelf<YaPawn>( true );
			victimHealth = hitGo.Components.GetInAncestorsOrSelf<YaPlayerHealth>( true );

			if ( victimHealth.IsValid() && victimPawn.IsValid() )
			{
				if ( victimPawn.GameObject == attackerRoot )
				{
					Log.Warning( "[YA] Ignoring self-hit after trace" );
					return false;
				}

				return true;
			}

			Log.Info( $"[YA] Hitscan passes through '{hitGo?.Name}' — continuing toward pawns" );
			cursor = segmentTr.HitPosition + dirN * penetrationSkin;
			if ( hitGo.IsValid() )
				ignored.Add( hitGo );
		}

		if ( TryAnalyticPawnAlongRay( attackerRoot, start, dirN, range, out hitGo, out victimPawn, out victimHealth, out analyticHitPosition ) )
		{
			if ( victimPawn.GameObject == attackerRoot )
				return false;
			usedAnalyticFallback = true;
			Log.Info( $"[YA] Hitscan: max penetrations — analytic pawn hit '{hitGo?.Name}'" );
			return true;
		}

		Log.Info( "[YA] Hitscan: gave up after max penetrations (no pawn hit)" );
		return false;
	}

	public static bool TryAnalyticPawnAlongRay(
		GameObject attackerRoot,
		Vector3 start,
		Vector3 dirN,
		float range,
		out GameObject hitGo,
		out YaPawn victimPawn,
		out YaPlayerHealth victimHealth,
		out Vector3 hitPosition )
	{
		hitGo = default;
		victimPawn = default;
		victimHealth = default;
		hitPosition = default;

		var physicsScene = ResolvePhysicsScene( attackerRoot );
		if ( physicsScene is null || !physicsScene.IsValid() )
			return false;

		var bestT = float.MaxValue;
		YaPlayerHealth bestH = default;
		Vector3 bestPos = default;

		foreach ( var h in physicsScene.GetAllComponents<YaPlayerHealth>() )
		{
			if ( !h.IsValid() || !h.IsAlive )
				continue;

			var root = h.GameObject;
			if ( !root.IsValid() || root == attackerRoot )
				continue;

			var pawn = root.Components.GetInAncestorsOrSelf<YaPawn>( true );
			if ( !pawn.IsValid() )
				continue;

			var ctrl = root.Components.Get<CharacterController>();
			var radius = ctrl.IsValid() ? ctrl.Radius : 28f;
			var height = ctrl.IsValid() ? ctrl.Height : 72f;
			var feet = root.WorldPosition;
			var center = feet + Vector3.Up * (height * 0.5f);
			var sphereR = Math.Max( radius * 2.25f, height * 0.42f );

			var w = start - center;
			var wd = Vector3.Dot( w, dirN );
			var inner = wd * wd - Vector3.Dot( w, w ) + sphereR * sphereR;
			if ( inner < 0f )
				continue;

			var rootT = MathF.Sqrt( inner );
			var tNear = -wd - rootT;
			var tFar = -wd + rootT;
			if ( tNear > tFar )
				(tNear, tFar) = (tFar, tNear);

			// Require overlap between ray segment [0, range] and sphere chord [tNear, tFar].
			// Old logic required an intersection *distance* to lie in [0, range]; when overlapping point‑blank,
			// the forward exit can exceed melee MaxRange while the segment still crosses the target — misses only up close.
			var overlapStart = Math.Max( 0f, tNear );
			var overlapEnd = Math.Min( range, tFar );
			if ( overlapStart > overlapEnd )
				continue;

			var tHit = overlapStart;

			if ( tHit > bestT )
				continue;

			bestT = tHit;
			bestH = h;
			bestPos = start + dirN * tHit;
		}

		if ( !bestH.IsValid() )
			return false;

		victimHealth = bestH;
		hitGo = bestH.GameObject;
		victimPawn = hitGo.Components.GetInAncestorsOrSelf<YaPawn>( true );
		hitPosition = bestPos;
		return victimPawn.IsValid();
	}
}
