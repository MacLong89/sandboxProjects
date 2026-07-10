using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host-only hitscan traces shared by <see cref="ThornsWeapon"/> and NPC combat (<see cref="ThornsBanditCombat"/>).
/// THORNS §3 — damage application stays on host; no client replication of traces.
/// </summary>
public static class ThornsSharedHostHitscan
{
	static readonly List<ThornsHealth> AnalyticHealthScratch = new();
	static readonly List<GameObject> HitscanIgnoreScratch = new();

	/// <summary>
	/// Feet-to-feet |ΔZ| cap for melee damage (players, tools, wildlife bite, etc.). Hitscan passes <c>0</c> to disable.
	/// Tuned so someone on a roof / full floor above does not eat street-level swings.
	/// </summary>
	public const float MeleeMaxAbsVerticalSeparationFeetDefault = 120f;

	/// <summary>When <paramref name="maxAbsDeltaZ"/> ≤ 0, always allows. Otherwise compares pawn/wildlife/bandit root Z.</summary>
	public static bool MeleeVerticalSeparationAllowsHit( GameObject attackerRoot, GameObject victimRoot, float maxAbsDeltaZ )
	{
		if ( maxAbsDeltaZ <= 0.001f || attackerRoot is null || !attackerRoot.IsValid()
		     || victimRoot is null || !victimRoot.IsValid() )
			return true;

		return MathF.Abs( victimRoot.WorldPosition.z - attackerRoot.WorldPosition.z ) <= maxAbsDeltaZ;
	}

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

	static readonly List<GameObject> CombatLosPierceScratch = new();

	/// <summary>Player / NPC / fauna weapon and melee — requires <see cref="DamageContext.AttackerRoot"/>.</summary>
	public static bool IsDirectCombatDamageKind( string kind )
	{
		if ( string.IsNullOrEmpty( kind ) )
			return false;

		return string.Equals( kind, "hitscan", StringComparison.Ordinal )
		       || string.Equals( kind, "melee", StringComparison.Ordinal )
		       || string.Equals( kind, "bandit_hitscan", StringComparison.Ordinal )
		       || string.Equals( kind, "wildlife_melee", StringComparison.Ordinal )
		       || string.Equals( kind, "tamed_assist", StringComparison.Ordinal );
	}

	public static GameObject ResolveCombatDamageVictimRoot( GameObject victimRootOrChild )
	{
		if ( victimRootOrChild is null || !victimRootOrChild.IsValid() )
			return default;

		var hp = victimRootOrChild.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
		return hp.IsValid() ? hp.GameObject : victimRootOrChild;
	}

	public static bool TryGetCombatDamageTraceStart( GameObject attackerRoot, out Vector3 traceStart )
	{
		traceStart = default;
		if ( attackerRoot is null || !attackerRoot.IsValid() )
			return false;

		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( attackerRoot, out traceStart, out _ ) )
			return true;

		var wildId = attackerRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( wildId.IsValid() )
		{
			traceStart = attackerRoot.WorldPosition + Vector3.Up * wildId.Definition.SenseHeightOffset;
			return true;
		}

