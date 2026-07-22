namespace Offshore;

/// <summary>
/// Stardew-style fishing: charge cast → wait → hook → vertical bar minigame.
/// Hold to raise the green bar (with momentum); release to fall. Keep the fish inside to fill catch progress.
/// </summary>
public sealed class FishingController
{
	public float CastPower { get; private set; }
	public bool Charging { get; private set; }
	/// <summary>0 = none, else elapsed seconds of the forward rod swing after release.</summary>
	public float CastSwingT { get; private set; }
	public bool Swinging => CastSwingT > 0f;
	public float LureDistance { get; private set; }
	public float LureDepth { get; private set; }
	public float TargetDepth { get; set; } = 12f;
	public float BiteTimer { get; private set; }
	public float HookWindow { get; private set; }
	public bool BiteReady { get; private set; }
	public FishDefinition PendingFish { get; private set; }
	public CaughtFish LastCatch { get; private set; }
	public string Status { get; private set; }

	// Stardew bar minigame (0 = bottom, 1 = top)
	public float BarBottom { get; private set; }
	public float BarHeight { get; private set; } = 0.18f;
	public float FishY { get; private set; } = 0.5f;
	public float CatchProgress { get; private set; } = 0.3f;
	public bool BarOnFish { get; private set; }

	/// <summary>Alias for HUD/systems that still read reel progress.</summary>
	public float ReelProgress => CatchProgress;
	public float Tension => 0f;
	public float FishStamina => PendingFish is null ? 0f : PendingFish.Stamina * (1f - CatchProgress);
	public float PullDir { get; private set; } = 1f;

	float _charge;
	float _baitLife;
	float _barVel;
	float _fishVel;
	float _fishTarget;
	float _fishPause;
	float _dartClock;
	float _fightClock;
	readonly Random _rng = new();

	const float CastSwingDuration = 0.38f;

	public void BeginCharge()
	{
		Charging = true;
		CastSwingT = 0f;
		_charge = 0.15f;
		CastPower = _charge;
		Status = "Charging cast…";
	}

	public void UpdateCharge( float dt )
	{
		if ( !Charging ) return;
		_charge = Math.Min( 1f, _charge + dt * 0.85f );
		CastPower = _charge;
	}

	/// <summary>Advances the forward swing after release. Returns true when the swing finishes.</summary>
	public bool TickCastSwing( float dt )
	{
		if ( CastSwingT <= 0f ) return false;
		CastSwingT += dt;
		if ( CastSwingT < CastSwingDuration ) return false;
		CastSwingT = 0f;
		return true;
	}

	public void ReleaseCast( InventoryService inv, BoatDefinition boat, WeatherService weather, bool fromDock )
	{
		Charging = false;
		CastSwingT = 0.001f;
		if ( inv.BaitCount <= 0 )
		{
			CastSwingT = 0f;
			Status = "No bait equipped.";
			return;
		}

		var rod = inv.Rod;
		var line = inv.Line;
		var accuracy = rod.CastAccuracy * (boat?.CastAccuracy ?? 0.8f) * Math.Clamp( 1.1f - weather.Wind * 0.35f - weather.Rain * 0.2f, 0.45f, 1.2f );
		var distMul = rod.CastDistance * line.CastDistance * (boat?.CastDistanceMod ?? 0.85f) * (0.55f + CastPower * 0.7f);
		LureDistance = (fromDock ? 40f : 70f) * distMul * (0.75f + accuracy * 0.25f);
		LureDepth = 0f;
		TargetDepth = Math.Clamp( TargetDepth, 3f, boat?.MaxDepth ?? 25f );
		BiteReady = false;
		PendingFish = null;
		// Stardew-like wait: several seconds, bait attraction still helps a bit.
		var attract = Math.Clamp( inv.Bait.AttractionSpeed, 0.55f, 1.5f );
		BiteTimer = 5.5f + (float)_rng.NextDouble() * 9f; // ~5.5–14.5s base
		BiteTimer /= attract;
		BiteTimer *= Math.Clamp( 1.1f - accuracy * 0.15f, 0.85f, 1.2f );
		_baitLife = inv.Bait.Durability;
		Status = "Line settling…";
	}

