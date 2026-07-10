namespace Terraingen.TerrainGen;

/// <summary>World water column sampling for swim / wade / float locomotion.</summary>
public readonly struct ThornsWaterBodyState
{
	public bool IsActive { get; init; }
	public bool ShouldWade { get; init; }
	public bool ShouldSwim { get; init; }
	public float WaterSurfaceZ { get; init; }
	public float TerrainFloorZ { get; init; }
	public float WaterColumnDepth { get; init; }
	public float FeetDepthBelowSurface { get; init; }
}

public static class ThornsNaturalWaterBody
{
	public const float ChestHeightInches = 36f;
	public const float EnterSwimFeetDepthInches = 18f;
	public const float WadeMaxWaterColumnInches = 32f;
	public const float WadeMaxFeetDepthInches = 16f;
	public const float MinFeetSubmergeInches = 4f;

	public static bool TrySample( Scene scene, GameObject playerRoot, out ThornsWaterBodyState state )
	{
		state = default;

		if ( scene is null || !scene.IsValid() || playerRoot is null || !playerRoot.IsValid() )
			return false;

		var bootstrap = ResolveBootstrap( scene );
		if ( bootstrap is null || !bootstrap.IsValid() )
			return false;

		var terrain = bootstrap.WorldTerrain;
		var field = bootstrap.GetHeightFieldForMap();
		var config = bootstrap.Config;
		if ( !terrain.IsValid() || field is null || config is null || !config.CreateWaterSheet )
			return false;

		var origin = terrain.GameObject.WorldPosition;
		var size = terrain.TerrainSize;
		var feet = playerRoot.WorldPosition;
		var localX = feet.x - origin.x;
		var localY = feet.y - origin.y;

		if ( localX < 32f || localY < 32f || localX > size - 32f || localY > size - 32f )
			return false;

		var seaZ = origin.z + config.SeaLevelNormalized * terrain.TerrainHeight;
		var sampler = new TerrainChunkSampler( field, size, terrain.TerrainHeight );
		var seaNorm = config.SeaLevelNormalized + 0.012f;

		if ( !sampler.IsUnderSeaLevel( localX, localY, seaNorm ) )
			return false;

		var terrainFloorZ = origin.z + sampler.SampleHeightInches( localX, localY );
		var waterColumn = MathF.Max( 0f, seaZ - terrainFloorZ );
		var feetDepth = seaZ - feet.z;

		var isActive = waterColumn > 6f;
		if ( !isActive )
			return false;

		var feetSubmerged = feetDepth > MinFeetSubmergeInches;
		var chestSubmerged = feet.z + ChestHeightInches < seaZ - 2f;
		var deepColumn = waterColumn > WadeMaxWaterColumnInches;

		var shouldSwim = feetSubmerged
		                 && ( chestSubmerged || deepColumn || feetDepth >= EnterSwimFeetDepthInches );

		var shouldWade = !shouldSwim
		                 && feetSubmerged
		                 && feetDepth <= WadeMaxFeetDepthInches
		                 && waterColumn <= WadeMaxWaterColumnInches + 8f;

		state = new ThornsWaterBodyState
		{
			IsActive = true,
			ShouldSwim = shouldSwim,
			ShouldWade = shouldWade,
			WaterSurfaceZ = seaZ,
			TerrainFloorZ = terrainFloorZ,
			WaterColumnDepth = waterColumn,
			FeetDepthBelowSurface = feetDepth
		};

		return true;
	}

	public static bool IsSwimming( Scene scene, GameObject playerRoot ) =>
		TrySample( scene, playerRoot, out var state ) && state.ShouldSwim;

	/// <summary>0–1 blend for underwater camera tint from eye depth below the water surface.</summary>
	public static float ComputeViewSubmergeBlend( in ThornsWaterBodyState state, float eyeWorldZ )
	{
		if ( !state.IsActive )
			return 0f;

		var eyeDepth = state.WaterSurfaceZ - eyeWorldZ;
		if ( eyeDepth <= 0f )
		{
			if ( !state.ShouldWade )
				return 0f;

			return Math.Clamp( state.FeetDepthBelowSurface / WadeMaxFeetDepthInches, 0f, 1f ) * 0.22f;
		}

		return Math.Clamp( eyeDepth / 26f, 0f, 1f );
	}

	static ThornsTerrainBootstrap ResolveBootstrap( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return null;

		return scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault( b => b.IsWorldApplied );
	}
}
