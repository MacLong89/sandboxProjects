namespace Fauna2;

/// <summary>Verbose console diagnostics — filter log with "Fauna2Debug".</summary>
public static class Fauna2Debug
{
	public const string Tag = "[Fauna2Debug]";

	public static bool Enabled { get; set; }

	private static int _frame;
	private static TimeUntil _nextHeartbeat;

	public static void Info( string category, string message )
	{
		if ( !Enabled ) return;
		Log.Info( $"{Tag} [{category}] {message}" );
	}

	public static void Warn( string category, string message )
	{
		if ( !Enabled ) return;
		Log.Warning( $"{Tag} [{category}] {message}" );
	}

	public static void Error( string category, string message )
	{
		Log.Error( $"{Tag} [{category}] {message}" );
	}

	public static void TickHeartbeat()
	{
		if ( !Enabled ) return;
		_frame++;

		if ( _nextHeartbeat ) return;
		_nextHeartbeat = 3f;

		LogHeartbeat();
	}

	public static void LogSceneProbe( Scene scene, string reason )
	{
		if ( !Enabled || !scene.IsValid() ) return;

		var objects = scene.GetAllObjects( true ).ToList();
		var cameras = scene.GetAllComponents<CameraComponent>().ToList();
		var panels = scene.GetAllComponents<PanelComponent>().ToList();

		Info( "Scene", $"Probe ({reason}): objects={objects.Count}, cameras={cameras.Count}, panelComponents={panels.Count}" );

		foreach ( var cam in cameras )
		{
			if ( !cam.IsValid() ) continue;
			var go = cam.GameObject;
			Info( "Camera",
				$"  '{go.Name}' main={cam.IsMainCamera} enabled={cam.Enabled} ortho={cam.Orthographic} " +
				$"orthoH={cam.OrthographicHeight:0.#} pos={go.WorldPosition} rot={go.WorldRotation} " +
				$"bg={cam.BackgroundColor} clear={cam.ClearFlags}" );
		}

		foreach ( var name in new[] { "Systems", "Camera", "UI", "Sun" } )
		{
			var found = objects.FirstOrDefault( o => o.Name == name );
			Info( "Scene", $"  object '{name}': {(found.IsValid() ? $"OK enabled={found.Enabled} comps={found.Components.Count}" : "MISSING")}" );
		}

		var groundTiles = objects.Count( o => o.Name.EndsWith( "Tile" ) && o.Tags.Has( "ground" ) );
		Info( "Scene", $"  ground tiles: {(WorldEnvironment.Instance.IsValid() ? $"{groundTiles} sprites (procedural)" : "WorldEnvironment not ready")}" );

		foreach ( var typeName in new[]
		{
			"Fauna2.GameManager",
			"Fauna2.WorldEnvironment",
			"Fauna2.ZooCameraController",
			"Fauna2.UI.MainMenuPanel",
			"Fauna2.UI.HudRoot",
			"Sandbox.ScreenPanel",
		} )
		{
			var count = 0;
			foreach ( var go in objects )
			{
				foreach ( var c in go.Components.GetAll() )
				{
					if ( c?.GetType().FullName == typeName )
						count++;
				}
			}
			Info( "Scene", $"  component {typeName}: count={count}" );
		}
	}

	public static void LogNetworking( string reason )
	{
		Info( "Net", $"{reason} | active={Networking.IsActive} host={Networking.IsHost} " +
			$"local={Connection.Local?.DisplayName ?? "null"} game={GameManager.Instance?.GameStarted}" );
	}

	public static void LogDefinitions()
	{
		try
		{
			DefinitionCatalog.EnsureInitialized();
			Info( "Defs", $"animals={DefinitionCatalog.Animals.Count} placeables={DefinitionCatalog.Placeables.Count}" );
			foreach ( var a in DefinitionCatalog.Animals.Take( 8 ) )
				Info( "Defs", $"  animal: {DefinitionCatalog.AnimalId( a )} = {a.DisplayName}" );
		}
		catch ( Exception e )
		{
			Error( "Defs", $"catalog failed: {e.Message}" );
		}
	}

	public static void LogAssets()
	{
		var paths = new[]
		{
			SuppliedSpriteManifest.PlayerSpritePath,
			SuppliedSpriteManifest.ShopPath,
			SuppliedSpriteManifest.RabbitPath,
			SuppliedSpriteManifest.FenceBottomLeftPath,
			SuppliedSpriteManifest.FenceBottomRightPath,
			SuppliedSpriteManifest.FenceTopLeftPath,
			SuppliedSpriteManifest.FenceTopRightPath,
			SuppliedTileManifest.GrassPath,
			SuppliedTileManifest.WildernessPath,
			SuppliedTileManifest.PathPath,
		};

		foreach ( var path in paths )
		{
			var exists = FileSystem.Mounted.FileExists( path );
			Info( "Assets", $"{path} exists={exists}" );
		}
	}

	public static void LogSystems( string reason )
	{
		Info( "Systems", $"{reason} | GM={Fmt( GameManager.Instance )} started={GameManager.Instance?.GameStarted} " +
			$"ZooState={Fmt( ZooState.Instance )} Plots={Fmt( PlotSystem.Instance )} " +
			$"WorldEnv={Fmt( WorldEnvironment.Instance )} CamCtrl={Fmt( ZooCameraController.Instance )} " +
			$"Player={Fmt( PlayerState.Local )}" );
	}

	private static void LogHeartbeat()
	{
		Info( "Heartbeat", $"frame~{_frame} gameStarted={GameManager.Instance?.GameStarted} " +
			$"player={PlayerState.Local?.FeetPosition} cam={ZooCameraController.Instance?.GameObject.WorldPosition} " +
			$"orthoH={ZooCameraController.Instance?.GetOrthoHeight():0.#} " +
			$"animals={AnimalRegistry.Count} habitats={HabitatRegistry.Count} guests={GuestSystem.Instance?.GuestCount}" );
	}

	private static string Fmt( Component c ) => c.IsValid() ? "OK" : "null";

	private static string Fmt( PlayerState p ) => p.IsValid() ? p.PlayerName : "null";
}
