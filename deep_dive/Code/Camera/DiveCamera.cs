namespace DeepDive;

/// <summary>
/// Side-view camera sitting on +Y, following the diver with mild vertical look-ahead.
/// </summary>
public sealed class DiveCamera : Component
{
	public CameraComponent Camera => _camera;

	private CameraComponent _camera;
	private bool _initialized;

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.FieldOfView = GameConstants.CamFov;
		_camera.ZNear = GameConstants.CamZNear;
		_camera.ZFar = GameConstants.CamZFar;
		_camera.BackgroundColor = GameConstants.WaterSunlit;

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam != _camera )
				cam.IsMainCamera = false;
		}

		_camera.IsMainCamera = true;
	}

	protected override void OnUpdate()
	{
		var diver = DeepDiveGame.Instance?.Diver;
		if ( diver is null || !_camera.IsValid() )
			return;

		var p = diver.WorldPosition;
		var lookAheadZ = diver.Velocity.z * GameConstants.CamLookAheadScale;
		if ( lookAheadZ < -GameConstants.CamMaxLookAhead ) lookAheadZ = -GameConstants.CamMaxLookAhead;
		if ( lookAheadZ > GameConstants.CamMaxLookAhead ) lookAheadZ = GameConstants.CamMaxLookAhead;

		var targetPos = new Vector3( p.x, GameConstants.CamDistance, p.z );
		var lookAt = new Vector3( p.x, 0f, p.z + lookAheadZ );

		if ( !_initialized )
		{
			WorldPosition = targetPos;
			_initialized = true;
		}
		else
		{
			WorldPosition = Vector3.Lerp(
				WorldPosition,
				targetPos,
				MathF.Min( 1f, Time.Delta * GameConstants.CamFollowSpeed ) );
		}

		var dir = (lookAt - WorldPosition);
		if ( dir.Length > 0.001f )
			WorldRotation = Rotation.LookAt( dir.Normal, Vector3.Up );
	}

	public void SetBackground( Color color )
	{
		if ( _camera.IsValid() )
			_camera.BackgroundColor = color;
	}
}
