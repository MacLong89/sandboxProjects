namespace Terraingen.Buildings;

using System.Linq;
using System.Threading.Tasks;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Animals + nav for the POI settlement gallery movement sandbox.</summary>
public static class ThornsSettlementAnimalTestScene
{
	public const string DefaultTestSpeciesKey = "deer";
	const string NavFloorName = "Settlement Test Nav Floor";

	public static ThornsAnimalManager EnsureAnimalManager( GameObject host )
		=> EnsureAnimalManager( host, ignoreAnimals: false );

	public static ThornsAnimalManager EnsureAnimalManager( GameObject host, bool ignoreAnimals )
	{
		if ( !host.IsValid() )
			return null;

		var manager = host.Scene.GetAllComponents<ThornsAnimalManager>().FirstOrDefault()
		              ?? host.Components.Get<ThornsAnimalManager>()
		              ?? host.Components.Create<ThornsAnimalManager>();

		manager.IgnorePlayers = true;
		manager.IgnoreAnimals = ignoreAnimals;
		manager.AmbientSpawnChance = 0f;
		manager.MaxWorldAnimals = Math.Max( 48, ThornsSettlementTestSceneBootstrap.GallerySettlementCount * 4 );
		manager.NavBakeHalfExtent = 16000f;
		return manager;
	}

	public static async Task<bool> WaitForNavMeshReadyAsync( Scene scene )
	{
		for ( var i = 0; i < 180; i++ )
		{
			if ( ThornsAnimalManager.NavMeshReady
			     && ThornsAnimalManager.NavMeshUsableForAnimals
			     && (scene?.NavMesh is null || !scene.NavMesh.IsGenerating) )
				return true;

			await Task.Delay( 33 );
		}

		Log.Warning(
			$"[Thorns Settlement Test] Nav mesh unavailable after wait — spawning animals with fallback movement. " +
			$"ready={ThornsAnimalManager.NavMeshReady}, usable={ThornsAnimalManager.NavMeshUsableForAnimals}, " +
			$"generating={scene?.NavMesh?.IsGenerating == true}." );
		return false;
	}

	public static void EnsureFlatNavFloor( Scene scene, GameObject host, Terrain terrain )
	{
		if ( scene is null || !scene.IsValid() || !host.IsValid() || !terrain.IsValid() )
			return;

		GameObject floor = null;
		foreach ( var child in host.Children )
		{
			if ( child.IsValid() && child.Name == NavFloorName )
			{
				floor = child;
				break;
			}
		}

		if ( !floor.IsValid() )
		{
			floor = scene.CreateObject( true );
			floor.Name = NavFloorName;
			floor.Parent = host;
		}

		var min = terrain.GameObject.WorldPosition;
		var center = min + new Vector3( terrain.TerrainSize * 0.5f, terrain.TerrainSize * 0.5f, 0f );
		if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, center, out var snapped ) )
			center = snapped;

		floor.WorldPosition = new Vector3( center.x, center.y, center.z - 6f );
		floor.WorldRotation = Rotation.Identity;
		floor.WorldScale = Vector3.One;
		floor.Tags.Add( "thorns_nav_floor" );

		var collider = floor.Components.GetOrCreate<BoxCollider>();
		collider.Center = Vector3.Zero;
		collider.Scale = new Vector3( terrain.TerrainSize, terrain.TerrainSize, 12f );
		collider.IsTrigger = false;
		collider.Static = true;
		collider.Enabled = true;
	}

	public static int HostSpawnGalleryAnimals( Scene scene, Terrain terrain, string speciesKey, int perPoi )
		=> HostSpawnGalleryAnimals( scene, terrain, speciesKey, perPoi, null, 0 );

	public static int HostSpawnGalleryAnimals(
		Scene scene,
		Terrain terrain,
		string speciesKey,
		int perPoi,
		string predatorSpeciesKey,
		int predatorsPerPoi )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() || !terrain.IsValid() )
			return 0;

		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		if ( !ThornsAnimalSpeciesRegistry.TryGet( speciesKey, out var species ) || species is null )
		{
			Log.Warning( $"[Thorns Settlement Test] Unknown species '{speciesKey}' — using '{DefaultTestSpeciesKey}'." );
			if ( !ThornsAnimalSpeciesRegistry.TryGet( DefaultTestSpeciesKey, out species ) || species is null )
				return 0;
		}

		perPoi = Math.Clamp( perPoi, 1, 8 );
		predatorsPerPoi = Math.Clamp( predatorsPerPoi, 0, 4 );
		var center = ThornsWorldInterest.ResolveTerrainCenter( terrain );
		var total = 0;

		ThornsAnimalSpeciesData predatorSpecies = null;
		if ( predatorsPerPoi > 0 && !string.IsNullOrWhiteSpace( predatorSpeciesKey ) )
		{
			if ( !ThornsAnimalSpeciesRegistry.TryGet( predatorSpeciesKey, out predatorSpecies ) || predatorSpecies is null )
				Log.Warning( $"[Thorns Settlement Test] Unknown predator species '{predatorSpeciesKey}' — predators skipped." );
		}

		for ( var i = 0; i < ThornsSettlementTestSceneBootstrap.GallerySettlementCount; i++ )
		{
			var anchor = center + ThornsSettlementTestSceneBootstrap.GetGalleryOffset( i );
			total += ThornsAnimalSpawnUtil.HostSpawnGroup( scene, species, anchor, perPoi );

			if ( predatorSpecies is not null )
			{
				var predatorAnchor = anchor + new Vector3( 280f, -220f, 0f );
				total += ThornsAnimalSpawnUtil.HostSpawnGroup( scene, predatorSpecies, predatorAnchor, predatorsPerPoi );
			}
		}

		return total;
	}
}
