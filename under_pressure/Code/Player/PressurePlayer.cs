namespace UnderPressure;

/// <summary>
/// First-person pawn: built-in <see cref="PlayerController"/> for movement, a child camera
/// for the view, and a <see cref="PressureWasher"/> for the core verb. Mouse look is driven
/// here so we can hand the cursor to the shop UI cleanly.
/// </summary>
public sealed class PressurePlayer : Component
{
	public static PressurePlayer Instance { get; private set; }

	private PlayerController _controller;
	private GameObject _view;
	private CameraComponent _camera;
	private PressureWasher _washer;
	private WandView _wand;
	private Angles _look;
	private int _lastJobGeneration = -1;

	// Footstep cadence: play a step every time the player covers this much ground, so faster
	// movement naturally steps faster.
	private const float StrideLength = 72f;
	private float _strideAccum;

	public Vector3 EyePosition => _view.IsValid() ? _view.WorldPosition : WorldPosition + Vector3.Up * GameConstants.EyeHeight;
	public Vector3 EyeForward => _view.IsValid() ? _view.WorldRotation.Forward : WorldRotation.Forward;
	public Rotation EyeRotation => _view.IsValid() ? _view.WorldRotation : WorldRotation;

	/// <summary>World position of the wand nozzle, or the eye if the viewmodel isn't ready.</summary>
	public Vector3 NozzlePosition => _wand.IsValid() ? _wand.NozzleWorldPos : EyePosition;

	protected override void OnAwake()
	{
		Instance = this;

		_controller = Components.GetOrCreate<PlayerController>();
		_controller.UseCameraControls = false;
		_controller.UseLookControls = false;
		_controller.UseAnimatorControls = false;
		_controller.EnablePressing = false;
		_controller.BodyHeight = 72f;
		_controller.BodyRadius = 16f;
		_controller.WalkSpeed = GameConstants.WalkSpeedBase;
		_controller.RunSpeed = GameConstants.WalkSpeedBase * GameConstants.RunMultiplier;

		_view = new GameObject( GameObject, true, "View" );
		_view.LocalPosition = new Vector3( 0f, 0f, GameConstants.EyeHeight );

		_camera = _view.Components.Create<CameraComponent>();
		_camera.FieldOfView = GameConstants.FieldOfView;
		_camera.ZNear = 2f;
		_camera.ZFar = 12000f;
		_camera.BackgroundColor = new Color( 0.42f, 0.74f, 0.98f );
		_camera.EnablePostProcessing = true;

		var wandGo = new GameObject( _view, true, "Wand" );
		_wand = wandGo.Components.Create<WandView>();

		_washer = Components.GetOrCreate<PressureWasher>();
	}

	protected override void OnStart()
	{
		ActivateCamera();
		SnapToSpawn();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		// Re-spawn whenever a new job loads.
		if ( core.Jobs.LoadGeneration != _lastJobGeneration )
			SnapToSpawn();

		_controller.WalkSpeed = core.Upgrades.MoveSpeed;
		_controller.RunSpeed = core.Upgrades.MoveSpeed * GameConstants.RunMultiplier;

		var uiBlocking = core.IsUiBlocking;
		Mouse.Visibility = uiBlocking ? MouseVisibility.Visible : MouseVisibility.Hidden;

		if ( uiBlocking )
		{
			_controller.UseInputControls = false;
			_controller.WishVelocity = Vector3.Zero;
			return;
		}

		var look = Input.AnalogLook;
		_look.pitch = Math.Clamp( _look.pitch - look.pitch, -85f, 85f );
		_look.yaw += look.yaw;
		_look.roll = 0f;
		ApplyLook();

		_controller.UseInputControls = true;

		TickFootsteps();
	}

	/// <summary>Emit a positional footstep each stride while walking on the ground.</summary>
	private void TickFootsteps()
	{
		if ( _controller is null || !_controller.IsOnGround )
		{
			_strideAccum = 0f;
			return;
		}

		var speed = _controller.Velocity.WithZ( 0f ).Length;
		if ( speed < 20f )
		{
			// Nearly stopped: prime the next step so it lands soon after moving again.
			_strideAccum = Math.Min( _strideAccum, StrideLength * 0.5f );
			return;
		}

		_strideAccum += speed * Time.Delta;
		if ( _strideAccum >= StrideLength )
		{
			_strideAccum -= StrideLength;
			Sfx.PlayAt( Sfx.Footstep, WorldPosition );
		}
	}

	private void ApplyLook()
	{
		_controller.EyeAngles = _look;
		GameObject.WorldRotation = Rotation.FromYaw( _look.yaw );
		_view.LocalRotation = Rotation.FromAxis( Vector3.Right, _look.pitch );
	}

	/// <summary>Teleport back to the job spawn (e.g. after passing out on site).</summary>
	public void RecoverAtSpawn() => SnapToSpawn();

	private void SnapToSpawn()
	{
		var jobs = GameCore.Instance?.Jobs;
		if ( jobs is null ) return;

		_lastJobGeneration = jobs.LoadGeneration;
		GameObject.WorldPosition = jobs.SpawnPosition + Vector3.Up * 8f;
		_look = new Angles( 10f, jobs.SpawnYaw, 0f );
		ApplyLook();

		if ( _controller.Body.IsValid() )
			_controller.Body.Velocity = Vector3.Zero;
	}

	/// <summary>Shove the player away from a pest attack (no health — just disruption).</summary>
	public void Jostle( Vector3 from, float force )
	{
		if ( _controller?.Body is not { IsValid: true } body || force <= 0f )
			return;

		var push = (WorldPosition - from).WithZ( 0f );
		if ( push.Length < 0.01f )
			push = new Vector3( Game.Random.Float( -1f, 1f ), Game.Random.Float( -1f, 1f ), 0f );

		body.Velocity += push.Normal * force;
	}

	private void ActivateCamera()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam == _camera ) continue;
			cam.IsMainCamera = false;
		}

		_camera.IsMainCamera = true;
	}
}
