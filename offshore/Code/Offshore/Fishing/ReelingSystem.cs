namespace Offshore;

public enum ReelResolveResult
{
	None,
	Caught,
	Escaped
}

/// <summary>Tension-based reeling. Catch and escape are mutually exclusive via latch.</summary>
public sealed class ReelingSystem
{
	public FishEncounter Encounter { get; private set; }
	public bool IsActive { get; private set; }

	private bool _resolved;
	private BalanceConfig _balance;

	public void Begin( FishEncounter encounter, BalanceConfig balance )
	{
		Encounter = encounter;
		_balance = balance ?? BalanceConfig.Defaults;
		IsActive = encounter is not null;
		_resolved = false;

		if ( encounter is null )
			return;

		encounter.CatchProgress = 0f;
		encounter.LineTension = 0.15f;
		encounter.Direction = Game.Random.Float() > 0.5f ? 1f : -1f;
		encounter.DirectionTimer = Game.Random.Float( 0.45f, 1.1f );
		encounter.Stamina = encounter.MaxStamina;
	}

	public void Clear()
	{
		Encounter = null;
		IsActive = false;
		_resolved = false;
	}

	public ReelResolveResult Tick( float dt, bool reeling, float steer )
	{
		if ( !IsActive || _resolved || Encounter is null )
			return ReelResolveResult.None;

		var b = _balance;
		var fish = Encounter;

		fish.DirectionTimer -= dt;
		if ( fish.DirectionTimer <= 0f )
		{
			fish.Direction *= -1f;
			fish.DirectionTimer = Game.Random.Float( 0.4f, 1.2f ) / MathF.Max( 0.4f, fish.Speed );
		}

		var fight = fish.Strength * (0.55f + 0.45f * (fish.Stamina / MathF.Max( 0.01f, fish.MaxStamina )));
		var steerAlign = Math.Clamp( 1f - MathF.Abs( steer - fish.Direction ) * 0.35f, 0.55f, 1f );

		if ( reeling )
		{
			var progressGain = b.ReelProgressPerSecond * (1.15f - 0.35f * fight) * steerAlign;
			fish.CatchProgress = Math.Clamp( fish.CatchProgress + progressGain * dt, 0f, 1f );
			fish.LineTension = Math.Clamp( fish.LineTension + (b.TensionGainPerSecond + fight * 0.55f) * dt, 0f, 1.25f );
			fish.Stamina = Math.Clamp( fish.Stamina - b.FishStaminaDrainWhileReeling * dt, 0f, fish.MaxStamina );
		}
		else
		{
			fish.LineTension = Math.Clamp( fish.LineTension - b.TensionReleasePerSecond * dt, 0f, 1.25f );
			fish.Stamina = Math.Clamp( fish.Stamina + b.FishStaminaRecoverPerSecond * fight * dt, 0f, fish.MaxStamina );
			// Fish pulls progress back slightly when not reeling.
			fish.CatchProgress = Math.Clamp( fish.CatchProgress - b.FishProgressStealPerSecond * fight * 0.35f * dt, 0f, 1f );
		}

		// Snap check near max tension.
		if ( fish.LineTension >= b.LineBreakTension )
		{
			var snapChance = (fish.LineTension - b.LineBreakTension) * 2.5f + fish.EscapeDifficulty * 0.15f;
			if ( Game.Random.Float() < snapChance * dt * 4f )
				return Resolve( ReelResolveResult.Escaped );
		}

		if ( fish.CatchProgress >= 1f )
		{
			// Same-frame tension-at-limit vs complete: catch wins if not already breaking hard.
			if ( fish.LineTension >= b.LineBreakTension + 0.12f )
				return Resolve( ReelResolveResult.Escaped );

			return Resolve( ReelResolveResult.Caught );
		}

		return ReelResolveResult.None;
	}

	private ReelResolveResult Resolve( ReelResolveResult result )
	{
		if ( _resolved )
			return ReelResolveResult.None;

		_resolved = true;
		IsActive = false;
		return result;
	}
}
