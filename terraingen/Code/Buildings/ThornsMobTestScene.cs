namespace Terraingen.Buildings;

using System.Linq;
using System.Threading.Tasks;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Animals + nav for the flat mob combat sandbox (<see cref="ThornsBanditTestSceneBootstrap"/>).</summary>
public static class ThornsMobTestScene
{
	public static ThornsAnimalManager EnsureAnimalManager( GameObject host )
	{
		if ( !host.IsValid() )
			return null;

		var manager = host.Scene.GetAllComponents<ThornsAnimalManager>().FirstOrDefault()
		              ?? host.Components.Get<ThornsAnimalManager>()
		              ?? host.Components.Create<ThornsAnimalManager>();

		manager.IgnorePlayers = false;
		manager.IgnoreAnimals = false;
		manager.AmbientSpawnChance = 0f;
		manager.MaxWorldAnimals = Math.Max( 64, manager.MaxWorldAnimals );
		manager.NavBakeHalfExtent = 16000f;
		return manager;
	}

	public static Task<bool> WaitForNavMeshReadyAsync( Scene scene )
		=> ThornsSettlementAnimalTestScene.WaitForNavMeshReadyAsync( scene );

	public static void EnsureFlatNavFloor( Scene scene, GameObject host, Terrain terrain )
		=> ThornsSettlementAnimalTestScene.EnsureFlatNavFloor( scene, host, terrain );

	/// <summary>Spawns wolf packs (and optional extras) on a ring for bandit/wildlife interaction tests.</summary>
	public static int HostSpawnArenaAnimals(
		Scene scene,
		Terrain terrain,
		int groupCount,
		float groupDistanceFromCenter,
		int deerPerGroup,
		int wolvesPerGroup,
		bool spawnPanther,
		bool spawnMoose )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() || !terrain.IsValid() )
			return 0;

		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		if ( !ThornsAnimalSpeciesRegistry.TryGet( "wolf", out var wolf ) )
		{
			Log.Warning( "[Thorns Mob Test] Missing wolf species — animals skipped." );
			return 0;
		}

		groupCount = Math.Max( 1, groupCount );
		deerPerGroup = Math.Clamp( deerPerGroup, 0, 12 );
		wolvesPerGroup = Math.Clamp( wolvesPerGroup, 0, 8 );
		groupDistanceFromCenter = Math.Max( 400f, groupDistanceFromCenter );

		if ( deerPerGroup <= 0 && wolvesPerGroup <= 0 && !spawnPanther && !spawnMoose )
			return 0;

		var center = ThornsWorldInterest.ResolveTerrainCenter( terrain );
		var total = 0;

		for ( var g = 0; g < groupCount; g++ )
		{
			var angleDeg = groupCount == 1 ? 45f : (360f / groupCount) * g + 45f;
			var rad = angleDeg * MathF.PI / 180f;
			var anchor = SampleFlatSurface( terrain, center, MathF.Cos( rad ) * groupDistanceFromCenter, MathF.Sin( rad ) * groupDistanceFromCenter );

			if ( deerPerGroup > 0 && ThornsAnimalSpeciesRegistry.TryGet( "deer", out var deer ) )
				total += ThornsAnimalSpawnUtil.HostSpawnGroup( scene, deer, anchor, deerPerGroup );

			if ( wolvesPerGroup > 0 )
			{
				var wolfAnchor = deerPerGroup > 0
					? anchor + new Vector3( 260f, -180f, 0f )
					: anchor;
				total += ThornsAnimalSpawnUtil.HostSpawnGroup( scene, wolf, wolfAnchor, wolvesPerGroup );
			}
		}

		if ( spawnPanther && ThornsAnimalSpeciesRegistry.TryGet( "panther", out var panther ) )
		{
			var pantherAnchor = SampleFlatSurface( terrain, center, -900f, 700f );
			total += ThornsAnimalSpawnUtil.HostSpawnSolitary( scene, panther, pantherAnchor );
		}

		if ( spawnMoose && ThornsAnimalSpeciesRegistry.TryGet( "moose", out var moose ) )
		{
			var mooseAnchor = SampleFlatSurface( terrain, center, 900f, -700f );
			total += ThornsAnimalSpawnUtil.HostSpawnSolitary( scene, moose, mooseAnchor );
		}

		return total;
	}

	static Vector3 SampleFlatSurface( Terrain terrain, Vector3 center, float offsetX, float offsetY )
	{
		var x = center.x + offsetX;
		var y = center.y + offsetY;
		var rayStart = new Vector3( x, y, terrain.TerrainHeight * 2f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, terrain.TerrainHeight * 4f, out var localHit ) )
			return terrain.GameObject.WorldTransform.PointToWorld( localHit );

		return new Vector3( x, y, center.z );
	}
}
