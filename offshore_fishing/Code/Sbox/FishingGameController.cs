using OffshoreFishing.Core;

namespace Sandbox;

/// <summary>Owns the portable session and bridges input/save/presentation.</summary>
[Title( "Fishing Game Controller" )]
public sealed class FishingGameController : Component
{
	public static FishingGameController Instance { get; private set; }

	public GameSession Session { get; private set; }
	public GameContent Content => Session?.Content;

	[Property] public bool LoadSaveOnStart { get; set; } = false;

	float _autosaveTimer;
	WorldPresenter _world;
	FishingHudRoot _hud;

	protected override void OnAwake()
	{
		Instance = this;
		StartFreshOrLoad();
	}

	protected override void OnStart()
	{
		_world = Scene.GetAllComponents<WorldPresenter>().FirstOrDefault();
		_hud = Scene.GetAllComponents<FishingHudRoot>().FirstOrDefault();
		_world?.Bind( this );
		_hud?.Bind( this );
		_hud?.OnEvent( new TutorialPromptEvent
		{
			Text = "A/D move · Hold LMB to cast · E opens shop (walk left) or board boat (walk right)"
		} );
	}

	protected override void OnUpdate()
	{
		if ( Session == null ) return;

		HandleInput();
		Session.Advance( Time.Delta );
		DispatchEvents( Session.DrainEvents() );
		_world?.Sync( Session );
		_hud?.Sync( Session );

		_autosaveTimer += Time.Delta;
		if ( _autosaveTimer >= 20f )
		{
			_autosaveTimer = 0f;
			SaveAdapter.Save( Session );
		}
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
		{
			if ( Session != null )
				SaveAdapter.Save( Session );
			Instance = null;
		}
	}

	void StartFreshOrLoad()
	{
		var content = ContentCatalog.Create();
		if ( LoadSaveOnStart && SaveAdapter.TryLoad( out var dto ) )
		{
			Session = GameSession.FromSave( content, dto );
			Session.ApplyOfflineProgress( DateTimeOffset.UtcNow );
		}
		else
		{
			Session = new GameSession( content );
		}
	}

	void HandleInput()
	{
		var state = Session.State;

		if ( Input.EscapePressed )
		{
			Input.EscapePressed = false;
			if ( state.Mode == GameMode.Shop )
				Session.CloseShop();
			else if ( state.Mode == GameMode.CatchReveal )
				Session.CloseCatchReveal();
			else
				Session.TogglePause();
			return;
		}

		if ( state.Mode == GameMode.Paused ) return;

		if ( state.Mode == GameMode.CatchReveal )
		{
			if ( Clicked() || Input.Pressed( "use" ) || Input.Pressed( "jump" ) )
				Session.CloseCatchReveal();
			return;
		}

		if ( state.Mode == GameMode.Shop )
		{
			if ( Input.Pressed( "use" ) )
				Session.CloseShop();
			return;
		}

		// Movement works on dock and while sailing (not mid-fight).
		var axis = MoveAxis();
		if ( !state.OnBoat && state.Fishing.Phase is FishingPhase.Idle or FishingPhase.Failed )
		{
			Session.MoveOnDock( axis * 140f * Time.Delta, 40f, 520f );
		}
		else if ( state.OnBoat && state.Fishing.Phase is FishingPhase.Idle or FishingPhase.Failed or FishingPhase.Waiting )
		{
			if ( MathF.Abs( axis ) > 0.1f )
				Session.Travel( axis, Time.Delta );
		}

		if ( Input.Pressed( "use" ) )
		{
			if ( !state.OnBoat )
			{
				if ( state.DockPlayerX < 180f )
					Session.OpenShop();
				else if ( state.DockPlayerX > 380f )
					Session.BoardBoat();
			}
			else if ( state.BoatDistanceM < 30f && state.Fishing.Phase is FishingPhase.Idle or FishingPhase.Failed )
			{
				Session.Disembark();
			}
		}

		if ( Input.Pressed( "reload" ) && !state.OnBoat )
			Session.OpenShop();

		if ( Input.Pressed( "score" ) )
			_hud?.ToggleLog();

		HandleFishingInput();
	}

	void HandleFishingInput()
	{
		var state = Session.State;
		if ( state.Mode is GameMode.Shop or GameMode.Paused or GameMode.CatchReveal )
			return;

		var f = state.Fishing;

		switch ( f.Phase )
		{
			case FishingPhase.Idle:
			case FishingPhase.Failed:
				if ( Clicked() )
				{
					Session.BeginCastAim();
					// Instant helpful cast charge so a click still works.
					for ( var i = 0; i < 10; i++ )
						Session.ChargeCast( 0.05f );
				}
				break;

			case FishingPhase.Aiming:
				if ( Input.Down( "attack1" ) )
					Session.ChargeCast( Time.Delta );

				// Click-cast: if player already charged via Idle burst and isn't holding, release.
				if ( Input.Released( "attack1" ) || (!Input.Down( "attack1" ) && f.CastCharge >= 0.45f) )
					Session.ReleaseCast();
				break;

			case FishingPhase.BiteWindow:
				if ( Clicked() || Input.Pressed( "use" ) )
					Session.TryHook();
				break;

			case FishingPhase.Fighting:
				Session.SetReelHeld( Input.Down( "attack1" ) || Input.Down( "Attack1" ) || Input.Down( "reload" ) );
				break;
		}
	}

	static bool Clicked()
		=> Input.Pressed( "attack1" ) || Input.Pressed( "Attack1" );

	static float MoveAxis()
	{
		var axis = Input.AnalogMove.x;
		if ( MathF.Abs( axis ) > 0.1f )
			return axis;

		var right = Input.Down( "right" ) || Input.Down( "Right" ) || Input.Down( "forward" );
		var left = Input.Down( "left" ) || Input.Down( "Left" ) || Input.Down( "backward" );

		// Keyboard fallback
		if ( Input.Keyboard.Down( "A" ) ) left = true;
		if ( Input.Keyboard.Down( "D" ) ) right = true;

		if ( right && !left ) return 1f;
		if ( left && !right ) return -1f;
		return 0f;
	}

	void DispatchEvents( IReadOnlyList<IDomainEvent> events )
	{
		foreach ( var e in events )
		{
			switch ( e )
			{
				case FishCaughtEvent:
				case ItemPurchasedEvent:
				case FishSoldEvent:
					SaveAdapter.Save( Session );
					break;
				case ZoneUnlockedEvent z:
					Log.Info( $"[Offshore] Zone unlocked: {z.ZoneId}" );
					break;
				case NotificationEvent n:
					Log.Info( $"[Offshore] {n.Text}" );
					break;
			}

			_hud?.OnEvent( e );
			_world?.OnEvent( e );
			Scene.GetAllComponents<AudioDirector>().FirstOrDefault()?.OnEvent( e );
		}
	}

	public void UiBuyItem( string id ) => Session.BuyItem( id );
	public void UiBuyBoat( string id ) => Session.BuyBoat( id );
	public void UiHire( string id ) => Session.HireBoat( id );
	public void UiSellAll() => Session.SellAll();
	public void UiEquip( string id ) => Session.Equip( id );
	public void UiCloseShop() => Session.CloseShop();
	public void UiCloseCatch() => Session.CloseCatchReveal();
	public void UiNewGame()
	{
		SaveAdapter.Delete();
		Session = new GameSession( ContentCatalog.Create() );
		_world?.Bind( this );
		_hud?.Bind( this );
	}
}
