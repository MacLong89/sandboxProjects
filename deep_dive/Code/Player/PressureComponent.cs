namespace DeepDive;

public sealed class PressureComponent : Component
{
	public bool IsWarning { get; private set; }
	public bool IsDamaging { get; private set; }
	public float OverDepthMeters { get; private set; }

	protected override void OnUpdate()
	{
		var game = DeepDiveGame.Instance;
		if ( game is null )
		{
			IsWarning = false;
			IsDamaging = false;
			return;
		}

		if ( !game.State.IsDivingActive )
		{
			IsWarning = false;
			IsDamaging = false;
			OverDepthMeters = 0f;
			return;
		}

		var diver = game.Diver;
		var health = game.Health;
		if ( diver is null || health is null )
			return;

		var balance = game.Balance;
		var depth = diver.CurrentDepthMeters;
		var safe = balance.SafeDepthMeters;
		var warnAt = MathF.Max( 0f, safe - balance.PressureWarnMarginMeters );

		IsWarning = depth >= warnAt && depth < safe;
		IsDamaging = depth >= safe;
		OverDepthMeters = MathF.Max( 0f, depth - safe );

		if ( IsDamaging )
		{
			var dmg = balance.PressureDamagePerSecond * Time.Delta;
			health.ApplyDamage( dmg, "Pressure" );
			game.Run.AddDamage( dmg );
		}
	}
}
