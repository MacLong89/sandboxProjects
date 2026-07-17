namespace Offshore;

/// <summary>
/// DIAGNOSTIC SAFE BOOT — camera + status only.
/// Re-enable systems one at a time after Play no longer hard-crashes.
/// </summary>
public sealed class OffshoreGameController : Component
{
	public static OffshoreGameController Instance { get; private set; }
	public static bool BootComplete { get; private set; }

	public OffshoreStateMachine StateMachine { get; } = new();
	public BalanceConfig Balance { get; private set; } = BalanceConfig.Defaults.Clone();
	public PlayerProgressionData Progression { get; private set; } = new();
	public UpgradeSystem Upgrades { get; private set; } = new();

	public FishingSessionState State => StateMachine.State;
	public FishingController Fishing { get; private set; }
	public WaterVolumeComponent Water { get; private set; }
	public OffshoreCameraController Camera { get; private set; }
	public AnglerController Player { get; private set; }
	public DayNightCycle DayNight { get; private set; }
	public DockMenuController Menus { get; private set; }
	public BoatBoardController Boarding { get; private set; }
	public GameObject RodTip { get; private set; }
	public string StatusMessage { get; private set; } = "Safe boot";

	protected override void OnAwake()
	{
		Instance = this;
		BootComplete = false;
		Log.Info( "[Offshore] SAFE Boot: OnAwake" );
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		Log.Info( "[Offshore] SAFE Boot: OnStart" );

		try
		{
			// Plain camera only — no SpriteRenderer, no Texture.Create, no Razor HUD.
			var camGo = new GameObject( true, "SafeCamera" );
			var cam = camGo.Components.Create<CameraComponent>();
			cam.IsMainCamera = true;
			cam.FieldOfView = 50f;
			cam.ZNear = 1f;
			cam.ZFar = 5000f;
			cam.BackgroundColor = new Color( 0.15f, 0.35f, 0.55f );
			camGo.WorldPosition = new Vector3( 12f, -40f, 0f );
			camGo.WorldRotation = Rotation.LookAt( Vector3.Forward, Vector3.Up );

			StatusMessage = "SAFE BOOT OK — editor Play is stable";
			BootComplete = true;
			Log.Info( "[Offshore] SAFE Boot: complete — if you see this, Play no longer native-crashes" );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Offshore] SAFE Boot failed: {e}" );
			BootComplete = true;
		}
	}

	protected override void OnUpdate()
	{
		// Intentionally empty during diagnostic boot.
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
		{
			Instance = null;
			BootComplete = false;
		}
	}

	public bool TrySetState( FishingSessionState next ) => StateMachine.TrySet( next );
	public void SetStatus( string message ) => StatusMessage = message ?? "";
}
