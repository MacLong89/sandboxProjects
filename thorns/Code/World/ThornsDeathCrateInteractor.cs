using System;

namespace Sandbox;

/// <summary>
/// Local client: send loot intent to the host for the nearest <see cref="ThornsDeathCrate"/> (THORNS_EVERYTHING_DOCUMENT — server validates).
/// </summary>
[Title( "Thorns — Death Crate Interactor" )]
[Category( "Thorns" )]
[Icon( "backpack" )]
[Order( 65 )]
public sealed class ThornsDeathCrateInteractor : Component
{
	[Property] public float MaxLootRange { get; set; } = 130f;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( !ThornsInputInteract.IsUseOrInteractPressed() )
			return;

		var radioShop = Components.Get<ThornsRadioShopInteractor>();
		if ( radioShop.IsValid() && radioShop.TryUsePressedOpenRadioShop() )
			return;

		var shellEarly = Components.Get<ThornsGameShell>();
		if ( shellEarly.IsValid() && shellEarly.Enabled && shellEarly.BlocksGameplayShellOverlay )
			return;

		if ( shellEarly.IsValid() && shellEarly.StorageChestUiOpen )
		{
			shellEarly.CloseStorageChestUi();
			return;
		}

		if ( shellEarly.IsValid() && shellEarly.CampfireUiOpen )
		{
			shellEarly.CloseCampfireUi();
			return;
		}

		if ( shellEarly.IsValid() && shellEarly.WorkbenchUiOpen )
		{
			shellEarly.CloseWorkbenchUi();
			return;
		}

		var pawnRoot = GameObject;

		if ( ThornsPlayerDoor.TryFindBestUnderAim( pawnRoot, ThornsPlayerDoor.InteractionRange, out var playerDoor )
		     && playerDoor.IsValid() )
		{
			playerDoor.RequestToggleFromLocalOwner();
			return;
		}

		var invOpen = Components.Get<ThornsInventory>();

		if ( invOpen.IsValid() )
		{
			var chestHit = FindBestStorageChestUnderAim( pawnRoot, ThornsStorageChest.InteractionRange, out var storageChest );
			var furnitureHit = FindBestFurnitureContainerUnderAim(
				pawnRoot,
				ThornsFurnitureContainer.InteractionRange,
				out var furnitureContainer );
			var fireHit = FindBestCampfireUnderAim( pawnRoot, ThornsCampfire.InteractionRange, out var campfire );
			var benchHit = FindBestWorkbenchUnderAim( pawnRoot, ThornsWorkbench.InteractionRange, out var workbench );

			var best = (
				hit: false,
				chest: false,
				furniture: false,
				fire: false,
				bench: false,
				d: float.PositiveInfinity,
				chestC: default( ThornsStorageChest ),
				furnitureC: default( ThornsFurnitureContainer ),
				fireC: default( ThornsCampfire ),
				benchC: default( ThornsWorkbench ) );

			if ( chestHit && storageChest.IsValid() )
			{
				var d = (storageChest.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
				best = (true, true, false, false, false, d, storageChest, default, default, default);
			}

			if ( furnitureHit && furnitureContainer.IsValid() )
			{
				var d = (furnitureContainer.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
				if ( !best.hit || d < best.d )
					best = (true, false, true, false, false, d, default, furnitureContainer, default, default);
			}

			if ( fireHit && campfire.IsValid() )
			{
				var d = (campfire.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
				if ( !best.hit || d < best.d )
					best = (true, false, false, true, false, d, default, default, campfire, default);
			}

			if ( benchHit && workbench.IsValid() )
			{
				var d = (workbench.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
				if ( !best.hit || d < best.d )
					best = (true, false, false, false, true, d, default, default, default, workbench);
			}

			if ( best.hit )
			{
				if ( shellEarly.IsValid() && shellEarly.RadioShopUiOpen )
					shellEarly.CloseRadioShopUi();

				StopWalkingOnInteract( pawnRoot );

				if ( best.chest )
					invOpen.RequestOpenStorageChest( best.chestC.StructureInstanceId.ToString( "D" ) );
				else if ( best.furniture )
					invOpen.RequestOpenStorageChest( best.furnitureC.ContainerId.ToString( "D" ) );
				else if ( best.fire )
					invOpen.RequestOpenCampfire( best.fireC.StructureInstanceId.ToString( "D" ) );
				else
					invOpen.RequestOpenWorkbench( best.benchC.StructureInstanceId.ToString( "D" ) );

				return;
			}
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Info( "[Thorns] Death crate use ignored: player is dead" );
			return;
		}

		if ( FindBestLootCrateUnderAim( pawnRoot, MaxLootRange, out var loot ) && loot.IsValid() )
		{
			StopWalkingOnInteract( pawnRoot );
			loot.RequestLootAll( loot.CrateId );
			return;
		}

		if ( !FindBestDeathCrateUnderAim( pawnRoot, MaxLootRange, out var crate ) || !crate.IsValid() )
		{
			Log.Info( "[Thorns] Crate use: no loot or death crate in range" );
			return;
		}

		StopWalkingOnInteract( pawnRoot );
		crate.RequestLootAll( crate.CrateId );
	}

	static void StopWalkingOnInteract( GameObject pawnRoot )
	{
		var move = pawnRoot.Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() )
			move.StopLocalMovement();
	}

	static bool FindBestCampfireUnderAim( GameObject pawnRoot, float maxDist, out ThornsCampfire best )
	{
		best = default;
		ThornsCampfire pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsCampfire.ActiveByStructureId.Values )
		{
			if ( !c.IsValid() )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, c.GameObject, maxDist ) )
				continue;

			bestD = d;
			pick = c;
		}

