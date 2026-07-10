namespace ThinkDrink;

using ThinkDrink.UI;

/// <summary>Local walk + look for contestants on the studio floor.</summary>
public sealed class PlayerPawn : Component
{
	[Property] public float WalkSpeed { get; set; } = 280f;
	[Property] public float RunSpeed { get; set; } = 420f;
	[Property] public float EyeHeight { get; set; } = 64f;
	[Property] public float FieldOfView { get; set; } = 75f;

	static Model _citizenModel;
	static Model _boxModel;

	PlayerController _controller;
	GameObject _view;
	GameObject _remoteVisual;
	SkinnedModelRenderer _remoteSkinnedRenderer;
	ModelRenderer _remoteFallbackRenderer;
	CameraComponent _camera;
	Angles _look;
	bool _cameraActive;
	bool _spawned;

	public bool IsLocalOwner
	{
		get
		{
			var player = Components.Get<ThinkDrinkPlayer>();
			var local = ThinkDrinkPlayer.Local;
			return player.IsValid() && local.IsValid()
				? player == local
				: !IsProxy;
		}
	}

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<PlayerController>();
		_controller.UseCameraControls = false;
		_controller.UseLookControls = false;
		_controller.UseAnimatorControls = false;
		_controller.EnablePressing = false;
		_controller.BodyHeight = 72f;
		_controller.BodyRadius = 14f;
		_controller.WalkSpeed = WalkSpeed;
		_controller.RunSpeed = RunSpeed;

		_view = new GameObject( true, "View" );
		_view.Parent = GameObject;
		_view.LocalPosition = new Vector3( 0f, 0f, EyeHeight );