	public bool TickWaiting( float dt, EcologyContext ctx, InventoryService inv, out string eventMsg )
	{
		eventMsg = null;
		LureDepth = LureDepth.LerpTo( TargetDepth, dt * (1.2f + inv.Reel.RetrievalDepth * 0.4f) );
		// Mild speed-ups only — hotspots / bait shouldn't collapse the wait to a second.
		BiteTimer -= dt * (0.7f + ctx.HotspotStrength * 0.25f + inv.Bait.AttractionSpeed * 0.08f);
		_baitLife -= dt * 0.02f;

		if ( BiteTimer > 0f )
			return false;

		PendingFish = FishEcologyService.RollSpecies( ctx, _rng );
		var steal = PendingFish.Behavior == FishBehavior.Skittish && _rng.NextDouble() < 0.15;
		if ( steal || _baitLife <= 0f )
		{
			inv.ConsumeBait();
			eventMsg = steal ? "A fish stole your bait!" : "Your bait fell apart.";
			Status = eventMsg;
			return true;
		}

		BiteReady = true;
		HookWindow = inv.Hook.HookWindow * (1.1f - PendingFish.HookDifficulty * 0.4f);
		Status = "!";
		eventMsg = "bite";
		return false;
	}

	public bool TickHookWindow( float dt )
	{
		if ( !BiteReady ) return false;
		HookWindow -= dt;
		return HookWindow <= 0f;
	}

	public bool TrySetHook( InventoryService inv, out string fail )
	{
		fail = null;
		if ( !BiteReady || PendingFish is null )
		{
			fail = "No bite.";
			return false;
		}

		var hook = inv.Hook;
		var chance = hook.HookSuccess * (1.1f - PendingFish.HookDifficulty * 0.5f);
		if ( PendingFish.MaxWeight > hook.MaxFishSize )
			chance *= 0.55f;
		if ( PendingFish.Behavior == FishBehavior.Aggressive )
			chance *= 0.85f + hook.AggressiveRetention * 0.3f;

		BiteReady = false;
		if ( _rng.NextDouble() > chance )
		{
			inv.ConsumeBait();
			fail = "The fish got away!";
			Status = fail;
			PendingFish = null;
			return false;
		}

		BeginMinigame( inv );
		Status = "Keep the green bar on the fish!";
		return true;
	}

	void BeginMinigame( InventoryService inv )
	{
		var fish = PendingFish;
		var gear = (inv.Rod.CastAccuracy + inv.Reel.ReelSpeed * 0.15f + inv.Hook.HookSuccess * 0.2f) / 2.2f;
		BarHeight = Math.Clamp( 0.16f + gear * 0.1f - fish.HookDifficulty * 0.04f, 0.12f, 0.28f );
		// Start on the fish so the opening beat isn't an instant miss.
		FishY = 0.42f + (float)_rng.NextDouble() * 0.16f;
		BarBottom = Math.Clamp( FishY - BarHeight * 0.5f, 0f, 1f - BarHeight );
		_barVel = 0f;
		_fishVel = 0f;
		_fishTarget = FishY;
		_fishPause = 0.4f; // brief calm, then full fight
		_dartClock = 0f;
		_fightClock = 0f;
		CatchProgress = 0.32f;
		BarOnFish = true;
		PullDir = 1f;
	}

	public enum FightResult { Ongoing, Caught, Escaped, LineBroke }

	public FightResult TickReel( float dt, bool reeling, InventoryService inv, BoatDefinition boat, WeatherService weather )
	{
		if ( PendingFish is null ) return FightResult.Escaped;

		var fish = PendingFish;
		_fightClock += dt;
		TickFishMotion( dt, fish, weather );
		TickBarMotion( dt, reeling );
		UpdateOverlapAndProgress( dt, inv, fish );

		if ( CatchProgress <= 0f )
		{
			inv.ConsumeBait();
			Status = "The fish escaped.";
			PendingFish = null;
			return FightResult.Escaped;
		}

		if ( CatchProgress >= 1f )
		{
			inv.ConsumeBait();
			LastCatch = FishEcologyService.CreateCatch( fish, _rng );
			Status = "Got one!";
			PendingFish = null;
			return FightResult.Caught;
		}

		Status = BarOnFish ? "Reeling…" : "Fish slipping…";
		return FightResult.Ongoing;
	}

