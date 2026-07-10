namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Boulders;
using Terraingen.Buildings;
using Terraingen.Buildings.Settlement;
using Terraingen.Core;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Physics;
using Terraingen.Player;
using Terraingen.World;

/// <summary>Opt-in collision overlay — <c>thorns_collision_debug 1</c> (H binding disabled).</summary>
[Title( "Thorns Collision Debug" )]
[Category( "Debug" )]
public sealed class ThornsCollisionDebug : Component
{
	public const float MaxDrawDistanceInches = 7200f;
	public const float BuildingDetailDistanceInches = 2400f;
	public const float ForwardProbeDistanceInches = 320f;
	public const float OverlayDurationSeconds = 0.12f;

	[ConVar( "thorns_collision_debug" )]
	public static bool OverlayEnabled { get; set; }

	protected override void OnUpdate()
	{
		if ( !OverlayEnabled )
			return;

		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !TryResolveObserver( scene, out var observer, out var pawn ) )
			return;

		var duration = OverlayDurationSeconds;
		var maxDistSq = MaxDrawDistanceInches * MaxDrawDistanceInches;
		var buildingDetailDistSq = BuildingDetailDistanceInches * BuildingDetailDistanceInches;
		var overlay = DebugOverlay;

		ThornsCollisionDebugDraw.DrawLegend( overlay, observer + Vector3.Up * 88f, duration );
		DrawForwardProbe( scene, overlay, pawn, observer, duration );

		ThornsPlayerRootCache.RefreshIfStale( scene );
		foreach ( var playerRoot in ThornsPlayerRootCache.RootsReadOnly )
		{
			if ( !playerRoot.IsValid() )
				continue;

			if ( (playerRoot.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			if ( !ThornsCitizenHitbox.TryGetExtents( playerRoot, out var feet, out var height, out var radius ) )
				continue;

			ThornsCollisionDebugDraw.DrawCitizenCapsule(
				overlay,
				feet,
				height,
				radius,
				duration,
				ThornsCollisionDebugDraw.Category.Player );
		}

		foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !bandit.IsValid() || !bandit.GameObject.IsValid() )
				continue;

			var root = bandit.GameObject;
			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			if ( !ThornsCitizenHitbox.TryGetExtents( root, out var feet, out var height, out var radius ) )
				continue;

			ThornsCollisionDebugDraw.DrawCitizenCapsule(
				overlay,
				feet,
				height,
				radius,
				duration,
				ThornsCollisionDebugDraw.Category.Bandit );
		}

		foreach ( var animal in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !animal.IsValid() || !animal.GameObject.IsValid() )
				continue;

