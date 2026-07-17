using OffshoreFishing.Core;

namespace Sandbox;

/// <summary>
/// Full-bleed 2D game view + HUD. World is drawn in UI space so it always fills the screen
/// and stays playable regardless of SpriteRenderer / ortho camera quirks.
/// </summary>
[Title( "Fishing HUD" )]
public sealed class FishingHudRoot : PanelComponent
{
	public FishingGameController Controller { get; private set; }
	public GameSession Session { get; private set; }

	Panel _worldLayer;
	Image _bg;
	Image _boat;
	Image _player;
	Image _hook;
	Image _fish;
	Panel _line;

	Label _topLeft;
	Label _topRight;
	Label _prompt;
	Label _hotbar;
	Label _fight;
	Label _toast;
	Panel _modal;

	bool _showLog;
	string _toastText;
	RealTimeSince _toastAge;
	string _bgId;

	public void Bind( FishingGameController controller )
	{
		Controller = controller;
		Session = controller?.Session;
	}

	public void Sync( GameSession session )
	{
		Session = session;
		if ( _toastAge > 3.5f )
			_toastText = null;
		Rebuild();
	}

	public void OnEvent( IDomainEvent e )
	{
		switch ( e )
		{
			case NotificationEvent n:
				_toastText = n.Text;
				_toastAge = 0;
				break;
			case ZoneUnlockedEvent z:
				_toastText = $"Unlocked: {z.ZoneId}";
				_toastAge = 0;
				break;
			case FishSoldEvent sold:
				_toastText = $"Sold {sold.Count} fish for {sold.GoldGained} gold";
				_toastAge = 0;
				break;
			case TutorialPromptEvent t:
				_toastText = t.Text;
				_toastAge = 0;
				break;
			case FishingPhaseChangedEvent phase:
				_toastText = phase.StatusText;
				_toastAge = 0;
				break;
		}
	}

	public void ToggleLog()
	{
		_showLog = !_showLog;
		Rebuild();
	}

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();

		// Critical: let mouse/keyboard reach game input except on modal buttons.
		Panel.Style.Width = Length.Fraction( 1f );
		Panel.Style.Height = Length.Fraction( 1f );
		Panel.Style.PointerEvents = PointerEvents.None;
		Panel.Style.FontColor = new Color( 0.95f, 0.94f, 0.9f );
		Panel.Style.FontSize = 15;
		Panel.Style.FontFamily = "Poppins";

		_worldLayer = new Panel { Parent = Panel };
		_worldLayer.Style.Position = PositionMode.Absolute;
		_worldLayer.Style.Left = 0;
		_worldLayer.Style.Top = 0;
		_worldLayer.Style.Width = Length.Fraction( 1f );
		_worldLayer.Style.Height = Length.Fraction( 1f );
		_worldLayer.Style.Overflow = OverflowMode.Hidden;
		_worldLayer.Style.PointerEvents = PointerEvents.None;

		_bg = MakeImage( _worldLayer, 100f, 100f, 0f, 0f );
		_bg.Style.Width = Length.Fraction( 1f );
		_bg.Style.Height = Length.Fraction( 1f );
		_boat = MakeImage( _worldLayer, 180f, 90f, 58f, 42f );
		_player = MakeImage( _worldLayer, 48f, 72f, 40f, 38f );
		_hook = MakeImage( _worldLayer, 14f, 14f, 55f, 55f );
		_fish = MakeImage( _worldLayer, 72f, 36f, 58f, 58f );
		_line = new Panel { Parent = _worldLayer };
		_line.Style.Position = PositionMode.Absolute;
		_line.Style.BackgroundColor = new Color( 0.92f, 0.95f, 1f, 0.85f );
		_line.Style.Width = 3;
		_line.Style.PointerEvents = PointerEvents.None;

