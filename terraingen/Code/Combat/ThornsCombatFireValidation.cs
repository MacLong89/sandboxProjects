namespace Terraingen.Combat;

using Terraingen.Player;

/// <summary>Host-side validation for client-submitted fire rays.</summary>
public static class ThornsCombatFireValidation
{
	const float MaxOriginErrorInches = 64f;
	const float MaxDirectionErrorDegrees = 10f;
	const float DefaultEyeHeightInches = 64f;

	public static bool TryResolveAuthoritativeShot(
		GameObject pawn,
		Vector3 clientOrigin,
		Vector3 clientDirection,
		float maxRange,
		out Vector3 origin,
		out Vector3 direction )
	{
		origin = default;
		direction = default;

		if ( !pawn.IsValid() || clientDirection.Normal.Length < 0.95f )
		{
			ThornsCombatHitscanDebug.LogFireValidation( pawn, clientOrigin, clientDirection, default, default, false, "invalid-pawn-or-dir" );
			return false;
		}

		if ( !TryResolveHostAimRay( pawn, out var serverOrigin, out var serverDir ) )
		{
			ThornsCombatHitscanDebug.LogFireValidation( pawn, clientOrigin, clientDirection, default, default, false, "no-aim-ray" );
			return false;
		}

		if ( Vector3.DistanceBetween( clientOrigin, serverOrigin ) > MaxOriginErrorInches )
		{
			ThornsCombatHitscanDebug.LogFireValidation( pawn, clientOrigin, clientDirection, serverOrigin, serverDir, false, "origin-error" );
			return false;
		}

		var serverNormal = serverDir.Normal;
		var clientNormal = clientDirection.Normal;
		var dot = Math.Clamp( Vector3.Dot( serverNormal, clientNormal ), -1f, 1f );
		var angleDeg = MathF.Acos( dot ) * (180f / MathF.PI );

		if ( angleDeg > MaxDirectionErrorDegrees )
		{
			ThornsCombatHitscanDebug.LogFireValidation( pawn, clientOrigin, clientDirection, serverOrigin, serverDir, false, "angle-error" );
			return false;
		}

		origin = serverOrigin;
		direction = serverNormal;
		_ = maxRange;

		ThornsCombatHitscanDebug.LogFireValidation( pawn, clientOrigin, clientDirection, serverOrigin, serverDir, true );
		return true;
	}

	/// <summary>
	/// Resolves aim on the host machine. Remote pawns never use the host's camera/mouse —
	/// only synced <see cref="PlayerController.EyeAngles"/> and pawn eye position.
	/// </summary>
	static bool TryResolveHostAimRay( GameObject pawn, out Vector3 origin, out Vector3 direction )
	{
		origin = default;
		direction = default;

		if ( !pawn.IsValid() )
			return false;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( pawn )
		     && ThornsSceneObserver.TryResolveLocalAimRay( pawn, out origin, out direction, useScreenCenter: true ) )
			return true;

		var controller = pawn.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = pawn.WorldPosition + Vector3.Up * DefaultEyeHeightInches;
		direction = controller.EyeAngles.ToRotation().Forward.Normal;
		return direction.Length >= 0.95f;
	}
}
