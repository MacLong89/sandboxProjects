namespace Offshore;

/// <summary>
/// Mario side-view presenter. Waterline at Z=0 (screen mid).
/// Sky + water are fixed screen backdrops; dock/boat/player scroll in world space.
/// Gameplay sprites keep PNG aspect via FitHeight — never arbitrary stretch.
/// </summary>
public sealed class WorldPresenter
{
	public const float WaterlineZ = 0f;
	public const float CameraZ = 0f;

	// Goal layout: fixed sky/water like sky lab; dock scrolls in world space.
	const float ShopDockHeight = 320f;
	const float PlayerHeight = 52f;
	const float BoatHeight = 78f;
	const float PropHeight = 36f;
	const float FishHeight = 24f;
	const float WalkwayZ = 8f; // dock boards just above wave crests
	// shop_dock PNG: walkway ~38% from top → center = walkway - height*(0.5-0.38)
	const float ShopDockCenterZ = WalkwayZ - ShopDockHeight * 0.12f;
	/// <summary>Drop player/boat so they sit on the waterline visually.</summary>
	const float ActorZNudge = -80f;
	const float PlayerCenterZ = WalkwayZ + PlayerHeight * 0.48f + ActorZNudge;
	const float BoatCenterZ = WaterlineZ + 6f + ActorZNudge;
	const float SeabedZ = -195f;

	readonly Scene _scene;
	readonly List<GameObject> _owned = new();
	readonly List<(SpriteActor actor, float worldX, float parallax)> _layered = new();
	readonly List<(SpriteActor actor, float worldX)> _world = new();
	readonly List<(SpriteActor actor, float worldX, float z)> _decor = new();
	readonly List<SpriteActor> _fishSchool = new();
	readonly List<SpriteActor> _eventSprites = new();
	readonly List<SpriteActor> _hotspotSprites = new();
	readonly List<(SpriteActor actor, float worldX)> _pillars = new();
	readonly SkyBackdrop _sky = new();
	readonly WaterBackdrop _water = new();

	SpriteActor _shopDock;
	SpriteActor _boat;
	SpriteActor _boatFront;
	SpriteActor _player;
	SpriteActor _wake;

	float _anim;
	string _boatSprite = "boats/boat_dinghy";
	string _boatFrontSprite = "";
	// Combined shop+pier plate: center so the pier runs toward the boat berth (DockX=300).
	float _shopDockWorldX = 90f;
	float _shopDockHalfWidth = 365f;

	/// <summary>Matches Camera OrthographicHeight=560 treated as half-extent.</summary>
	const float OrthoHalfHeight = 560f;

	// Depth layering (smaller Y = closer to camera)
	const float PlayerY = 2f;
	const float BoatFrontY = 2.5f; // near gunwale covers seated legs
	const float BoatSeatY = 3.5f;  // player torso between hull and rim
	const float BoatY = 5f;        // full hull behind seated player
	const float DockY = 22f; // behind water so waves layer over pilings
	const float DecorY = 30f;
	const float SeabedY = 36f;

	public WorldPresenter( Scene scene ) => _scene = scene;

