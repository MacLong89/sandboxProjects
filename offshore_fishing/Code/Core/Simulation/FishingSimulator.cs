namespace OffshoreFishing.Core;

public sealed class FishingSimulator
{
	private readonly GameContent _content;
	private readonly List<IDomainEvent> _events = new();

	public FishingSimulator( GameContent content )
	{
		_content = content;
	}

	public IReadOnlyList<IDomainEvent> DrainEvents()
	{
		var copy = _events.ToList();
		_events.Clear();
		return copy;
	}

	public void BeginAim( GameState state )
	{
		if ( state.Mode != GameMode.Fishing && state.Mode != GameMode.Travel ) return;
		state.Mode = GameMode.Fishing;
		var f = state.Fishing;
		if ( f.Phase is FishingPhase.Idle or FishingPhase.Failed or FishingPhase.Landing )
		{
			f.Phase = FishingPhase.Aiming;
			f.CastCharge = 0f;
			f.StatusText = "Hold to charge cast";
			EmitPhase( f );
		}
	}

	public void SetAim( GameState state, float aimAngle )
	{
		if ( state.Fishing.Phase != FishingPhase.Aiming ) return;
		state.Fishing.AimAngle = Math.Clamp( aimAngle, -1.2f, -0.15f );
	}

	public void ChargeCast( GameState state, float dt )
	{
		if ( state.Fishing.Phase != FishingPhase.Aiming ) return;
		state.Fishing.CastCharge = Math.Clamp( state.Fishing.CastCharge + dt * 0.85f, 0f, 1f );
	}

	public void ReleaseCast( GameState state, SeededRng rng )
	{
		var f = state.Fishing;
		if ( f.Phase != FishingPhase.Aiming ) return;

		var rodPower = _content.TryGetItem( state.EquippedRodId, out var rod ) ? rod.CastPower : 1f;
		var charge = Math.Max( 0.2f, f.CastCharge );
		var castDist = 8f + charge * 42f * rodPower;
		f.HookX = state.BoatDistanceM + castDist;
		f.HookDepthM = Math.Clamp( 4f + charge * 60f * MathF.Abs( f.AimAngle ), 2f, GetMaxDepth( state ) );
		f.Phase = FishingPhase.Casting;
		f.StatusText = "Casting...";
		f.BiteTimer = rng.NextFloat( 0.35f, 1.1f );
		EmitPhase( f );
	}

	public void TryHook( GameState state, SeededRng rng )
	{
		var f = state.Fishing;
		if ( f.Phase != FishingPhase.BiteWindow ) return;

		StartFight( state, rng );
	}

	public void SetReelHeld( GameState state, bool held )
	{
		state.Fishing.ReelingHeld = held;
	}

	public void Tick( GameState state, SeededRng rng, float dt )
	{
		var f = state.Fishing;
		switch ( f.Phase )
		{
			case FishingPhase.Casting:
				f.BiteTimer -= dt;
				if ( f.BiteTimer <= 0f )
				{
					f.Phase = FishingPhase.Waiting;
					f.BiteTimer = rng.NextFloat( 0.6f, 2.2f );
					f.StatusText = "Waiting for a bite...";
					EmitPhase( f );
				}
				break;

			case FishingPhase.Waiting:
				f.BiteTimer -= dt;
				if ( f.BiteTimer <= 0f )
				{
					f.Phase = FishingPhase.BiteWindow;
					f.BiteWindowRemaining = 1.6f;
					f.StatusText = "Bite! Click to hook!";
					EmitPhase( f );
				}
				break;

			case FishingPhase.BiteWindow:
				f.BiteWindowRemaining -= dt;
				if ( f.BiteWindowRemaining <= 0f )
				{
					f.Phase = FishingPhase.Failed;
					f.StatusText = "Got away...";
					EmitPhase( f );
				}
				break;

			case FishingPhase.Fighting:
				TickFight( state, rng, dt );
				break;

			case FishingPhase.Failed:
				f.FightTimer += dt;
				if ( f.FightTimer > 1.2f )
					ResetToIdle( state, "Ready to cast" );
				break;
		}
	}

	private void StartFight( GameState state, SeededRng rng )
	{
		var f = state.Fishing;
		var catchFish = CatchGenerator.Generate( _content, state, rng );
		f.PendingCatch = catchFish;
		f.PendingFishId = catchFish.FishId;
		var def = _content.GetFish( catchFish.FishId );

		f.Phase = FishingPhase.Fighting;
		f.ReelProgress = 0f;
		f.LineTension = 0.45f;
		f.SafeZoneCenter = 0.5f;
		f.SafeZoneWidth = Math.Clamp( 0.34f - def.FightSpeed * 0.04f, 0.16f, 0.36f );
		f.FishStamina = 1f;
		f.FightTimer = 0f;
		f.StatusText = "Fish on the line!";
		EmitPhase( f );
	}

