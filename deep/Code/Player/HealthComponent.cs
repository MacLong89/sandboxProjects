namespace Deep;

public sealed class HealthComponent : Component
{
	public float MaxHealth { get; private set; } = 55f;
	public float CurrentHealth { get; private set; } = 55f;
	public float Fraction => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;
	public bool IsDead => CurrentHealth <= 0f;

	private TimeUntil _iFrames;

	public void ResetToFull( float maxHealth )
	{
		MaxHealth = MathF.Max( 1f, maxHealth );
		CurrentHealth = MaxHealth;
	}

	public void ApplyDamage( float amount, string source = null )
	{
		if ( amount <= 0f || IsDead )
			return;

		var game = DeepGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
			return;

		// TimeUntil bool = elapsed. Skip while i-frames active.
		if ( !_iFrames )
			return;

		_iFrames = 0.45f;

		var sub = game.Vehicles?.ActiveSub;
		if ( sub is not null && sub.Occupied )
			amount = sub.AbsorbDamage( amount );

		if ( amount <= 0.01f )
			return;

		CurrentHealth = MathF.Max( 0f, CurrentHealth - amount );

		if ( CurrentHealth <= 0f )
		{
			var reason = source == "Pressure" ? DiveFailureReason.PressureDamage : DiveFailureReason.HealthDepleted;
			game.FailDive( reason );
		}
	}
}
