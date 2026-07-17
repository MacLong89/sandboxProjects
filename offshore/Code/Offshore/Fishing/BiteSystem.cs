namespace Offshore;

/// <summary>Wait-for-bite timer and reaction window. Does not resolve catches.</summary>
public sealed class BiteSystem
{
	public FishEncounter Encounter { get; private set; }
	public float BiteDelayRemaining { get; private set; }
	public float ReactionRemaining { get; private set; }
	public bool HasBite { get; private set; }
	public bool MissedBite { get; private set; }
	public bool Hooked { get; private set; }

	private bool _resolved;

	public void Begin( FishEncounter encounter, BalanceConfig balance, float biteSpeedMultiplier = 1f )
	{
		Encounter = encounter;
		_resolved = false;
		HasBite = false;
		MissedBite = false;
		Hooked = false;
		ReactionRemaining = 0f;

		var biteSpeed = MathF.Max( 0.25f, encounter?.Definition?.BiteSpeed ?? 1f );
		var caution = Math.Clamp( encounter?.Definition?.BiteCaution ?? 0.2f, 0f, 1f );
		var min = balance.MinBiteSeconds / biteSpeed;
		var max = balance.MaxBiteSeconds / biteSpeed;
		min = MathX.Lerp( min, max, caution * 0.35f );
		BiteDelayRemaining = Game.Random.Float( min, MathF.Max( min + 0.1f, max ) );
		BiteDelayRemaining /= MathF.Max( 0.5f, biteSpeedMultiplier );
	}

	public void Clear()
	{
		Encounter = null;
		HasBite = false;
		MissedBite = false;
		Hooked = false;
		_resolved = false;
		BiteDelayRemaining = 0f;
		ReactionRemaining = 0f;
	}

	/// <summary>Returns true when waiting ends and bite window opens.</summary>
	public bool TickWaiting( float dt )
	{
		if ( _resolved || HasBite )
			return false;

		BiteDelayRemaining -= dt;
		if ( BiteDelayRemaining > 0f )
			return false;

		HasBite = true;
		var window = OffshoreGameController.Instance?.Balance?.BiteReactionSeconds ?? 1.25f;
		var caution = Math.Clamp( Encounter?.Definition?.BiteCaution ?? 0.2f, 0f, 1f );
		ReactionRemaining = MathF.Max( 0.55f, window * (1f - caution * 0.25f) );
		return true;
	}

	/// <summary>Returns true if the reaction window expired without a hook.</summary>
	public bool TickBiteWindow( float dt, bool tryHook )
	{
		if ( _resolved || !HasBite )
			return false;

		if ( tryHook )
		{
			_resolved = true;
			Hooked = true;
			MissedBite = false;
			return false;
		}

		ReactionRemaining -= dt;
		if ( ReactionRemaining > 0f )
			return false;

		_resolved = true;
		Hooked = false;
		MissedBite = true;
		return true;
	}
}
