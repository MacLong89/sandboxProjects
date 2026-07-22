namespace PawnShop;

/// <summary>
/// First-person shopkeeper: built-in PlayerController for movement, child camera for
/// the view, and an aim-based interaction check against Interactable zones.
/// </summary>
public sealed class ShopPlayer : Component
{
	public static ShopPlayer Instance { get; private set; }

	private PlayerController _controller;
	private GameObject _view;
	private CameraComponent _camera;
	private GameObject _heldProp;
	private int _heldVisualId = -1;
	private Angles _look;
	private float _strideAccum;
	private const float StrideLength = 72f;

	/// <summary>Interactable currently under the crosshair, if any.</summary>
	public Interactable Focused { get; private set; }

	public Vector3 EyePosition => _view.IsValid() ? _view.WorldPosition : WorldPosition + Vector3.Up * GameConstants.EyeHeight;
	public Vector3 EyeForward => _view.IsValid() ? _view.WorldRotation.Forward : WorldRotation.Forward;

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
		_controller.WalkSpeed = GameConstants.WalkSpeed;
		_controller.RunSpeed = GameConstants.WalkSpeed * GameConstants.RunMultiplier;

		_view = new GameObject( GameObject, true, "View" );
		_view.LocalPosition = new Vector3( 0f, 0f, GameConstants.EyeHeight );

		_camera = _view.Components.Create<CameraComponent>();
		_camera.FieldOfView = GameConstants.FieldOfView;
		_camera.ZNear = 2f;
		_camera.ZFar = 8000f;
		_camera.BackgroundColor = new Color( 0.42f, 0.74f, 0.98f );
		_camera.EnablePostProcessing = true;

		_look = new Angles( 8f, -90f, 0f ); // face the front door
	}

	protected override void OnStart()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
			cam.IsMainCamera = cam == _camera;

		ApplyLook();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		var game = GameManager.Instance;
		if ( game is null ) return;

		var blocking = game.IsUiBlocking;
		Mouse.Visibility = blocking ? MouseVisibility.Visible : MouseVisibility.Hidden;

		if ( blocking )
		{
			_controller.UseInputControls = false;
			_controller.WishVelocity = Vector3.Zero;
			Focused = null;
			return;
		}

		// Mouse look.
		var look = Input.AnalogLook;
		_look.pitch = Math.Clamp( _look.pitch + look.pitch, -85f, 85f );
		_look.yaw += look.yaw;
		_look.roll = 0f;
		ApplyLook();

		_controller.UseInputControls = true;
		TickFootsteps();
		TickInteraction( game );
		TickHeldProp( game );

		// Always allow dropping whatever you're holding (item or trash).
		var holding = game.CarriedItem is not null || game.Chores?.CarryingTrash == true;
		if ( holding && (Input.Pressed( "Reload" ) || Input.Pressed( "Drop" ) || Input.Keyboard.Pressed( "Q" ) || Input.Pressed( "Slot0" )) )
			game.DropHeld();
	}

	private void TickHeldProp( GameManager game )
	{
		if ( game.Chores?.CarryingTrash == true )
		{
			if ( _heldVisualId != -2 || !_heldProp.IsValid() )
			{
				_heldProp?.Destroy();
				_heldProp = new GameObject( _view.IsValid() ? _view : GameObject, true, "HeldTrash" );
				_heldProp.LocalPosition = new Vector3( 16f, -12f, -14f );
				_heldProp.LocalRotation = Rotation.FromPitch( 8f );
				_heldProp.LocalScale = new Vector3( 0.7f, 0.7f, 0.7f );
				MeshKit.Spawn( _heldProp, "Bag", new Vector3( 0, 0, 10 ), new Vector3( 14, 14, 20 ), new Color( 0.28f, 0.32f, 0.26f ) );
				MeshKit.Spawn( _heldProp, "Tie", new Vector3( 0, 0, 22 ), new Vector3( 6, 6, 5 ), new Color( 0.2f, 0.22f, 0.18f ) );
				_heldVisualId = -2;
			}
			return;
		}

		var item = game.CarriedItem;
		if ( item is null )
		{
			_heldProp?.Destroy();
			_heldProp = null;
			_heldVisualId = -1;
			return;
		}

		if ( _heldVisualId != item.Id || !_heldProp.IsValid() )
		{
			_heldProp?.Destroy();
			_heldProp = new GameObject( _view.IsValid() ? _view : GameObject, true, "HeldItem" );
			_heldProp.LocalPosition = new Vector3( 18f, -10f, -12f );
			_heldProp.LocalRotation = Rotation.FromPitch( 12f ) * Rotation.FromYaw( -20f );
			_heldProp.LocalScale = new Vector3( 0.55f, 0.55f, 0.55f );
			ItemProp.Build( _heldProp, item );
			_heldVisualId = item.Id;
		}
	}

	private void TickInteraction( GameManager game )
	{
		Focused = null;

		var best = float.MaxValue;
		foreach ( var zone in Scene.GetAllComponents<Interactable>() )
		{
			if ( !zone.IsValid() || !zone.Enabled ) continue;
			if ( string.IsNullOrEmpty( zone.Prompt ) ) continue;
			if ( !zone.IntersectsRay( EyePosition, EyeForward, GameConstants.InteractRange ) ) continue;

			var dist = zone.WorldPosition.Distance( EyePosition );
			if ( dist < best )
			{
				best = dist;
				Focused = zone;
			}
		}

		if ( Focused is not null && Input.Pressed( "Use" ) )
		{
			if ( Focused.Use() )
				Sfx.Play( Sfx.UiClick, 0.4f );
		}
	}

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
		_view.LocalRotation = Rotation.FromPitch( _look.pitch );
	}
}