	void TickBarMotion( float dt, bool holding )
	{
		const float gravity = 2.35f;
		const float accel = 4.6f;
		const float maxVel = 1.7f;
		const float bounce = 0.32f;

		if ( holding )
			_barVel += accel * dt;
		else
			_barVel -= gravity * dt;

		_barVel = Math.Clamp( _barVel, -maxVel, maxVel );
		BarBottom += _barVel * dt;

		var maxBottom = 1f - BarHeight;
		if ( BarBottom < 0f )
		{
			BarBottom = 0f;
			_barVel = Math.Abs( _barVel ) * bounce;
			if ( _barVel < 0.12f ) _barVel = 0f;
		}
		else if ( BarBottom > maxBottom )
		{
			BarBottom = maxBottom;
			_barVel = -Math.Abs( _barVel ) * bounce;
		}
	}

	void TickFishMotion( float dt, FishDefinition fish, WeatherService weather )
	{
		_dartClock += dt;
		_fishPause -= dt;

		var difficulty = Math.Clamp( fish.Aggression * 0.55f + fish.HookDifficulty * 0.35f + fish.PullForce / 180f, 0.2f, 1.1f );
		// Short opening buffer only — after ~0.9s the fish fights at full strength.
		if ( _fightClock < 0.9f )
			difficulty *= 0.45f;

		var dartChance = (0.22f + difficulty * 1.05f) * dt;
		if ( weather.WaveIntensity > 0.5f )
			dartChance *= 1.15f;

		if ( _fishPause <= 0f && _rng.NextDouble() < dartChance )
		{
			var pattern = fish.FightPattern ?? "steady";
			var span = pattern switch
			{
				"dart" or "slash" or "burst" or "zigzag" => 0.32f + difficulty * 0.35f,
				"dive" or "deep_run" => 0.38f + difficulty * 0.3f,
				"jump" => 0.34f + difficulty * 0.3f,
				"steady" or "glide" => 0.16f + difficulty * 0.2f,
				_ => 0.24f + difficulty * 0.28f
			};
			_fishTarget = Math.Clamp( FishY + ((float)_rng.NextDouble() * 2f - 1f) * span, 0.08f, 0.92f );
			PullDir = Math.Sign( _fishTarget - FishY );
			if ( PullDir == 0 ) PullDir = 1f;
			_fishPause = 0.18f + (float)_rng.NextDouble() * (0.45f - difficulty * 0.12f);
		}

		var speed = 0.45f + difficulty * 1.15f + fish.PullForce * 0.0025f;
		var err = _fishTarget - FishY;
		_fishVel = _fishVel.LerpTo( Math.Sign( err ) * Math.Min( Math.Abs( err ) * 3.4f, speed ), dt * 5.5f );
		FishY = Math.Clamp( FishY + _fishVel * dt, 0.05f, 0.95f );

		if ( _dartClock > 2.4f && _fightClock > 0.9f && _rng.NextDouble() < dt * 0.22f )
		{
			_fishTarget = _rng.NextDouble() < 0.5 ? 0.08f + (float)_rng.NextDouble() * 0.12f : 0.78f + (float)_rng.NextDouble() * 0.12f;
			_dartClock = 0f;
		}
	}

	void UpdateOverlapAndProgress( float dt, InventoryService inv, FishDefinition fish )
	{
		var barTop = BarBottom + BarHeight;
		BarOnFish = FishY >= BarBottom && FishY <= barTop;

		var fill = 0.2f + inv.Reel.ReelSpeed * 0.06f;
		var drain = 0.28f + fish.Aggression * 0.32f + fish.HookDifficulty * 0.22f;
		drain *= Math.Clamp( 1.1f - inv.Hook.HookSuccess * 0.18f, 0.75f, 1.15f );

		// Buffer only: ~0.85s with no escape drain so the fight can start. Then full drain.
		const float graceSeconds = 0.85f;
		if ( _fightClock < graceSeconds )
			drain = 0f;

		if ( BarOnFish )
			CatchProgress = Math.Min( 1f, CatchProgress + fill * dt );
		else
			CatchProgress = Math.Max( 0f, CatchProgress - drain * dt );
	}

	public void Cancel()
	{
		Charging = false;
		CastSwingT = 0f;
		BiteReady = false;
		PendingFish = null;
		CastPower = 0;
		CatchProgress = 0;
		Status = null;
	}

	public void AdjustDepth( float delta )
	{
		TargetDepth = Math.Clamp( TargetDepth + delta, 2f, 300f );
	}
}
