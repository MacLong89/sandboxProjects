namespace RunGun;

public sealed class RunnerPlayer : Component
{
	public static RunnerPlayer Instance { get; private set; }

	private GameObject _camGo;
	private CameraComponent _camera;
	private SquadFormation _squad;
	private float _fireTimer;
	private bool _camInit;
	private float _camShake;

	protected override void OnAwake()
	{
		Instance = this;
		BuildBody();
		BuildCamera();
		_squad = Components.Create<SquadFormation>();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		_camGo?.Destroy();
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		var blocking = core.IsUiBlocking;
		Mouse.Visibility = blocking ? MouseVisibility.Visible : MouseVisibility.Hidden;

		if ( core.Run.Active )
		{
			if ( Input.Pressed( "Jump" ) && core.Run.TryActivateOverdrive() )
				Sfx.Play( Sfx.Overdrive );
			Tick( core );
		}

		UpdateCamera();
	}

	public void ApplyCameraShake( float amount ) => _camShake = MathF.Max( _camShake, amount );

	private void Tick( GameCore core )
	{
		var dt = Time.Delta;
		var pos = WorldPosition;

		pos.x += GameConstants.RunSpeed * dt;

		var strafe = Input.AnalogMove.y;
		var limit = GameConstants.LaneHalf - GameConstants.PlayerRadius;
		pos.y = Math.Clamp( pos.y + strafe * core.Upgrades.StrafeSpeed * dt, -limit, limit );

		WorldPosition = pos;

		_fireTimer -= dt;
		if ( _fireTimer <= 0f )
		{
			var origin = new Vector3( pos.x + GameConstants.PlayerRadius, pos.y, GameConstants.BodyHeight * 0.55f );
			TrackManager.Instance?.Fire( origin );
			_fireTimer = core.Run.FireInterval;
		}
	}

	public void ResetToStart( float startX )
	{
		WorldPosition = new Vector3( startX, 0f, 0f );
		_fireTimer = 0f;
		_camInit = false;
		_camShake = 0f;
		_squad?.Reset();
		UpdateCamera();
	}

	private void BuildBody()
	{
		var visual = Components.Create<CitizenVisual>();
		visual.BodyTint = new Color( 0.3f, 0.62f, 1f );
		visual.BodyScale = 1f;
	}

	private void BuildCamera()
	{
		_camGo = new GameObject( true, "RunCamera" );
		_camera = _camGo.Components.Create<CameraComponent>();
		_camera.FieldOfView = GameConstants.CamFov;
		_camera.ZNear = 5f;
		_camera.ZFar = 20000f;
		_camera.BackgroundColor = new Color( 0.5f, 0.62f, 0.75f );

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
			if ( cam != _camera ) cam.IsMainCamera = false;

		_camera.IsMainCamera = true;
	}

	private void UpdateCamera()
	{
		if ( !_camera.IsValid() ) return;

		var p = WorldPosition;
		var shakeX = Game.Random.Float( -1f, 1f ) * _camShake;
		var shakeY = Game.Random.Float( -1f, 1f ) * _camShake * 0.5f;
		_camShake = MathF.Max( 0f, _camShake - Time.Delta * GameConstants.CamShakeDecay );

		var targetPos = new Vector3(
			p.x - GameConstants.CamBack + shakeX,
			p.y * GameConstants.CamYFollow + shakeY,
			GameConstants.CamUp );
		var lookAt = new Vector3( p.x + GameConstants.CamLookAhead, p.y * GameConstants.CamYFollow * 0.5f, GameConstants.CamLookUp );

		if ( !_camInit )
		{
			_camGo.WorldPosition = targetPos;
			_camInit = true;
		}
		else
		{
			_camGo.WorldPosition = Vector3.Lerp( _camGo.WorldPosition, targetPos, MathF.Min( 1f, Time.Delta * 12f ) );
		}

		_camGo.WorldRotation = Rotation.LookAt( (lookAt - _camGo.WorldPosition).Normal );
	}
}
