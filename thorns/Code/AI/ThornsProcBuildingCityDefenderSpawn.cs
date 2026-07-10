namespace Sandbox;

/// <summary>Shared city-defender rolls: per-floor chance, hard cap per building, interior placement.</summary>
public static class ThornsProcBuildingCityDefenderSpawn
{
	public static int HostCountNearAnchor( Scene scene, Vector3 leashAnchorWorld )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var n = 0;
		var ax = leashAnchorWorld.x;
		var ay = leashAnchorWorld.y;

		foreach ( var brain in scene.GetAllComponents<ThornsBanditBrain>() )
		{
			if ( !brain.IsValid() || !brain.UseLeashAnchor )
				continue;

			if ( !string.Equals( brain.GameObject.Name, "ThornsCityDefender", StringComparison.Ordinal ) )
				continue;

			var la = brain.LeashAnchorWorld;
			var dx = la.x - ax;
			var dy = la.y - ay;
			if ( dx * dx + dy * dy < 48f * 48f )
				n++;
		}

		return n;
	}

	/// <summary>
	/// Rolls each floor for a defender spawn until <paramref name="maxPerBuilding"/> living defenders are present.
	/// Returns how many were spawned this call.
	/// </summary>
	public static int HostTryFillBuilding(
		Scene scene,
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		float floorChance,
		int maxPerBuilding,
		ThornsProcBuildingInteriorSample.InteriorPlacementHints hints = default )
	{
		if ( scene is null || !scene.IsValid() || buildingRoot is null || !buildingRoot.IsValid() )
			return 0;

		floorChance = Math.Clamp( floorChance, 0f, 1f );
		maxPerBuilding = Math.Max( 0, maxPerBuilding );
		if ( maxPerBuilding == 0 || stories < 1 )
			return 0;

		var anchor = buildingRoot.WorldPosition;
		var existing = HostCountNearAnchor( scene, anchor );
		if ( existing >= maxPerBuilding )
			return 0;

		var spawned = 0;
		for ( var story = 0; story < stories && existing + spawned < maxPerBuilding; story++ )
		{
			if ( rnd.NextDouble() >= floorChance )
				continue;

			if ( !HostTrySpawnOneOnStory(
				     scene,
				     rnd,
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     story,
				     anchor,
				     hints ) )
				continue;

			spawned++;
		}

		return spawned;
	}

	static bool HostTrySpawnOneOnStory(
		Scene scene,
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		Vector3 anchor,
		ThornsProcBuildingInteriorSample.InteriorPlacementHints hints )
	{
		var wp = ThornsProcBuildingInteriorSample.SampleInteriorNpcWorldPositionOnStory(
			scene,
			buildingRoot,
			widthCells,
			depthCells,
			stories,
			storyIndex,
			rnd );
		if ( wp == default )
			return false;

		var cell = ThornsBuildingModule.Cell;
		var footprint = MathF.Max( widthCells, depthCells ) * cell;
		var leashR = Math.Clamp( footprint * 0.52f + 90f, 260f, 520f );
		var wanderR = leashR * 0.96f;
		var cfg = ThornsNpcHumanBanditSpawn.CityDefender( anchor, leashR, wanderR );
		ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, wp, rnd, cfg );
		return true;
	}
}
