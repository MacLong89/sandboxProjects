using Sandbox.Citizen;

namespace CatchACritter;

/// <summary>
/// Networked keeper. Movement and catching are owner-authoritative; progression
/// lives in the owner's local <see cref="PlayerProgress"/>. A few display stats
/// sync so lobby-mates see your net, crowns, and followers.
/// </summary>
public sealed class CritterPlayer : Component
{
	public static CritterPlayer Local { get; private set; }

	[Sync] public string DisplayName { get; set; } = "Keeper";
	[Sync] public int NetPower { get; set; }
	[Sync] public int Crowns { get; set; }
	[Sync] public int SessionCatches { get; set; }
	[Sync] public bool IsSneaking { get; set; }
	[Sync] public float SpawnLuck { get; set; }
	[Sync] public string FollowersCsv { get; set; } = "";
	[Sync] public TimeSince SwungAgo { get; set; } = 100f;

	// Synced movement state so every client can drive the citizen animgraph.
	[Sync] public Vector3 Velocity { get; set; }
	[Sync] public bool Grounded { get; set; } = true;

	public Angles EyeAngles { get; set; }
	public TimeSince LastCatchAt { get; private set; } = 100f;

	CharacterController _controller;
	GameObject _bodyRoot;
	SkinnedModelRenderer _body;
	CitizenAnimationHelper _anim;
	GameObject _netRoot;
	TextRenderer _nameplate;
	TimeUntil _swingReady;
	string _builtFollowers = null;
	readonly List<GameObject> _followerVisuals = new();
	readonly List<(SkinnedModelRenderer Renderer, string SeqPrefix, string Playing)> _followerGaits = new();
	int _builtNetPower = -1;
	float _prevSwungAgo = 100f;
	bool _wasGrounded = true;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.Height = 72f;
		_controller.Radius = 14f;
	}

	protected override void OnStart()
	{
		BuildBody();

		if ( !IsProxy )
		{
			Local = this;
			DisplayName = Connection.Local?.DisplayName ?? "Keeper";
			var progress = PlayerProgress.Local;
			if ( progress is not null )
				PushStatsFromProgress( progress );
		}
	}

	protected override void OnDestroy()
	{
		foreach ( var f in _followerVisuals ) f?.Destroy();
		_followerVisuals.Clear();
		if ( Local == this ) Local = null;
	}

	protected override void OnUpdate()
	{
		BuildBody();
		UpdateAnimation();
		UpdateNameplate();
		UpdateNetVisual();
		UpdateFollowers();

		if ( IsProxy ) return;

		var progress = PlayerProgress.Local;
		if ( progress is not null )
			PushStatsFromProgress( progress );

		if ( CritterGame.Instance?.IsUiOpen == true )
		{
			IsSneaking = false;
			return;
		}

		HandleLook();
		HandleMovement( progress );
		HandleSwing( progress );
		HandleUse( progress );
	}

	void PushStatsFromProgress( PlayerProgress p )
	{
		NetPower = p.NetPower;
		Crowns = p.Data.Crowns;
		SessionCatches = p.SessionCatches;
		SpawnLuck = p.Luck01;
		FollowersCsv = p.FollowerCsv();
	}

	// ---------------- Visuals ----------------

	void BuildBody()
	{
		if ( _bodyRoot.IsValid() ) return;

		_bodyRoot = new GameObject( true, "KeeperBody" );
		_bodyRoot.SetParent( GameObject );
		_bodyRoot.LocalPosition = Vector3.Zero;
		_bodyRoot.LocalRotation = Rotation.Identity;

		_body = _bodyRoot.Components.Create<SkinnedModelRenderer>();
		_body.Model = Model.Load( "models/citizen/citizen.vmdl" );

		_anim = _bodyRoot.Components.Create<CitizenAnimationHelper>();
		_anim.Target = _body;
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Swing;

		DressFromAvatar();

		if ( !_nameplate.IsValid() )
		{
			var plateGo = new GameObject( true, "Nameplate" );
			plateGo.SetParent( GameObject );
			plateGo.LocalPosition = Vector3.Up * 96f;
			_nameplate = plateGo.Components.Create<TextRenderer>();
			_nameplate.Scale = 0.16f;
			_nameplate.FontSize = 34;
			_nameplate.Billboard = TextRenderer.BillboardMode.YOnly;
			_nameplate.HorizontalAlignment = TextRenderer.HAlignment.Center;
		}
	}

	/// <summary>Dress the citizen in the owning player's avatar clothing.</summary>
	void DressFromAvatar()
	{
		try
		{
			var owner = Network.Owner ?? Connection.Local;
			if ( owner is null ) return;
			var clothing = ClothingContainer.CreateFromConnection( owner );
			clothing?.Apply( _body );
		}
		catch
		{
			// No avatar data (offline / bots) — bare citizen is fine.
		}
	}

	/// <summary>Drives the citizen animgraph on every client from synced movement state.</summary>
	void UpdateAnimation()
	{
		if ( !_anim.IsValid() || !_body.IsValid() ) return;

		_anim.WithVelocity( Velocity );
		_anim.WithWishVelocity( Velocity );
		_anim.IsGrounded = Grounded;
		_anim.DuckLevel = _anim.DuckLevel.LerpTo( IsSneaking ? 0.7f : 0f, Time.Delta * 12f );

		// Leaving the ground with upward speed reads as a jump for everyone.
		if ( _wasGrounded && !Grounded && Velocity.z > 80f )
			_anim.TriggerJump();
		_wasGrounded = Grounded;

		// Swing animation on all clients when the synced swing timer resets.
		if ( SwungAgo < _prevSwungAgo )
			_body.Set( "b_attack", true );
		_prevSwungAgo = SwungAgo;
	}

	void UpdateNetVisual()
	{
		if ( _builtNetPower != NetPower || !_netRoot.IsValid() )
		{
			_netRoot?.Destroy();
			_builtNetPower = NetPower;

			var net = NetCatalog.Get( NetPower );
			_netRoot = new GameObject( true, "Net" );
			_netRoot.SetParent( GameObject );
			Kit.BoxCentered( _netRoot, "Pole", new Vector3( 20f, 0, 0 ), new Vector3( 44f, 3.5f, 3.5f ), new Color( 0.55f, 0.42f, 0.3f ) );
			Kit.Sphere( _netRoot, "Hoop", new Vector3( 48f, 0, 0 ), new Vector3( 26f, 26f, 6f ), net.Color );
			Kit.Sphere( _netRoot, "Mesh", new Vector3( 48f, 0, -3f ), new Vector3( 20f, 20f, 8f ), net.Color.WithAlpha( 0.55f ) );
		}

		// Held in the citizen's right hand; the animgraph swings the arm for us.
		var hand = _body.IsValid() ? _body.GetAttachment( "hand_R" ) : null;
		if ( hand.HasValue )
		{
			_netRoot.WorldPosition = hand.Value.Position;
			_netRoot.WorldRotation = hand.Value.Rotation;
		}
	}

	void UpdateNameplate()
	{
		if ( !_nameplate.IsValid() ) return;
		var isLocal = !IsProxy;
		_nameplate.GameObject.Enabled = !isLocal;
		if ( isLocal ) return;

		var crowns = Crowns > 0 ? $"[{Crowns}] " : "";
		_nameplate.Text = $"{crowns}{DisplayName}";
		_nameplate.Color = new Color( 1f, 0.96f, 0.75f );
	}

	void UpdateFollowers()
	{
		var csv = FollowersCsv ?? "";
		if ( _builtFollowers != csv )
		{
			_builtFollowers = csv;
			foreach ( var f in _followerVisuals ) f?.Destroy();
			_followerVisuals.Clear();
			_followerGaits.Clear();

			foreach ( var token in csv.Split( ',', StringSplitOptions.RemoveEmptyEntries ) )
			{
				var parts = token.Split( ':' );
				var def = SpeciesCatalog.Get( parts[0] );
				if ( def is null ) continue;
				var shiny = parts.Length > 1 && parts[1] == "1";

				var follower = new GameObject( true, $"Follower_{def.Id}" );
				follower.WorldPosition = WorldPosition;
				CritterBody.Build( follower, def, shiny, 0.62f );
				_followerVisuals.Add( follower );
				_followerGaits.Add( (
					follower.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndChildren ),
					SpeciesCatalog.SkinFor( def.Id )?.SeqPrefix,
					null) );
			}
		}

		// Trail behind the player in a lazy conga line.
		for ( int i = 0; i < _followerVisuals.Count; i++ )
		{
			var f = _followerVisuals[i];
			if ( !f.IsValid() ) continue;
			var behind = WorldPosition - WorldRotation.Forward * (65f + i * 55f) + WorldRotation.Left * MathF.Sin( i * 2.1f ) * 30f;
			behind.z = WorldPosition.z + MathF.Abs( MathF.Sin( Time.Now * 6f + i ) ) * 5f;
			f.WorldPosition = f.WorldPosition.LerpTo( behind, Time.Delta * 4.5f );
			var vel = behind - f.WorldPosition;
			if ( vel.WithZ( 0 ).Length > 4f )
				f.WorldRotation = Rotation.Lerp( f.WorldRotation, Rotation.LookAt( vel.WithZ( 0 ) ), Time.Delta * 5f );

			// Modeled followers swap between idle and walk with the conga line.
			var (renderer, prefix, playing) = _followerGaits[i];
			if ( renderer.IsValid() && prefix is not null )
			{
				var seq = $"{prefix}{(vel.WithZ( 0 ).Length > 6f ? "_walk" : "_idle")}";
				if ( seq != playing )
				{
					renderer.Sequence.Name = seq;
					renderer.Sequence.Looping = true;
					_followerGaits[i] = (renderer, prefix, seq);
				}
			}
		}
	}

	// ---------------- Input ----------------

	void HandleLook()
	{
		var angles = EyeAngles;
		angles += Input.AnalogLook;
		angles.pitch = angles.pitch.Clamp( -35f, 70f );
		angles.roll = 0f;
		EyeAngles = angles;
	}

	void HandleMovement( PlayerProgress progress )
	{
		IsSneaking = Input.Down( "duck" );

		var speed = progress?.MoveSpeed ?? Balance.BaseWalkSpeed;
		if ( IsSneaking ) speed = Balance.SneakSpeed;

		var wish = Input.AnalogMove;
		var rot = Rotation.FromYaw( EyeAngles.yaw );
		var wishDir = (rot * wish).WithZ( 0f );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;

		if ( wishDir.Length > 0.05f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( wishDir ), Time.Delta * 10f );

		if ( _controller.IsOnGround )
		{
			_controller.Velocity = _controller.Velocity.WithZ( 0f );
			_controller.Accelerate( wishDir * speed );
			_controller.ApplyFriction( 4f, 0f );

			if ( Input.Pressed( "jump" ) )
				_controller.Punch( Vector3.Up * 300f );
		}
		else
		{
			_controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
			_controller.Accelerate( wishDir * speed * 0.25f );
		}

		_controller.Move();

		// Share movement state so proxies animate correctly.
		Velocity = _controller.Velocity;
		Grounded = _controller.IsOnGround;

		// Safety net — never fall through the world.
		if ( WorldPosition.z < -200f )
			WorldPosition = Vector3.Up * 10f;
	}

	void HandleSwing( PlayerProgress progress )
	{
		if ( progress is null ) return;
		if ( !Input.Pressed( "attack1" ) || !_swingReady ) return;

		_swingReady = progress.SwingCooldown;
		SwungAgo = 0f;
		Sfx.Play( "swing", WorldPosition );

		var radius = progress.CatchRadius;
		var center = WorldPosition + WorldRotation.Forward * radius * 0.55f;

		var caughtAny = false;
		var blockedByPower = false;
		foreach ( var critter in Scene.GetAllComponents<CritterAgent>() )
		{
			if ( critter.Caught || critter.Def is null ) continue;
			if ( Vector3.DistanceBetween( critter.WorldPosition, center ) > radius ) continue;

			if ( progress.NetPower < critter.Def.RequiredNetPower )
			{
				blockedByPower = true;
				continue;
			}

			if ( progress.BackpackCount >= progress.BackpackCapacity )
			{
				CatchEffects.FloatText( WorldPosition + Vector3.Up * 90f, "Backpack full! Sell at the hub", new Color( 1f, 0.6f, 0.5f ) );
				return;
			}

			critter.RequestCatch( Connection.Local.Id, progress.NetPower );
			caughtAny = true;
		}

		if ( !caughtAny && blockedByPower )
			CatchEffects.FloatText( WorldPosition + Vector3.Up * 90f, "Need a stronger net!", new Color( 1f, 0.75f, 0.4f ) );
	}

	void HandleUse( PlayerProgress progress )
	{
		if ( progress is null ) return;
		if ( !Input.Pressed( "use" ) ) return;

		// Gate unlocks first — they sit between zones.
		var gate = GateController.Nearest( WorldPosition, 260f );
		if ( gate is not null )
		{
			progress.TryUnlockZone( gate.Target );
			return;
		}

		var station = NearestStation( 170f );
		if ( station is null ) return;

		switch ( station.Kind )
		{
			case StationKind.Sell:
				progress.SellBackpack();
				break;
			case StationKind.Nest:
				CritterGame.Instance?.OpenMenu( MenuTab.Sanctuary );
				break;
			case StationKind.Ascend:
				CritterGame.Instance?.OpenMenu( MenuTab.Ascend );
				break;
			case StationKind.Board:
				CritterGame.Instance?.OpenMenu( MenuTab.Daily );
				break;
		}
	}

	public Station NearestStation( float range )
	{
		Station best = null;
		var bestDist = range;
		foreach ( var s in Scene.GetAllComponents<Station>() )
		{
			var d = Vector3.DistanceBetween( s.WorldPosition, WorldPosition );
			if ( d < bestDist ) { bestDist = d; best = s; }
		}
		return best;
	}

	public void RecordCatch() => LastCatchAt = 0f;
}
