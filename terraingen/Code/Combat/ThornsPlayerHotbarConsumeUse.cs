namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Hold Attack2 (RMB) while a survival consumable is on the active hotbar slot.</summary>
[Title( "Thorns Player Hotbar Consume Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerHotbarConsumeUse : Component
{
	float _holdSeconds;

	public bool IsConsuming =>
		_holdSeconds > 0f && TryGetActiveConsumable( out _, out _ );

	public float ConsumeHoldFraction =>
		ThornsSurvivalConsumables.ConsumeHoldSeconds <= 0f
			? 0f
			: Math.Clamp( _holdSeconds / ThornsSurvivalConsumables.ConsumeHoldSeconds, 0f, 1f );

	public bool TryGetActiveConsumablePrompt( out string displayName )
	{
		displayName = "";
		if ( !TryGetActiveConsumable( out var itemId, out displayName ) )
			return false;

		displayName = ThornsSurvivalConsumables.GetDisplayName( itemId );
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
		{
			_holdSeconds = 0f;
			return;
		}

		if ( ShouldBlockConsume() || !TryGetActiveConsumable( out _, out _ ) )
		{
			ResetHold();
			return;
		}

		if ( !Input.Down( "Attack2" ) && !Input.Down( "attack2" ) )
		{
			ResetHold();
			return;
		}

		_holdSeconds += Time.Delta;
		if ( _holdSeconds < ThornsSurvivalConsumables.ConsumeHoldSeconds )
			return;

		ResetHold();

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay is null || !TryResolveActiveHotbarSlot( out var hotbarIndex, out _ ) )
			return;

		gameplay.RequestConsumeFromSlot( ThornsContainerKind.Hotbar, hotbarIndex );
	}

	bool ShouldBlockConsume()
	{
		// AUDIT FIX: consume while dead / blocked overlays (kept alongside menu checks below).
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return true;

		if ( ThornsMenuHost.IsOpen || ThornsMenuHost.IsWorldContainerOpen || ThornsMenuHost.IsRadioShopOpen || ThornsMenuHost.IsResearchOpen )
			return true;

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return true;

		if ( Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true )
			return true;

		if ( ThornsPlayerContainerUse.HasOpenableTargetInFront( GameObject ) )
			return true;

		if ( ThornsPlayerCraftStationUse.HasCraftStationInFront( GameObject ) )
			return true;

		if ( ThornsPlayerResearchStationUse.HasResearchStationTargetInFront( GameObject ) )
			return true;

		if ( ThornsPlayerRadioShopUse.HasRadioShopTargetInFront( GameObject ) )
			return true;

		if ( ThornsPlayerBuildingController.Local?.IsHotbarPlaceModeActive == true )
			return true;

		if ( ThornsPlayerWeaponCombat.IsRangedWeaponEquipped( GameObject )
		     || ThornsPlayerBowCombat.IsBowEquipped( GameObject ) )
			return true;

		return false;
	}

	bool TryGetActiveConsumable( out string itemId, out string displayName )
	{
		itemId = "";
		displayName = "";

		if ( !TryResolveActiveHotbarSlot( out _, out var stack ) )
			return false;

		if ( stack.IsEmpty || !ThornsSurvivalConsumables.IsConsumable( stack.ItemId ) )
			return false;

		itemId = stack.ItemId;
		displayName = ThornsSurvivalConsumables.GetDisplayName( itemId );
		return true;
	}

	static bool TryResolveActiveHotbarSlot( out int hotbarIndex, out ThornsItemStack stack )
	{
		stack = ThornsItemStack.EmptyStack;
		hotbarIndex = 0;

		if ( !ThornsUiClientState.HasSnapshot )
			return false;

		hotbarIndex = ThornsUiClientState.Snapshot.Inventory.ActiveHotbarIndex;
		hotbarIndex = Math.Clamp( hotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		var slotIndex = hotbarIndex;
		ThornsInventorySlotDto dto = null;
		var slots = ThornsUiClientState.Snapshot.Inventory.Slots;
		for ( var i = 0; i < slots.Count; i++ )
		{
			var candidate = slots[i];
			if ( candidate.Container == ThornsContainerKind.Hotbar && candidate.Index == slotIndex )
			{
				dto = candidate;
				break;
			}
		}

		if ( dto is null || string.IsNullOrEmpty( dto.ItemId ) || dto.Count <= 0 )
			return true;

		stack = new ThornsItemStack
		{
			ItemId = dto.ItemId,
			Count = dto.Count,
			HasDurability = dto.HasDurability,
			Durability = dto.Durability,
			WeaponLoadedAmmo = dto.WeaponLoadedAmmo
		};
		return true;
	}

	void ResetHold() => _holdSeconds = 0f;

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
