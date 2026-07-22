namespace NoFly;

public sealed class PlayerScore
{
	public int Total { get; set; }
	public int Objectives { get; set; }
	public int Inspection { get; set; }
	public int Deception { get; set; }
	public int Security { get; set; }
	public int Reports { get; set; }
	public int Boarding { get; set; }
}

/// <summary>
/// Core networked player identity and role state for NO FLY.
/// </summary>
public sealed class NoFlyPlayer : Component
{
	[Sync( SyncFlags.FromHost )] public string PlayerId { get; set; }
	[Sync( SyncFlags.FromHost )] public string DisplayName { get; set; }
	[Sync( SyncFlags.FromHost )] public RoleType Role { get; set; } = RoleType.None;
	[Sync( SyncFlags.FromHost )] public TeamType Team { get; set; } = TeamType.None;
	[Sync( SyncFlags.FromHost )] public RolePreference Preference { get; set; } = RolePreference.NoPreference;
	[Sync( SyncFlags.FromHost )] public bool IsReady { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsBot { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsSpectator { get; set; }
	[Sync( SyncFlags.FromHost )] public PassengerFlowState FlowState { get; set; } = PassengerFlowState.EnteringAirport;
	[Sync( SyncFlags.FromHost )] public string FlightNumber { get; set; }
	[Sync( SyncFlags.FromHost )] public string AssignedGate { get; set; }
	[Sync( SyncFlags.FromHost )] public bool DocumentApproved { get; set; }
	[Sync( SyncFlags.FromHost )] public bool BagCleared { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsFlagged { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsDetained { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsArrested { get; set; }
	[Sync( SyncFlags.FromHost )] public bool HasBoarded { get; set; }
	[Sync( SyncFlags.FromHost )] public bool MissedFlight { get; set; }
	[Sync( SyncFlags.FromHost )] public bool ForgeryComplete { get; set; }
	[Sync( SyncFlags.FromHost )] public bool HideComplete { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Exposed { get; set; }
	[Sync( SyncFlags.FromHost )] public bool UndercoverExposed { get; set; }
	[Sync( SyncFlags.FromHost )] public bool AppearanceHasHat { get; set; }
	[Sync( SyncFlags.FromHost )] public int ScoreTotal { get; set; }
	[Sync( SyncFlags.FromHost )] public string MarkedSuspectId { get; set; }
	[Sync( SyncFlags.FromHost )] public bool ArrestAvailable { get; set; } = true;
	/// <summary>Staff have pressed E / checked in at their assigned desk.</summary>
	[Sync( SyncFlags.FromHost )] public bool AtStation { get; set; }
	[Sync( SyncFlags.FromHost )] public Color OutfitColor { get; set; } = Color.White;
	[Sync( SyncFlags.FromHost )] public string ActivePrompt { get; set; }
	[Sync( SyncFlags.FromHost )] public string ObjectiveSummary { get; set; }
	[Sync( SyncFlags.FromHost )] public string DocumentJson { get; set; }
	[Sync( SyncFlags.FromHost )] public string BagJson { get; set; }
	[Sync( SyncFlags.FromHost )] public string ObjectivesJson { get; set; }
	[Sync( SyncFlags.FromHost )] public string CluesJson { get; set; }

	public DocumentInstance Document
	{
		get => DocNet.FromJson( DocumentJson );
		set
		{
			if ( Networking.IsHost )
				DocumentJson = DocNet.ToJson( value );
		}
	}

	public BagInstance Bag
	{
		get => BagNet.FromJson( BagJson );
		set
		{
			if ( Networking.IsHost )
				BagJson = BagNet.ToJson( value );
		}
	}

	public List<PlayerObjective> Objectives
	{
		get => ObjectiveNet.FromJson( ObjectivesJson );
		set
		{
			if ( Networking.IsHost )
				ObjectivesJson = ObjectiveNet.ToJson( value ?? new() );
		}
	}

	public List<string> Clues
	{
		get => string.IsNullOrEmpty( CluesJson ) ? new() : CluesJson.Split( "||", StringSplitOptions.RemoveEmptyEntries ).ToList();
		set
		{
			if ( Networking.IsHost )
				CluesJson = string.Join( "||", value ?? new() );
		}
	}

	public void SyncLoadout()
	{
		// JSON already written by setters when assigned on host.
	}

	public PlayerScore Score { get; set; } = new();
	public TimeSince TimeSinceReport { get; set; } = 1000f;

	public bool IsPassengerSide => Team == TeamType.Passenger || Role == RoleType.UndercoverAgent || Role == RoleType.Smuggler || Role == RoleType.RegularPassenger;
	public bool NeedsSecurityCheck => Role is RoleType.RegularPassenger or RoleType.Smuggler or RoleType.UndercoverAgent;
	public bool CanBoard => DocumentApproved && BagCleared && !IsArrested && !IsDetained && !MissedFlight;

	public RoleInfo RoleInfo => RoleCatalog.Get( Role );

	/// <summary>Synced so remote clients can drive citizen walk/run cycles.</summary>
	[Sync] public Vector3 AnimVelocity { get; set; }
	[Sync] public bool AnimGrounded { get; set; } = true;

	CharacterController _controller;
	CitizenAvatar _avatar;
	TextRenderer _nameplate;
	Vector3 _velocity;

	public Vector3 EyePosition => WorldPosition + Vector3.Up * 64f;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.Height = 72f;
		_controller.Radius = 16f;
		GameObject.Tags.Add( "player" );
	}

	protected override void OnStart()
	{
		EnsureVisuals();
		if ( string.IsNullOrEmpty( PlayerId ) )
			PlayerId = Guid.NewGuid().ToString( "N" )[..8];
		if ( string.IsNullOrEmpty( DisplayName ) )
			DisplayName = IsBot ? $"Traveler {PlayerId[..4]}" : (Network.Owner?.DisplayName ?? "Player");
	}

	protected override void OnUpdate()
	{
		EnsureVisuals();
		UpdateAnimation();
		UpdateNameplate();

		if ( IsProxy || IsBot || IsSpectator || IsArrested )
			return;

		HandleMovement();
		HandleLook();
	}

	void EnsureVisuals()
	{
		if ( !_avatar.IsValid() )
			_avatar = Components.GetOrCreate<CitizenAvatar>();

		var dress = IsBot ? null : (Network.Owner ?? Connection.Local);
		_avatar.Ensure( OutfitColor, dress );
		_avatar.SetLocalFirstPerson( !IsProxy && !IsBot );

		if ( _nameplate.IsValid() ) return;

		var plate = new GameObject( true, "Nameplate" );
		plate.SetParent( GameObject );
		plate.LocalPosition = Vector3.Up * 88f;
		_nameplate = plate.Components.Create<TextRenderer>();
		_nameplate.FontSize = 48;
		_nameplate.Scale = 8f / 48f;
		_nameplate.Billboard = TextRenderer.BillboardMode.YOnly;
		_nameplate.HorizontalAlignment = TextRenderer.HAlignment.Center;
		_nameplate.VerticalAlignment = TextRenderer.VAlignment.Center;
	}

	public void ApplyAppearance()
	{
		EnsureVisuals();
		var tint = Role switch
		{
			RoleType.DocumentAgent => new Color( 0.95f, 0.85f, 0.35f ),
			RoleType.ScannerAgent => new Color( 0.35f, 0.85f, 0.75f ),
			RoleType.SecurityOfficer => new Color( 0.25f, 0.35f, 0.55f ),
			_ => OutfitColor
		};
		_avatar?.SetTint( tint );
	}

	void UpdateAnimation()
	{
		if ( !_avatar.IsValid() ) return;

		if ( !IsProxy && !IsBot && _controller.IsValid() )
		{
			AnimVelocity = _controller.Velocity;
			AnimGrounded = _controller.IsOnGround;
		}

		_avatar.Tick( AnimVelocity, AnimGrounded, EyeAngles );
	}

	void UpdateNameplate()
	{
		if ( !_nameplate.IsValid() ) return;

		// Never draw your own nameplate; others only at close range.
		var isLocal = !IsProxy && !IsBot;
		var local = NoFlyGame.LocalPlayer;
		var dist = local.IsValid() ? Vector3.DistanceBetween( WorldPosition, local.WorldPosition ) : 9999f;
		var show = !isLocal && dist < 220f;
		_nameplate.GameObject.Enabled = show;
		if ( show )
		{
			_nameplate.Text = DisplayName;
			_nameplate.Color = IsBot ? new Color( 0.8f, 0.84f, 0.9f ) : new Color( 1f, 0.9f, 0.35f );
		}
	}

	void HandleLook()
	{
		// Cursor-visible menus/minigames zero AnalogLook; don't fight the UI.
		if ( Mouse.Visibility == MouseVisibility.Visible )
			return;

		var angles = EyeAngles;
		angles += Input.AnalogLook;
		angles.pitch = angles.pitch.Clamp( -80f, 80f );
		angles.roll = 0f;
		EyeAngles = angles;
		WorldRotation = Rotation.FromYaw( angles.yaw );
	}

	public Angles EyeAngles { get; set; }

	void HandleMovement()
	{
		if ( NoFlyGame.Instance is { } game && game.BlocksPlayerMovement )
			return;

		var wish = Input.AnalogMove;
		var speed = Input.Down( "run" ) ? 280f : 160f;
		if ( IsDetained ) speed *= 0.35f;
		if ( Exposed && Role == RoleType.Smuggler ) speed = 320f;

		var wishDir = (WorldRotation * wish).WithZ( 0f );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;

		if ( _controller.IsValid() )
		{
			if ( _controller.IsOnGround )
			{
				_controller.Accelerate( wishDir * speed );
				_controller.ApplyFriction( 4f, 0f );
				if ( Input.Pressed( "jump" ) )
					_controller.Punch( Vector3.Up * 320f );
			}
			else
			{
				_controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
				_controller.Accelerate( wishDir * speed * 0.25f );
			}

			_controller.Move();
			AnimVelocity = _controller.Velocity;
			AnimGrounded = _controller.IsOnGround;
			return;
		}

		// Fallback if CharacterController unavailable
		_velocity = wishDir * speed;
		WorldPosition += _velocity * Time.Delta;
		AnimVelocity = _velocity;
		AnimGrounded = true;
	}

	public void AddScore( int amount, string category = null )
	{
		if ( !Networking.IsHost ) return;
		Score.Total += amount;
		ScoreTotal = Score.Total;
		switch ( category )
		{
			case "objectives": Score.Objectives += amount; break;
			case "inspection": Score.Inspection += amount; break;
			case "deception": Score.Deception += amount; break;
			case "security": Score.Security += amount; break;
			case "reports": Score.Reports += amount; break;
			case "boarding": Score.Boarding += amount; break;
		}
	}

	public void ResetForRound()
	{
		FlowState = PassengerFlowState.EnteringAirport;
		DocumentApproved = false;
		BagCleared = false;
		IsFlagged = false;
		IsDetained = false;
		IsArrested = false;
		HasBoarded = false;
		MissedFlight = false;
		ForgeryComplete = false;
		HideComplete = false;
		Exposed = false;
		UndercoverExposed = false;
		MarkedSuspectId = null;
		ArrestAvailable = true;
		AtStation = false;
		Score = new PlayerScore();
		ScoreTotal = 0;
		Objectives.Clear();
		Clues.Clear();
		Document = null;
		Bag = null;
		ActivePrompt = null;
		ObjectiveSummary = null;
		DocumentJson = null;
		BagJson = null;
		ObjectivesJson = null;
		CluesJson = null;
	}

	[Rpc.Host]
	public void RpcSetReady( bool ready )
	{
		IsReady = ready;
	}

	[Rpc.Host]
	public void RpcSetPreference( RolePreference pref )
	{
		Preference = pref;
	}
}
