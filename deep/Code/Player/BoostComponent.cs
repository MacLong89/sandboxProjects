namespace Deep;

/// <summary>Shift swim boost energy. Concept HUD "Boost" bar.</summary>
public sealed class BoostComponent : Component
{
	public float MaxEnergy { get; private set; } = 40f;
	public float CurrentEnergy { get; private set; } = 40f;
	public float Fraction => MaxEnergy <= 0f ? 0f : CurrentEnergy / MaxEnergy;
	public bool IsBoosting { get; private set; }
	public float SpeedMultiplier => IsBoosting ? 1.55f : 1f;

	public void ResetToFull( float maxEnergy = 40f )
	{
		MaxEnergy = MathF.Max( 1f, maxEnergy );
		CurrentEnergy = MaxEnergy;
		IsBoosting = false;
	}

	public void RestoreFraction( float fraction )
	{
		if ( fraction <= 0f ) return;
		CurrentEnergy = MathF.Min( MaxEnergy, CurrentEnergy + MaxEnergy * fraction );
	}

	protected override void OnUpdate()
	{
		var game = DeepGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
		{
			IsBoosting = false;
			return;
		}

		var want = Input.Down( "Run" ); // Shift
		var can = CurrentEnergy > 1f && want;

		var balance = game.Balance;
		var drain = balance.BoostDrainPerSecond;
		var regen = balance.BoostRegenPerSecond;

		if ( can )
		{
			IsBoosting = true;
			CurrentEnergy = MathF.Max( 0f, CurrentEnergy - drain * Time.Delta );
			if ( CurrentEnergy <= 0f )
				IsBoosting = false;
		}
		else
		{
			IsBoosting = false;
			CurrentEnergy = MathF.Min( MaxEnergy, CurrentEnergy + regen * Time.Delta );
		}
	}
}
