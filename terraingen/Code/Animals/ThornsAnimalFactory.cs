namespace Terraingen.Animals;

using Terraingen.Combat;
using Terraingen.Core;
using Terraingen.Rendering;
using Terraingen.TerrainGen;

/// <summary>Host-only factory for networked animal entities.</summary>
public static class ThornsAnimalFactory
{
	public static ThornsAnimalBrain HostSpawn(
		Scene scene,
		ThornsAnimalSpeciesData species,
		Vector3 worldPosition,
		Rotation rotation,
		bool ignorePopulationCap = false )
	{
		if ( scene is null || species is null || !ThornsMultiplayer.IsHostOrOffline )
		{
			Log.Warning( "[Thorns Animals] HostSpawn skipped — not host or missing scene/species." );
			return null;
		}

		if ( !ignorePopulationCap && !ThornsAnimalManager.CanSpawnMore() )
			return null;

		var scale = ThornsAnimalManager.VisualScale * ThornsNatureScaleVariance.Sample( Game.Random );
		var root = scene.CreateObject( true );
		root.Name = $"Animal ({species.DisplayName})";
		root.WorldPosition = worldPosition;
		root.WorldRotation = rotation;
		root.WorldScale = scale;
		root.Tags.Add( "animal" );
		root.NetworkMode = NetworkMode.Object;

		var agent = root.Components.Create<NavMeshAgent>();
		agent.Height = ThornsAnimalManager.BaseAgentHeight * scale;
		agent.Radius = ThornsAnimalManager.BaseAgentRadius * scale;
		agent.UpdateRotation = true;
		agent.UpdatePosition = true;
		agent.AutoTraverseLinks = true;

		var mesh = root.Components.Create<SkinnedModelRenderer>();
		var model = Model.Load( species.ModelPath );
		mesh.Model = model;
		ThornsAnimalCameraGuard.ConfigureRenderer( mesh );
		ThornsWorldShadowUtil.EnableWorldShadows( mesh );
		ThornsAnimalCameraGuard.SuppressStrayCameras( root );
		_ = root.Components.Get<ThornsAnimalCameraGuardHost>() ?? root.Components.Create<ThornsAnimalCameraGuardHost>();

		if ( !model.IsValid() )
		{
			Log.Warning( $"[Thorns Animals] Model failed to load for {species.DisplayName}: '{species.ModelPath}'." );
		}
		else
		{
			ConfigureAnimalHitCollider( root, model );
		}

		_ = root.Components.Create<ThornsAnimalVisual>();
		var corpse = root.Components.Create<ThornsAnimalCorpse>();
		corpse.Enabled = false;

		var brain = root.Components.Create<ThornsAnimalBrain>();
		brain.HostInitialize( species );
		ThornsAnimalDamageReceiver.EnsureOn( brain );
		ThornsAnimalManager.Register( brain );

		var networked = root.NetworkSpawn();
		if ( !networked )
		{
			Log.Warning( $"[Thorns Animals] NetworkSpawn failed for {species.DisplayName} at {worldPosition:F0}." );
		}

		if ( ThornsAnimalDebug.Verbose )
		{
			Log.Info(
				$"[Thorns Animals] Spawned {species.DisplayName} id={root.Id} net={networked} " +
				$"pos={worldPosition:F0} modelOk={model.IsValid()} agent={agent.IsValid()} " +
				$"navEnabled={scene.NavMesh.IsEnabled}" );
		}

		return brain;
	}

	/// <summary>Spawn a restored tame without consuming the wild animal population cap.</summary>
	public static ThornsAnimalBrain HostSpawnTameRestore(
		Scene scene,
		ThornsAnimalSpeciesData species,
		Vector3 worldPosition,
		Rotation rotation )
	{
		if ( scene is null || species is null || !ThornsMultiplayer.IsHostOrOffline )
			return null;

		var scale = ThornsAnimalManager.VisualScale * ThornsNatureScaleVariance.Sample( Game.Random );
		var root = scene.CreateObject( true );
		root.Name = $"Animal ({species.DisplayName})";
		root.WorldPosition = worldPosition;
		root.WorldRotation = rotation;
		root.WorldScale = scale;
		root.Tags.Add( "animal" );
		root.NetworkMode = NetworkMode.Object;

		var agent = root.Components.Create<NavMeshAgent>();
		agent.Height = ThornsAnimalManager.BaseAgentHeight * scale;
		agent.Radius = ThornsAnimalManager.BaseAgentRadius * scale;
		agent.UpdateRotation = true;
		agent.UpdatePosition = true;
		agent.AutoTraverseLinks = true;

		var mesh = root.Components.Create<SkinnedModelRenderer>();
		var model = Model.Load( species.ModelPath );
		mesh.Model = model;
		ThornsAnimalCameraGuard.ConfigureRenderer( mesh );
		ThornsWorldShadowUtil.EnableWorldShadows( mesh );
		ThornsAnimalCameraGuard.SuppressStrayCameras( root );
		_ = root.Components.Get<ThornsAnimalCameraGuardHost>() ?? root.Components.Create<ThornsAnimalCameraGuardHost>();

		if ( model.IsValid() )
		{
			ConfigureAnimalHitCollider( root, model );
		}

		_ = root.Components.Create<ThornsAnimalVisual>();
		var corpse = root.Components.Create<ThornsAnimalCorpse>();
		corpse.Enabled = false;

		var brain = root.Components.Create<ThornsAnimalBrain>();
		brain.HostInitialize( species );
		ThornsAnimalDamageReceiver.EnsureOn( brain );
		ThornsAnimalManager.Register( brain );
		_ = root.NetworkSpawn();

		return brain;
	}

