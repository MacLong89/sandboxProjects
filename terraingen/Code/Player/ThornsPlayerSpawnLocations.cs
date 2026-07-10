namespace Terraingen.Player;

using Terraingen.Buildings;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Random coastal spawn points (0–5 m above sea level) for join, respawn, and first spawn.</summary>
public static class ThornsPlayerSpawnLocations
{
	public const float MaxAltitudeAboveSeaMeters = 5f;
	public const float MaxAltitudeAboveSeaInches = MaxAltitudeAboveSeaMeters * ThornsTerrainSurface.InchesPerMeter;
	public const float SpawnFeetClearanceInches = 8f;
	/// <summary>Planar clearance around the pawn center — keeps trees, boulders, minerals, and buildings out of the spawn disc.</summary>
	public const float SpawnClearRadiusInches = 108f;
	public const int MaxPickAttempts = 192;

	public static bool TryPickRandom(
		Scene scene,
		out Vector3 worldPosition,
		float feetClearance = SpawnFeetClearanceInches,
		int? deterministicSeed = null )
	{
		worldPosition = default;
		if ( !TryGetSpawnBounds( scene, out var terrain, out var config, out var seaZ, out var maxGroundZ, out var minX, out var maxX, out var minY, out var maxY ) )
			return false;

		var rng = deterministicSeed.HasValue
			? new Random( deterministicSeed.Value )
			: new Random( HashCode.Combine( (int)(Time.Now * 1000), Guid.NewGuid().GetHashCode() ) );
		for ( var attempt = 0; attempt < MaxPickAttempts; attempt++ )
		{
			var x = minX + rng.NextSingle() * (maxX - minX );
			var y = minY + rng.NextSingle() * (maxY - minY );
			if ( !TryBuildClearSpawnAt( scene, terrain, x, y, seaZ, maxGroundZ, feetClearance, out worldPosition ) )
				continue;

			return true;
		}

		return false;
	}

	public static Vector3 SnapToTerrain( Scene scene, Vector3 requested, float heightOffset = SpawnFeetClearanceInches )
	{
		if ( TryResolveClearSpawn( scene, requested, heightOffset, out var clear ) )
			return clear;

		if ( scene is null || !scene.IsValid() )
			return requested + Vector3.Up * heightOffset;

		var terrain = scene.GetAllComponents<Terrain>().FirstOrDefault( t => t.IsValid() );
		if ( !terrain.IsValid() )
			return requested + Vector3.Up * heightOffset;

		var maxHeight = terrain.TerrainHeight;
		var clamped = ThornsTerrainSurface.ClampToTerrainBounds( terrain, requested );
		var x = clamped.x;
		var y = clamped.y;
		var rayStart = new Vector3( x, y, terrain.GameObject.WorldPosition.z + maxHeight * 2f );
		if ( terrain.RayIntersects( new Ray( rayStart, Vector3.Down ), maxHeight * 4f, out var localHit ) )
			return terrain.GameObject.WorldTransform.PointToWorld( localHit ) + Vector3.Up * heightOffset;

		return new Vector3( x, y, requested.z + heightOffset );
	}

	public static bool TryResolveClearSpawn(
		Scene scene,
		Vector3 requested,
		float feetClearance,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		var terrain = scene.GetAllComponents<Terrain>().FirstOrDefault( t => t.IsValid() );
		if ( !terrain.IsValid() )
			return false;

		var config = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.Config;
		var seaZ = ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, config );
		var maxGroundZ = seaZ + MaxAltitudeAboveSeaInches;