	public void Build()
	{
		Clear();

		// Fixed screen backdrops (same as sky lab) — do not scroll with the player.
		_sky.Build( _scene, _owned, SkyBackdrop.LayoutMode.Lab );
		_water.Build( _scene, _owned );

		// World props behind the ocean plate (visible later if water alpha drops / deep view).
		AddSeabed( "env/kelp", PropHeight, 10f, SeabedZ + 20f );
		AddSeabed( "env/kelp", PropHeight, 110f, SeabedZ + 16f );
		AddSeabed( "env/kelp", PropHeight * 0.9f, 200f, SeabedZ + 18f );
		AddSeabed( "env/rock", PropHeight * 0.75f, 50f, SeabedZ + 8f );
		AddSeabed( "env/rock", PropHeight * 0.7f, 160f, SeabedZ + 6f );
		AddSeabed( "env/coral", PropHeight, -40f, SeabedZ + 14f );
		AddSeabed( "env/coral", PropHeight * 0.85f, 240f, SeabedZ + 12f );
		AddSeabed( "env/treasure_chest", PropHeight * 0.8f, 280f, SeabedZ + 10f );

		// Dock scrolls with world; behind water so waves cover pilings.
		_shopDock = Fit( "ShopDock", "env/shop_dock", ShopDockHeight, _shopDockWorldX, ShopDockCenterZ, y: DockY );
		_shopDockHalfWidth = Math.Max( 1f, SpriteSizer.FitHeight( "env/shop_dock", ShopDockHeight ).x * 0.5f );

		_boat = Fit( "Boat", _boatSprite, BoatHeight, BoatController.DockX, BoatCenterZ, y: BoatY );
		_boatFront = Fit( "BoatFront", "boats/boat_dinghy_front", BoatHeight, BoatController.DockX, BoatCenterZ, y: BoatFrontY );
		_boatFront.GameObject.Enabled = false;
		_wake = Fit( "Wake", "boats/wake_0", 28f, BoatController.DockX - 40f, WaterlineZ - 4f, y: BoatY + 1f );
		_player = Fit( "Player", "art/player_idle_0", PlayerHeight, 0f, PlayerCenterZ, y: PlayerY );

		for ( var i = 0; i < 7; i++ )
		{
			var path = i % 3 == 0 ? "fish/sardine" : i % 3 == 1 ? "fish/mackerel" : "fish/redsnapper";
			var wx = 20 + i * 48f;
			var wz = -55f - (i % 3) * 28f;
			var f = Fit( $"School{i}", path, FishHeight, wx, wz, y: SeabedY );
			_fishSchool.Add( f );
			_world.Add( (f, wx) );
		}
	}

	public void Clear()
	{
		foreach ( var go in _owned )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_owned.Clear();
		_layered.Clear();
		_world.Clear();
		_pillars.Clear();
		_decor.Clear();
		_fishSchool.Clear();
		_eventSprites.Clear();
		_hotspotSprites.Clear();
	}