	private void TickFight( GameState state, SeededRng rng, float dt )
	{
		var f = state.Fishing;
		var def = _content.GetFish( f.PendingFishId );
		var spool = _content.TryGetItem( state.EquippedSpoolId, out var sp ) ? sp : null;
		var rod = _content.TryGetItem( state.EquippedRodId, out var rd ) ? rd : null;
		var hook = _content.TryGetItem( state.EquippedHookId, out var hk ) ? hk : null;

		var lineStr = spool?.LineStrength ?? 1f;
		var reelSpd = spool?.ReelSpeed ?? 1f;
		var rodPower = rod?.CastPower ?? 1f;
		var hookPower = hook?.HookPower ?? 1f;

		f.FightTimer += dt;

		// Move safe zone.
		var wobble = MathF.Sin( f.FightTimer * (1.6f + def.FightSpeed) ) * 0.18f * def.FightSpeed;
		if ( rng.Chance( def.SurgeChance * dt ) )
			wobble += rng.NextFloat( -0.25f, 0.25f );
		f.SafeZoneCenter = Math.Clamp( 0.5f + wobble, 0.18f, 0.82f );

		// Tension from reel hold.
		var target = f.ReelingHeld ? 0.72f : 0.28f;
		f.LineTension = Approach( f.LineTension, target, dt * (1.3f + def.EscapePressure * 0.4f) );

		var inZone = MathF.Abs( f.LineTension - f.SafeZoneCenter ) <= f.SafeZoneWidth * 0.5f;
		if ( inZone )
		{
			var progressRate = (0.18f + reelSpd * 0.12f) * (0.85f + hookPower * 0.15f) / Math.Max( 0.5f, def.FightStamina );
			f.ReelProgress = Math.Clamp( f.ReelProgress + dt * progressRate, 0f, 1f );
			f.FishStamina = Math.Clamp( f.FishStamina - dt * 0.12f, 0f, 1f );
			f.HookDepthM = Math.Max( 1f, f.HookDepthM - dt * 8f * reelSpd );
		}
		else
		{
			var danger = MathF.Abs( f.LineTension - f.SafeZoneCenter ) - f.SafeZoneWidth * 0.5f;
			f.ReelProgress = Math.Clamp( f.ReelProgress - dt * 0.08f * def.EscapePressure, 0f, 1f );
			if ( f.LineTension > 0.95f || danger > 0.42f )
			{
				var snapChance = dt * (0.18f + def.EscapePressure * 0.15f) / Math.Max( 0.5f, lineStr * rodPower );
				if ( rng.Chance( snapChance ) )
				{
					Fail( state, "Line snapped!" );
					return;
				}
			}
		}

		if ( f.ReelProgress >= 1f )
		{
			Land( state );
		}
		else if ( f.FightTimer > 45f )
		{
			Fail( state, "Fish escaped..." );
		}
	}

	private void Land( GameState state )
	{
		var f = state.Fishing;
		var fish = f.PendingCatch;
		if ( fish == null )
		{
			ResetToIdle( state, "Ready to cast" );
			return;
		}

		if ( _content.TryGetItem( state.EquippedBaitId, out var bait ) && bait.Consumable )
			state.TryConsumeItem( state.EquippedBaitId, 1 );

		var boat = _content.GetBoat( state.OwnedBoatId );
		if ( state.Hold.Count >= boat.StorageSlots )
		{
			f.StatusText = "Hold full! Sold nothing — fish released.";
			ResetToIdle( state, "Hold full — return to dock" );
			_events.Add( new NotificationEvent { Text = "Hold is full. Return to dock to sell." } );
			return;
		}

		state.Hold.Add( fish );
		state.TotalCatches++;
		state.TutorialFirstCatchDone = true;

		var isNew = !state.FishLog.ContainsKey( fish.FishId );
		if ( !state.FishLog.TryGetValue( fish.FishId, out var entry ) )
		{
			entry = new FishLogEntry
			{
				FishId = fish.FishId,
				FirstCaughtUtc = fish.CaughtAtUtc
			};
			state.FishLog[fish.FishId] = entry;
		}

		entry.TimesCaught++;
		if ( fish.SizeCm >= entry.BestCm )
		{
			entry.BestCm = fish.SizeCm;
			entry.BestKg = fish.WeightKg;
			entry.BestWorth = fish.Worth;
			entry.BestRarity = fish.Rarity;
		}

		f.Phase = FishingPhase.Landing;
		f.StatusText = "Caught!";
		state.Mode = GameMode.CatchReveal;
		EmitPhase( f );
		_events.Add( new FishCaughtEvent { Fish = fish, IsNewSpecies = isNew } );
		_events.Add( new ModeChangedEvent { Mode = state.Mode } );
	}

	private void Fail( GameState state, string reason )
	{
		var f = state.Fishing;
		f.Phase = FishingPhase.Failed;
		f.FightTimer = 0f;
		f.PendingCatch = null;
		f.PendingFishId = null;
		f.StatusText = reason;
		EmitPhase( f );
	}

	private void ResetToIdle( GameState state, string status )
	{
		var f = state.Fishing;
		f.Phase = FishingPhase.Idle;
		f.CastCharge = 0f;
		f.ReelProgress = 0f;
		f.LineTension = 0.5f;
		f.PendingCatch = null;
		f.PendingFishId = null;
		f.FightTimer = 0f;
		f.ReelingHeld = false;
		f.StatusText = status;
		EmitPhase( f );
	}

	public void CloseCatchReveal( GameState state )
	{
		if ( state.Mode != GameMode.CatchReveal ) return;
		state.Mode = GameMode.Fishing;
		ResetToIdle( state, "Ready to cast" );
		_events.Add( new ModeChangedEvent { Mode = state.Mode } );
		_events.Add( new CatchRevealClosedEvent() );
	}

	private float GetMaxDepth( GameState state )
	{
		var boat = _content.GetBoat( state.OwnedBoatId );
		return boat.MaxDepthM;
	}

	private void EmitPhase( FishingSession f )
	{
		_events.Add( new FishingPhaseChangedEvent { Phase = f.Phase, StatusText = f.StatusText } );
	}

	private static float Approach( float current, float target, float maxDelta )
	{
		if ( current < target ) return Math.Min( current + maxDelta, target );
		return Math.Max( current - maxDelta, target );
	}
}
