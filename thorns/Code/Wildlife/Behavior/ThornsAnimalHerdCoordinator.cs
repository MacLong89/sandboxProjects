namespace Sandbox;

/// <summary>Elk/deer herd panic propagation and loose group wander alignment.</summary>
public static class ThornsAnimalHerdCoordinator
{
	public const float HerdRadius = 2200f;
	public const float PanicBroadcastSeconds = 6f;

	public static void BroadcastFleePanic( GameObject source, GameObject threat )
	{
		if ( !Networking.IsHost || source is null || threat is null || !source.IsValid() || !threat.IsValid() )
			return;

		var srcId = source.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( !srcId.IsValid() )
			return;

		var profile = ThornsAnimalBehaviorProfile.Get( srcId.Species );
		if ( profile.HerdPreference < 0.4f )
			return;

		var flat = source.WorldPosition.WithZ( 0 );
		var maxDist2 = HerdRadius * HerdRadius;

		foreach ( var mateBrain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !mateBrain.IsValid() )
				continue;

			var go = mateBrain.GameObject;
			if ( go == source || !go.IsValid() )
				continue;

			var mateId = go.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( !mateId.IsValid() || mateId.Species != srcId.Species || mateId.HostIsDead || mateId.HostIsTamed )
				continue;

			if ( ( go.WorldPosition.WithZ( 0 ) - flat ).LengthSquared > maxDist2 )
				continue;

			mateBrain.HostTryInheritHerdFlee( threat, source );
		}
	}

	public static Vector3 ComputeHerdWanderBias( GameObject self, ThornsWildlifeSpeciesKind species )
	{
		if ( self is null || !self.IsValid() )
			return Vector3.Zero;

		var profile = ThornsAnimalBehaviorProfile.Get( species );
		if ( profile.HerdPreference < 0.35f )
			return Vector3.Zero;

		var flat = self.WorldPosition.WithZ( 0 );
		var innerR = HerdRadius * 0.55f;
		var innerR2 = innerR * innerR;
		Vector3 sum = Vector3.Zero;
		var count = 0;

		foreach ( var herdMate in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !herdMate.IsValid() )
				continue;

			var go = herdMate.GameObject;
			if ( go == self || !go.IsValid() )
				continue;

			var id = go.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( !id.IsValid() || id.Species != species || id.HostIsDead || id.HostIsTamed )
				continue;

			var dist2 = ( go.WorldPosition.WithZ( 0 ) - flat ).LengthSquared;
			if ( dist2 > innerR2 || dist2 < 64f )
				continue;

			sum += ( go.WorldPosition.WithZ( 0 ) - flat ).Normal;
			count++;
		}

		if ( count == 0 )
			return Vector3.Zero;

		return ( sum / count ).WithZ( 0 ).Normal * 0.35f;
	}
}