		best = pick;
		return best.IsValid();
	}

	static bool FindBestFurnitureContainerUnderAim(
		GameObject pawnRoot,
		float maxDist,
		out ThornsFurnitureContainer best )
	{
		best = default;
		ThornsFurnitureContainer pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsFurnitureContainer.ActiveByContainerId.Values )
		{
			if ( !c.IsValid() )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, c.GameObject, maxDist ) )
				continue;

			bestD = d;
			pick = c;
		}

		best = pick;
		return best.IsValid();
	}

	static bool FindBestStorageChestUnderAim( GameObject pawnRoot, float maxDist, out ThornsStorageChest best )
	{
		best = default;
		ThornsStorageChest pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsStorageChest.ActiveByStructureId.Values )
		{
			if ( !c.IsValid() )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, c.GameObject, maxDist ) )
				continue;

			bestD = d;
			pick = c;
		}

		best = pick;
		return best.IsValid();
	}

	static bool FindBestWorkbenchUnderAim( GameObject pawnRoot, float maxDist, out ThornsWorkbench best )
	{
		best = default;
		ThornsWorkbench pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsWorkbench.ActiveByStructureId.Values )
		{
			if ( !c.IsValid() )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, c.GameObject, maxDist ) )
				continue;

			bestD = d;
			pick = c;
		}

		best = pick;
		return best.IsValid();
	}

	static bool FindBestLootCrateUnderAim( GameObject pawnRoot, float maxDist, out ThornsLootCrate best )
	{
		best = default;
		ThornsLootCrate pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsLootCrate.ActiveById.Values )
		{
			if ( !c.IsValid() || c.CrateId == Guid.Empty )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, c.GameObject, maxDist ) )
				continue;

			bestD = d;
			pick = c;
		}

		best = pick;
		return best.IsValid();
	}

	static bool FindBestDeathCrateUnderAim( GameObject pawnRoot, float maxDist, out ThornsDeathCrate best )
	{
		best = default;
		ThornsDeathCrate pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsDeathCrate.ActiveById.Values )
		{
			if ( !c.IsValid() || c.CrateId == Guid.Empty )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, c.GameObject, maxDist ) )
				continue;

			bestD = d;
			pick = c;
		}

		best = pick;
		return best.IsValid();
	}
}
