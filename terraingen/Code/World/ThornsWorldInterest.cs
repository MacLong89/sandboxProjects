namespace Terraingen.World;

using Terraingen.Player;

/// <summary>Shared player-proximity checks for chunk activation and deferred world populate.</summary>
public static class ThornsWorldInterest
{
	public static bool HasAnyPlayer( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( gameplay.IsValid() && gameplay.GameObject.IsValid() )
				return true;
		}

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( controller.IsValid() && controller.GameObject.IsValid() && controller.UseInputControls )
				return true;
		}

		return false;
	}

	public static bool IsNearAnyPlayer( Scene scene, Vector3 worldPosition, float radiusInches )
	{
		if ( scene is null || !scene.IsValid() || radiusInches <= 0f )
			return false;

		var radiusSq = radiusInches * radiusInches;

		foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() || !gameplay.GameObject.IsValid() )
				continue;

			if ( (gameplay.GameObject.WorldPosition - worldPosition).LengthSquared <= radiusSq )
				return true;
		}

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( !controller.IsValid() || !controller.GameObject.IsValid() || !controller.UseInputControls )
				continue;

			if ( (controller.GameObject.WorldPosition - worldPosition).LengthSquared <= radiusSq )
				return true;
		}

		return false;
	}

	/// <summary>Interest test for populate: players first, then observer (camera / pending spawn).</summary>
	public static bool IsNearPopulationInterest(
		Scene scene,
		Vector3 worldPosition,
		float radiusInches,
		Vector3 observer )
	{
		if ( IsNearAnyPlayer( scene, worldPosition, radiusInches ) )
			return true;

		if ( radiusInches <= 0f || observer == Vector3.Zero )
			return false;

		var radiusSq = radiusInches * radiusInches;
		return (observer - worldPosition).LengthSquared <= radiusSq;
	}

	/// <summary>World center of a centered terrain (local size/2 from origin).</summary>
	public static Vector3 ResolveTerrainCenter( Terrain terrain )
	{
		if ( !terrain.IsValid() )
			return Vector3.Zero;

		var origin = terrain.GameObject.WorldPosition;
		var half = terrain.TerrainSize * 0.5f;
		return origin + new Vector3( half, half, 0f );
	}

	/// <summary>
	/// Effective populate radius — large enough that a player anywhere on the terrain can
	/// populate every chunk (furthest chunk center is roughly one terrain diagonal away).
	/// </summary>
	public static float ResolvePopulateRadius( Terrain terrain, float configuredInches, float chunkSizeInches = 8000f )
	{
		if ( !terrain.IsValid() )
			return configuredInches;

		var fullDiagonal = terrain.TerrainSize * MathF.Sqrt( 2f );
		var coverFromAnywhere = fullDiagonal + chunkSizeInches * 0.5f;

		if ( configuredInches <= 0f )
			return coverFromAnywhere;

		return Math.Max( configuredInches, coverFromAnywhere );
	}

	public static Vector3 ResolveObserverPosition( Scene scene, Vector3 fallback )
	{
		if ( scene is null || !scene.IsValid() )
			return fallback;

		if ( ThornsPlayerGameplay.Local.IsValid() )
			return ThornsPlayerGameplay.Local.GameObject.WorldPosition;

		foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( gameplay.IsValid() && gameplay.GameObject.IsValid() )
				return gameplay.GameObject.WorldPosition;
		}

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( controller.IsValid() && controller.GameObject.IsValid() && controller.UseInputControls )
				return controller.GameObject.WorldPosition;
		}

		return fallback;
	}
}