		return TryBuildClearSpawnAt( scene, terrain, requested.x, requested.y, seaZ, maxGroundZ, feetClearance, out worldPosition );
	}

	/// <summary>Return to a saved logout position without coastal/new-player clearance rules.</summary>
	public static bool TryResolveSavedReturnSpawn(
		Scene scene,
		Vector3 saved,
		float feetClearance,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		var terrain = scene.GetAllComponents<Terrain>().FirstOrDefault( t => t.IsValid() );
		if ( !terrain.IsValid() )
			return false;

		var config = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.Config;
		var seaZ = ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, config );
		if ( saved.z < seaZ - 48f )
			return false;

		var hasGround = ThornsTerrainSurface.TryRaycastGround( terrain, saved.x, saved.y, out var ground );
		if ( hasGround && saved.z < ground.z - 48f )
			return false;

		if ( hasGround && MathF.Abs( saved.z - ground.z ) <= 160f )
		{
			worldPosition = saved;
			return true;
		}

		if ( hasGround )
		{
			worldPosition = ground + Vector3.Up * Math.Max( 0f, feetClearance );
			return true;
		}

		worldPosition = saved;
		return true;
	}

	/// <summary>Search near a saved logout point before falling back to a random coastal spawn.</summary>
	public static bool TryResolveSavedReturnNear(
		Scene scene,
		Vector3 saved,
		float feetClearance,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		ReadOnlySpan<float> radii = stackalloc float[] { 96f, 180f, 280f };
		for ( var ring = 0; ring < radii.Length; ring++ )
		{
			var radius = radii[ring];
			for ( var i = 0; i < 8; i++ )
			{
				var ang = i * MathF.PI * 0.25f;
				var offset = new Vector3( MathF.Cos( ang ) * radius, MathF.Sin( ang ) * radius, 0f );
				if ( TryResolveSavedReturnSpawn( scene, saved + offset, feetClearance, out worldPosition ) )
					return true;
			}
		}

		return false;
	}

	public static bool TryResolveBedRespawn(
		Scene scene,
		Vector3 bedPosition,
		float bedYawDegrees,
		float feetClearance,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		var yaw = Rotation.FromYaw( bedYawDegrees );
		var forward = yaw.Forward.WithZ( 0f );
		var right = yaw.Right.WithZ( 0f );
		if ( forward.LengthSquared < 0.001f )
			forward = Vector3.Forward;
		else
			forward = forward.Normal;

		if ( right.LengthSquared < 0.001f )
			right = Vector3.Right;
		else
			right = right.Normal;

		ReadOnlySpan<Vector3> offsets = stackalloc Vector3[]
		{
			right * 110f,
			-right * 110f,
			forward * 130f,
			-forward * 130f,
			(right + forward).Normal * 150f,
			(-right + forward).Normal * 150f,
			(right - forward).Normal * 150f,
			(-right - forward).Normal * 150f,
			right * 180f,
			-right * 180f,
			forward * 210f,
			-forward * 210f
		};

		for ( var i = 0; i < offsets.Length; i++ )
		{
			var candidate = bedPosition + offsets[i];
			if ( TryBuildBedSpawnAt( scene, candidate.WithZ( bedPosition.z ), feetClearance, out worldPosition ) )
				return true;
		}

		return TryBuildBedSpawnAt( scene, bedPosition + Vector3.Up * 6f, feetClearance, out worldPosition );
	}

	static bool TryGetSpawnBounds(
		Scene scene,
		out Terrain terrain,
		out ThornsTerrainConfig config,
		out float seaZ,
		out float maxGroundZ,
		out float minX,
		out float maxX,
		out float minY,
		out float maxY )
	{
		terrain = default;
		config = null;
		seaZ = 0f;
		maxGroundZ = 0f;
		minX = maxX = minY = maxY = 0f;

		if ( scene is null || !scene.IsValid() )
			return false;

		terrain = scene.GetAllComponents<Terrain>().FirstOrDefault( t => t.IsValid() );
		if ( !terrain.IsValid() )
			return false;

		config = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.Config;
		seaZ = ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, config );
		maxGroundZ = seaZ + MaxAltitudeAboveSeaInches;

		var terrainSize = terrain.TerrainSize;
		var margin = Math.Max( 1200f, terrainSize * 0.08f );
		minX = terrain.GameObject.WorldPosition.x + margin;
		maxX = terrain.GameObject.WorldPosition.x + terrainSize - margin;
		minY = terrain.GameObject.WorldPosition.y + margin;
		maxY = terrain.GameObject.WorldPosition.y + terrainSize - margin;
		return true;
	}

	static bool TryBuildClearSpawnAt(
		Scene scene,
		Terrain terrain,
		float worldX,
		float worldY,
		float seaZ,
		float maxGroundZ,
		float feetClearance,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !ThornsTerrainSurface.TryRaycastGround( terrain, worldX, worldY, out var ground ) )
			return false;

		if ( ground.z < seaZ - 2f || ground.z > maxGroundZ )
			return false;

		if ( !IsSpawnAreaClear( scene, ground ) )
			return false;

		worldPosition = ground + Vector3.Up * Math.Max( 0f, feetClearance );
		return true;
	}

	static bool TryBuildBedSpawnAt(
		Scene scene,
		Vector3 requested,
		float feetClearance,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		var basePosition = requested;
		var terrain = scene.GetAllComponents<Terrain>().FirstOrDefault( t => t.IsValid() );
		if ( terrain.IsValid() && ThornsTerrainSurface.TryRaycastGround( terrain, requested.x, requested.y, out var terrainGround ) )
		{
			if ( MathF.Abs( terrainGround.z - requested.z ) < 72f )
				basePosition = terrainGround;
		}

		if ( HasBlockingGeometry( scene, basePosition, SpawnClearRadiusInches * 0.65f ) )
			return false;

		worldPosition = basePosition + Vector3.Up * Math.Max( 0f, feetClearance );
		return true;
	}

	public static bool IsSpawnAreaClear( Scene scene, Vector3 groundPosition, float clearRadiusInches = SpawnClearRadiusInches )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		var x = groundPosition.x;
		var y = groundPosition.y;

		if ( ThornsWorldScatterFootprintRegistry.ContainsWorldPoint( x, y, clearRadiusInches ) )
			return false;

		if ( IsNearPlacedPlayerStructure( x, y, clearRadiusInches ) )
			return false;

		if ( HasBlockingGeometry( scene, groundPosition, clearRadiusInches ) )
			return false;

		return true;
	}

	static bool IsNearPlacedPlayerStructure( float worldX, float worldY, float clearRadiusInches )
	{
		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( placed is null || !placed.IsValid() || !placed.GameObject.IsValid() )
				continue;

			if ( !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var def ) )
				continue;

			var pos = placed.GameObject.WorldPosition;
			var dx = pos.x - worldX;
			var dy = pos.y - worldY;
			var required = clearRadiusInches + def.FootprintRadius;
			if ( dx * dx + dy * dy < required * required )
				return true;
		}

		return false;
	}

	static bool HasBlockingGeometry( Scene scene, Vector3 groundPosition, float clearRadiusInches )
	{
		var upStart = groundPosition + Vector3.Up * 6f;
		var upEnd = groundPosition + Vector3.Up * 132f;
		var upTrace = scene.Trace.Ray( upStart, upEnd ).Run();
		if ( upTrace.Hit && upTrace.Distance < 112f && !IsWalkableGroundHit( upTrace, groundPosition ) )
			return true;

		ReadOnlySpan<float> checkHeights = stackalloc float[] { 20f, 52f, 84f };
		for ( var i = 0; i < 8; i++ )
		{
			var ang = i * MathF.PI * 0.25f;
			var dir = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f );

			for ( var h = 0; h < checkHeights.Length; h++ )
			{
				var start = groundPosition + Vector3.Up * checkHeights[h];
				var end = start + dir * clearRadiusInches;
				var trace = scene.Trace.Ray( start, end ).Run();
				if ( !trace.Hit || trace.Distance >= clearRadiusInches - 10f )
					continue;

				if ( IsWalkableGroundHit( trace, groundPosition ) )
					continue;

				return true;
			}
		}

		return false;
	}

	static bool IsWalkableGroundHit( SceneTraceResult trace, Vector3 groundPosition )
	{
		if ( !trace.Hit )
			return false;

		if ( trace.Normal.z < 0.72f )
			return false;

		return trace.HitPosition.z <= groundPosition.z + 14f;
	}
}
