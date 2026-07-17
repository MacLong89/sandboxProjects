namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen;
using Terraingen.Buildings;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI.Core;
using Terraingen.World;

/// <summary>Press Use (E) on loot containers to open drag-drop transfer UI; ESC closes.</summary>
[Title( "Thorns Player Container Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerContainerUse : Component
{
	ThornsPlayerAnimalTaming _taming;

	protected override void OnAwake()
	{
		_taming = Components.Get<ThornsPlayerAnimalTaming>();
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
			return;

		if ( gameplay.HasOpenWorldContainer )
		{
			if ( !ThornsMenuHost.IsWorldContainerOpen )
				gameplay.RequestCloseWorldContainer();
			else if ( Input.Pressed( "Menu" ) || Input.Pressed( "Cancel" ) )
				gameplay.RequestCloseWorldContainer();

			return;
		}

		// AUDIT FIX: Use while dead or while a non-container overlay is up (inventory, etc.).
		// AllowUse intentionally does NOT use BlocksGameplayInput when ONLY a world container is open —
		// that path already returns above. Here we still refuse dead pawns / inventory modals.
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return;

		if ( !Input.Pressed( "Use" ) && !Input.Pressed( "use" ) )
			return;

		if ( _taming is not null && _taming.HasTameTargetInFront() )
			return;

		if ( Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true )
			return;

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return;

		if ( ThornsPlayerNpcGuildCoreUse.HasCoreTargetInFront( GameObject ) )
			return;

		if ( ThornsPlayerBloomSeedUse.HasBloomSeedTargetInFront( GameObject ) )
			return;

		if ( ThornsPlayerDoorUse.HasOwnedDoorTargetInFront( GameObject ) )
			return;

		if ( ThornsPlayerResearchStationUse.HasResearchStationTargetInFront( GameObject ) )
			return;

		if ( !TryResolveOpenableContainerKey( GameObject, out var containerKey ) )
			return;

		gameplay.RequestOpenWorldContainer( containerKey );
		ThornsPlayerUseGrabPresentation.PlayOpenLoot( GameObject );
	}

	public static bool HasOpenableTargetInFront( GameObject playerRoot )
	{
		if ( ThornsPlayerNpcGuildCoreUse.HasCoreTargetInFront( playerRoot ) )
			return false;

		if ( ThornsPlayerBloomSeedUse.HasBloomSeedTargetInFront( playerRoot ) )
			return false;

		if ( ThornsPlayerResearchStationUse.HasResearchStationTargetInFront( playerRoot ) )
			return false;

		if ( ThornsPlayerCampfireUse.HasCampfireTargetInFront( playerRoot ) )
			return false;

		return TryResolveOpenableContainerKey( playerRoot, out _ );
	}

	public static bool TryResolveOpenableContainerKey( GameObject playerRoot, out string containerKey )
	{
		containerKey = "";

		if ( TryResolveStructureStorageKey( playerRoot, out containerKey ) )
			return true;

		if ( TryResolveDeathCrateKey( playerRoot, out containerKey ) )
			return true;

		if ( TryResolveAirdropKey( playerRoot, out containerKey ) )
			return true;

		return TryResolveFurnitureKey( playerRoot, out containerKey );
	}

	static bool TryResolveFurnitureKey( GameObject playerRoot, out string containerKey )
	{
		containerKey = "";
		var service = ThornsBuildingLootWorldService.Instance;
		if ( service is null || !service.IsValid() )
			return false;

		if ( !service.TryPickFurnitureInFront( playerRoot, out _, out containerKey ) )
			return false;

		return !string.IsNullOrWhiteSpace( containerKey );
	}

	static bool TryResolveAirdropKey( GameObject playerRoot, out string containerKey )
	{
		containerKey = "";
		var service = ThornsAirdropWorldService.Instance;
		if ( service is null || !service.IsValid() )
			return false;

		if ( !service.TryPickAlongRay( playerRoot, out var airdropId, out _ ) || airdropId <= 0 )
			return false;

		containerKey = ThornsWorldLootContainerService.AirdropKey( airdropId );
		return true;
	}

	static bool TryResolveDeathCrateKey( GameObject playerRoot, out string containerKey )
	{
		containerKey = "";
		var service = ThornsDeathCrateWorldService.Instance;
		if ( service is null || !service.IsValid() )
			return false;

		if ( !service.TryPickAlongRay( playerRoot, out var crateId, out _ ) || crateId <= 0 )
			return false;

		containerKey = ThornsWorldLootContainerService.DeathCrateKey( crateId );
		return true;
	}

	static bool TryResolveStructureStorageKey( GameObject playerRoot, out string containerKey )
	{
		containerKey = "";
		if ( !ThornsInteractAimPick.TryResolveCrosshairAimRay( playerRoot, out var origin, out var forward ) )
			return false;

		return TryPickStorageStructureAlongRay( playerRoot, origin, forward, 220f, out containerKey );
	}

	static bool TryPickStorageStructureAlongRay(
		GameObject playerRoot,
		Vector3 origin,
		Vector3 forward,
		float range,
		out string containerKey )
	{
		containerKey = "";
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

		ThornsPlacedBuildStructure picked = null;
		if ( trace.Hit && TryGetStorageStructureFromHit( trace, out var traceTarget ) )
		{
			if ( !TryPickStorageAlongRayRegistry( origin, dir, range, out var registryTarget ) )
			{
				picked = traceTarget;
			}
			else
			{
				var traceAlong = Vector3.Dot( trace.HitPosition - origin, dir );
				var registryAlong = Vector3.Dot( registryTarget.GameObject.WorldPosition - origin, dir );
				picked = traceAlong <= registryAlong + 16f ? traceTarget : registryTarget;
			}
		}
		else if ( !TryPickStorageAlongRayRegistry( origin, dir, range, out picked ) )
		{
			return false;
		}

		if ( !picked.IsValid() || string.IsNullOrWhiteSpace( picked.InstanceKey ) )
			return false;

		// AUDIT FIX: do not advertise / resolve foreign storage for Use prompts.
		// Host ownership is authoritative; this keeps prompt text from lying.
		var callerGameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		var callerKey = callerGameplay.IsValid()
			? callerGameplay.AccountKey
			: ThornsPersistenceIdentity.GetStableAccountKey( playerRoot );
		if ( !ThornsBuildingOwnership.HostAccountMayUseStructure( picked.OwnerAccountKey, callerKey ) )
			return false;

		containerKey = ThornsWorldLootContainerService.StructureKey( picked.InstanceKey );
		return true;
	}

	static bool TryGetStorageStructureFromHit( SceneTraceResult hit, out ThornsPlacedBuildStructure placed )
	{
		placed = null;
		if ( !hit.Hit || !hit.GameObject.IsValid() )
			return false;

		placed = hit.GameObject.Components.Get<ThornsPlacedBuildStructure>( FindMode.EverythingInSelfAndParent );
		if ( !placed.IsValid() || !ThornsPlacedStructureStorage.IsStorageStructure( placed.StructureId ) )
		{
			placed = null;
			return false;
		}

		return true;
	}

	static bool TryPickStorageAlongRayRegistry(
		Vector3 origin,
		Vector3 dir,
		float maxRange,
		out ThornsPlacedBuildStructure best )
	{
		best = null;
		var bestAlong = float.MaxValue;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlacedStructureStorage.IsStorageStructure( placed.StructureId ) )
				continue;

			if ( string.IsNullOrWhiteSpace( placed.InstanceKey ) )
				continue;

			if ( !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var def ) )
				continue;

			var center = placed.GameObject.WorldPosition + Vector3.Up * (def.Size.z * 0.5f);
			var pickRadius = MathF.Max( 48f, def.Size.Length * 0.55f );
			if ( !ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var along )
			     || along > maxRange
			     || along >= bestAlong )
				continue;

			bestAlong = along;
			best = placed;
		}

		return best.IsValid();
	}

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocallyControlledPawn( GameObject );

	/// <summary>Host-side gate: player must be looking at the container or within interact range.</summary>
	public static bool HostPlayerCanAccessContainer( GameObject playerRoot, string containerKey )
	{
		if ( playerRoot is null || !playerRoot.IsValid() || string.IsNullOrWhiteSpace( containerKey ) )
			return false;

		// AUDIT FIX: dead players must not open / keep using world containers via RPC.
		if ( ThornsPlayerActionGate.BlocksHostWorldActions( playerRoot ) )
			return false;

		// AUDIT FIX: player-placed structure storage requires owner (or guild). World loot stays public.
		if ( !HostPlayerMayAccessContainerOwnership( playerRoot, containerKey ) )
			return false;

		if ( TryResolveOpenableContainerKey( playerRoot, out var pickedKey )
		     && string.Equals( pickedKey, containerKey, StringComparison.Ordinal ) )
			return true;

		if ( !TryGetContainerWorldPosition( containerKey, out var worldPos ) )
			return false;

		return Vector3.DistanceBetween( playerRoot.WorldPosition, worldPos ) <= ContainerAccessMaxDistance;
	}

	/// <summary>
	/// AUDIT FIX: <c>struct:</c> storage chests were openable by anyone in range.
	/// Death crates / airdrops / furniture remain public world loot.
	/// Revert: delete this method and the call above if intentional free-loot bases return.
	/// </summary>
	public static bool HostPlayerMayAccessContainerOwnership( GameObject playerRoot, string containerKey )
	{
		if ( string.IsNullOrWhiteSpace( containerKey )
		     || !containerKey.StartsWith( "struct:", StringComparison.Ordinal ) )
			return true;

		var instanceKey = containerKey["struct:".Length..];
		if ( string.IsNullOrWhiteSpace( instanceKey )
		     || !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var placed )
		     || !placed.IsValid() )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		var callerKey = gameplay.IsValid()
			? gameplay.AccountKey
			: ThornsPersistenceIdentity.GetStableAccountKey( playerRoot );

		return ThornsBuildingOwnership.HostAccountMayUseStructure( placed.OwnerAccountKey, callerKey );
	}

	public const float ContainerAccessMaxDistance = 260f;

	public static bool TryGetContainerWorldPosition( string containerKey, out Vector3 worldPos )
	{
		worldPos = default;
		if ( string.IsNullOrWhiteSpace( containerKey ) )
			return false;

		if ( containerKey.StartsWith( "furn:", StringComparison.Ordinal )
		     && int.TryParse( containerKey.AsSpan( 5 ), out var furnitureId )
		     && ThornsBuildingLootWorldService.Instance?.TryGetFurnitureWorldPosition( furnitureId, out worldPos ) == true )
			return true;

		if ( containerKey.StartsWith( "death:", StringComparison.Ordinal )
		     && int.TryParse( containerKey.AsSpan( 6 ), out var crateId )
		     && ThornsDeathCrateWorldService.Instance?.TryGetCrateWorldPosition( crateId, out worldPos ) == true )
			return true;

		if ( containerKey.StartsWith( "air:", StringComparison.Ordinal )
		     && int.TryParse( containerKey.AsSpan( 4 ), out var airdropId )
		     && ThornsAirdropWorldService.Instance?.TryGetAirdropWorldPosition( airdropId, out worldPos ) == true )
			return true;

		if ( !containerKey.StartsWith( "struct:", StringComparison.Ordinal ) )
			return false;

		var instanceKey = containerKey["struct:".Length..];
		if ( string.IsNullOrWhiteSpace( instanceKey )
		     || !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var placed )
		     || !placed.IsValid() )
			return false;

		worldPos = placed.GameObject.WorldPosition;
		return true;
	}
}
