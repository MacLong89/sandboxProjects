namespace Deep;

/// <summary>
/// Free 2D swim in an open water column — dive down from the boat, explore left/right,
/// surface near the boat (Dave the Diver style).
/// </summary>
public sealed class DiverController : Component
{
	public static DiverController Instance { get; private set; }

	public Vector3 Velocity { get; private set; }
	public bool IsUnderwater { get; private set; }
	public bool IsAtSurface => !IsUnderwater;
	public float CurrentDepthMeters { get; private set; }
	public float DiveMaxDepthMeters { get; private set; }

	public float SwimSpeed { get; set; } = 9f;
	public float AscentSpeed { get; set; } = 10f;
	public float DescentSpeed { get; set; } = 11f;
	public DiverSwimAnimator Animator { get; private set; }
	public bool IsInVehicle { get; private set; }

	private bool _wasUnderwater = true;

	public void SetInVehicle( bool inVehicle ) => IsInVehicle = inVehicle;

	protected override void OnAwake()
	{
		Instance = this;
		BuildVisual();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void ApplyMovementFromBalance( BalanceConfig balance )
	{
		if ( balance is null ) return;
		SwimSpeed = balance.SwimSpeed;
		AscentSpeed = balance.AscentSpeed;
		DescentSpeed = balance.DescentSpeed;
	}

	protected override void OnUpdate()
	{
		var game = DeepGame.Instance;
		if ( game is null )
			return;

		Mouse.Visibility = game.IsUiBlocking || game.Phase == GamePhase.SurfaceIdle
			|| game.Phase == GamePhase.DiverHub || game.State.IsDivingActive
			? MouseVisibility.Visible
			: MouseVisibility.Hidden;

		UpdateDepthState( game );

		if ( game.State.IsDivingActive )
		{
			TickMovement( game.Balance );
			TrySurfaceSuccess( game );
			game.NotifyDepth( CurrentDepthMeters );
		}
		else
		{
			Velocity = Vector3.Zero;
		}
	}

	public void BeginDive()
	{
		var balance = DeepGame.Instance?.Balance ?? BalanceConfig.Defaults;
		DiveMaxDepthMeters = 0f;
		Velocity = Vector3.Zero;

		// Drop straight under the boat into open water.
		WorldPosition = new Vector3( balance.SurfaceSpawnX, 0f, balance.DiveStartZ );
		UpdateDepthState( DeepGame.Instance );
		_wasUnderwater = IsUnderwater;
	}

	public void ReturnToSurface( bool snap )
	{
		var balance = DeepGame.Instance?.Balance ?? BalanceConfig.Defaults;
		Velocity = Vector3.Zero;
		WorldPosition = new Vector3( balance.SurfaceSpawnX, 0f, balance.SurfaceSpawnZ );
		UpdateDepthState( DeepGame.Instance );
		_wasUnderwater = IsUnderwater;
		_ = snap;
	}

	private void TickMovement( BalanceConfig balance )
	{
		balance ??= BalanceConfig.Defaults;

		// Camera on +Y looking at play plane: +X ≈ screen-left, +Z ≈ screen-up.
		var move = Input.AnalogMove;
		var wish = new Vector3( move.y, 0f, move.x );

		if ( Input.Keyboard.Down( "LEFTARROW" ) ) wish.x += 1f;
		if ( Input.Keyboard.Down( "RIGHTARROW" ) ) wish.x -= 1f;
		if ( Input.Keyboard.Down( "UPARROW" ) ) wish.z += 1f;
		if ( Input.Keyboard.Down( "DOWNARROW" ) ) wish.z -= 1f;

		if ( wish.Length > 1f )
			wish = wish.Normal;

		var boost = Components.Get<BoostComponent>();
		var tools = DeepGame.Instance?.Tools;
		var mult = (boost?.SpeedMultiplier ?? 1f)
			* (tools?.FinSpeedMultiplier ?? 1f)
			* (tools?.SubSpeedMultiplier ?? 1f)
			* (IsInVehicle ? 1.85f : 1f);

		var target = Vector3.Zero;
		if ( wish.Length > 0.01f )
		{
			var speedX = SwimSpeed * mult;
			var speedZ = (wish.z >= 0f ? AscentSpeed : DescentSpeed) * mult;
			target = new Vector3( wish.x * speedX, 0f, wish.z * speedZ );
		}

		var dt = Time.Delta;
		var rate = target.Length > 0.01f ? balance.MoveAcceleration : balance.MoveDeceleration;
		Velocity = Vector3.Lerp( Velocity, target, MathF.Min( 1f, rate * dt / MathF.Max( SwimSpeed, 1f ) ) );

		var pos = WorldPosition + Velocity * dt;
		pos.y = 0f;

		var bed = SeabedTerrain.Instance;
		if ( bed is not null )
			pos = bed.ClampSwimPosition( pos );
		else
		{
			if ( pos.x < -balance.HorizontalHalfWidth ) pos.x = -balance.HorizontalHalfWidth;
			if ( pos.x > balance.HorizontalHalfWidth ) pos.x = balance.HorizontalHalfWidth;
			var maxZ = balance.SurfaceSpawnZ + 0.5f;
			if ( pos.z < balance.MinWorldZ ) pos.z = balance.MinWorldZ;
			if ( pos.z > maxZ ) pos.z = maxZ;
		}

		WorldPosition = pos;
	}

	private void TrySurfaceSuccess( DeepGame game )
	{
		var bed = SeabedTerrain.Instance;
		var atSurface = !IsUnderwater;

		if ( _wasUnderwater && atSurface )
		{
			if ( bed is null || bed.IsNearBoat( WorldPosition ) )
				game.CompleteDiveSuccess();
			else
			{
				var balance = game.Balance;
				var pos = WorldPosition;
				pos.z = balance.SurfaceZ - balance.SurfaceEpsilon - 0.35f;
				if ( bed is not null )
					pos = bed.ClampSwimPosition( pos );
				WorldPosition = pos;
				game.ShowMessage( "Surface under the boat to end the dive", 1.4f );
			}
		}

		_wasUnderwater = IsUnderwater;
	}

	private void UpdateDepthState( DeepGame game )
	{
		var balance = game?.Balance ?? BalanceConfig.Defaults;
		IsUnderwater = WorldPosition.z < balance.SurfaceZ - balance.SurfaceEpsilon;
		CurrentDepthMeters = balance.DepthFromWorldZ( WorldPosition.z );
		if ( game?.State.IsDivingActive == true )
			DiveMaxDepthMeters = MathF.Max( DiveMaxDepthMeters, CurrentDepthMeters );
	}

	private void BuildVisual()
	{
		WorldRotation = Rotation.Identity;

		var renderer = DeepSprites.SpawnDiver( GameObject );
		Animator = renderer.GameObject.AddComponent<DiverSwimAnimator>();
		Animator.MovementRoot = GameObject;
	}
}
