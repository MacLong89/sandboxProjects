namespace SkyEmpire;

/// <summary>
/// Networked sky baron. Movement is owner-authoritative; the tycoon itself is
/// local progression, with the purchase set and rebirth count synced so
/// everyone renders everyone's island via <see cref="PlotVisual"/>.
/// </summary>
public sealed class TycoonPlayer : Component
{
	public static TycoonPlayer Local { get; private set; }

	[Sync] public string DisplayName { get; set; } = "Sky Baron";
	[Sync] public int PlotIndex { get; set; }
	[Sync] public string PurchasedCsv { get; set; } = "";
	[Sync] public int Rebirths { get; set; }
	[Sync] public double SessionEarned { get; set; }
	[Sync] public Color OutfitColor { get; set; } = Color.Cyan;

	public Angles EyeAngles { get; set; }

	CharacterController _controller;
	GameObject _bodyRoot;
	GameObject _plotGo;
	TextRenderer _nameplate;
	Color _builtOutfit;
	int _pushedStamp = -1;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.Height = 64f;
		_controller.Radius = 14f;
	}

	protected override void OnStart()
	{
		BuildBody();

		// Every client renders this player's island locally.
		_plotGo = new GameObject( true, $"Plot_{DisplayName}" );
		var visual = _plotGo.Components.Create<PlotVisual>();
		visual.Player = this;

		if ( !IsProxy )
		{
			Local = this;
			DisplayName = Connection.Local?.DisplayName ?? "Sky Baron";
			PushFromProgress();
		}
	}

	protected override void OnDestroy()
	{
		_plotGo?.Destroy();
		if ( Local == this ) Local = null;
	}

	protected override void OnUpdate()
	{
		BuildBody();
		UpdateNameplate();

		if ( IsProxy ) return;

		PushFromProgress();

		if ( WorldPosition.z < Balance.FallRespawnZ )
		{
			RespawnAtPlot();
			Effects.FloatText( WorldPosition + Vector3.Up * 100f, "Whoops! The clouds caught you.", new Color( 0.8f, 0.9f, 1f ) );
		}

		if ( TycoonGame.Instance?.IsUiOpen == true ) return;

		HandleLook();
		HandleMovement();
	}

	void PushFromProgress()
	{
		var progress = PlayerProgress.Local;
		if ( progress is null ) return;

		Rebirths = progress.Data.Rebirths;
		SessionEarned = progress.SessionEarned;

		if ( _pushedStamp != progress.PurchaseStamp )
		{
			_pushedStamp = progress.PurchaseStamp;
			PurchasedCsv = string.Join( ",", progress.Data.Purchased );
		}
	}

	public void RespawnAtPlot()
	{
		WorldPosition = WorldBuilder.PlotSpawn( PlotIndex );
		_controller.Velocity = Vector3.Zero;
	}

	// ---------------- Visuals ----------------

	void BuildBody()
	{
		if ( _bodyRoot.IsValid() && _builtOutfit == OutfitColor ) return;
		_bodyRoot?.Destroy();
		_builtOutfit = OutfitColor;

		_bodyRoot = new GameObject( true, "BaronBody" );
		_bodyRoot.SetParent( GameObject );
		_bodyRoot.LocalPosition = Vector3.Zero;

		var outfit = OutfitColor;
		Kit.Sphere( _bodyRoot, "Torso", new Vector3( 0, 0, 30f ), new Vector3( 34f, 30f, 42f ), outfit );
		Kit.Sphere( _bodyRoot, "Head", new Vector3( 0, 0, 62f ), new Vector3( 26f, 25f, 25f ), new Color( 1f, 0.87f, 0.72f ) );
		Kit.BoxCentered( _bodyRoot, "HatBrim", new Vector3( 0, 0, 72f ), new Vector3( 34f, 34f, 4f ), outfit.Darken( 0.25f ) );
		Kit.BoxCentered( _bodyRoot, "HatTop", new Vector3( 0, 0, 84f ), new Vector3( 20f, 20f, 22f ), outfit.Darken( 0.25f ) );
		Kit.Sphere( _bodyRoot, "FootL", new Vector3( 2f, -9f, 5f ), new Vector3( 13f, 10f, 9f ), outfit.Darken( 0.3f ) );
		Kit.Sphere( _bodyRoot, "FootR", new Vector3( 2f, 9f, 5f ), new Vector3( 13f, 10f, 9f ), outfit.Darken( 0.3f ) );

		if ( !_nameplate.IsValid() )
		{
			var plateGo = new GameObject( true, "Nameplate" );
			plateGo.SetParent( GameObject );
			plateGo.LocalPosition = Vector3.Up * 108f;
			_nameplate = plateGo.Components.Create<TextRenderer>();
			_nameplate.Scale = 0.16f;
			_nameplate.FontSize = 34;
			_nameplate.Billboard = TextRenderer.BillboardMode.YOnly;
			_nameplate.HorizontalAlignment = TextRenderer.HAlignment.Center;
		}
	}

	void UpdateNameplate()
	{
		if ( !_nameplate.IsValid() ) return;
		var isLocal = !IsProxy;
		_nameplate.GameObject.Enabled = !isLocal;
		if ( isLocal ) return;

		var badge = Rebirths > 0 ? $"[{Rebirths}] " : "";
		_nameplate.Text = $"{badge}{DisplayName}";
		_nameplate.Color = new Color( 1f, 0.96f, 0.75f );
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

	void HandleMovement()
	{
		var wish = Input.AnalogMove;
		var rot = Rotation.FromYaw( EyeAngles.yaw );
		var wishDir = (rot * wish).WithZ( 0f );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;

		if ( wishDir.Length > 0.05f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( wishDir ), Time.Delta * 10f );

		if ( _controller.IsOnGround )
		{
			_controller.Velocity = _controller.Velocity.WithZ( 0f );
			_controller.Accelerate( wishDir * Balance.WalkSpeed );
			_controller.ApplyFriction( 4f, 0f );

			if ( Input.Pressed( "jump" ) )
				_controller.Punch( Vector3.Up * Balance.JumpPower );
		}
		else
		{
			_controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
			_controller.Accelerate( wishDir * Balance.WalkSpeed * 0.3f );
		}

		_controller.Move();
	}
}