		traceStart = attackerRoot.WorldPosition + Vector3.Up * 64f;
		return true;
	}

	public static Vector3 ResolveCombatDamageAimPoint( GameObject victimRoot )
	{
		if ( victimRoot is null || !victimRoot.IsValid() )
			return default;

		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( victimRoot, out var eye, out _ ) )
			return eye;

		return victimRoot.WorldPosition + Vector3.Up * 56f;
	}

	public static bool TraceHitBelongsToRoot( GameObject hitGo, GameObject targetRoot )
	{
		if ( hitGo is null || !hitGo.IsValid() || targetRoot is null || !targetRoot.IsValid() )
			return false;

		if ( hitGo == targetRoot )
			return true;

		for ( var p = hitGo; p.IsValid(); p = p.Parent )
		{
			if ( p == targetRoot )
				return true;
		}

		return false;
	}

	/// <summary>
	/// True when no world / structure / third-party creature geometry blocks damage from <paramref name="attackerRoot"/> to <paramref name="victimRoot"/>.
	/// Fauna hulls and other wildlife bodies pierce so melee is not blocked by animals circling between eye and torso aim point.
	/// </summary>
	public static bool CombatDamageHasClearLineOfSight( GameObject attackerRoot, GameObject victimRoot )
	{
		victimRoot = ResolveCombatDamageVictimRoot( victimRoot );
		if ( attackerRoot is null || !attackerRoot.IsValid() || victimRoot is null || !victimRoot.IsValid() )
			return false;

		if ( attackerRoot == victimRoot )
			return false;

		if ( !TryGetCombatDamageTraceStart( attackerRoot, out var traceStart ) )
			return false;

		var aim = ResolveCombatDamageAimPoint( victimRoot );
		var delta = aim - traceStart;
		var len = delta.Length;
		if ( len < 8f )
			return true;

		var dir = delta / len;
		var traceLen = Math.Max( 12f, len - 6f );

		var physicsScene = ResolvePhysicsScene( attackerRoot );
		if ( physicsScene is null || !physicsScene.IsValid() )
			return false;

		CombatLosPierceScratch.Clear();
		var cursor = traceStart;
		var rayEnd = traceStart + dir * traceLen;
		const float penetrationSkin = 1.5f;

		for ( var iter = 0; iter < 16; iter++ )
		{
			var segLen = Vector3.Dot( rayEnd - cursor, dir );
			if ( segLen <= 0.001f )
				return true;

			var tr = ThornsTraceUtility.RunWeaponHitscanSegment(
				physicsScene,
				new Ray( cursor, dir ),
				segLen,
				attackerRoot,
				CombatLosPierceScratch );

			if ( !tr.Hit )
				return true;

			if ( TraceHitBelongsToRoot( tr.GameObject, victimRoot ) )
				return true;

			if ( IsCombatLosFaunaPierceable( tr.GameObject, victimRoot ) )
			{
				cursor = tr.HitPosition + dir * penetrationSkin;
				if ( tr.GameObject.IsValid() )
					CombatLosPierceScratch.Add( tr.GameObject );
				continue;
			}

			return false;
		}

		return true;
	}

	static bool IsCombatLosFaunaPierceable( GameObject hitGo, GameObject victimRoot )
	{
		if ( hitGo is null || !hitGo.IsValid() )
			return false;

		if ( hitGo.Tags.Has( ThornsCollisionTags.WildlifeHull ) )
			return true;

		var hitWild = hitGo.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		return hitWild.IsValid() && !TraceHitBelongsToRoot( hitWild.GameObject, victimRoot );
	}

	/// <summary>
	/// After hitscan / analytic target resolution — skip redundant eye-to-center LOS when the attack ray already hit this victim.
	/// </summary>
	static bool HitscanTargetPassesLineOfSightGate(
		GameObject attackerRoot,
		GameObject victimRoot,
		GameObject directTraceHitGo,
		bool usedAnalyticFallback )
	{
		victimRoot = ResolveCombatDamageVictimRoot( victimRoot );
		if ( !victimRoot.IsValid() )
			return false;

		if ( !usedAnalyticFallback
		     && directTraceHitGo.IsValid()
		     && TraceHitBelongsToRoot( directTraceHitGo, victimRoot ) )
			return true;

		return CombatDamageHasClearLineOfSight( attackerRoot, victimRoot );
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
		return ThornsTraceUtility.RunWeaponHitscanSegment( physicsScene, ray, segmentLen, attackerRoot, ignoredRoots );
	}

	public static bool TryResolveHitscanDamageTarget(
		GameObject attackerRoot,
		Vector3 start,
		Vector3 dir,
		float range,
		float meleeMaxAbsVerticalSeparationFeet,
		out SceneTraceResult tr,
		out GameObject hitGo,
		out ThornsPawn victimPawn,
		out ThornsHealth victimHealth,
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
		var ignored = HitscanIgnoreScratch;
		ignored.Clear();
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
				if ( TryAnalyticCreatureAlongRay(
					     attackerRoot,
					     start,
					     dirN,
					     range,
					     meleeMaxAbsVerticalSeparationFeet,
					     out hitGo,
					     out victimPawn,
					     out victimHealth,
					     out analyticHitPosition ) )
				{
					if ( HitscanIsSelfHit( attackerRoot, victimHealth, victimPawn ) )
					{
						Log.Warning( "[Thorns] Analytic hit rejected: self" );
						return false;
					}

					if ( !HitscanTargetPassesLineOfSightGate( attackerRoot, victimHealth.GameObject, hitGo, usedAnalyticFallback: true ) )
						return false;

					usedAnalyticFallback = true;
					return true;
				}

				return false;
			}

			tr = segmentTr;
			hitGo = segmentTr.GameObject;
			victimPawn = hitGo.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
			victimHealth = hitGo.Components.GetInAncestorsOrSelf<ThornsHealth>( true );

			if ( IsResolvedDamageTarget( victimHealth, victimPawn, hitGo ) )
			{
				var victimRoot = victimHealth.IsValid() ? victimHealth.GameObject : hitGo;
				if ( !MeleeVerticalSeparationAllowsHit( attackerRoot, victimRoot, meleeMaxAbsVerticalSeparationFeet ) )
				{
					cursor = segmentTr.HitPosition + dirN * penetrationSkin;
					if ( hitGo.IsValid() )
						ignored.Add( hitGo );
					continue;
				}

				if ( HitscanIsSelfHit( attackerRoot, victimHealth, victimPawn ) )
				{
					Log.Warning( "[Thorns] Ignoring self-hit after trace" );
					return false;
				}

				if ( !HitscanTargetPassesLineOfSightGate( attackerRoot, victimRoot, hitGo, usedAnalyticFallback: false ) )
					return false;

				return true;
			}

			if ( hitGo.Components.GetInAncestorsOrSelf<ThornsPlacedStructure>( true ).IsValid() )
				return false;

			cursor = segmentTr.HitPosition + dirN * penetrationSkin;
			if ( hitGo.IsValid() )
				ignored.Add( hitGo );
		}

		if ( TryAnalyticCreatureAlongRay(
			     attackerRoot,
			     start,
			     dirN,
			     range,
			     meleeMaxAbsVerticalSeparationFeet,
			     out hitGo,
			     out victimPawn,
			     out victimHealth,
			     out analyticHitPosition ) )
		{
			if ( HitscanIsSelfHit( attackerRoot, victimHealth, victimPawn ) )
				return false;

			if ( !HitscanTargetPassesLineOfSightGate( attackerRoot, victimHealth.GameObject, hitGo, usedAnalyticFallback: true ) )
				return false;

			usedAnalyticFallback = true;
			return true;
		}

		return false;
	}

	/// <summary>Pawn, wildlife, or humanoid bandit (<see cref="ThornsBanditBrain"/>) with <see cref="ThornsHealth"/>.</summary>
	public static bool IsResolvedDamageTarget( ThornsHealth victimHealth, ThornsPawn victimPawn, GameObject hitGo ) =>
		victimHealth.IsValid() && ( victimPawn.IsValid()
		                            || hitGo.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true ).IsValid()
		                            || hitGo.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid() );

	static bool HitscanIsSelfHit( GameObject attackerWeaponOrRoot, ThornsHealth victimHealth, ThornsPawn victimPawn )
	{
		if ( !victimPawn.IsValid() )
			return false;

		var attackerPawn = attackerWeaponOrRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		return attackerPawn.IsValid() && attackerPawn.GameObject == victimPawn.GameObject;
	}

	/// <summary>
	/// Builds melee analytic candidates from registries (players / wildlife / bandits) instead of scanning every
	/// <see cref="ThornsHealth"/> in the scene — scales with combatants, not world loot or props (THORNS §13).
	/// </summary>
	static void CollectMeleeAnalyticHealthTargets( GameObject attackerRoot, Scene physicsScene, List<ThornsHealth> dst )
	{
		dst.Clear();

		var anyIndexedPlayer = false;
		foreach ( var pawn in ThornsPawnConnectionIndex.AllWithOwnerId )
		{
			if ( !pawn.IsValid() )
				continue;

			var root = pawn.GameObject;
			if ( !root.IsValid() || root == attackerRoot )
				continue;

			if ( root.Components.Get<ThornsBanditBrain>( FindMode.EnabledInSelf ).IsValid() )
				continue;

			var h = root.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
			if ( !h.IsValid() || !h.IsAlive )
				continue;

			dst.Add( h );
			anyIndexedPlayer = true;
		}

		if ( !anyIndexedPlayer && physicsScene is not null && physicsScene.IsValid() )
		{
			foreach ( var pawn in physicsScene.GetAllComponents<ThornsPawn>() )
			{
				if ( !pawn.IsValid() )
					continue;

				var root = pawn.GameObject;
				if ( !root.IsValid() || root == attackerRoot )
					continue;

				if ( root.Components.Get<ThornsBanditBrain>( FindMode.EnabledInSelf ).IsValid() )
					continue;

				var h = root.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
				if ( h.IsValid() && h.IsAlive )
					dst.Add( h );
			}
		}

		if ( Networking.IsHost && ThornsPopulationDirector.HostWildlifeGlobalCount > 0 )
		{
			foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
			{
				if ( !brain.IsValid() )
					continue;

				var h = brain.GameObject.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
				if ( h.IsValid() && h.IsAlive )
					dst.Add( h );
			}
		}
		else if ( physicsScene is not null && physicsScene.IsValid() )
		{
			foreach ( var brain in physicsScene.GetAllComponents<ThornsWildlifeBrain>() )
			{
				if ( !brain.IsValid() )
					continue;

				var h = brain.GameObject.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
				if ( h.IsValid() && h.IsAlive )
					dst.Add( h );
			}
		}

		if ( Networking.IsHost && ThornsPopulationDirector.HostBanditGlobalCount > 0 )
		{
			foreach ( var brain in ThornsPopulationDirector.HostBanditBrainsReadOnly )
			{
				if ( !brain.IsValid() )
					continue;

				var h = brain.GameObject.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
				if ( h.IsValid() && h.IsAlive )
					dst.Add( h );
			}
		}
		else if ( physicsScene is not null && physicsScene.IsValid() )
		{
			foreach ( var brain in physicsScene.GetAllComponents<ThornsBanditBrain>() )
			{
				if ( !brain.IsValid() )
					continue;

				var h = brain.GameObject.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
				if ( h.IsValid() && h.IsAlive )
					dst.Add( h );
			}
		}
	}

	/// <summary>Analytic sphere fallback for pawns (capsule proxy), bandits (<see cref="ThornsBanditMotor"/> CC), and wildlife.</summary>
	public static bool TryAnalyticCreatureAlongRay(
		GameObject attackerRoot,
		Vector3 start,
		Vector3 dirN,
		float range,
		float meleeMaxAbsVerticalSeparationFeet,
		out GameObject hitGo,
		out ThornsPawn victimPawn,
		out ThornsHealth victimHealth,
		out Vector3 hitPosition )
	{
		hitGo = default;
		victimPawn = default;
		victimHealth = default;
		hitPosition = default;

		var physicsScene = ResolvePhysicsScene( attackerRoot );
		if ( physicsScene is null || !physicsScene.IsValid() )
			return false;

		var attackerPawn = attackerRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		var attackerBandit = attackerRoot.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );

		var bestT = float.MaxValue;
		ThornsHealth bestH = default;
		Vector3 bestPos = default;

		CollectMeleeAnalyticHealthTargets( attackerRoot, physicsScene, AnalyticHealthScratch );

		foreach ( var h in AnalyticHealthScratch )
		{
			if ( !h.IsValid() || !h.IsAlive )
				continue;

			var root = h.GameObject;
			if ( !root.IsValid() || root == attackerRoot )
				continue;

			var pawn = root.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
			var wild = root.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			var bandit = root.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
			if ( !pawn.IsValid() && !wild.IsValid() && !bandit.IsValid() )
				continue;

			if ( pawn.IsValid() && attackerPawn.IsValid() && pawn.GameObject == attackerPawn.GameObject )
				continue;

			// Bandit NPCs ignore hits on other bandits (matches <see cref="ThornsBanditCombat"/> intent).
			if ( attackerBandit.IsValid() && bandit.IsValid() )
				continue;

			if ( !MeleeVerticalSeparationAllowsHit( attackerRoot, root, meleeMaxAbsVerticalSeparationFeet ) )
				continue;

			var ctrl = root.Components.Get<CharacterController>();
			var pc = root.Components.Get<PlayerController>();
			float radius;
			float height;
			if ( pc.IsValid() )
			{
				radius = pc.BodyRadius;
				height = pc.BodyHeight;
			}
			else if ( ctrl.IsValid() )
			{
				radius = ctrl.Radius;
				height = ctrl.Height;
			}
			else if ( wild.IsValid() )
			{
				if ( ctrl.IsValid() )
				{
					var ws = root.WorldScale;
					radius = ctrl.Radius * MathF.Max( ws.x, ws.y );
					height = ctrl.Height * MathF.Abs( ws.z );
				}
				else
				{
					radius = ThornsWildlifeMotor.DefaultCapsuleRadius;
					height = ThornsWildlifeMotor.DefaultCapsuleHeight;
				}
			}
			else
			{
				continue;
			}

			var feet = root.WorldPosition;
			var center = feet + Vector3.Up * (height * 0.5f);
			var sphereR = Math.Max( radius * 2.25f, height * 0.42f );

			if ( wild.IsValid() && wild.Species == ThornsWildlifeSpeciesKind.Panther )
			{
				center += Vector3.Up * (height * ThornsWildlifeSpawn.PantherHitscanCapsuleCenterLiftFraction);
				sphereR *= ThornsWildlifeSpawn.PantherHitscanSphereRadiusMul;
			}

			var w = start - center;
			var wd = Vector3.Dot( w, dirN );
			var inner = wd * wd - Vector3.Dot( w, w ) + sphereR * sphereR;
			if ( inner < 0f )
				continue;

			var rootT = MathF.Sqrt( inner );
			var t0 = -wd - rootT;
			var t1 = -wd + rootT;
			float tHit = -1f;
			if ( t0 >= 0f && t0 <= range )
				tHit = t0;
			else if ( t1 >= 0f && t1 <= range )
				tHit = t1;

			if ( tHit < 0f || tHit > bestT )
				continue;

			bestT = tHit;
			bestH = h;
			bestPos = start + dirN * tHit;
		}

		if ( !bestH.IsValid() )
			return false;

		victimHealth = bestH;
		hitGo = bestH.GameObject;
		victimPawn = hitGo.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		hitPosition = bestPos;
		return victimPawn.IsValid()
		       || hitGo.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true ).IsValid()
		       || hitGo.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid();
	}

}
