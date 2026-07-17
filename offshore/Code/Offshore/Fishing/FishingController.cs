namespace Offshore;

/// <summary>
/// Orchestrates cast → bite → reel → catch/escape. Input blocked while menus/pause active.
/// Catch and escape resolutions are latched and mutually exclusive.
/// </summary>
public sealed class FishingController : Component
{
	public CastComponent Cast { get; set; }
	public HookComponent Hook { get; set; }
	public FishingLineComponent Line { get; set; }
	public WaterVolumeComponent Water { get; set; }
	public GameObject RodTip { get; set; }

	public BiteSystem Bite { get; } = new();
	public ReelingSystem Reel { get; } = new();
	public FishEncounter ActiveEncounter => Reel.Encounter ?? Bite.Encounter;
	public PendingCatch Pending { get; private set; }

	/// <summary>Set by the HUD CAST/REEL button (hold). LMB elsewhere does not cast.</summary>
	public bool UiCastHeld { get; set; }
	public bool PendingKeepRequested { get; set; }
	public bool PendingReleaseRequested { get; set; }

	public void RequestKeep() => PendingKeepRequested = true;
	public void RequestRelease() => PendingReleaseRequested = true;

	private bool _completionLatched;
	private bool _outcomeLatched;
	private bool _decisionLatched;
	private TimeUntil _holdEnds;
	private string _status = "Hold Cast to charge";
	private SpriteRenderer _fightFishSprite;

	public string StatusText => _status;
	public float Tension01 => ActiveEncounter?.LineTension ?? 0f;
	public float Progress01 => ActiveEncounter?.CatchProgress ?? 0f;
	public float Stamina01
	{
		get
		{
			var e = ActiveEncounter;
			if ( e is null || e.MaxStamina <= 0.001f )
				return 0f;
			return e.Stamina / e.MaxStamina;
		}
	}

