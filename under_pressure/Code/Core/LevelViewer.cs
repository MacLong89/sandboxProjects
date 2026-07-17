namespace UnderPressure;

/// <summary>
/// Scene entry for the level art viewer. Walk the job sites with no washing, enemies,
/// briefings, or save writes. Hotkeys cycle levels.
/// </summary>
public sealed class LevelViewer : Component
{
	public static LevelViewer Instance { get; private set; }

	private GameObject _jobRoot;
	private GameObject _vanRoot;
	private LevelViewerPawn _pawn;

	public JobDef Current { get; private set; }
	public int Index { get; private set; }
	public int LoadGeneration { get; private set; }
	public Vector3 SpawnPosition { get; private set; }
	public float SpawnYaw { get; private set; }

	public int LevelNumber => Index + 1;
	public int LevelCount => JobCatalog.Jobs.Count;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig
			{
				MaxPlayers = 1,
				Name = "Under Pressure — Level Viewer",
				Privacy = Sandbox.Network.LobbyPrivacy.Private
			} );
		}

		VisualGrade.EnsureInScene( Scene );
		SpawnHud();
		LoadLevel( 0 );
		SpawnPawn();
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor )
			return;

		if ( Input.Keyboard.Pressed( "RIGHTARROW" ) || Input.Keyboard.Pressed( "]" ) )
			LoadLevel( Index + 1 );
		else if ( Input.Keyboard.Pressed( "LEFTARROW" ) || Input.Keyboard.Pressed( "[" ) )
			LoadLevel( Index - 1 );
		else if ( Input.Keyboard.Pressed( "HOME" ) )
			LoadLevel( 0 );
		else if ( Input.Keyboard.Pressed( "END" ) )
			LoadLevel( LevelCount - 1 );
		else if ( Input.Keyboard.Pressed( "R" ) )
			_pawn?.SnapToSpawn();
	}

	public void LoadLevel( int index )
	{
		JobCatalog.Reload();
		var count = JobCatalog.Jobs.Count;
		if ( count <= 0 )
			return;

		Index = ((index % count) + count) % count;
		Current = JobCatalog.Get( Index );

		_jobRoot?.Destroy();
		_jobRoot = new GameObject( true, $"Preview_{Current.Name}" );
		JobWorldBuilder.BuildEnvironment( _jobRoot, Current, Index );
		JobWorldBuilder.BuildPanels( _jobRoot, Current );

		SpawnPosition = Current.SpawnPosition;
		SpawnYaw = Current.SpawnYaw;
		LoadGeneration++;

		ParkVan();
		_pawn?.SnapToSpawn();

		Log.Info( $"[LevelViewer] Level {LevelNumber}/{LevelCount}: {Current.Name} — {Current.Location} | decor={Current.Decor.Count} panels={Current.Panels.Count} props={Current.Props.Count} theme={Current.Theme}" );
	}

	private void ParkVan()
	{
		_vanRoot?.Destroy();
		_vanRoot = new GameObject( true, "ViewerVan" );

		var rot = Rotation.FromYaw( SpawnYaw );
		_vanRoot.WorldPosition = SpawnPosition + rot.Backward * 170f + rot.Left * 130f;
		_vanRoot.WorldRotation = Rotation.FromYaw( SpawnYaw + 90f );
		Scenery.BuildVan( _vanRoot, tier: 0 );
	}

	private void SpawnPawn()
	{
		var go = new GameObject( true, "Viewer" );
		go.WorldPosition = SpawnPosition + Vector3.Up * 8f;
		_pawn = go.Components.Create<LevelViewerPawn>();
	}

	private void SpawnHud()
	{
		var go = new GameObject( true, "ViewerHUD" );
		go.Components.Create<ScreenPanel>();
		go.Components.Create<UnderPressure.UI.LevelViewerHud>();
	}
}