	public void Update(
		float dt,
		float cameraX,
		float playerDockX,
		bool aboard,
		BoatController boat,
		BoatDefinition boatDef,
		PlayerController player,
		TimeOfDayService time,
		WeatherService weather,
		float waterDepth,
		FishingController fishing,
		GamePhase phase,
		HotspotService hotspots,
		WorldEventService events )
	{
		_anim += dt;
		var cam = cameraX;

		_sky.Update( dt, cam, time, weather );
		_water.Update( dt, time, weather );

		foreach ( var (actor, worldX, parallax) in _layered )
		{
			if ( actor?.GameObject is null || !actor.GameObject.IsValid() ) continue;
			var p = actor.GameObject.WorldPosition;
			actor.GameObject.WorldPosition = new Vector3( worldX - cam * parallax, p.y, p.z );
		}

		// Solid dock — stay visible until the pier edge actually leaves the frustum (not a distance fade).
		var dockOnScreen = DockIntersectsView( cam );
		SetDockVisible( dockOnScreen );
		Place( _shopDock, _shopDockWorldX - cam, DockY, ShopDockCenterZ, 1f );

		foreach ( var (actor, wx, z) in _decor )
			Place( actor, wx - cam, DecorY, z, 1f );

		var boatId = (boatDef?.Sprite ?? "boat_dinghy").Replace( "boat_", "" );
		var emptyBoatPath = $"boats/boat_{boatId}";
		var frontBoatPath = $"boats/boat_{boatId}_front";

		var bob = MathF.Sin( boat.BobPhase ) * (1.6f + weather.WaveIntensity * 2f) * (boatDef?.WaveResponse ?? 1f) * 0.35f;
		var boatScreenX = aboard ? 0f : BoatController.DockX - cam;
		var boatFacing = aboard ? boat.Facing : 1f;
		var boatFlip = boatFacing < 0f;
		var boarding = phase == GamePhase.Boarding;
		var inBoat = aboard || boarding;
		var boatZ = BoatCenterZ + bob;
		var rock = inBoat ? boat.RockAngle * 0.35f : 0f;

		// Full hull behind the seated player — size never changes when boarding.
		var showBoat = boatDef is not null;
		if ( _boat is not null )
		{
			_boat.GameObject.Enabled = showBoat;
			if ( showBoat )
			{
				if ( _boatSprite != emptyBoatPath || Math.Abs( _boat.Size.y - BoatHeight ) > 0.5f )
				{
					_boatSprite = emptyBoatPath;
					_boat.SetFitHeight( emptyBoatPath, BoatHeight );
				}
				_boat.GameObject.WorldPosition = new Vector3( boatScreenX, BoatY, boatZ );
				_boat.GameObject.LocalRotation = Rotation.From( 0, 0, rock );
				_boat.Flip = boatFlip;
				_boat.Tint = Color.White;
			}
		}

		// Near gunwale overlay — only while boarded so legs sit inside the hull.
		if ( _boatFront is not null )
		{
			_boatFront.GameObject.Enabled = showBoat && inBoat;
			if ( showBoat && inBoat )
			{
				if ( _boatFrontSprite != frontBoatPath || Math.Abs( _boatFront.Size.y - BoatHeight ) > 0.5f )
				{
					_boatFrontSprite = frontBoatPath;
					_boatFront.SetFitHeight( frontBoatPath, BoatHeight );
				}
				_boatFront.GameObject.WorldPosition = new Vector3( boatScreenX, BoatFrontY, boatZ );
				_boatFront.GameObject.LocalRotation = Rotation.From( 0, 0, rock );
				_boatFront.Flip = boatFlip;
				_boatFront.Tint = Color.White;
			}
		}

		if ( _wake is not null )
		{
			_wake.GameObject.Enabled = showBoat && aboard && boat.WakeStrength > 0.05f;
			var wakeX = boatScreenX + (boatFlip ? 36f : -36f);
			_wake.GameObject.WorldPosition = new Vector3( wakeX, BoatY + 1f, boatZ - 10f );
			_wake.Flip = boatFlip;
			var wakePath = boat.WakeStrength > 0.66f ? "boats/wake_2" : boat.WakeStrength > 0.33f ? "boats/wake_1" : "boats/wake_0";
			if ( _wake.Path != wakePath )
				_wake.SetFitHeight( wakePath, 28f );
		}

		if ( _player is not null )
		{
			var anim = ResolvePlayerAnim( player.Anim, phase, _anim, fishing );
			// Walk/idle: stable idle footprint. Rod frames: own aspect, but height matches PlayerHeight
			// (sprites are tight-cropped so the body isn't shrunk by empty canvas padding).
			var playerSize = anim.Contains( "player_rod_" )
				? SpriteSizer.FitHeight( anim, PlayerHeight )
				: SpriteSizer.FitHeight( "art/player_idle_0", PlayerHeight );

			if ( inBoat )
			{
				if ( _player.Path != anim || Math.Abs( _player.Size.x - playerSize.x ) > 0.1f || Math.Abs( _player.Size.y - playerSize.y ) > 0.1f )
					_player.Set( anim, playerSize );

				var seatX = (boatDef?.PlayerAnchor.x ?? 14f) * (BoatHeight / 96f) * 0.85f;
				var withRod = phase is GamePhase.Casting or GamePhase.WaitingBite or GamePhase.Hooking or GamePhase.Reeling
					|| player.Anim is "cast" or "fish" or "hook" or "reel";
				var seatZ = withRod ? BoatHeight * 0.18f : BoatHeight * 0.12f;
				var px = boatScreenX + (boatFlip ? -seatX : seatX);
				_player.Flip = boatFlip;
				_player.Tint = Color.White;
				_player.GameObject.Enabled = true;
				_player.GameObject.LocalRotation = Rotation.From( 0, 0, boat.RockAngle * 0.5f );
				_player.GameObject.WorldPosition = new Vector3( px, BoatSeatY, boatZ + seatZ );
			}
			else
			{
				if ( _player.Path != anim || Math.Abs( _player.Size.x - playerSize.x ) > 0.1f || Math.Abs( _player.Size.y - playerSize.y ) > 0.1f )
					_player.Set( anim, playerSize );
				_player.Flip = player.Facing < 0;
				_player.Tint = Color.White;
				_player.GameObject.Enabled = true;
				_player.GameObject.LocalRotation = Rotation.Identity;
				_player.GameObject.WorldPosition = new Vector3( 0f, PlayerY, PlayerCenterZ );
			}
		}

		for ( var i = 0; i < _fishSchool.Count; i++ )
		{
			var f = _fishSchool[i];
			if ( !f.GameObject.Enabled ) continue;
			var baseX = _world[i].worldX;
			var x = baseX - cam + MathF.Sin( _anim * 1.2f + i ) * 14f;
			var z = -55f - (i % 3) * 28f + MathF.Sin( _anim * 2f + i * 0.7f ) * 5f;
			f.GameObject.WorldPosition = new Vector3( x, SeabedY, z );
			f.Flip = MathF.Sin( _anim * 0.4f + i ) > 0;
			f.Tint = new Color( 1, 1, 1, dockOnScreen ? 0.95f : 0.5f );
		}

		SyncList( _hotspotSprites, hotspots.Active.Count, "Hotspot", "env/bird_0", 22f );
		for ( var i = 0; i < hotspots.Active.Count && i < _hotspotSprites.Count; i++ )
		{
			var h = hotspots.Active[i];
			_hotspotSprites[i].GameObject.WorldPosition = new Vector3( h.WorldX - cam, 12, WaterlineZ + 40f + MathF.Sin( _anim * 4f + i ) * 6f );
			_hotspotSprites[i].Tint = new Color( 1, 1, 1, 0.55f );
		}

		// World-event props (crate, npc boats, etc.) stay off until they sit on the waterline cleanly.
		SyncList( _eventSprites, 0, "Event", "env/buoy", 36f );
		for ( var i = 0; i < events.Events.Count && i < _eventSprites.Count; i++ )
		{
			var e = events.Events[i];
			_eventSprites[i].SetFitHeight( SpriteForEvent( e.Kind ), EventHeight( e.Kind ) );
			_eventSprites[i].GameObject.WorldPosition = new Vector3( e.WorldX - cam, EventY( e.Kind ), EventZ( e.Kind ) );
		}
	}