	public static bool TryResolveSpawnPosition( Scene scene, Vector3 requested, out Vector3 position, out SpawnPositionResult result )
	{
		result = new SpawnPositionResult { Requested = requested };
		position = requested;

		if ( scene is null || !scene.IsValid() )
		{
			result.FailureReason = "invalid_scene";
			return false;
		}

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );

		if ( !ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, requested, out var snapped ) )
		{
			result.FailureReason = "terrain_ray_miss";
			return false;
		}

		result.TerrainSnapped = snapped;

		if ( config is not null && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, snapped ) )
		{
			result.WasUnderwater = true;
			result.FailureReason = "underwater";
			return false;
		}

		if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( snapped, 48f ) )
		{
			result.FailureReason = "building_footprint";
			return false;
		}

		if ( scene.NavMesh.IsEnabled )
		{
			if ( ThornsAnimalWorldUtil.TryGetNavPoint( scene, snapped, out var nav ) )
			{
				snapped = nav;
				result.NavSnapped = true;
			}
			else
			{
				result.NavSnapFailed = true;
			}
		}

		if ( config is not null && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, snapped ) )
		{
			result.WasUnderwater = true;
			result.FailureReason = "underwater";
			return false;
		}

		if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( snapped, 48f ) )
		{
			result.FailureReason = "building_footprint";
			return false;
		}

		position = snapped;
		result.Final = snapped;
		return true;
	}

	public static Vector3 ResolveSpawnPosition( Scene scene, Vector3 requested, out SpawnPositionResult result )
	{
		if ( TryResolveSpawnPosition( scene, requested, out var position, out result ) )
			return position;

		return requested;
	}

	public static Vector3 ResolveSpawnPosition( Scene scene, Vector3 requested )
		=> ResolveSpawnPosition( scene, requested, out _ );

	public static bool TryPickDrySpawnPosition(
		Scene scene,
		Vector3 requested,
		float retryRadius,
		out Vector3 position,
		out SpawnPositionResult result )
	{
		position = requested;
		result = default;

		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			var probe = attempt == 0
				? requested
				: requested + Vector3.Random.WithZ( 0f ).Normal * Game.Random.Float( 24f, retryRadius );

			if ( TryResolveSpawnPosition( scene, probe, out position, out result ) )
				return true;
		}

		position = default;
		return false;
	}

	public static void ConfigureAnimalHitCollider( GameObject root, Model model )
	{
		if ( !root.IsValid() || !model.IsValid() )
			return;

		// Wildlife should be hittable, but not world-solid. A solid moving model can push the
		// first-person player/camera into the animal when it closes to melee range.
		var collider = root.Components.Create<ModelCollider>();
		collider.Model = model;
		collider.IsTrigger = true;
		collider.Static = false;
		collider.Enabled = true;
	}

	public static bool TryGetHitboxRadius( GameObject root, out float radius, out float height )
	{
		radius = 0f;
		height = 0f;
		if ( !root.IsValid() )
			return false;

		var mesh = root.Components.Get<SkinnedModelRenderer>();
		var scale = MathF.Max( root.WorldScale.x, 0.01f );
		radius = ThornsAnimalHitbox.GetPlanarRadius( mesh?.Model, scale );
		height = ThornsAnimalHitbox.GetBodyHeight( mesh?.Model, scale );
		return radius > 0f;
	}

	public struct SpawnPositionResult
	{
		public Vector3 Requested;
		public Vector3 TerrainSnapped;
		public Vector3 Final;
		public string FailureReason;
		public bool WasUnderwater;
		public bool NavSnapped;
		public bool NavSnapFailed;
	}
}
