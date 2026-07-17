namespace Offshore;

/// <summary>
/// Aim, charge, and parabolic hook flight. Does not own session state â€” FishingController does.
/// </summary>
public sealed class CastComponent : Component
{
	[Property] public float MinAimDegrees { get; set; } = OffshoreConstants.MinAimDegrees;
	[Property] public float MaxAimDegrees { get; set; } = OffshoreConstants.MaxAimDegrees;
	[Property] public float AimSpeedDegrees { get; set; } = OffshoreConstants.AimSpeedDegrees;
	[Property] public float ChargeRate { get; set; } = OffshoreConstants.ChargeRate;
	[Property] public float MinChargeToCast { get; set; } = OffshoreConstants.MinChargeToCast;
	[Property] public float MinCastDistance { get; set; } = OffshoreConstants.MinCastDistance;
	[Property] public float MaxCastDistance { get; set; } = OffshoreConstants.MaxCastDistance;
	[Property] public float CastFlightSeconds { get; set; } = OffshoreConstants.CastFlightSeconds;
	[Property] public float HookSubmerge { get; set; } = OffshoreConstants.HookSubmerge;

	public float AimDegrees { get; private set; } = OffshoreConstants.DefaultAimDegrees;
	public float Charge { get; private set; }
	public bool IsFlying { get; private set; }
	public Vector3 FlightStart { get; private set; }
	public Vector3 FlightEnd { get; private set; }
	public Vector3 FlightPeak { get; private set; }
	public float FlightProgress { get; private set; }

	private float _flightElapsed;
	private Action<Vector3> _onComplete;
	private Action _onTimeout;
	private float _timeout = OffshoreConstants.CastTimeoutSeconds;

	public void ApplyBalance( BalanceConfig balance )
	{
		if ( balance is null )
			return;

		MinAimDegrees = balance.MinAimDegrees;
		MaxAimDegrees = balance.MaxAimDegrees;
		AimSpeedDegrees = balance.AimSpeedDegrees;
		ChargeRate = balance.ChargeRate;
		MinChargeToCast = balance.MinChargeToCast;
		MinCastDistance = balance.MinCastDistance;
		MaxCastDistance = balance.MaxCastDistance;
		CastFlightSeconds = balance.CastFlightSeconds;
		HookSubmerge = balance.HookSubmerge;
		AimDegrees = balance.DefaultAimDegrees;
		_timeout = balance.CastTimeoutSeconds;
	}

	public void ResetCharge()
	{
		Charge = 0f;
	}

	public void AdjustAim( float deltaDegrees )
	{
		AimDegrees = Math.Clamp( AimDegrees + deltaDegrees, MinAimDegrees, MaxAimDegrees );
	}

	public void SetAimFromAnalog( float analogX )
	{
		if ( MathF.Abs( analogX ) < 0.05f )
			return;

		AdjustAim( analogX * AimSpeedDegrees * Time.Delta );
	}

	public void AddCharge( float dt )
	{
		Charge = Math.Clamp( Charge + ChargeRate * dt, 0f, 1f );
	}

	public bool TryBeginCast( Vector3 tip, WaterVolumeComponent water, Action<Vector3> onComplete, Action onTimeout )
	{
		if ( IsFlying || Charge < MinChargeToCast )
			return false;

		var rangeMul = BoatSystem.ActiveCastRangeMul( OffshoreGameController.Instance );
		var distance = MathX.Lerp( MinCastDistance, MaxCastDistance, Charge ) * rangeMul;
		var aimRad = AimDegrees * MathF.PI / 180f;
		// Do not clamp X here â€” landing validation decides water hit vs miss.
		var endX = tip.x + distance * MathF.Cos( aimRad );

		// Rest below the angler (dock deck or boat waterline).
		var deckZ = DeckZ();
		var restZ = deckZ - OffshoreConstants.BobberBelowPlayerZ;
		var end = new Vector3( endX, tip.y, restZ );
		var peakZ = tip.z + distance * OffshoreConstants.ArcPeakScale * MathF.Max( 0.2f, MathF.Sin( MathF.Max( aimRad, 0.15f ) ) );
		var peak = new Vector3( (tip.x + end.x) * 0.5f, tip.y, peakZ );

		FlightStart = tip;
		FlightEnd = end;
		FlightPeak = peak;
		FlightProgress = 0f;
		_flightElapsed = 0f;
		_timeout = OffshoreGameController.Instance?.Balance?.CastTimeoutSeconds
			?? OffshoreConstants.CastTimeoutSeconds;
		_onComplete = onComplete;
		_onTimeout = onTimeout;
		IsFlying = true;
		Charge = 0f;
		return true;
	}

	public void CancelFlight()
	{
		IsFlying = false;
		FlightProgress = 0f;
		_onComplete = null;
		_onTimeout = null;
	}

	public Vector3 EvaluateFlight( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		var a = Vector3.Lerp( FlightStart, FlightPeak, t );
		var b = Vector3.Lerp( FlightPeak, FlightEnd, t );
		return Vector3.Lerp( a, b, t );
	}

	public Vector3 PreviewLanding( Vector3 tip, WaterVolumeComponent water, float chargeOverride = -1f )
	{
		var charge = chargeOverride >= 0f ? chargeOverride : Math.Max( Charge, 0.15f );
		var rangeMul = BoatSystem.ActiveCastRangeMul( OffshoreGameController.Instance );
		var distance = MathX.Lerp( MinCastDistance, MaxCastDistance, charge ) * rangeMul;
		var aimRad = AimDegrees * MathF.PI / 180f;
		var endX = tip.x + distance * MathF.Cos( aimRad );
		endX = water?.ClampX( endX ) ?? endX;
		var restZ = DeckZ() - OffshoreConstants.BobberBelowPlayerZ;
		return new Vector3( endX, tip.y, restZ );
	}

	private static float DeckZ()
	{
		var player = OffshoreGameController.Instance?.Player;
		if ( player is not null && player.Mode == AnglerController.LocomotionMode.InBoat )
			return OffshoreConstants.BoatMooringZ;
		return OffshoreConstants.PlayerStartZ;
	}

	public void TickFlight( float dt, HookComponent hook )
	{
		if ( !IsFlying )
			return;

		_flightElapsed += dt;
		_timeout -= dt;

		var duration = Math.Max( 0.05f, CastFlightSeconds );
		FlightProgress = Math.Clamp( _flightElapsed / duration, 0f, 1f );
		var pos = EvaluateFlight( FlightProgress );
		hook?.SetPosition( pos );

		if ( FlightProgress >= 1f )
		{
			IsFlying = false;
			var complete = _onComplete;
			_onComplete = null;
			_onTimeout = null;
			complete?.Invoke( pos );
			return;
		}

		if ( _timeout <= 0f )
		{
			IsFlying = false;
			_onComplete = null;
			var timeout = _onTimeout;
			_onTimeout = null;
			timeout?.Invoke();
		}
	}
}
