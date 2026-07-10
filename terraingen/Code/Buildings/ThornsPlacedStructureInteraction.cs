namespace Terraingen.Buildings;

using Terraingen.Combat;
using Terraingen.GameData;

/// <summary>Player use rules for placed portables (craft stations, storage, etc.).</summary>
public static class ThornsPlacedStructureInteraction
{
	public const float UseRange = 260f;

	public static bool IsCraftStationStructure( string structureId ) =>
		TryGetCraftStationKind( structureId, out _ );

	public static bool TryGetCraftStationKind( string structureId, out ThornsCraftStationKind station )
	{
		station = ThornsCraftStationKind.Hand;

		if ( string.IsNullOrWhiteSpace( structureId ) )
			return false;

		if ( string.Equals( structureId, "campfire", StringComparison.OrdinalIgnoreCase ) )
		{
			station = ThornsCraftStationKind.Campfire;
			return true;
		}

		if ( string.Equals( structureId, "workbench", StringComparison.OrdinalIgnoreCase ) )
		{
			station = ThornsCraftStationKind.Workbench;
			return true;
		}

		return false;
	}

	public static string PromptVerbForStructure( string structureId ) => structureId switch
	{
		"campfire" => "Smelt Metal",
		"workbench" => "Open Workbench",
		"research" => "Open Research Station",
		_ => "Use"
	};

	public static string DefaultCraftCategoryForStation( ThornsCraftStationKind station ) => station switch
	{
		ThornsCraftStationKind.Campfire => "forge",
		ThornsCraftStationKind.Workbench => "build",
		_ => "tools"
	};

	public static bool TryPickCraftStationInFront( GameObject playerRoot, out ThornsPlacedBuildStructure structure, out ThornsCraftStationKind station )
	{
		structure = null;
		station = ThornsCraftStationKind.Hand;

		if ( !playerRoot.IsValid() )
			return false;

		if ( !ThornsInteractAimPick.TryResolveCrosshairAimRay( playerRoot, out var origin, out var forward ) )
			return false;

		if ( TryPickPlacedCraftStationAlongRay( playerRoot, origin, forward, UseRange, out structure, out station ) )
			return true;

		structure = null;
		var lootWorld = ThornsBuildingLootWorldService.Instance;
		if ( lootWorld is not null
		     && lootWorld.IsValid()
		     && lootWorld.TryPickCraftStationFurnitureInFront( playerRoot, out station )
		     && station != ThornsCraftStationKind.Hand )
			return true;

		station = ThornsCraftStationKind.Hand;
		return false;
	}

	static bool TryPickPlacedCraftStationAlongRay(
		GameObject playerRoot,
		Vector3 origin,
		Vector3 forward,
		float range,
		out ThornsPlacedBuildStructure structure,
		out ThornsCraftStationKind station )
	{
		structure = null;
		station = ThornsCraftStationKind.Hand;

		var dir = forward.Normal;
		if ( dir.Length < 0.95f || !playerRoot.IsValid() )
			return false;

		var scene = playerRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var end = origin + dir * range;
		var trace = scene.Trace
			.Sphere( ThornsInteractAimPick.DefaultSphereTraceRadius, origin, end )
			.IgnoreGameObjectHierarchy( playerRoot )
			.Run();

		ThornsPlacedBuildStructure traceTarget = null;
		ThornsCraftStationKind traceStation = ThornsCraftStationKind.Hand;
		var hasTrace = trace.Hit && TryGetCraftStationFromHit( trace, out traceTarget, out traceStation );
		var hasRegistry = TryPickCraftStationAlongRayRegistry( origin, dir, range, out var registryTarget, out var registryStation );

		if ( hasTrace && !hasRegistry )
		{
			structure = traceTarget;
			station = traceStation;
			return true;
		}

		if ( !hasTrace && hasRegistry )
		{
			structure = registryTarget;
			station = registryStation;
			return true;
		}

		if ( !hasTrace || !hasRegistry )
			return false;

		var traceAlong = ResolveCraftStationAlongDistance( origin, dir, traceTarget, trace.HitPosition );
		var registryAlong = ResolveCraftStationAlongDistance( origin, dir, registryTarget );
		structure = traceAlong <= registryAlong + 16f ? traceTarget : registryTarget;
		station = structure == traceTarget ? traceStation : registryStation;
		return structure.IsValid();
	}

