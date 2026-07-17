namespace DeepDive;

/// <summary>
/// Oxygen drains only while diving, underwater, and not paused.
/// Depth increases consumption via <see cref="BalanceConfig.OxygenUseMultiplierAtDepth"/>.
/// </summary>
public sealed class OxygenComponent : Component
{
	public float MaxOxygen { get; private set; } = 28f;
	public float CurrentOxygen { get; private set; } = 28f;
	public float Fraction => MaxOxygen <= 0f ? 0f : CurrentOxygen / MaxOxygen;
	public bool IsLow
	{
		get
		{
			var low = DeepDiveGame.Instance?.Balance.LowOxygenFraction ?? BalanceConfig.Defaults.LowOxygenFraction;
			return Fraction <= low && Fraction > 0f;
		}
	}
	public bool IsCritical
	{
		get
		{
			var crit = DeepDiveGame.Instance?.Balance.CriticalOxygenFraction ?? BalanceConfig.Defaults.CriticalOxygenFraction;
			return Fraction <= crit && Fraction > 0f;
		}
	}
	public bool IsEmpty => CurrentOxygen <= 0f;

	private bool _warnedLow;
	private bool _warnedCritical;

	public void ResetToFull()
	{
		var max = DeepDiveGame.Instance?.Balance.MaxOxygenSeconds ?? BalanceConfig.Defaults.MaxOxygenSeconds;
		MaxOxygen = max;
		CurrentOxygen = MaxOxygen;
		_warnedLow = false;
		_warnedCritical = false;
	}

	public void SetMaxOxygen( float seconds )
	{
		MaxOxygen = MathF.Max( 1f, seconds );
		CurrentOxygen = MathF.Min( CurrentOxygen, MaxOxygen );
	}

	public void RestoreFraction( float fraction )
	{
		if ( fraction <= 0f ) return;
		CurrentOxygen = MathF.Min( MaxOxygen, CurrentOxygen + MaxOxygen * fraction );
	}

	protected override void OnUpdate()
	{
		var game = DeepDiveGame.Instance;
		if ( game is null )
			return;

		// Surface refills (not drained at SurfaceIdle).
		if ( game.Phase == GamePhase.SurfaceIdle )
		{
			if ( CurrentOxygen < MaxOxygen )
				CurrentOxygen = MaxOxygen;
			return;
		}

		if ( !game.State.IsDivingActive )
			return;

		var diver = game.Diver;
		if ( diver is null || !diver.IsUnderwater )
			return;

		var balance = game.Balance;
		var mult = balance.OxygenUseMultiplierAtDepth( diver.CurrentDepthMeters );
		if ( game.Boost?.IsBoosting == true )
			mult *= 1.35f;
		if ( diver.IsInVehicle )
			mult *= 0.45f;
		var used = Time.Delta * mult;
		CurrentOxygen = MathF.Max( 0f, CurrentOxygen - used );
		game.Run.AddOxygenUsed( used );

		if ( !_warnedCritical && IsCritical )
		{
			_warnedCritical = true;
			_warnedLow = true;
			game.ShowMessage( "OXYGEN CRITICAL!", 2.5f );
		}
		else if ( !_warnedLow && IsLow )
		{
			_warnedLow = true;
			game.ShowMessage( "OXYGEN LOW!", 2.5f );
		}

		if ( CurrentOxygen <= 0f )
			game.FailDive( DiveFailureReason.OxygenDepleted );
	}
}
