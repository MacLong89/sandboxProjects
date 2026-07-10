namespace Sandbox;

/// <summary>Wolf pack coordination — shared targets, confidence, flank offsets.</summary>
public static class ThornsAnimalPackCoordinator
{
	public const float PackRadius = 2400f;
	public const float FlankRadius = 140f;

	public static int CountPackMembers( GameObject self, ThornsWildlifeSpeciesKind species )
	{
		if ( self is null || !self.IsValid() )
			return 0;

		var flat = self.WorldPosition.WithZ( 0 );
		var packR2 = PackRadius * PackRadius;
		var count = 0;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() )
				continue;

			var go = brain.GameObject;
			if ( go == self || !go.IsValid() )
				continue;

			var id = go.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( !id.IsValid() || id.Species != species || id.HostIsDead || id.HostIsTamed )
				continue;

			if ( ( go.WorldPosition.WithZ( 0 ) - flat ).LengthSquared > packR2 )
				continue;

			count++;
		}

		return count;
	}

	public static int CountSpeciesNear( GameObject center, ThornsWildlifeSpeciesKind species, float radius )
	{
		if ( center is null || !center.IsValid() )
			return 0;

		var flat = center.WorldPosition.WithZ( 0 );
		var r2 = radius * radius;
		var count = 0;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() )
				continue;

			var go = brain.GameObject;
			if ( go == center || !go.IsValid() )
				continue;

			var id = go.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( !id.IsValid() || id.Species != species || id.HostIsDead || id.HostIsTamed )
				continue;

			if ( ( go.WorldPosition.WithZ( 0 ) - flat ).LengthSquared > r2 )
				continue;

			count++;
		}

		return count;
	}

	public static float PackConfidence( int packMembers, ThornsAnimalBehaviorProfile profile )
	{
		if ( profile.PackPreference < 0.01f )
			return 0f;

		var bonus = MathF.Min( 0.45f, packMembers * 0.12f );
		return MathX.Clamp( profile.Courage + bonus, 0f, 1f );
	}

	public static bool PackReadyToHunt( ThornsAnimalBehaviorProfile profile, int packMembers ) =>
		packMembers + 1 >= profile.MinPackMembersToHunt;

	public static void TryBroadcastPackHunt( GameObject hunter, GameObject prey )
	{
		if ( !Networking.IsHost || hunter is null || prey is null || !hunter.IsValid() || !prey.IsValid() )
			return;

		var hunterId = hunter.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( !hunterId.IsValid() )
			return;

		var profile = ThornsAnimalBehaviorProfile.Get( hunterId.Species );
		if ( profile.PackPreference < 0.5f )
			return;

		var flat = hunter.WorldPosition.WithZ( 0 );
		var packR2 = PackRadius * PackRadius;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() )
				continue;

			var go = brain.GameObject;
			if ( go == hunter || !go.IsValid() )
				continue;

			var mateId = go.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( !mateId.IsValid() || mateId.Species != hunterId.Species || mateId.HostIsDead || mateId.HostIsTamed )
				continue;

			if ( ( go.WorldPosition.WithZ( 0 ) - flat ).LengthSquared > packR2 )
				continue;

			brain.HostTryInheritPackHuntTarget( prey, hunter );
		}
	}

	public static Vector3 ComputeFlankGoal( GameObject self, GameObject prey, ThornsWildlifeSpeciesKind species )
	{
		if ( self is null || prey is null || !self.IsValid() || !prey.IsValid() )
			return prey?.WorldPosition.WithZ( 0 ) ?? Vector3.Zero;

		var preyFlat = prey.WorldPosition.WithZ( 0 );
		var selfFlat = self.WorldPosition.WithZ( 0 );
		var toPrey = preyFlat - selfFlat;
		if ( toPrey.LengthSquared < 1f )
			toPrey = Vector3.Random.WithZ( 0 ).Normal;

		var slot = HashCode.Combine( self.Id.GetHashCode(), prey.Id.GetHashCode() ) & 7;
		var angleRad = slot * ( MathF.PI * 2f / 8f );
		var offset = Rotation.FromYaw( angleRad.RadianToDegree() ).Forward.WithZ( 0 ) * FlankRadius;
		return preyFlat + offset;
	}
}
