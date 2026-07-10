namespace FinalOutpost;

/// <summary>
/// Night-time kill combo. Chaining kills quickly ramps a scrap multiplier that decays if you go too
/// long without a kill. The "keep the streak alive" tension is a classic arcade addictiveness hook
/// and makes active play during the night feel rewarding versus just watching towers.
/// </summary>
public sealed class ComboSystem : Component
{
	public static ComboSystem Instance { get; private set; }

	public int Streak { get; private set; }
	public float Multiplier { get; private set; } = 1f;

	private float _expire;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	/// <summary>0..1 amount of the combo window remaining (drives the decay bar in the HUD).</summary>
	public float WindowFraction => Streak <= 0 ? 0f : Math.Clamp( _expire / GameConstants.ComboWindowSeconds, 0f, 1f );

	public void RegisterKill()
	{
		Streak++;
		_expire = GameConstants.ComboWindowSeconds;
		Multiplier = MathF.Min( GameConstants.ComboMaxMult, 1f + Streak * GameConstants.ComboAddPerKill );
	}

	public void ResetCombo()
	{
		Streak = 0;
		Multiplier = 1f;
		_expire = 0f;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Night )
		{
			if ( Streak > 0 ) ResetCombo();
			return;
		}

		if ( Streak <= 0 ) return;

		_expire -= Time.Delta;
		if ( _expire <= 0f )
			ResetCombo();
	}
}