		_camera = _view.Components.Create<CameraComponent>();
		_camera.Enabled = false;
		_camera.IsMainCamera = false;
		_camera.FieldOfView = FieldOfView;
		_camera.BackgroundColor = new Color( 0.06f, 0.05f, 0.10f );
		_camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
		_camera.ZNear = 5f;
		_camera.ZFar = 10000f;
	}

	protected override void OnStart()
	{
		if ( !IsLocalOwner )
		{
			CreateRemotePlayerVisual();
			TrySpawnAtStudio();
			return;
		}

		ActivateLocalCamera();
		UpdateInputMode();
		TrySpawnAtStudio();
	}

	protected override void OnDestroy()
	{
		if ( IsLocalOwner )
			Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocalOwner )
		{
			if ( !_spawned )
				TrySpawnAtStudio();
			UpdateRemotePlayerVisual();
			return;
		}

		if ( !_controller.IsValid() ) return;

		if ( !_spawned )
			TrySpawnAtStudio();

		UpdateInputMode();

		if ( WantsMouseLook() )
		{
			var look = Input.AnalogLook;
			look.pitch = -look.pitch;
			_look += look;
			_look.pitch = Math.Clamp( _look.pitch, -80f, 80f );
			_look.roll = 0f;
			ApplyLook();
		}

		var allowMove = WantsMovement();
		_controller.UseInputControls = allowMove;
		if ( !allowMove )
			_controller.WishVelocity = Vector3.Zero;

		if ( _cameraActive )
			StudioCamera.Instance?.SetSuppressed( true );
	}

	public Vector3 GetEyeWorldPosition() =>
		_view.IsValid() ? _view.WorldPosition : GameObject.WorldPosition + Vector3.Up * EyeHeight;

	public Vector3 GetEyeForward() =>
		_view.IsValid() ? _view.WorldRotation.Forward : GameObject.WorldRotation.Forward;

	public Angles GetLookAngles() => _look;

	void ApplyLook()
	{
		_controller.EyeAngles = _look;
		GameObject.WorldRotation = Rotation.From( 0f, _look.yaw, 0f );
		_view.LocalRotation = Rotation.FromAxis( Vector3.Right, _look.pitch );
	}

	void UpdateInputMode()
	{
		Mouse.Visibility = WantsMouseCursor() ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	static bool WantsMouseCursor() => UIManager.WantsMouseCursor();

	static bool WantsMouseLook() => !WantsMouseCursor();

	static bool WantsMovement() => UIManager.WantsMovement();

	void TrySpawnAtStudio()
	{
		var env = StudioEnvironment.Instance;
		if ( env is null || env.GetContestantSpot( 0 ) is null ) return;

		var player = Components.Get<ThinkDrinkPlayer>();
		if ( player is null ) return;

		var index = env.GetContestantIndex( player );
		var spot = env.GetContestantSpot( index );
		if ( spot is null ) return;

		GameObject.WorldPosition = spot.Value.Position;
		GameObject.WorldRotation = spot.Value.Rotation;
		_look = spot.Value.Rotation.Angles();
		ApplyLook();
		_controller.WishVelocity = Vector3.Zero;
		if ( _controller.Body.IsValid() )
			_controller.Body.Velocity = Vector3.Zero;

		_spawned = true;

		Log.Info(
			$"[ThinkDrink][PlayerPawn] spawned spot={index} pos={GameObject.WorldPosition} eye={GetEyeWorldPosition()} " +
			$"look=({_look.pitch:0.#},{_look.yaw:0.#}) board={env.ScoreboardWorldPosition}" );
	}

	void CreateRemotePlayerVisual()
	{
		if ( _remoteVisual.IsValid() ) return;

		_remoteVisual = new GameObject( true, "Remote Player Model" );
		_remoteVisual.Parent = GameObject;
		_remoteVisual.LocalPosition = Vector3.Zero;
		_remoteVisual.LocalRotation = Rotation.Identity;
		_remoteVisual.LocalScale = Vector3.One;

		var citizen = GetCitizenModel();
		if ( citizen.IsValid() )
		{
			_remoteSkinnedRenderer = _remoteVisual.Components.Create<SkinnedModelRenderer>();
			_remoteSkinnedRenderer.Model = citizen;
			_remoteSkinnedRenderer.UseAnimGraph = true;
			_remoteSkinnedRenderer.PlaybackRate = 1f;
		}
		else
		{
			_remoteFallbackRenderer = _remoteVisual.Components.Create<ModelRenderer>();
			_remoteFallbackRenderer.Model = GetBoxModel();
			_remoteVisual.LocalPosition = new Vector3( 0f, 0f, 36f );
			_remoteVisual.LocalScale = new Vector3( 22f, 22f, 72f );
			_remoteFallbackRenderer.Tint = new Color( 0.35f, 0.78f, 1f );
		}

		UpdateRemotePlayerVisual();
	}

	void UpdateRemotePlayerVisual()
	{
		if ( !_remoteVisual.IsValid() )
			CreateRemotePlayerVisual();

		if ( _remoteSkinnedRenderer.IsValid() )
		{
			_remoteSkinnedRenderer.Enabled = !IsLocalOwner;
			_controller?.UpdateAnimation( _remoteSkinnedRenderer );
		}

		if ( _remoteFallbackRenderer.IsValid() )
			_remoteFallbackRenderer.Enabled = !IsLocalOwner;
	}

	static Model GetCitizenModel() =>
		_citizenModel ??= Model.Load( "models/citizen/citizen.vmdl" );

	static Model GetBoxModel() =>
		_boxModel ??= Model.Load( "models/dev/box.vmdl" );

	void ActivateLocalCamera()
	{
		if ( !_camera.IsValid() ) return;

		StudioCamera.Instance?.SetSuppressed( true );

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( !cam.IsValid() || cam == _camera ) continue;
			cam.IsMainCamera = false;
			cam.Enabled = false;
		}

		_camera.Enabled = true;
		_camera.IsMainCamera = true;
		_cameraActive = true;

		Log.Info( "[ThinkDrink][PlayerPawn] local camera active." );
	}
}
