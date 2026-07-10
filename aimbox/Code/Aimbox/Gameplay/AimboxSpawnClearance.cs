namespace Sandbox;

/// <summary>Validates player/bot spawn positions against arena geometry.</summary>
public static class AimboxSpawnClearance
{
	public static float MaxShiftRadius => 520f * AimboxArenaConfig.ActiveCombatScale;

	public static bool IsClear( Scene scene, GameObject ignore, Vector3 feetPosition, out Vector3 groundedFeet )
	{
		groundedFeet = SnapFeetToGround( scene, ignore, feetPosition );
		if ( !HasWalkableGround( scene, ignore, groundedFeet ) )
			return false;

		return !CapsuleIntersectsSolid( scene, ignore, groundedFeet );
	}

	public static Vector3 ResolveClearFeetPosition( Scene scene, GameObject ignore, Vector3 desired, float maxShiftRadius = 0f )
	{
		if ( maxShiftRadius <= 0f )
			maxShiftRadius = MaxShiftRadius;

		if ( IsClear( scene, ignore, desired, out var grounded ) )
			return grounded;

		var best = grounded;
		var bestShift = float.MaxValue;

		const int rings = 8;
		const int samplesPerRing = 12;
		for ( var ring = 1; ring <= rings; ring++ )
		{
			var radius = maxShiftRadius * ring / rings;
			for ( var sample = 0; sample < samplesPerRing; sample++ )
			{
				var angle = MathF.Tau * sample / samplesPerRing;
				var offset = new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
				if ( !IsClear( scene, ignore, desired + offset, out var candidate ) )
					continue;

				var shift = candidate.WithZ( 0 ).Distance( desired.WithZ( 0 ) );
				if ( shift >= bestShift )
					continue;

				bestShift = shift;
				best = candidate;
			}

			if ( bestShift < float.MaxValue )
				return best;
		}

		var center = AimboxSpawnResolve.GetArenaCenter( scene );
		var toCenter = center.WithZ( best.z ) - best;
		toCenter = toCenter.WithZ( 0 );
		if ( toCenter.LengthSquared > 1f )
		{
			var steps = 6;
			for ( var step = 1; step <= steps; step++ )
			{
				var probe = best + toCenter.Normal * (maxShiftRadius * step / steps);
				if ( IsClear( scene, ignore, probe, out var candidate ) )
					return candidate;
			}
		}

		Log.Warning( $"[Aimbox] Could not find clear spawn near {desired}; using best effort {best}." );
		return best;
	}

	public static IReadOnlyList<AimboxSpawnCandidate> FilterClear(
		Scene scene,
		GameObject ignore,
		IReadOnlyList<AimboxSpawnCandidate> candidates )
	{
		if ( candidates.Count <= 1 )
			return candidates;

		var clear = new List<AimboxSpawnCandidate>();
		foreach ( var candidate in candidates )
		{
			if ( IsClear( scene, ignore, candidate.Position, out _ ) )
				clear.Add( candidate );
		}

		return clear.Count > 0 ? clear : candidates;
	}

	static Vector3 SnapFeetToGround( Scene scene, GameObject ignore, Vector3 feetPosition )
	{
		if ( scene is null || !scene.IsValid() )
			return feetPosition;

		return ignore.IsValid()
			? AimboxCitizenMovementMotor.SnapToGround( scene, ignore, feetPosition )
			: SnapToGroundWithoutBody( scene, feetPosition );
	}

	static Vector3 SnapToGroundWithoutBody( Scene scene, Vector3 feetPosition )
	{
		var tr = scene.Trace.Ray(
				feetPosition + Vector3.Up * AimboxCitizenMovementMotor.GroundTraceUp,
				feetPosition + Vector3.Down * AimboxCitizenMovementMotor.GroundTraceDown )
			.Run();

		if ( tr.Hit && tr.Normal.z >= AimboxCitizenMovementMotor.MaxWalkableSlopeNormalZ )
			return feetPosition.WithZ( tr.HitPosition.z );

		var feetZ = AimboxGame.Instance?.GetSpawnFeetZ() ?? AimboxMapDesignRules.FloorWalkZ;
		return feetPosition.WithZ( feetZ );
	}

	static bool HasWalkableGround( Scene scene, GameObject ignore, Vector3 feetPosition )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		var trace = scene.Trace.Ray(
			feetPosition + Vector3.Up * AimboxCitizenMovementMotor.GroundTraceUp,
			feetPosition + Vector3.Down * AimboxCitizenMovementMotor.GroundTraceDown );

		if ( ignore.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignore );

		var tr = trace.Run();
		return tr.Hit && tr.Normal.z >= AimboxCitizenMovementMotor.MaxWalkableSlopeNormalZ;
	}

	static bool CapsuleIntersectsSolid( Scene scene, GameObject ignore, Vector3 feetPosition )
	{
		var capsule = new Capsule(
			feetPosition + AimboxHitboxes.CitizenCapsuleStart,
			feetPosition + AimboxHitboxes.CitizenCapsuleEnd,
			AimboxHitboxes.CitizenRadius );

		var start = feetPosition + Vector3.Up * 2f;
		var trace = scene.Trace.Capsule( capsule, start, start + Vector3.Up * 0.01f );
		if ( ignore.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignore );

		var tr = trace.Run();
		if ( tr.StartedSolid )
			return true;

		return tr.Hit && tr.Normal.z < AimboxCitizenMovementMotor.MaxWalkableSlopeNormalZ;
	}
}