	static string ResolvePlayerAnim( string anim, GamePhase phase, float t, FishingController fishing )
	{
		if ( phase is GamePhase.Casting )
			return CastFrame( fishing );
		if ( phase is GamePhase.WaitingBite )
			return "art/player_rod_wait";
		if ( phase is GamePhase.Hooking )
			return "art/player_rod_fight";
		if ( phase is GamePhase.Reeling )
			return $"art/player_rod_reel_{(int)(t * 10) % 3}";
		if ( phase is GamePhase.CatchResult )
			return "art/player_rod_keep";

		return anim switch
		{
			"walk" => $"art/player_walk_{(int)(t * 8) % 4}",
			"cast" => CastFrame( fishing ),
			"fish" => "art/player_rod_wait",
			"hook" => "art/player_rod_fight",
			"reel" => $"art/player_rod_reel_{(int)(t * 10) % 3}",
			"hold" => "art/player_rod_keep",
			"celebrate" => "art/player_celebrate",
			_ => "art/player_idle_0"
		};
	}

	/// <summary>
	/// Hold = rod back. Release swing = mid then forward.
	/// player_rod_* canvases are wide enough to keep the full rod on-screen.
	/// </summary>
	static string CastFrame( FishingController fishing )
	{
		if ( fishing is null || fishing.Charging || !fishing.Swinging )
			return "art/player_rod_charge";
		return fishing.CastSwingT < 0.16f ? "art/player_rod_swing" : "art/player_rod_release";
	}