			var root = animal.GameObject;
			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject(
				overlay,
				root,
				duration,
				ThornsCollisionDebugDraw.Category.Animal );
		}

		DrawProcBuildingColliders( scene, overlay, observer, buildingDetailDistSq, duration );

		ThornsTreeWorldService.ResolveInstance()?.DrawCollisionDebugOverlay( observer, MaxDrawDistanceInches, duration );
		ThornsMineralWorldService.Instance?.DrawCollisionDebugOverlay( observer, MaxDrawDistanceInches, duration );

		DrawBoulderColliders( scene, overlay, observer, maxDistSq, duration );
		DrawLootContainers( scene, overlay, observer, maxDistSq, duration );
		DrawPlayerStructures( overlay, observer, maxDistSq, duration );
		SettlementDebugOverlay.Draw( overlay, duration );
	}

	static void DrawProcBuildingColliders(
		Scene scene,
		DebugOverlaySystem overlay,
		Vector3 observer,
		float maxDistSq,
		float duration )
	{
		foreach ( var root in scene.GetAllObjects( true ) )
		{
			if ( !root.IsValid() || !root.Name.StartsWith( "ProcBuilding_", StringComparison.Ordinal ) )
				continue;

			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnHierarchy( overlay, root, duration, includeFloors: true );
		}
	}

	static void DrawForwardProbe(
		Scene scene,
		DebugOverlaySystem overlay,
		GameObject pawn,
		Vector3 observer,
		float duration )
	{
		if ( overlay is null || pawn is null || !pawn.IsValid() )
			return;

		var forward = pawn.WorldRotation.Forward;
		if ( forward.WithZ( 0f ).LengthSquared < 0.01f )
			forward = Vector3.Forward;
		else
			forward = forward.WithZ( 0f ).Normal;

		var radius = ThornsPlayerFirstPersonRig.DefaultBodyRadius;
		var start = observer + Vector3.Up * Math.Clamp( ThornsPlayerFirstPersonRig.DefaultBodyHeight * 0.35f, 20f, 36f );
		var end = start + forward * ForwardProbeDistanceInches;
		var probeColor = ThornsCollisionDebugDraw.ColorFor( ThornsCollisionDebugDraw.Category.ProbeHit );

		var trace = scene.Trace
			.Sphere( radius * 0.92f, start, end )
			.IgnoreGameObjectHierarchy( pawn )
			.Run();

		overlay.Trace( trace, duration );

		if ( !trace.Hit || trace.GameObject is null || !trace.GameObject.IsValid() )
			return;

		var hitObject = trace.GameObject;
		while ( hitObject.IsValid() && hitObject.Parent.IsValid() && !hitObject.Components.Get<Collider>( FindMode.EnabledInSelf ).IsValid() )
			hitObject = hitObject.Parent;

		foreach ( var collider in hitObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !collider.IsValid() || !collider.Enabled || collider.IsTrigger )
				continue;

			ThornsCollisionDebugDraw.DrawCollider( overlay, collider, duration, probeColor );
		}

		var category = ThornsCollisionDebugDraw.ClassifyObject( hitObject );
		overlay.Text(
			trace.HitPosition + Vector3.Up * 20f,
			$"BLOCKED: {hitObject.Name} ({ThornsCollisionDebugDraw.LabelFor( category )})",
			duration );
	}

	void DrawPlayerStructures(
		DebugOverlaySystem overlay,
		Vector3 observer,
		float maxDistSq,
		float duration )
	{
		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !placed.GameObject.IsValid() )
				continue;

			var root = placed.GameObject;
			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnHierarchy(
				overlay,
				root,
				duration,
				ThornsCollisionDebugDraw.Category.PlayerStructure );
		}
	}

	void DrawBoulderColliders(
		Scene scene,
		DebugOverlaySystem overlay,
		Vector3 observer,
		float maxDistSq,
		float duration )
	{
		foreach ( var marker in scene.GetAllComponents<ThornsBoulderColliderMarker>() )
		{
			if ( !marker.IsValid() || !marker.GameObject.IsValid() )
				continue;

			if ( (marker.GameObject.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject(
				overlay,
				marker.GameObject,
				duration,
				ThornsCollisionDebugDraw.Category.Boulder );
		}
	}

	void DrawLootContainers(
		Scene scene,
		DebugOverlaySystem overlay,
		Vector3 observer,
		float maxDistSq,
		float duration )
	{
		var color = ThornsCollisionDebugDraw.ColorFor( ThornsCollisionDebugDraw.Category.LootContainer );

		foreach ( var crate in scene.GetAllComponents<ThornsLootableDeathCrate>() )
		{
			if ( !crate.IsValid() || !crate.GameObject.IsValid() )
				continue;

			var root = crate.GameObject;
			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject( overlay, root, duration, ThornsCollisionDebugDraw.Category.LootContainer );
			ThornsCollisionDebugDraw.DrawHorizontalRing(
				overlay,
				root.WorldPosition + Vector3.Up * 12f,
				ThornsDeathCrateWorldService.InteractRange,
				duration,
				color );
		}

		foreach ( var drop in scene.GetAllComponents<ThornsLootableAirdrop>() )
		{
			if ( !drop.IsValid() || !drop.GameObject.IsValid() )
				continue;

			var root = drop.GameObject;
			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject( overlay, root, duration, ThornsCollisionDebugDraw.Category.LootContainer );
			ThornsCollisionDebugDraw.DrawHorizontalRing(
				overlay,
				root.WorldPosition + Vector3.Up * 12f,
				ThornsAirdropWorldService.InteractRange,
				duration,
				color );
		}

		foreach ( var furniture in scene.GetAllComponents<ThornsLootableFurniture>() )
		{
			if ( !furniture.IsValid() || !furniture.GameObject.IsValid() )
				continue;

			var root = furniture.GameObject;
			if ( (root.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject( overlay, root, duration, ThornsCollisionDebugDraw.Category.LootContainer );
			ThornsCollisionDebugDraw.DrawHorizontalRing(
				overlay,
				root.WorldPosition + Vector3.Up * 12f,
				ThornsBuildingLootWorldService.InteractRange,
				duration,
				color );
		}
	}

	static bool TryResolveObserver( Scene scene, out Vector3 observer, out GameObject pawn )
	{
		observer = default;
		pawn = null;

		if ( scene is null || !scene.IsValid() )
			return false;

		var local = ThornsPlayerGameplay.Local;
		if ( local.IsValid() )
		{
			pawn = local.GameObject;
			observer = pawn.WorldPosition;
			return true;
		}

		pawn = ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( pawn.IsValid() )
		{
			observer = pawn.WorldPosition;
			return true;
		}

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( !controller.IsValid() || !controller.GameObject.IsValid() || !controller.Enabled )
				continue;

			pawn = controller.GameObject;
			observer = pawn.WorldPosition;
			return true;
		}

		return false;
	}

	/// <summary>Attaches collision debug component (overlay off until <c>thorns_collision_debug 1</c>).</summary>
	public static void EnsureOn( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		if ( host.Components.Get<ThornsCollisionDebug>( FindMode.EnabledInSelf ).IsValid() )
			return;

		host.Components.Create<ThornsCollisionDebug>();
	}
}