		_topLeft = MakeHudBox( Panel, 16, 16, null, null, 280 );
		_topRight = MakeHudBox( Panel, null, 16, 16, null, 170 );
		_prompt = MakeHudBox( Panel, null, null, null, 96, 520 );
		_prompt.Style.Left = Length.Percent( 50f );
		_hotbar = MakeHudBox( Panel, null, null, null, 16, 700 );
		_hotbar.Style.Left = Length.Percent( 50f );
		_fight = MakeHudBox( Panel, null, null, 24, 160, 320 );
		_toast = MakeHudBox( Panel, null, 78, null, null, 480 );
		_toast.Style.Left = Length.Percent( 50f );

		_modal = new Panel { Parent = Panel };
		_modal.Style.Position = PositionMode.Absolute;
		_modal.Style.Left = Length.Percent( 50f );
		_modal.Style.Top = Length.Percent( 50f );
		_modal.Style.MarginLeft = -280;
		_modal.Style.MarginTop = -220;
		_modal.Style.Width = 560;
		_modal.Style.MaxHeight = Length.Percent( 80f );
		_modal.Style.BackgroundColor = new Color( 0.07f, 0.09f, 0.11f, 0.97f );
		_modal.Style.BorderColor = new Color( 0.77f, 0.57f, 0.23f );
		_modal.Style.BorderWidth = 3;
		_modal.Style.Padding = 18;
		_modal.Style.Overflow = OverflowMode.Scroll;
		_modal.Style.PointerEvents = PointerEvents.All;
		_modal.Style.Display = DisplayMode.None;
	}

	protected override void OnUpdate()
	{
		if ( Session != null )
			Rebuild();
	}

	void Rebuild()
	{
		if ( _topLeft == null || Session == null ) return;
		var s = Session.State;
		var zone = Session.Content.GetZone( s.CurrentZoneId );
		var obj = string.IsNullOrEmpty( s.ActiveObjectiveId )
			? null
			: Session.Content.Objectives.FirstOrDefault( o => o.Id == s.ActiveObjectiveId );

		UpdateWorld( s, zone );

		var hours = (int)s.TimeOfDayHours;
		var mins = (int)((s.TimeOfDayHours - hours) * 60f);
		var ampm = hours >= 12 ? "PM" : "AM";
		var h12 = hours % 12;
		if ( h12 == 0 ) h12 = 12;

		_topLeft.Text =
			$"{h12}:{mins:00} {ampm}  ·  DAY {s.Day}\n" +
			$"WIND {s.WindKts:0} KTS  ·  {s.WeatherId.ToUpperInvariant()}\n" +
			(obj == null
				? "OBJECTIVE: Explore"
				: $"OBJECTIVE: {obj.Title}  {s.ActiveObjectiveProgress}/{obj.TargetCount}");

		_topRight.Text = $"GOLD {s.Gold}\nDIST {s.BoatDistanceM:0}m\n{zone.Name}";

		_hotbar.Text =
			$"Rod  |  Bait x{s.CountItem( s.EquippedBaitId )}  |  Hold {s.Hold.Count} fish  |  " +
			$"{(s.OnBoat ? "AT SEA" : "AT DOCK")}";

		_prompt.Text = BuildPrompt( s );
		_toast.Text = _toastText ?? "";
		_toast.Style.Display = string.IsNullOrEmpty( _toastText ) ? DisplayMode.None : DisplayMode.Flex;

		if ( s.Fishing.Phase == FishingPhase.Fighting && s.Fishing.PendingCatch != null )
		{
			var pend = s.Fishing.PendingCatch;
			var def = Session.Content.GetFish( pend.FishId );
			var tensionPct = (int)(s.Fishing.LineTension * 100);
			var safePct = (int)(s.Fishing.SafeZoneCenter * 100);
			_fight.Style.Display = DisplayMode.Flex;
			_fight.Text =
				$"FISH ON!  HOLD LMB to reel\n" +
				$"{def.Name} ({pend.Rarity})\n" +
				$"Tension {tensionPct}%  (green ~{safePct}%)\n" +
				$"Progress {(int)(s.Fishing.ReelProgress * 100)}%   GOLD {pend.Worth}";
		}
		else
		{
			_fight.Style.Display = DisplayMode.None;
		}

		BuildModal( s );
	}

	void UpdateWorld( GameState s, ZoneDef zone )
	{
		var wantBg = s.Mode == GameMode.Shop
			? "shop_interior"
			: (!s.OnBoat && s.Mode != GameMode.Fishing ? "dock" : zone.BackgroundId);

		if ( wantBg != _bgId )
		{
			_bgId = wantBg;
			_bg.Texture = SpriteAtlas.Resolve( wantBg );
			_bg.Style.ImageRendering = ImageRendering.Point;
		}

		_boat.Texture = SpriteAtlas.Resolve( s.OwnedBoatId );
		_boat.Style.ImageRendering = ImageRendering.Point;
		_player.Texture = SpriteAtlas.Resolve( "player" );
		_player.Style.ImageRendering = ImageRendering.Point;

		if ( s.Mode == GameMode.Shop )
		{
			_boat.Style.Display = DisplayMode.None;
			_player.Style.Display = DisplayMode.None;
			_hook.Style.Display = DisplayMode.None;
			_fish.Style.Display = DisplayMode.None;
			_line.Style.Display = DisplayMode.None;
			return;
		}

		_boat.Style.Display = DisplayMode.Flex;
		_player.Style.Display = DisplayMode.Flex;

		var bob = MathF.Sin( Time.Now * 2.2f ) * 0.4f;

		if ( !s.OnBoat )
		{
			// Dock layout in % of screen
			var px = Math.Clamp( s.DockPlayerX / 560f, 0.08f, 0.72f );
			SetPct( _player, px * 100f, 42f + bob, 48, 72 );
			SetPct( _boat, 68f, 48f + bob, 200, 100 );
		}
		else
		{
			SetPct( _boat, 42f, 38f + bob, 220, 110 );
			SetPct( _player, 48f, 34f + bob, 48, 72 );
		}

		var f = s.Fishing;
		var showLine = f.Phase is FishingPhase.Aiming or FishingPhase.Casting or FishingPhase.Waiting
			or FishingPhase.BiteWindow or FishingPhase.Fighting;
		_line.Style.Display = showLine ? DisplayMode.Flex : DisplayMode.None;
		_hook.Style.Display = (showLine && f.Phase != FishingPhase.Aiming) ? DisplayMode.Flex : DisplayMode.None;
		_fish.Style.Display = f.Phase == FishingPhase.Fighting ? DisplayMode.Flex : DisplayMode.None;

		if ( showLine )
		{
			var top = s.OnBoat ? 40f + bob : 46f + bob;
			var depth = f.Phase == FishingPhase.Aiming
				? 8f + f.CastCharge * 28f
				: Math.Clamp( 8f + f.HookDepthM * 0.35f, 8f, 42f );
			_line.Style.Left = Length.Percent( s.OnBoat ? 52f : (Math.Clamp( s.DockPlayerX / 560f, 0.08f, 0.72f ) * 100f + 2f) );
			_line.Style.Top = Length.Percent( top );
			_line.Style.Height = Length.Percent( depth );

			if ( _hook.Style.Display != DisplayMode.None )
			{
				SetPct( _hook,
					s.OnBoat ? 53f + MathF.Sin( Time.Now * 2f ) : Math.Clamp( s.DockPlayerX / 560f, 0.08f, 0.72f ) * 100f + 3f,
					top + depth,
					16, 16 );
				_hook.Texture = ProceduralSpriteFactory.Solid( "ui_hook", 8, 8, new Color( 0.9f, 0.9f, 0.95f ) );
			}

			if ( _fish.Style.Display != DisplayMode.None && !string.IsNullOrEmpty( f.PendingFishId ) )
			{
				_fish.Texture = SpriteAtlas.Resolve( f.PendingFishId );
				_fish.Style.ImageRendering = ImageRendering.Point;
				SetPct( _fish,
					s.OnBoat ? 56f + MathF.Sin( Time.Now * 6f ) : Math.Clamp( s.DockPlayerX / 560f, 0.08f, 0.72f ) * 100f + 6f,
					top + depth + 1f,
					90, 44 );
			}
		}
	}

	string BuildPrompt( GameState s )
	{
		if ( s.Mode == GameMode.Paused ) return "PAUSED — press ESC or click RESUME";
		if ( s.Mode == GameMode.Shop ) return "Click items to buy · SELL ALL · ESC to leave";
		if ( s.Mode == GameMode.CatchReveal ) return "NEW CATCH — press LMB / E / Space for OK";

		return s.Fishing.Phase switch
		{
			FishingPhase.Aiming => "CHARGING CAST — release LMB to cast",
			FishingPhase.Casting => "Line out...",
			FishingPhase.Waiting => "Waiting for a bite...",
			FishingPhase.BiteWindow => "BITE! Click LMB now!",
			FishingPhase.Fighting => "HOLD LMB in the green tension zone",
			FishingPhase.Failed => "Got away — LMB to cast again",
			_ when !s.OnBoat => "A/D move · LMB cast · E near shop (left) or boat (right)",
			_ => "A/D sail · LMB cast · E to dock when near shore"
		};
	}

	void BuildModal( GameState s )
	{
		_modal.DeleteChildren( true );

		if ( s.Mode == GameMode.CatchReveal && s.Fishing.PendingCatch != null )
		{
			_modal.Style.Display = DisplayMode.Flex;
			var fish = s.Fishing.PendingCatch;
			var def = Session.Content.GetFish( fish.FishId );
			AddTitle( "NEW CATCH!" );
			AddLine( def.Name );
			AddLine( $"{fish.Rarity} · {fish.SizeCm:0} cm · {fish.WeightKg:0.0} kg" );
			AddLine( $"Worth GOLD {fish.Worth}" );
			AddLine( def.Description );
			AddButton( "OKAY", () => Controller?.UiCloseCatch() );
			return;
		}

		if ( s.Mode == GameMode.Shop )
		{
			_modal.Style.Display = DisplayMode.Flex;
			_modal.Style.Width = 640;
			AddTitle( "BAIT & TACKLE" );
			AddLine( $"GOLD {s.Gold}" );
			AddButton( $"SELL ALL FISH ({s.Hold.Count})", () => { Controller?.UiSellAll(); Rebuild(); } );

			foreach ( var item in Session.Content.Items
				.Where( i => i.Category is ItemCategory.Rod or ItemCategory.Spool or ItemCategory.Hook or ItemCategory.Bait )
				.OrderBy( i => i.Category ).ThenBy( i => i.Tier ) )
			{
				var owned = s.OwnedItemIds.Contains( item.Id ) && item.Category != ItemCategory.Bait;
				var id = item.Id;
				AddButton( owned ? $"{item.Name} (OWNED)" : $"Buy {item.Name} — {item.Price}g", () =>
				{
					Controller?.UiBuyItem( id );
					Rebuild();
				} );
			}

			foreach ( var boat in Session.Content.Boats )
			{
				var owned = s.OwnedBoatId == boat.Id;
				var id = boat.Id;
				AddButton( owned ? $"{boat.Name} (CURRENT)" : $"Buy {boat.Name} — {boat.Price}g", () =>
				{
					Controller?.UiBuyBoat( id );
					Rebuild();
				} );
			}

			AddButton( "EXIT SHOP", () => Controller?.UiCloseShop() );
			return;
		}

		if ( _showLog )
		{
			_modal.Style.Display = DisplayMode.Flex;
			AddTitle( $"FISH LOG ({s.FishLog.Count}/{Session.Content.Fish.Count})" );
			foreach ( var fish in Session.Content.Fish )
			{
				var known = s.FishLog.TryGetValue( fish.Id, out var entry );
				AddLine( known
					? $"{fish.Name} · {entry.TimesCaught}x · best {entry.BestCm:0}cm"
					: $"??? ({fish.ZoneId})" );
			}
			AddButton( "CLOSE", () => { _showLog = false; Rebuild(); } );
			return;
		}

		if ( s.Mode == GameMode.Paused )
		{
			_modal.Style.Display = DisplayMode.Flex;
			AddTitle( "PAUSED" );
			AddButton( "RESUME", () => Controller?.Session.TogglePause() );
			AddButton( "NEW GAME", () => { Controller?.UiNewGame(); Rebuild(); } );
			return;
		}

		_modal.Style.Display = DisplayMode.None;
		_modal.Style.Width = 560;
	}

	void AddTitle( string text )
	{
		var l = new Label { Text = text, Parent = _modal };
		l.Style.FontSize = 22;
		l.Style.FontColor = new Color( 0.95f, 0.9f, 0.78f );
		l.Style.MarginBottom = 10;
		l.Style.PointerEvents = PointerEvents.None;
	}

	void AddLine( string text )
	{
		var l = new Label { Text = text, Parent = _modal };
		l.Style.MarginBottom = 4;
		l.Style.PointerEvents = PointerEvents.None;
	}

	void AddButton( string text, Action action )
	{
		var btn = new Label { Text = text, Parent = _modal };
		btn.Style.MarginTop = 8;
		btn.Style.Padding = 10;
		btn.Style.BackgroundColor = new Color( 0.14f, 0.32f, 0.28f );
		btn.Style.BorderColor = new Color( 0.35f, 0.75f, 0.55f );
		btn.Style.BorderWidth = 2;
		btn.Style.PointerEvents = PointerEvents.All;
		btn.AddEventListener( "onclick", () => action?.Invoke() );
	}

	static Image MakeImage( Panel parent, Length w, Length h, float leftPct, float topPct )
	{
		var img = new Image { Parent = parent };
		img.Style.Position = PositionMode.Absolute;
		img.Style.Width = w;
		img.Style.Height = h;
		img.Style.Left = Length.Percent( leftPct );
		img.Style.Top = Length.Percent( topPct );
		img.Style.PointerEvents = PointerEvents.None;
		img.Style.ImageRendering = ImageRendering.Point;
		return img;
	}

	static Image MakeImage( Panel parent, float w, float h, float leftPct, float topPct )
	{
		var img = new Image { Parent = parent };
		img.Style.Position = PositionMode.Absolute;
		img.Style.Width = w;
		img.Style.Height = h;
		img.Style.Left = Length.Percent( leftPct );
		img.Style.Top = Length.Percent( topPct );
		img.Style.PointerEvents = PointerEvents.None;
		img.Style.ImageRendering = ImageRendering.Point;
		return img;
	}

	static void SetPct( Image img, float leftPct, float topPct, float w, float h )
	{
		img.Style.Left = Length.Percent( leftPct );
		img.Style.Top = Length.Percent( topPct );
		img.Style.Width = w;
		img.Style.Height = h;
	}

	static Label MakeHudBox( Panel parent, float? left, float? top, float? right, float? bottom, float width )
	{
		var l = new Label { Parent = parent };
		l.Style.Position = PositionMode.Absolute;
		l.Style.Width = width;
		l.Style.BackgroundColor = new Color( 0.07f, 0.09f, 0.11f, 0.9f );
		l.Style.BorderColor = new Color( 0.77f, 0.57f, 0.23f );
		l.Style.BorderWidth = 2;
		l.Style.Padding = 10;
		l.Style.PointerEvents = PointerEvents.None;
		if ( left.HasValue ) l.Style.Left = left.Value;
		if ( top.HasValue ) l.Style.Top = top.Value;
		if ( right.HasValue ) l.Style.Right = right.Value;
		if ( bottom.HasValue ) l.Style.Bottom = bottom.Value;
		return l;
	}
}