	protected override void OnUpdate()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null || Cast is null || Hook is null || RodTip is null || !RodTip.IsValid() )
			return;

		Hook.TipRestPosition = RodTip.WorldPosition;

		if ( game.StateMachine.BlocksGameplayInput )
		{
			UiCastHeld = false;
			return;
		}

		switch ( game.State )
		{
			case FishingSessionState.DockIdle:
			case FishingSessionState.AimingCast:
				TickAiming( game );
				break;
			case FishingSessionState.ChargingCast:
				TickCharging( game );
				break;
			case FishingSessionState.Casting:
				TickCasting();
				break;
			case FishingSessionState.CastFailed:
				TickHold( game );
				break;
			case FishingSessionState.HookInWater:
				BeginWaitingForBite( game );
				break;
			case FishingSessionState.WaitingForBite:
				TickWaitingForBite( game );
				break;
			case FishingSessionState.BiteWindow:
				TickBiteWindow( game );
				break;
			case FishingSessionState.FishHooked:
				BeginReeling( game );
				break;
			case FishingSessionState.Reeling:
				TickReeling( game );
				break;
			case FishingSessionState.CatchSuccess:
				TickCatchDecision( game );
				break;
			case FishingSessionState.FishEscaped:
			case FishingSessionState.CoolerFull:
				TickOutcomeHold( game );
				break;
		}
	}

	private void TickAiming( OffshoreGameController game )
	{
		_completionLatched = false;
		_outcomeLatched = false;
		Bite.Clear();
		Reel.Clear();
		HideFightFish();
		Cast.ResetCharge();
		ApplyAimInput();
		TryCycleBait( game );

		if ( IsCastHeld() )
		{
			if ( !game.TrySetState( FishingSessionState.ChargingCast ) )
				return;

			SetStatus( game, "Charging cast..." );
			Cast.AddCharge( Time.Delta );
		}
	}

	private void TickCharging( OffshoreGameController game )
	{
		ApplyAimInput();

		if ( Input.Pressed( "Attack2" ) )
		{
			Cast.ResetCharge();
			game.TrySetState( FishingSessionState.AimingCast );
			SetStatus( game, "Cast cancelled" );
			return;
		}

		if ( IsCastHeld() )
		{
			Cast.AddCharge( Time.Delta );
			SetStatus( game, $"Charge {(int)(Cast.Charge * 100f)}%  -  release to cast" );
			return;
		}

		if ( Cast.Charge < Cast.MinChargeToCast )
		{
			Cast.ResetCharge();
			game.TrySetState( FishingSessionState.AimingCast );
			SetStatus( game, "Cast cancelled (too weak)" );
			return;
		}

		BeginCast( game );
	}

	private void BeginCast( OffshoreGameController game )
	{
		if ( !game.TrySetState( FishingSessionState.Casting ) )
			return;

		_completionLatched = false;
		_outcomeLatched = false;
		game.Progression.TotalCasts++;
		Hook.ShowInFlight();
		Hook.SetPosition( RodTip.WorldPosition );

		var started = Cast.TryBeginCast(
			RodTip.WorldPosition,
			Water,
			onComplete: OnFlightComplete,
			onTimeout: OnFlightTimeout );

		if ( !started )
		{
			Hook.HideAtTip();
			game.TrySetState( FishingSessionState.AimingCast );
			SetStatus( game, "Cast failed to start" );
			return;
		}

		SetStatus( game, "Casting..." );
	}

	private void TickCasting() => Cast.TickFlight( Time.Delta, Hook );

	private void OnFlightComplete( Vector3 landPos )
	{
		if ( _completionLatched )
			return;

		_completionLatched = true;
		var game = OffshoreGameController.Instance;
		if ( game is null )
			return;

		Hook.SetPosition( landPos );

		if ( Water is not null && Water.ContainsPoint( landPos ) )
		{
			if ( !game.TrySetState( FishingSessionState.HookInWater ) )
				return;

			Hook.ShowBobberAtRest();
			SetStatus( game, "Waiting for a bite..." );
			Log.Info( $"[Offshore] Hook landed at {landPos}" );
			return;
		}

		FailCast( game, "Missed the water" );
	}

	private void OnFlightTimeout()
	{
		if ( _completionLatched )
			return;

		_completionLatched = true;
		var game = OffshoreGameController.Instance;
		if ( game is null )
			return;

		FailCast( game, "Cast timed out" );
	}

	private void FailCast( OffshoreGameController game, string reason )
	{
		if ( !game.TrySetState( FishingSessionState.CastFailed ) )
			return;

		Hook.HideAtTip();
		Cast.CancelFlight();
		_holdEnds = game.Balance.FailedHoldSeconds;
		SetStatus( game, $"{reason}  -  recast soon" );
	}

	private void BeginWaitingForBite( OffshoreGameController game )
	{
		if ( !game.TrySetState( FishingSessionState.WaitingForBite ) )
			return;

		var depth = MathF.Max( 0.25f, OffshoreConstants.WaterSurfaceZ - Hook.WorldPosition.z );
		depth += LocationCatalog.Get( game.Progression.CurrentLocationId )?.DepthBias ?? 0f;

		var bobber = Hook.WorldPosition;
		var conditions = FishConditionContext.From( game, bobber, depth );
		var proximity = AmbientFishSchool.Instance?.BuildProximityMultipliers( bobber );
		var nearbyHint = AmbientFishSchool.Instance?.DescribeNearby( bobber ) ?? "";

		var def = FishSpawnSystem.Select(
			game.Progression.CurrentLocationId,
			depth,
			game.Balance,
			game.Progression,
			proximity,
			conditions );
		var encounter = FishSpawnSystem.CreateEncounter( def, depth, game.Progression.CurrentLocationId, conditions );
		var biteMul = FishInteractionSystem.CombinedBiteSpeed( game.Progression, def, conditions );
		Bite.Begin( encounter, game.Balance, biteMul );

		var waitHint = BuildWaitHint( nearbyHint, conditions, encounter );
		SetStatus( game, waitHint );
	}

	private void TickWaitingForBite( OffshoreGameController game )
	{
		// Allow cancel while waiting.
		if ( Input.Pressed( "Attack2" ) )
		{
			Bite.Clear();
			Hook.HideAtTip();
			game.TrySetState( FishingSessionState.AimingCast );
			SetStatus( game, "Pulled line in" );
			return;
		}

		if ( !Bite.TickWaiting( Time.Delta ) )
			return;

		if ( !game.TrySetState( FishingSessionState.BiteWindow ) )
			return;

		Hook.ShowBobberAtRest();
		SetStatus( game, "BITE! Press Cast / Space!" );
		Log.Info( $"[Offshore] Bite — {Bite.Encounter?.DisplayName}" );
	}

	private void TickBiteWindow( OffshoreGameController game )
	{
		var tryHook = IsCastPressed();
		if ( Bite.TickBiteWindow( Time.Delta, tryHook ) )
		{
			if ( _outcomeLatched )
				return;

			_outcomeLatched = true;
			game.Progression.EscapedFish++;
			if ( game.TrySetState( FishingSessionState.FishEscaped ) )
			{
				_holdEnds = game.Balance.EscapeHoldSeconds;
				SetStatus( game, "Too slow  -  fish got away" );
			}
			return;
		}

		if ( !Bite.Hooked )
			return;

		if ( !game.TrySetState( FishingSessionState.FishHooked ) )
			return;

		SetStatus( game, $"Hooked {Bite.Encounter?.DisplayName}!" );
	}

	private void BeginReeling( OffshoreGameController game )
	{
		var encounter = Bite.Encounter;
		var maxSize = BoatSystem.ActiveMaxFishSize( game );
		if ( encounter is not null && encounter.Size > maxSize + 0.02f )
		{
			CompleteEscape( game, $"{encounter.DisplayName} is too big for this boat  -  it snapped free!" );
			return;
		}

		if ( !game.TrySetState( FishingSessionState.Reeling ) )
			return;

		Reel.Begin( encounter, game.Balance );
		ShowFightFish( encounter );
		Hook.ShowAsHook();
		SetStatus( game, $"Reeling {encounter?.DisplayName}  -  hold Cast, ease off if tension spikes" );
	}

	private void TickReeling( OffshoreGameController game )
	{
		var reeling = IsCastHeld();
		var steer = -Input.AnalogMove.x;
		if ( MathF.Abs( steer ) < 0.05f )
			steer = MathF.Sign( -Input.MouseDelta.x );

		var result = Reel.Tick( Time.Delta, reeling, steer );
		if ( result == ReelResolveResult.None )
		{
			var e = Reel.Encounter;
			if ( e is not null )
			{
				var danger = e.LineTension >= 0.78f ? "  -  EASE OFF!" : "";
				SetStatus( game, $"{e.DisplayName}  Progress {(int)(e.CatchProgress * 100f)}%  Tension {(int)(e.LineTension * 100f)}%{danger}" );
				NudgeFightFish( e );
			}
			return;
		}

		if ( _outcomeLatched )
			return;

		_outcomeLatched = true;

		if ( result == ReelResolveResult.Caught )
			CompleteCatch( game );
		else
			CompleteEscape( game, "Line snapped  -  fish escaped" );
	}

	private void CompleteCatch( OffshoreGameController game )
	{
		var encounter = Reel.Encounter ?? Bite.Encounter;
		if ( encounter is null )
		{
			CompleteEscape( game, "Fish escaped" );
			return;
		}

		var def = encounter.Definition;
		var isNew = def is not null && !game.Progression.DiscoveredFishIds.Contains( def.Id );
		var prevBest = 0f;
		if ( def is not null )
		{
			var entry = JournalService.Get( game.Progression, def.Id );
			prevBest = entry.HeaviestWeight;
		}

		var isPb = encounter.Weight > prevBest + 0.001f;
		var record = encounter.ToCatchRecord( isNew, isPb );
		var capacityCost = def is not null
			? MathF.Max( 0.5f, def.CapacityCost )
			: MathF.Max( 0.5f, encounter.Weight );
		var overflow = game.Progression.CoolerUsed + capacityCost > game.Progression.CoolerCapacity + 0.001f;

		Pending = new PendingCatch
		{
			Record = record,
			Definition = def,
			PreviousBestWeight = prevBest,
			IsPersonalBest = isPb,
			CoolerWouldOverflow = overflow,
			SpritePath = string.IsNullOrWhiteSpace( def?.SpritePath )
				? OffshoreSprites.Paths.FishBluegill
				: def.SpritePath
		};

		if ( !game.TrySetState( FishingSessionState.CatchSuccess ) )
			return;

		_decisionLatched = false;
		_holdEnds = 0;
		// Keep world visuals frozen under the catch card (fight fish, hook, pose).
		SetStatus( game, overflow
			? "NEW CATCH  -  cooler full! Keep blocked  -  Release or sell first"
			: "NEW CATCH  -  Keep or Release" );
		Log.Info( $"[Offshore] Catch card {encounter.DisplayName} size={encounter.Size:0.00} value={record.FinalValue:0}" );
	}

	private void TickCatchDecision( OffshoreGameController game )
	{
		if ( Pending is null )
		{
			ResetToAiming( game );
			return;
		}

		// Re-evaluate cooler room after a mid-card sell.
		var cost = Pending.Definition is not null
			? MathF.Max( 0.5f, Pending.Definition.CapacityCost )
			: MathF.Max( 0.5f, Pending.Record?.Weight ?? 1f );
		Pending.CoolerWouldOverflow = game.Progression.CoolerUsed + cost > game.Progression.CoolerCapacity + 0.001f;

		if ( Pending.CoolerWouldOverflow && (Input.Pressed( "Use" ) || Input.Pressed( "Score" )) )
		{
			game.Menus?.OpenSellFromCoolerFull( game );
			return;
		}

		if ( _decisionLatched )
		{
			if ( !Input.Down( "Jump" ) && !Input.Down( "Cast" ) && !Input.Down( "Attack2" ) && !Input.Down( "Slot1" ) && !Input.Down( "Slot2" ) )
				_decisionLatched = false;
			return;
		}

		var keep = Input.Pressed( "Jump" ) || Input.Pressed( "Cast" ) || Input.Pressed( "Slot1" ) || PendingKeepRequested;
		var release = Input.Pressed( "Attack2" ) || Input.Pressed( "Slot2" ) || PendingReleaseRequested;
		PendingKeepRequested = false;
		PendingReleaseRequested = false;

		if ( keep )
		{
			_decisionLatched = true;
			ResolveKeep( game );
		}
		else if ( release )
		{
			_decisionLatched = true;
			ResolveRelease( game );
		}
	}

	private void ResolveKeep( OffshoreGameController game )
	{
		var pending = Pending;
		if ( pending?.Record is null )
		{
			ResetToAiming( game );
			return;
		}

		if ( pending.CoolerWouldOverflow )
		{
			SetStatus( game, "Cooler full  -  press E to sell, or RMB to Release" );
			_decisionLatched = false;
			return;
		}

		var record = pending.Record;
		game.Progression.Cooler.Add( record );
		game.Progression.SuccessfulCatches++;
		JournalService.RegisterCatch( game.Progression, record );
		ContractSystem.NotifyCatch( game.Progression, record );
		CollectionSystem.NotifyCatch( game.Progression, record );
		TournamentSystem.NotifyCatch( game.Progression, record );
		TimeWeatherSystem.AdvanceOnCatch( game.Progression );
		OffshoreSaveSystem.Save( game.Progression );

		var discovery = record.IsNewDiscovery ? "  -  NEW SPECIES!" : "";
		var recordText = pending.IsPersonalBest ? "  -  NEW PERSONAL BEST!" : "";
		Pending = null;
		ForceResetVisuals();
		game.TrySetState( FishingSessionState.AimingCast );
		SetStatus( game, $"Kept {record.DisplayName}! +${(int)record.FinalValue}{discovery}{recordText}" );
	}

	private void ResolveRelease( OffshoreGameController game )
	{
		var pending = Pending;
		if ( pending?.Record is null )
		{
			ResetToAiming( game );
			return;
		}

		var record = pending.Record;
		JournalService.RegisterCatch( game.Progression, record );
		game.Progression.Experience += 4f;
		TimeWeatherSystem.AdvanceOnCatch( game.Progression );
		OffshoreSaveSystem.Save( game.Progression );

		Pending = null;
		ForceResetVisuals();
		game.TrySetState( FishingSessionState.AimingCast );
		SetStatus( game, $"Released {record.DisplayName}  -  +XP" );
	}

	private void CompleteEscape( OffshoreGameController game, string reason )
	{
		Pending = null;
		game.Progression.EscapedFish++;
		if ( !game.TrySetState( FishingSessionState.FishEscaped ) )
			return;

		_holdEnds = game.Balance.EscapeHoldSeconds;
		SetStatus( game, reason );
		HideFightFish();
		Log.Info( $"[Offshore] Escape: {reason}" );
	}

	private void TickHold( OffshoreGameController game )
	{
		if ( !_holdEnds )
			return;

		ResetToAiming( game );
	}

	private void TickOutcomeHold( OffshoreGameController game )
	{
		if ( !_holdEnds )
			return;

		ResetToAiming( game );
	}

	private void ResetToAiming( OffshoreGameController game )
	{
		Pending = null;
		ForceResetVisuals();
		game.TrySetState( FishingSessionState.AimingCast );
		SetStatus( game, "Aim with A/D  -  Hold CAST to charge  -  shop is left on the dock" );
	}

	/// <summary>Used when opening sell from CoolerFull / outcomes without waiting on the hold timer.</summary>
	public void ForceResetVisuals()
	{
		Cast?.CancelFlight();
		Cast?.ResetCharge();
		Hook?.HideAtTip();
		Bite.Clear();
		Reel.Clear();
		HideFightFish();
		_decisionLatched = false;
		_holdEnds = 0;
		UiCastHeld = false;
		// Leave Pending for catch-card / sell-then-keep flow.
	}

	private void ShowFightFish( FishEncounter encounter )
	{
		if ( encounter?.Definition is null || Hook is null )
			return;

		var path = string.IsNullOrWhiteSpace( encounter.Definition.SpritePath )
			? OffshoreSprites.Paths.FishBluegill
			: encounter.Definition.SpritePath;

		if ( _fightFishSprite is null || !_fightFishSprite.IsValid() )
		{
			_fightFishSprite = OffshoreSprites.Spawn(
				Hook.GameObject,
				path,
				new Vector2( 1.6f + encounter.Size, 0.9f + encounter.Size * 0.5f ),
				new Vector3( 0.8f, -0.1f, -0.35f ),
				"FightFish" );
		}
		else
		{
			_fightFishSprite.Sprite = OffshoreSprites.MakeSprite( OffshoreSprites.Load( path ) );
			_fightFishSprite.Size = new Vector2( 1.6f + encounter.Size, 0.9f + encounter.Size * 0.5f );
			_fightFishSprite.GameObject.Enabled = true;
		}
	}

	private void NudgeFightFish( FishEncounter encounter )
	{
		if ( _fightFishSprite is null || !_fightFishSprite.IsValid() || encounter is null )
			return;

		var bob = MathF.Sin( Time.Now * (3f + encounter.Speed) ) * 0.15f;
		_fightFishSprite.GameObject.LocalPosition = new Vector3( 0.8f + encounter.Direction * 0.25f, -0.1f, -0.35f + bob );
	}

	private void HideFightFish()
	{
		if ( _fightFishSprite is not null && _fightFishSprite.IsValid() )
			_fightFishSprite.GameObject.Enabled = false;
	}

	private void ApplyAimInput()
	{
		// WASD is locomotion — aim holds last angle (default from CastComponent).
		// Mouse pitch only when the cursor is captured (not free dock UI).
		if ( Mouse.Visibility == MouseVisibility.Visible )
			return;
		if ( MathF.Abs( Input.MouseDelta.y ) > 0.01f )
			Cast.AdjustAim( -Input.MouseDelta.y * 0.12f );
	}

	private void SetStatus( OffshoreGameController game, string text )
	{
		_status = text;
		game.SetStatus( text );
	}

	private static string BuildWaitHint( string nearbyHint, FishConditionContext conditions, FishEncounter encounter )
	{
		var bits = new List<string>();
		if ( !string.IsNullOrEmpty( nearbyHint ) )
			bits.Add( nearbyHint );

		if ( conditions is not null )
		{
			if ( conditions.Offshore01 >= 0.55f )
				bits.Add( "deep water" );
			else if ( conditions.Offshore01 >= 0.3f )
				bits.Add( "offshore" );

			if ( conditions.Weather is WeatherType.Rain or WeatherType.Storm or WeatherType.Fog )
				bits.Add( conditions.Weather.ToString().ToLowerInvariant() );
		}

		if ( !string.IsNullOrEmpty( encounter?.ConditionNote ) && conditions?.Offshore01 >= 0.4f )
			bits.Add( "prime conditions" );

		return bits.Count == 0
			? "Waiting for a bite..."
			: $"Waiting for a bite... ({string.Join( "  -  ", bits )})";
	}

	private bool IsCastHeld() =>
		UiCastHeld || Input.Down( "Cast" ) || Input.Down( "Jump" );

	private bool IsCastPressed() =>
		UiCastHeld || Input.Pressed( "Cast" ) || Input.Pressed( "Jump" );

	private void TryCycleBait( OffshoreGameController game )
	{
		if ( game?.Progression is null )
			return;
		if ( !(Input.Pressed( "Reload" ) || Input.Pressed( "Drop" )) )
			return;

		var id = BaitSystem.CycleNext( game.Progression );
		SetStatus( game, $"Bait: {BaitSystem.DisplayName( id )}" );
		OffshoreSaveSystem.Save( game.Progression );
	}
}