	void SetDockVisible( bool on )
	{
		if ( _shopDock is not null ) _shopDock.GameObject.Enabled = on;
		foreach ( var (d, _, _) in _decor ) d.GameObject.Enabled = on;
	}

	/// <summary>
	/// True while any part of the shop/dock plate is still in the camera frustum.
	/// Uses sprite edges (not the center), so sailing right keeps the pier until its near end exits left.
	/// </summary>
	bool DockIntersectsView( float cam )
	{
		var halfW = ViewHalfWidth();
		var left = _shopDockWorldX - _shopDockHalfWidth - cam;
		var right = _shopDockWorldX + _shopDockHalfWidth - cam;
		return right > -halfW && left < halfW;
	}

	static float ViewHalfWidth()
	{
		var aspect = Screen.Width / Math.Max( 1f, Screen.Height );
		return OrthoHalfHeight * aspect;
	}

	void Place( SpriteActor actor, float x, float y, float z, float alpha )
	{
		if ( actor?.GameObject is null || !actor.GameObject.IsValid() ) return;
		actor.GameObject.WorldPosition = new Vector3( x, y, z );
		actor.Tint = new Color( 1, 1, 1, alpha );
	}

	void SyncList( List<SpriteActor> list, int count, string prefix, string path, float height )
	{
		while ( list.Count < count )
			list.Add( Fit( $"{prefix}{list.Count}", path, height, 0, 0, y: 7f ) );
		for ( var i = 0; i < list.Count; i++ )
			list[i].GameObject.Enabled = i < count;
	}

	static string SpriteForEvent( string kind ) => kind switch
	{
		"dolphin" => "env/dolphin",
		"turtle" => "env/turtle",
		"crate" => "env/crate",
		"buoy" => "env/buoy",
		"npc_boat" => "env/npc_boat",
		"cargo" => "env/cargo_ship",
		"lighthouse" => "env/lighthouse",
		"rig" => "env/oil_rig",
		"kelp" => "env/kelp",
		"birds" => "env/bird_0",
		_ => "env/buoy"
	};

	static float EventHeight( string kind ) => kind switch
	{
		"npc_boat" or "cargo" => 48f,
		"lighthouse" or "rig" => 64f,
		"birds" => 20f,
		_ => 28f
	};

	/// <summary>Depth Y: floaters with front water; distant hulls behind front crest.</summary>
	static float EventY( string kind ) => kind switch
	{
		"birds" or "stormfront" => 72f,
		"npc_boat" or "cargo" or "lighthouse" or "rig" => 28f,
		_ => 5f
	};

	/// <summary>Match boat/player ActorZNudge so props sit on the water, not in the sky.</summary>
	static float EventZ( string kind ) => kind switch
	{
		"birds" or "stormfront" => WaterlineZ + 120f,
		"dolphin" or "turtle" or "whale" => ActorZNudge - 24f,
		"kelp" => SeabedZ + 20f,
		"lighthouse" or "rig" => ActorZNudge + 36f,
		_ => ActorZNudge + 8f
	};

	void AddSeabed( string path, float height, float worldX, float z )
	{
		var a = Fit( path, path, height, worldX, z, y: SeabedY );
		_decor.Add( (a, worldX, z) );
	}

	SpriteActor Fit( string name, string path, float height, float worldX, float z, float y )
	{
		var go = Create( name, new Vector3( worldX, y, z ) );
		var actor = go.AddComponent<SpriteActor>();
		actor.SetFitHeight( path, height );
		return actor;
	}

	SpriteActor FitParallax( string name, string path, float height, float worldX, float z, float y, float parallax )
	{
		var actor = Fit( name, path, height, worldX, z, y );
		_layered.Add( (actor, worldX, parallax) );
		return actor;
	}

	GameObject Create( string name, Vector3 pos )
	{
		var go = _scene.CreateObject();
		go.Name = name;
		go.WorldPosition = pos;
		_owned.Add( go );
		return go;
	}
}
