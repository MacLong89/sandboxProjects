namespace ThinkDrink;

/// <summary>Fallback spectator camera — disabled when a local player pawn owns the view.</summary>
public sealed class StudioCamera : Component
{
	public static StudioCamera Instance { get; private set; }

	[Property] public Vector3 CameraPosition { get; set; } = new( 0, 1200, 540 );
	[Property] public float FieldOfView { get; set; } = 62f;

	private CameraComponent _camera;
	private bool _suppressed;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		_camera = GameObject.GetComponent<CameraComponent>();
		if ( _camera is null )
			_camera = GameObject.AddComponent<CameraComponent>();
		_camera.FieldOfView = FieldOfView;
		_camera.BackgroundColor = new Color( 0.06f, 0.05f, 0.10f );
		ApplyActiveState();
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;

		if ( HasLocalPlayerPawn() )
			SetSuppressed( true );

		if ( _suppressed )
		{
			ApplyActiveState();
			return;
		}

		var lookAt = StudioEnvironment.Instance?.PlayAreaFocus ?? new Vector3( 0, 0, 130 );
		WorldPosition = CameraPosition;
		WorldRotation = Rotation.LookAt( lookAt - WorldPosition, Vector3.Up );
		ApplyActiveState();
	}

	public void SetSuppressed( bool suppressed )
	{
		_suppressed = suppressed;
		ApplyActiveState();
	}

	void ApplyActiveState()
	{
		if ( !_camera.IsValid() ) return;

		var active = !_suppressed && !HasLocalPlayerPawn();
		_camera.Enabled = active;
		_camera.IsMainCamera = active;
	}

	bool HasLocalPlayerPawn()
	{
		foreach ( var pawn in Scene.GetAllComponents<PlayerPawn>() )
		{
			if ( pawn.IsValid() && pawn.IsLocalOwner )
				return true;
		}
		return false;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}
}
