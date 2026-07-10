namespace Fauna2;

/// <summary>A tree or rock that blocks building on its grid cell until cleared.</summary>
public sealed class TerrainObstacleComponent : Component
{
	[Sync( SyncFlags.FromHost )] public string CellKey { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public int Type { get; set; }

	private bool _registered;
	private bool _visualsReady;

	public TerrainObstacleType ObstacleType => (TerrainObstacleType)Type;

	public string DisplayName => ObstacleType == TerrainObstacleType.Tree ? "tree" : "rock";

	protected override void OnStart()
	{
		TryInitialize();
	}

	protected override void OnUpdate()
	{
		if ( _registered && _visualsReady )
			return;

		TryInitialize();
	}

	private void TryInitialize()
	{
		if ( string.IsNullOrEmpty( CellKey ) )
			return;

		if ( !_registered )
		{
			TerrainObstacleRegistry.Register( this );
			_registered = true;
		}

		_visualsReady = EnsureVisuals();
	}

	private bool EnsureVisuals()
	{
		if ( GameObject.Children.Count > 0 )
			return true;

		var biome = ZooState.Instance?.StarterBiome ?? Biome.Grassland;
		if ( TerrainObstacleSystem.TryParseCellKey( CellKey, out var gx, out var gy ) )
			biome = WildernessBiomeMap.BiomeAtWorld( TerrainObstacleSystem.CellCenter( gx, gy ), biome );

		TerrainObstacleVisuals.Build( GameObject, ObstacleType, biome, HashCode.Combine( CellKey, Type ) );
		return GameObject.Children.Count > 0;
	}

	protected override void OnDestroy()
	{
		if ( _registered )
			TerrainObstacleRegistry.Unregister( this );
	}
}
