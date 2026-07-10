namespace Fauna2;

/// <summary>Raycast pick for animals, habitat fences, and terrain obstacles.</summary>
public static class WorldPicker
{
	public static bool TryPick(
		Scene scene,
		GameObject ignoreHierarchy,
		out AnimalComponent animal,
		out HabitatComponent habitat,
		out TerrainObstacleComponent obstacle )
	{
		animal = null;
		habitat = null;
		obstacle = null;

		if ( !TryRaycast( scene, ignoreHierarchy, out var result ) )
			return false;

		var go = result.GameObject;
		while ( go.IsValid() )
		{
			animal = go.Components.Get<AnimalComponent>();
			if ( animal is not null )
				return true;

			go = go.Parent;
		}

		go = result.GameObject;
		while ( go.IsValid() )
		{
			obstacle = go.Components.Get<TerrainObstacleComponent>();
			if ( obstacle is not null )
				return true;

			go = go.Parent;
		}

		habitat = HabitatRegistry.FindFromHierarchy( result.GameObject )
			?? HabitatRegistry.FindFenceAt( result.HitPosition );

		return habitat is not null;
	}

	private static bool TryRaycast( Scene scene, GameObject ignoreHierarchy, out SceneTraceResult result )
	{
		result = default;

		var camera = scene.Camera;
		if ( !camera.IsValid() )
			return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		var trace = scene.Trace.Ray( ray, 50_000f );
		if ( ignoreHierarchy.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignoreHierarchy );

		result = trace.Run();
		return result.Hit && result.GameObject.IsValid();
	}
}
