namespace SceneLab;

/// <summary>Thin look-dev boot: lobby, walk pawn, rebuildable workbench root.</summary>
public sealed class SceneLabBoot : Component
{
	public static SceneLabBoot Instance { get; private set; }

	private GameObject _worldRoot;
	private SceneLabPawn _pawn;

	public int BuildGeneration { get; private set; }
	public Vector3 SpawnPosition => WorkbenchScene.SpawnPosition;
	public float SpawnYaw => WorkbenchScene.SpawnYaw;
	public string SceneTitle => WorkbenchScene.Title;
	public string SceneNotes => WorkbenchScene.Notes;

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

		try
		{
			if ( !Networking.IsActive )
			{
				Networking.CreateLobby( new Sandbox.Network.LobbyConfig
				{
					MaxPlayers = 1,
					Name = "Scene Lab",
					Privacy = Sandbox.Network.LobbyPrivacy.Private
				} );
			}

			Rebuild();
			SpawnPawn();
			SpawnHud();
		}
		catch ( System.Exception e )
		{
			Log.Error( e, $"[SceneLab] Start failed: {e.Message}" );
		}
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor )
			return;

		if ( Input.Keyboard.Pressed( "R" ) )
		{
			Rebuild();
			_pawn?.SnapToSpawn();
		}
	}

	public void Rebuild()
	{
		try
		{
			_worldRoot?.Destroy();
			_worldRoot = new GameObject( true, "WorkbenchWorld" );
			WorkbenchScene.Build( _worldRoot );
			BuildGeneration++;
			Log.Info( $"[SceneLab] Built '{WorkbenchScene.Title}' — {WorkbenchScene.Notes}" );
		}
		catch ( System.Exception e )
		{
			Log.Error( e, $"[SceneLab] Rebuild failed: {e.Message}" );
			throw;
		}
	}

	private void SpawnPawn()
	{
		var go = new GameObject( true, "Viewer" );
		go.WorldPosition = SpawnPosition;
		_pawn = go.Components.Create<SceneLabPawn>();
	}

	private void SpawnHud()
	{
		var go = new GameObject( true, "HUD" );
		go.Components.Create<ScreenPanel>();
		go.Components.Create<SceneLab.UI.SceneLabHud>();
	}
}