	static float ResolveCraftStationAlongDistance( Vector3 origin, Vector3 dir, ThornsPlacedBuildStructure placed, Vector3? fallbackHit = null )
	{
		if ( !placed.IsValid() )
			return float.MaxValue;

		if ( ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var def ) )
		{
			TryGetPlacedStructurePickBounds( placed, def, out var center, out var pickRadius );
			if ( ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var along ) )
				return along;
		}

		if ( fallbackHit.HasValue )
			return Vector3.Dot( fallbackHit.Value - origin, dir );

		return Vector3.Dot( placed.GameObject.WorldPosition - origin, dir );
	}

	static bool TryGetCraftStationFromHit( SceneTraceResult hit, out ThornsPlacedBuildStructure placed, out ThornsCraftStationKind station )
	{
		placed = null;
		station = ThornsCraftStationKind.Hand;
		if ( !TryGetStructureFromHit( hit, out placed ) || !placed.IsValid() )
			return false;

		return TryGetCraftStationKind( placed.StructureId, out station );
	}

	static bool TryPickCraftStationAlongRayRegistry(
		Vector3 origin,
		Vector3 dir,
		float maxRange,
		out ThornsPlacedBuildStructure best,
		out ThornsCraftStationKind bestStation )
	{
		best = null;
		bestStation = ThornsCraftStationKind.Hand;
		var bestAlong = float.MaxValue;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || string.IsNullOrWhiteSpace( placed.InstanceKey ) )
				continue;

			if ( !TryGetCraftStationKind( placed.StructureId, out var candidate )
			     || candidate == ThornsCraftStationKind.Hand )
				continue;

			if ( !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var def ) )
				continue;

			TryGetPlacedStructurePickBounds( placed, def, out var center, out var pickRadius );
			if ( !ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var along )
			     || along > maxRange
			     || along >= bestAlong )
				continue;

			bestAlong = along;
			best = placed;
			bestStation = candidate;
		}

		return best.IsValid();
	}

	public static bool TryPickResearchStationInFront( GameObject playerRoot, out ThornsPlacedBuildStructure structure )
	{
		structure = null;
		if ( !TryPickStructureInFront( playerRoot, out structure ) || !structure.IsValid() )
			return false;

		return string.Equals( structure.StructureId, "research", StringComparison.OrdinalIgnoreCase );
	}

	public static bool TryPickStructureInFront( GameObject playerRoot, out ThornsPlacedBuildStructure structure )
	{
		structure = null;
		if ( !playerRoot.IsValid() )
			return false;

		if ( !ThornsInteractAimPick.TryResolveCrosshairAimRay( playerRoot, out var origin, out var forward ) )
			return false;

		return TryPickStructureAlongRay( playerRoot, origin, forward, UseRange, out structure );
	}

	public static bool TryPickStructureAlongRay(
		GameObject playerRoot,
		Vector3 origin,
		Vector3 forward,
		float range,
		out ThornsPlacedBuildStructure structure )
	{
		structure = null;
		var dir = forward.Normal;
		if ( dir.Length < 0.95f || !playerRoot.IsValid() )
			return false;

		var scene = playerRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var end = origin + dir * range;
		var trace = scene.Trace
			.Sphere( ThornsInteractAimPick.DefaultSphereTraceRadius, origin, end )
			.IgnoreGameObjectHierarchy( playerRoot )
			.Run();

		if ( trace.Hit && TryGetStructureFromHit( trace, out var traceTarget ) )
		{
			if ( !TryPickStructureAlongRayRegistry( origin, dir, range, out var registryTarget ) )
			{
				structure = traceTarget;
				return true;
			}

			var traceAlong = Vector3.Dot( trace.HitPosition - origin, dir );
			var registryAlong = Vector3.Dot( registryTarget.GameObject.WorldPosition - origin, dir );
			structure = traceAlong <= registryAlong + 16f ? traceTarget : registryTarget;
			return structure.IsValid();
		}

		return TryPickStructureAlongRayRegistry( origin, dir, range, out structure );
	}

	static bool TryGetStructureFromHit( SceneTraceResult hit, out ThornsPlacedBuildStructure placed )
	{
		placed = null;
		if ( !hit.Hit || !hit.GameObject.IsValid() )
			return false;

		placed = hit.GameObject.Components.Get<ThornsPlacedBuildStructure>( FindMode.EverythingInSelfAndParent );
		return placed.IsValid() && !string.IsNullOrWhiteSpace( placed.InstanceKey );
	}

	static bool TryPickStructureAlongRayRegistry(
		Vector3 origin,
		Vector3 dir,
		float maxRange,
		out ThornsPlacedBuildStructure best )
	{
		best = null;
		var bestAlong = float.MaxValue;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || string.IsNullOrWhiteSpace( placed.InstanceKey ) )
				continue;

			if ( !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var def ) )
				continue;

			TryGetPlacedStructurePickBounds( placed, def, out var center, out var pickRadius );
			if ( !ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var along )
			     || along > maxRange
			     || along >= bestAlong )
				continue;

			bestAlong = along;
			best = placed;
		}

		return best.IsValid();
	}

	static void TryGetPlacedStructurePickBounds(
		ThornsPlacedBuildStructure placed,
		ThornsBuildDefinition def,
		out Vector3 center,
		out float pickRadius )
	{
		center = placed.GameObject.WorldPosition + Vector3.Up * (def.Size.z * 0.45f);
		pickRadius = MathF.Max( 48f, MathF.Max( def.Size.x, def.Size.y ) * 0.6f );
		if ( !placed.GameObject.IsValid() )
			return;

		foreach ( var collider in placed.GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !collider.IsValid() || !collider.Enabled || collider.IsTrigger )
				continue;

			var bounds = collider.GetWorldBounds();
			center = ( bounds.Mins + bounds.Maxs ) * 0.5f;
			pickRadius = MathF.Max( pickRadius, ( bounds.Maxs - bounds.Mins ).Length * 0.55f );
			return;
		}
	}
}
