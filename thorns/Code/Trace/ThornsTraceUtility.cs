using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Central ray construction for Thorns — keeps <c>UseHitboxes</c> / <c>UsePhysicsWorld</c> / <c>UseHitPosition</c> aligned per profile.
/// </summary>
public static class ThornsTraceUtility
{
	public static SceneTrace PrepareRay( Scene scene, in Ray ray, float distance, ThornsTraceProfile profile )
	{
		var rules = ThornsTraceRuleSet.For( profile );
		return PrepareRay( scene, ray, distance, rules );
	}

	public static SceneTrace PrepareRay( Scene scene, in Ray ray, float distance, in ThornsTraceRuleSet rules )
	{
		var t = scene.Trace.Ray( ray, distance );
		if ( rules.UseHitPosition )
			t = t.UseHitPosition( true );
		if ( rules.UseHitboxes )
			t = t.UseHitboxes( true );
		if ( rules.UsePhysicsWorld )
			t = t.UsePhysicsWorld( true );
		return t;
	}

	public static SceneTrace WithIgnoredRoots(
		SceneTrace trace,
		GameObject primaryIgnore,
		IEnumerable<GameObject> extraIgnores = null )
	{
		var t = trace;
		if ( primaryIgnore.IsValid() )
			t = t.IgnoreGameObjectHierarchy( primaryIgnore );

		if ( extraIgnores is not null )
		{
			foreach ( var g in extraIgnores )
			{
				if ( g.IsValid() )
					t = t.IgnoreGameObjectHierarchy( g );
			}
		}

		return t;
	}

	public static SceneTraceResult RunRay(
		Scene scene,
		in Ray ray,
		float distance,
		ThornsTraceProfile profile,
		GameObject primaryIgnore = null,
		IEnumerable<GameObject> extraIgnores = null )
	{
		if ( scene is null || !scene.IsValid() )
			return default;

		var t = WithIgnoredRoots( PrepareRay( scene, ray, distance, profile ), primaryIgnore, extraIgnores );
		var tr = t.Run();
		ThornsTraceDiagnostics.BumpRay( profile );
		ThornsTraceDebug.Record( profile, ray, distance, tr );
		return tr;
	}

	/// <summary>Host weapon hitscan: hitboxes first, then solid physics fallback (same contract as legacy hitscan helper).</summary>
	public static SceneTraceResult RunWeaponHitscanSegment(
		Scene physicsScene,
		in Ray ray,
		float segmentLen,
		GameObject attackerRoot,
		List<GameObject> ignoredRoots )
	{
		if ( physicsScene is null || !physicsScene.IsValid() )
			return default;

		var hitRules = ThornsTraceRuleSet.For( ThornsTraceProfile.WeaponHitscan );
		var solidRules = new ThornsTraceRuleSet( hitRules.UseHitPosition, useHitboxes: false, hitRules.UsePhysicsWorld );

		var b = WithIgnoredRoots(
			PrepareRay( physicsScene, ray, segmentLen, hitRules ),
			attackerRoot,
			ignoredRoots );

		var tr = b.Run();
		if ( !tr.Hit )
		{
			b = WithIgnoredRoots(
				PrepareRay( physicsScene, ray, segmentLen, solidRules ),
				attackerRoot,
				ignoredRoots );
			tr = b.Run();
		}

		ThornsTraceDiagnostics.BumpRay( ThornsTraceProfile.WeaponHitscan );
		ThornsTraceDebug.Record( ThornsTraceProfile.WeaponHitscan, ray, segmentLen, tr );
		return tr;
	}
}
