namespace Terraingen.Animals;

using Terraingen.Combat;

/// <summary>Host-side aim picking for player vs animal combat and interactions.</summary>
public static class ThornsAnimalHitUtil
{
	const float TraceSphereRadius = 28f;
	const float RegistryRadiusScale = 1.45f;
	const float RegistryMinRadius = 40f;

	public static bool TryPickBrainAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out ThornsAnimalBrain brain,
		Func<ThornsAnimalBrain, bool> accept = null )
		=> TryPickBrainAlongRay( scene, origin, direction, maxRange, ignoreRoot, out brain, out _, out _, accept );

	public static bool TryPickBrainAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out ThornsAnimalBrain brain,
		out float hitDistance,
		out SceneTraceResult resolveTrace,
		Func<ThornsAnimalBrain, bool> accept = null )
	{
		brain = null;
		hitDistance = float.MaxValue;
		resolveTrace = default;

		if ( scene is null || !scene.IsValid )
			return false;

		bool CanAccept( ThornsAnimalBrain candidate ) =>
			candidate.IsValid() && !candidate.IsDead && ( accept is null || accept( candidate ) );

		var dir = direction.Normal;
		if ( dir.Length < 0.95f || maxRange <= 0f )
			return false;

		var end = origin + dir * maxRange;
		var bestDistance = float.MaxValue;
		ThornsAnimalBrain best = null;
		SceneTraceResult bestTrace = default;

		void TryAcceptTraceHit( SceneTraceResult trace )
		{
			if ( !trace.Hit || !trace.GameObject.IsValid() )
				return;

			if ( !TryResolveBrainFromHit( trace.GameObject, out var hitBrain ) || !CanAccept( hitBrain ) )
				return;

			var distance = trace.Distance;
			if ( distance >= bestDistance )
				return;

			bestDistance = distance;
			best = hitBrain;
			bestTrace = trace;
		}

		TryAcceptTraceHit(
			scene.Trace
				.Sphere( TraceSphereRadius, origin, end )
				.UseHitboxes( true )
				.IgnoreGameObjectHierarchy( ignoreRoot )
				.Run() );

		if ( !best.IsValid() )
		{
			TryAcceptTraceHit(
				scene.Trace
					.Sphere( TraceSphereRadius, origin, end )
					.HitTriggers()
					.WithTag( "animal" )
					.IgnoreGameObjectHierarchy( ignoreRoot )
					.Run() );

			if ( !best.IsValid() )
			{
				TryAcceptTraceHit(
					scene.Trace
						.Sphere( TraceSphereRadius, origin, end )
						.HitTriggers()
						.IgnoreGameObjectHierarchy( ignoreRoot )
						.Run() );
			}
		}

		if ( !best.IsValid() )
		{
			foreach ( var candidate in ThornsAnimalManager.AnimalRegistry )
			{
				if ( !CanAccept( candidate ) )
					continue;

				if ( ignoreRoot.IsValid() && candidate.GameObject == ignoreRoot )
					continue;

				var bodyRadius = candidate.GetBodyRadius();
				var center = candidate.GameObject.WorldPosition + Vector3.Up * bodyRadius * 0.5f;
				var toCenter = center - origin;
				var forward = Vector3.Dot( toCenter, dir );
				if ( forward < -bodyRadius || forward > maxRange + bodyRadius )
					continue;

				var lateralSq = (toCenter - dir * forward ).LengthSquared;
				var rejectRadius = MathF.Max( bodyRadius * RegistryRadiusScale, RegistryMinRadius ) + maxRange * 0.05f;
				if ( lateralSq > rejectRadius * rejectRadius )
					continue;

				var radius = MathF.Max( bodyRadius * RegistryRadiusScale, RegistryMinRadius );

				if ( !ThornsInteractAimPick.TryRaySphere( origin, dir, center, radius, out var distance )
				     || distance > maxRange
				     || distance >= bestDistance )
					continue;

				bestDistance = distance;
				best = candidate;
				bestTrace = default;
			}
		}

		brain = best;
		if ( !brain.IsValid() )
			return false;

		hitDistance = bestDistance;
		resolveTrace = bestTrace.Hit
			? bestTrace
			: ThornsCombatTraceUtil.SyntheticHit( brain.GameObject, origin + dir * bestDistance );
		return true;
	}

	static bool TryResolveBrainFromHit( GameObject hit, out ThornsAnimalBrain brain )
	{
		brain = null;
		if ( !hit.IsValid() )
			return false;

		brain = hit.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return brain.IsValid() && !brain.IsDead;
	}
}
