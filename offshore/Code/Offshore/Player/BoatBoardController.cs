namespace Offshore;

/// <summary>
/// Pier boarding: walk to moored boat â†’ E Get in Boat.
/// Drive alongside dock â†’ E Get Out of Boat.
/// </summary>
public sealed class BoatBoardController : Component
{
	public static BoatBoardController Instance { get; private set; }

	public bool ShowBoatPrompt { get; private set; }
	public bool ShowDockPrompt { get; private set; }
	public float TripFuel01 { get; private set; } = 1f;

	private bool _interactLatched;

	protected override void OnAwake() => Instance = this;
	protected override void OnStart() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null || game.State == FishingSessionState.Paused )
		{
			ShowBoatPrompt = false;
			ShowDockPrompt = false;
			return;
		}

		var player = game.Player;
		if ( player is null || !player.IsValid() )
		{
			ShowBoatPrompt = false;
			ShowDockPrompt = false;
			return;
		}

		EnsureEquippedIfOwned( game );

		var boarded = player.Mode == AnglerController.LocomotionMode.InBoat;
		var boat = BoatSystem.Equipped( game.Progression );
		var canInteract = CanBoardInteract( game );

		ShowBoatPrompt = !boarded && boat is not null && canInteract && IsNearMooredBoat( game );
		ShowDockPrompt = boarded && canInteract && IsAlongsideDock( game );

		TickTripFuel( player, boat );

		if ( game.Menus?.IsOpen == true )
			return;

		if ( !canInteract )
			return;

		if ( !(Input.Pressed( "Use" ) || Input.Pressed( "Score" )) )
		{
			_interactLatched = false;
			return;
		}

		if ( _interactLatched )
			return;

		_interactLatched = true;

		if ( ShowBoatPrompt )
			TryBoard( game );
		else if ( ShowDockPrompt )
			TryDisembark( game );
	}

	/// <summary>On foot at the pier tip / moored hull (same idea as shop zone).</summary>
	public static bool IsNearMooredBoat( OffshoreGameController game )
	{
		var player = game?.Player;
		if ( player is null || !player.IsValid() )
			return false;
		if ( player.Mode == AnglerController.LocomotionMode.InBoat )
			return false;

		var x = player.WorldPosition.x;
		return x >= OffshoreConstants.BoatZoneMinX && x <= OffshoreConstants.BoatZoneMaxX;
	}

	/// <summary>Boat pulled up alongside the dock.</summary>
	public static bool IsAlongsideDock( OffshoreGameController game )
	{
		var player = game?.Player;
		if ( player is null || !player.IsValid() )
			return false;
		if ( player.Mode != AnglerController.LocomotionMode.InBoat )
			return false;

		var x = player.WorldPosition.x;
		return x >= OffshoreConstants.DockExitZoneMinX && x <= OffshoreConstants.DockExitZoneMaxX;
	}

	public static bool IsNearBoat( OffshoreGameController game ) =>
		IsNearMooredBoat( game ) || IsAlongsideDock( game );

	/// <summary>HUD-friendly: can show board prompt right now?</summary>
	public static bool ShouldShowBoardPrompt( OffshoreGameController game )
	{
		if ( game is null || (game.Menus?.IsOpen ?? false) )
			return false;
		if ( game.Player?.Mode == AnglerController.LocomotionMode.InBoat )
			return false;
		if ( BoatSystem.Equipped( game.Progression ) is null )
			return false;
		if ( !CanBoardInteract( game ) )
			return false;
		return IsNearMooredBoat( game );
	}

	/// <summary>HUD-friendly: can show get-out prompt right now?</summary>
	public static bool ShouldShowDisembarkPrompt( OffshoreGameController game )
	{
		if ( game is null || (game.Menus?.IsOpen ?? false) )
			return false;
		if ( game.Player?.Mode != AnglerController.LocomotionMode.InBoat )
			return false;
		if ( !CanBoardInteract( game ) )
			return false;
		return IsAlongsideDock( game );
	}

	private static void EnsureEquippedIfOwned( OffshoreGameController game )
	{
		var p = game?.Progression;
		if ( p is null || p.OwnedBoatIds.Count == 0 )
			return;

		if ( !string.IsNullOrWhiteSpace( p.EquippedBoatId ) && BoatCatalog.Get( p.EquippedBoatId ) is not null )
			return;

		foreach ( var id in p.OwnedBoatIds )
		{
			if ( BoatCatalog.Get( id ) is null )
				continue;
			p.EquippedBoatId = id;
			BoatSystem.ApplyCapacity( game );
			BoatSystem.EquippedBoatChanged?.Invoke();
			Log.Info( $"[Offshore] Auto-equipped boat '{id}'" );
			break;
		}
	}

	private static bool CanBoardInteract( OffshoreGameController game )
	{
		if ( game.Menus?.IsOpen == true )
			return false;

		return game.State is FishingSessionState.DockIdle
			or FishingSessionState.AimingCast
			or FishingSessionState.FishingFromBoat;
	}

	private void TryBoard( OffshoreGameController game )
	{
		var boat = BoatSystem.Equipped( game.Progression );
		if ( boat is null )
		{
			game.SetStatus( "Buy a boat at the shop first" );
			return;
		}

		if ( game.Fishing?.Pending is not null )
			return;

		game.Fishing?.ForceResetVisuals();
		game.Player.EnterBoat( boat );
		TripFuel01 = 1f;
		game.StateMachine.ForceSet( FishingSessionState.AimingCast );
		game.SetStatus( $"Aboard {boat.DisplayName}  -  A/D cruise  -  E at dock to get out" );
		DockBoatVisuals.Instance?.Refresh();
		Log.Info(
			$"[Offshore Boat] TryBoard OK id={boat.Id} dockSprite='{boat.DockSpritePath}' " +
			$"boardedSprite='{boat.BoardedSpritePath}' " +
			$"dockHas={OffshoreSprites.HasTexture( boat.DockSpritePath )} " +
			$"boardedHas={OffshoreSprites.HasTexture( boat.BoardedSpritePath )} " +
			$"dockVisuals={(DockBoatVisuals.Instance is null ? "NULL" : "ok")}" );
	}

	private void TryDisembark( OffshoreGameController game )
	{
		if ( game.Fishing?.Pending is not null )
			return;

		if ( !IsAlongsideDock( game ) )
		{
			game.SetStatus( "Pull up alongside the dock to get out" );
			return;
		}

		if ( game.StateMachine.IsFishingActive
		     && game.State is not FishingSessionState.AimingCast
			     and not FishingSessionState.DockIdle
			     and not FishingSessionState.FishingFromBoat )
		{
			game.SetStatus( "Finish the cast before docking" );
			return;
		}

		game.Fishing?.ForceResetVisuals();
		game.Player.ExitBoatToDock();
		TripFuel01 = 1f;
		game.StateMachine.ForceSet( FishingSessionState.AimingCast );
		game.SetStatus( "Back on the dock" );
		DockBoatVisuals.Instance?.Refresh();
		var boat = BoatSystem.Equipped( game.Progression );
		Log.Info(
			$"[Offshore Boat] TryDisembark OK equipped={(boat?.Id ?? "none")} " +
			$"dockHas={(boat is null ? false : OffshoreSprites.HasTexture( boat.DockSpritePath ))} " +
			$"playerMode={game.Player?.Mode}" );
	}

	private void TickTripFuel( AnglerController player, BoatDefinition boat )
	{
		if ( player.Mode != AnglerController.LocomotionMode.InBoat || boat is null )
			return;

		var outbound = MathF.Max( 0f, player.WorldPosition.x - OffshoreConstants.BoatMooringX );
		var range = MathF.Max( 8f, boat.TripRange );
		var duration = MathF.Max( 25f, boat.TripDurationSeconds );

		if ( outbound > 1.25f )
		{
			var burn = MathF.Abs( player.Velocity.x ) > 0.15f ? 1f : 0.4f;
			TripFuel01 = Math.Clamp( TripFuel01 - Time.Delta * burn / duration, 0f, 1f );
		}
		else
		{
			TripFuel01 = Math.Clamp( TripFuel01 + Time.Delta * 0.55f, 0f, 1f );
		}

		var fuelRange = range * MathF.Max( 0.15f, TripFuel01 );
		player.BoatOutboardLimitX = OffshoreConstants.BoatMooringX + fuelRange;

		if ( TripFuel01 <= 0.02f && outbound > 2f )
		{
			var game = OffshoreGameController.Instance;
			if ( game is not null && (int)(Time.Now * 2f) % 7 == 0 )
				game.SetStatus( "FUEL LOW  -  head back to the pier!" );
		}
	}
}
