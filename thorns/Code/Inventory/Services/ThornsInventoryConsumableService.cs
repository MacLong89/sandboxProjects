namespace Sandbox;

public sealed class ThornsInventoryConsumableService
{
	IThornsInventoryConsumableHost _host;

	int _consumablePendingSlot = -1;
	double _consumableReadyAt;
	double _consumableCooldownUntil;

	public void Bind( IThornsInventoryConsumableHost host ) => _host = host;

	public void OnFixedUpdate()
	{
		if ( !Networking.IsHost || _consumablePendingSlot < 0 )
			return;

		if ( Time.Now < _consumableReadyAt )
			return;

		var slot = _consumablePendingSlot;
		_consumablePendingSlot = -1;
		ServerFinalizeConsumableUse( slot, fromDelayedChannel: true );
	}

	public void RequestUseItemFromSlot( int slotIndex )
	{
		Log.Info( $"[Thorns] Use item request received slot={slotIndex} caller={_host.RpcCaller?.Id}" );

		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
		{
			_host.NotifyConsumableRejected( "not_owner" );
			return;
		}

		if ( _host.IsPlayerDead() )
		{
			_host.NotifyConsumableRejected( "dead" );
			return;
		}

		if ( !_host.IsValidSlot( slotIndex ) )
		{
			_host.NotifyConsumableRejected( "invalid_slot" );
			return;
		}

		if ( Time.Now < _consumableCooldownUntil )
		{
			_host.NotifyConsumableRejected( "cooldown" );
			return;
		}

		if ( _consumablePendingSlot >= 0 )
		{
			_host.NotifyConsumableRejected( "already_using" );
			return;
		}

		if ( !_host.TryGetHostSlot( slotIndex, out var slot ) || slot.IsEmpty || slot.Quantity <= 0 )
		{
			_host.NotifyConsumableRejected( "empty_slot" );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def ) || !ThornsItemRegistry.IsUsableConsumable( def ) )
		{
			_host.NotifyConsumableRejected( "not_usable_consumable" );
			return;
		}

		if ( def.ConsumableKind != ThornsConsumableKind.Medical )
		{
			if ( !_host.HasVitalsComponent() )
			{
				_host.NotifyConsumableRejected( "no_vitals_component" );
				return;
			}
		}

		var channel = def.UseTimeSeconds;
		if ( channel > 0.01f )
		{
			_consumablePendingSlot = slotIndex;
			_consumableReadyAt = Time.Now + channel;
			Log.Info( $"[Thorns] Consumable channel started slot={slotIndex} item={def.Id} kind={def.ConsumableKind} delay={channel:F2}s qty={slot.Quantity}" );
			return;
		}

		ServerFinalizeConsumableUse( slotIndex, fromDelayedChannel: false );
	}

	public void HostCancelPendingConsumableUse()
	{
		if ( !Networking.IsHost )
			return;

		if ( _consumablePendingSlot < 0 )
			return;

		Log.Info( $"[Thorns] Pending consumable cancelled slot={_consumablePendingSlot}" );
		_consumablePendingSlot = -1;
	}

	void ServerFinalizeConsumableUse( int slotIndex, bool fromDelayedChannel )
	{
		if ( !Networking.IsHost )
			return;

		if ( _host.IsPlayerDead() )
		{
			if ( fromDelayedChannel )
				Log.Warning( "[Thorns] Consumable apply aborted (dead after channel)" );
			else
				_host.NotifyConsumableRejected( "dead" );
			return;
		}

		if ( !_host.TryGetHostSlot( slotIndex, out var slot ) || slot.IsEmpty || slot.Quantity <= 0 )
		{
			if ( fromDelayedChannel )
				Log.Warning( "[Thorns] Consumable apply aborted (slot empty after channel)" );
			else
				_host.NotifyConsumableRejected( "empty_slot" );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def ) || !ThornsItemRegistry.IsUsableConsumable( def ) )
		{
			_host.NotifyConsumableRejected( "not_usable_consumable" );
			return;
		}

		var qtyBefore = slot.Quantity;
		Log.Info( $"[Thorns] Consumable item type detected item={def.Id} kind={def.ConsumableKind} qtyBefore={qtyBefore}" );

		if ( def.ConsumableKind == ThornsConsumableKind.Explosive )
		{
			if ( fromDelayedChannel )
				Log.Warning( "[Thorns] C4 cannot be used from inventory — equip on hotbar and place with LMB" );
			else
				_host.NotifyConsumableRejected( "c4_equip_to_place" );
			return;
		}

		HostApplyConsumableEffects( def );

		_host.ServerRemoveItem( slotIndex, 1 );

		var qtyAfter = 0;
		if ( _host.TryGetHostSlot( slotIndex, out var after ) && !after.IsEmpty )
			qtyAfter = after.Quantity;

		Log.Info( $"[Thorns] Consumable finished item={def.Id} quantity {qtyBefore}→{qtyAfter} removed={(qtyAfter == 0 ? "stack_gone" : "partial")}" );

		_consumableCooldownUntil = Time.Now + 0.15;

		var vitals = _host.GetComponent<ThornsVitals>();
		if ( vitals.IsValid() )
			_host.RpcNotifyOwnerConsumableApplied( def.Id, def.ConsumableKind.ToString(), vitals.Hunger, vitals.Thirst, vitals.PoisonLevel );
		else
			_host.RpcNotifyOwnerConsumableApplied( def.Id, def.ConsumableKind.ToString(), 0f, 0f, 0f );
	}

	void HostApplyConsumableEffects( ThornsItemRegistry.ThornsItemDefinition def )
	{
		var vitals = _host.GetComponent<ThornsVitals>();
		var hp = _host.GetComponent<ThornsHealth>();

		switch ( def.ConsumableKind )
		{
			case ThornsConsumableKind.Food:
				if ( vitals.IsValid() )
					vitals.HostRestoreHunger( def.HungerRestore, def.Id );
				_host.GetComponent<ThornsPlayerMilestones>()?.HostRecordEvent( ThornsMilestoneEventTokens.ConsumeFood );
				break;
			case ThornsConsumableKind.WaterClean:
				if ( vitals.IsValid() )
					vitals.HostRestoreThirst( def.ThirstRestore, def.Id );
				break;
			case ThornsConsumableKind.WaterDirty:
				if ( vitals.IsValid() )
				{
					vitals.HostRestoreThirst( def.ThirstRestore, def.Id );
					vitals.HostAddPoison( def.PoisonAmount, def.Id );
				}

				break;
			case ThornsConsumableKind.Medical:
				if ( hp.IsValid() )
					hp.HostApplyHealing( def.HealthRestore, $"consumable:{def.Id}" );
				break;
			default:
				Log.Warning( $"[Thorns] Consumable kind not handled: {def.ConsumableKind}" );
				break;
		}
	}
}
