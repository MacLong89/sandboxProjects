namespace Sandbox;

/// <summary>
/// Server-authoritative armor layers (helmet / chest / pants). THORNS_EVERYTHING_DOCUMENT §3 (DR sum, 75% cap, ceil),
/// §death strip into crate (serialize these three + backpack later).
/// </summary>
[Title( "Thorns — Armor Equipment" )]
[Category( "Thorns" )]
[Icon( "shield" )]
[Order( 130 )]
public sealed class ThornsArmorEquipment : Component, Component.INetworkSpawn
{
	public const float MaxTotalDamageReductionPercent = 75f;

	readonly ThornsEquippedArmorPiece[] _equipped =
	{
		default,
		default,
		default
	};

	/// <summary>Owner-client mirror (Rpc.Owner) — non-authoritative; used by debug HUD only.</summary>
	string _uiHelmetId, _uiChestId, _uiPantsId;
	string _uiHelmetRoll, _uiChestRoll, _uiPantsRoll;
	float _uiHelmetDur, _uiChestDur, _uiPantsDur;
	int _armorMirrorVersion;
	bool _armorOwnerMirrorInitialized;

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !Networking.IsHost )
			return;

		PushArmorStateToOwner();
	}

	/// <summary>Computes mitigated damage on host. DR only from pieces with durability &gt; 0.</summary>
	public float HostComputeMitigatedDamage( float incomingDamage, out float drSumBeforeCap, out float drSumAfterCap )
	{
		drSumBeforeCap = 0f;
		drSumAfterCap = 0f;

		if ( !Networking.IsHost || incomingDamage <= 0f )
			return incomingDamage;

		drSumBeforeCap = SumEquippedDamageReductionPercent();
		drSumAfterCap = MathF.Min( drSumBeforeCap, MaxTotalDamageReductionPercent );

		Log.Info( $"[Thorns] Armor DR raw sum (before cap)={drSumBeforeCap:F1}%" );
		Log.Info( $"[Thorns] Armor DR after {MaxTotalDamageReductionPercent}% cap={drSumAfterCap:F1}%" );

		var factor = 1f - drSumAfterCap / 100f;
		var final = MathF.Ceiling( incomingDamage * factor );

		Log.Info( $"[Thorns] Damage after armor: incoming={incomingDamage:F1} final={final:F0} (ceil)" );

		return final;
	}

	/// <summary>
	/// Placeholder durability loss when HP damage is applied after mitigation — tune/replace when armor combat loop ships.
	/// </summary>
	public void HostApplyArmorDurabilityStub( float mitigatedDamageDealtToHealth )
	{
		if ( !Networking.IsHost || mitigatedDamageDealtToHealth <= 0f )
			return;

		// Stub: scale loss from mitigated damage; minimum tick so durability drains under sustained fire.
		var totalLoss = MathF.Max( 0.25f, mitigatedDamageDealtToHealth * 0.04f );
		var worn = 0;
		for ( var i = 0; i < 3; i++ )
		{
			if ( !_equipped[i].IsEmpty && _equipped[i].DurabilityRemaining > 0f )
				worn++;
		}

		if ( worn <= 0 )
			return;

		var per = totalLoss / worn;
		var changed = false;
		for ( var i = 0; i < 3; i++ )
		{
			if ( _equipped[i].IsEmpty || _equipped[i].DurabilityRemaining <= 0f )
				continue;

			var d = _equipped[i].DurabilityRemaining - per;
			if ( d < 0f )
				d = 0f;

			Log.Info( $"[Thorns] Armor durability (stub): slot={SlotName( i )} {_equipped[i].DurabilityRemaining:F2}->{d:F2} (loss={per:F2})" );

			_equipped[i] = _equipped[i] with { DurabilityRemaining = d };
			changed = true;
		}

		if ( changed )
			PushArmorStateToOwner();
	}

	static string SlotName( int i ) =>
		i switch { 0 => "helmet", 1 => "chest", _ => "pants" };

	float SumEquippedDamageReductionPercent()
	{
		var sum = 0f;
		for ( var i = 0; i < 3; i++ )
		{
			var p = _equipped[i];
			if ( p.IsEmpty || p.DurabilityRemaining <= 0f )
				continue;

			if ( !ThornsItemRegistry.TryGet( p.ItemId, out var def ) || def.ItemType != ThornsItemType.Armor )
				continue;

			var rollMul = 1f;
			if ( ThornsGearRoll.TryParseArmor( p.ArmorRollPayload ?? "", out _, out var drMul ) )
				rollMul = drMul;

			sum += def.ArmorDamageReductionPercent * rollMul;
		}

		return sum;
	}

	[Rpc.Host]
	public void RequestEquipArmorFromInventory( int inventorySlotIndex )
	{
		Log.Info( $"[Thorns] Armor equip request: invSlot={inventorySlotIndex}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] Armor equip rejected: caller does not own pawn" );
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			Log.Warning( "[Thorns] Armor equip rejected: no inventory" );
			return;
		}

		HostTryEquipArmorFromInventorySlot( inv, inventorySlotIndex );
	}

	/// <summary>Host-only: equip armor from inventory (internal callers). Returns false if nothing equipped.</summary>
	public bool HostTryEquipArmorFromInventorySlot( ThornsInventory inv, int inventorySlotIndex )
	{
		if ( !Networking.IsHost || inv is null || !inv.IsValid() )
			return false;

		if ( !inv.TryGetHostSlot( inventorySlotIndex, out var slot ) || slot.IsEmpty )
		{
			Log.Warning( $"[Thorns] Armor equip rejected: empty or invalid inventory slot {inventorySlotIndex}" );
			return false;
		}

		if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def ) || def.ItemType != ThornsItemType.Armor )
		{
			Log.Warning( $"[Thorns] Armor equip rejected: item '{slot.ItemId}' is not armor" );
			return false;
		}

		if ( def.ArmorSlot == ThornsArmorSlotKind.None )
		{
			Log.Warning( "[Thorns] Armor equip rejected: definition has no armor slot" );
			return false;
		}

		var idx = ArmorSlotToIndex( def.ArmorSlot );
		if ( idx < 0 )
		{
			Log.Warning( "[Thorns] Armor equip rejected: unknown armor slot kind" );
			return false;
		}

		if ( !_equipped[idx].IsEmpty )
		{
			Log.Warning( $"[Thorns] Armor equip rejected: slot '{def.ArmorSlot}' already occupied (unequip first)" );
			return false;
		}

		var dur = slot.HasDurability ? slot.Durability : def.ArmorMaxDurability;
		var roll = slot.ArmorRollPayload ?? "";

		var take = inv.ServerRemoveItem( inventorySlotIndex, 1 );
		if ( take < 1 )
		{
			Log.Warning( "[Thorns] Armor equip rejected: could not remove item from inventory" );
			return false;
		}

		_equipped[idx] = new ThornsEquippedArmorPiece
		{
			ItemId = def.Id,
			DurabilityRemaining = dur,
			ArmorRollPayload = roll
		};

		Log.Info( $"[Thorns] Armor equipped: {def.ArmorSlot} item={def.Id} dur={dur:F1}" );

		PushArmorStateToOwner();
		return true;
	}

	[Rpc.Host]
	public void RequestUnequipArmor( int armorSlotIndex )
	{
		Log.Info( $"[Thorns] Armor unequip request: armorSlotIndex={armorSlotIndex}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] Armor unequip rejected: caller does not own pawn" );
			return;
		}

		if ( armorSlotIndex < 0 || armorSlotIndex > 2 )
		{
			Log.Warning( $"[Thorns] Armor unequip rejected: invalid armor slot {armorSlotIndex}" );
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			Log.Warning( "[Thorns] Armor unequip rejected: no inventory" );
			return;
		}

		var piece = _equipped[armorSlotIndex];
		if ( piece.IsEmpty )
		{
			Log.Warning( "[Thorns] Armor unequip rejected: slot empty" );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( piece.ItemId, out var def ) )
		{
			Log.Warning( "[Thorns] Armor unequip rejected: unknown item id on equipped piece" );
			return;
		}

		var gridSlot = new ThornsInventorySlot
		{
			ItemId = piece.ItemId,
			Quantity = 1,
			HasDurability = true,
			Durability = piece.DurabilityRemaining,
			ArmorRollPayload = piece.ArmorRollPayload ?? ""
		};

		if ( !inv.ServerTryPlaceSingleStackInFirstEmpty( gridSlot ) )
		{
			Log.Warning( "[Thorns] Armor unequip rejected: no free inventory space" );
			return;
		}

		_equipped[armorSlotIndex] = default;

		Log.Info( $"[Thorns] Armor unequipped to backpack: slot={(armorSlotIndex == 0 ? "helmet" : armorSlotIndex == 1 ? "chest" : "pants")} item={piece.ItemId}" );

		PushArmorStateToOwner();
	}

	[Rpc.Host]
	public void RequestUnequipArmorToInventorySlot( int armorSlotIndex, int inventorySlotIndex )
	{
		Log.Info( $"[Thorns] Armor unequip-to-slot request: armorSlot={armorSlotIndex} invSlot={inventorySlotIndex}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] Armor unequip-to-slot rejected: caller does not own pawn" );
			return;
		}

		if ( armorSlotIndex < 0 || armorSlotIndex > 2 )
		{
			Log.Warning( $"[Thorns] Armor unequip-to-slot rejected: invalid armor slot {armorSlotIndex}" );
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			Log.Warning( "[Thorns] Armor unequip-to-slot rejected: no inventory" );
			return;
		}

		var piece = _equipped[armorSlotIndex];
		if ( piece.IsEmpty )
		{
			Log.Warning( "[Thorns] Armor unequip-to-slot rejected: slot empty" );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( piece.ItemId, out var def ) )
		{
			Log.Warning( "[Thorns] Armor unequip-to-slot rejected: unknown item id on equipped piece" );
			return;
		}

		var gridSlot = new ThornsInventorySlot
		{
			ItemId = piece.ItemId,
			Quantity = 1,
			HasDurability = true,
			Durability = piece.DurabilityRemaining,
			ArmorRollPayload = piece.ArmorRollPayload ?? ""
		};

		if ( !inv.HostTryPlaceArmorUnequipAtSlot( gridSlot, inventorySlotIndex ) )
		{
			Log.Warning( "[Thorns] Armor unequip-to-slot rejected: target slot not empty or invalid" );
			return;
		}

		_equipped[armorSlotIndex] = default;

		Log.Info( $"[Thorns] Armor unequipped to inv slot={inventorySlotIndex}: item={piece.ItemId}" );

		PushArmorStateToOwner();
	}

	void PushArmorStateToOwner()
	{
		if ( !Networking.IsHost )
			return;

		var h = _equipped[0];
		var c = _equipped[1];
		var p = _equipped[2];

		ClientReceiveArmorState(
			h.ItemId ?? "", h.DurabilityRemaining, h.ArmorRollPayload ?? "",
			c.ItemId ?? "", c.DurabilityRemaining, c.ArmorRollPayload ?? "",
			p.ItemId ?? "", p.DurabilityRemaining, p.ArmorRollPayload ?? "" );
	}

	[Rpc.Owner]
	void ClientReceiveArmorState(
		string helmetId, float helmetDur, string helmetRoll,
		string chestId, float chestDur, string chestRoll,
		string pantsId, float pantsDur, string pantsRoll )
	{
		var prevH = _uiHelmetId ?? "";
		var prevC = _uiChestId ?? "";
		var prevP = _uiPantsId ?? "";
		var nextH = helmetId ?? "";
		var nextC = chestId ?? "";
		var nextP = pantsId ?? "";

		_uiHelmetId = nextH;
		_uiHelmetDur = helmetDur;
		_uiHelmetRoll = helmetRoll ?? "";
		_uiChestId = nextC;
		_uiChestDur = chestDur;
		_uiChestRoll = chestRoll ?? "";
		_uiPantsId = nextP;
		_uiPantsDur = pantsDur;
		_uiPantsRoll = pantsRoll ?? "";
		_armorMirrorVersion++;
		Log.Info( $"[Thorns] Owner armor mirror: helmet={helmetId} ({helmetDur:F1}) chest={chestId} ({chestDur:F1}) pants={pantsId} ({pantsDur:F1}) revision={_armorMirrorVersion}" );

		if ( _armorOwnerMirrorInitialized
		     && Game.IsPlaying
		     && ( ( string.IsNullOrEmpty( prevH ) && !string.IsNullOrEmpty( nextH ) )
		          || ( string.IsNullOrEmpty( prevC ) && !string.IsNullOrEmpty( nextC ) )
		          || ( string.IsNullOrEmpty( prevP ) && !string.IsNullOrEmpty( nextP ) ) ) )
			ThornsGameplaySfx.PlayAtPawnEar( GameObject, ThornsGameplaySfx.ArmorEquip );

		_armorOwnerMirrorInitialized = true;
	}

	/// <summary>Debug UI: mirror revision only bumps on Rpc.Owner armor snapshot.</summary>
	public int ClientArmorMirrorRevision => _armorMirrorVersion;

	/// <summary>Debug UI: sum DR% from equipped mirror pieces (durability &gt; 0), capped at doc max (THORNS §3).</summary>
	public float GetClientUiTotalDrPercentCapped()
	{
		var sum = 0f;
		AddPiece( _uiHelmetId, _uiHelmetDur, ref sum );
		AddPiece( _uiChestId, _uiChestDur, ref sum );
		AddPiece( _uiPantsId, _uiPantsDur, ref sum );
		return MathF.Min( sum, MaxTotalDamageReductionPercent );
	}

	/// <summary>HUD mirror: 0=helmet, 1=chest, 2=pants.</summary>
	public void GetClientMirrorEquippedPiece( int slotIndex, out string itemId, out float durabilityRemaining )
	{
		itemId = "";
		durabilityRemaining = 0f;
		switch ( slotIndex )
		{
			case 0:
				itemId = _uiHelmetId ?? "";
				durabilityRemaining = _uiHelmetDur;
				break;
			case 1:
				itemId = _uiChestId ?? "";
				durabilityRemaining = _uiChestDur;
				break;
			case 2:
				itemId = _uiPantsId ?? "";
				durabilityRemaining = _uiPantsDur;
				break;
		}
	}

	/// <summary>HUD mirror: 0=helmet, 1=chest, 2=pants; includes roll payload for inspect panel parity.</summary>
	public void GetClientMirrorEquippedPieceFull( int slotIndex, out string itemId, out float durabilityRemaining, out string armorRollPayload )
	{
		itemId = "";
		durabilityRemaining = 0f;
		armorRollPayload = "";
		switch ( slotIndex )
		{
			case 0:
				itemId = _uiHelmetId ?? "";
				durabilityRemaining = _uiHelmetDur;
				armorRollPayload = _uiHelmetRoll ?? "";
				break;
			case 1:
				itemId = _uiChestId ?? "";
				durabilityRemaining = _uiChestDur;
				armorRollPayload = _uiChestRoll ?? "";
				break;
			case 2:
				itemId = _uiPantsId ?? "";
				durabilityRemaining = _uiPantsDur;
				armorRollPayload = _uiPantsRoll ?? "";
				break;
		}
	}

	static void AddPiece( string id, float dur, ref float sum )
	{
		if ( string.IsNullOrWhiteSpace( id ) || dur <= 0f )
			return;
		if ( !ThornsItemRegistry.TryGet( id, out var def ) || def.ItemType != ThornsItemType.Armor )
			return;
		sum += def.ArmorDamageReductionPercent;
	}

	bool ValidateRpcCallerOwnsPawn() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	static int ArmorSlotToIndex( ThornsArmorSlotKind kind ) =>
		kind switch
		{
			ThornsArmorSlotKind.Helmet => 0,
			ThornsArmorSlotKind.Chest => 1,
			ThornsArmorSlotKind.Pants => 2,
			_ => -1
		};

	public void HostRestoreEquippedArmorFromPersistence(
		ThornsPersistentArmorPieceDto helmet,
		ThornsPersistentArmorPieceDto chest,
		ThornsPersistentArmorPieceDto pants )
	{
		if ( !Networking.IsHost )
			return;

		void ApplyPiece( int idx, ThornsPersistentArmorPieceDto dto )
		{
			if ( dto is null || string.IsNullOrWhiteSpace( dto.ItemId ) )
			{
				_equipped[idx] = default;
				return;
			}

			_equipped[idx] = new ThornsEquippedArmorPiece
			{
				ItemId = dto.ItemId,
				DurabilityRemaining = dto.DurabilityRemaining,
				ArmorRollPayload = dto.ArmorRollPayload ?? ""
			};
		}

		ApplyPiece( 0, helmet );
		ApplyPiece( 1, chest );
		ApplyPiece( 2, pants );
		PushArmorStateToOwner();
	}

	public void HostSnapshotEquippedArmorForPersistence(
		out ThornsPersistentArmorPieceDto helmet,
		out ThornsPersistentArmorPieceDto chest,
		out ThornsPersistentArmorPieceDto pants )
	{
		helmet = PieceToDto( _equipped[0] );
		chest = PieceToDto( _equipped[1] );
		pants = PieceToDto( _equipped[2] );
	}

	static ThornsPersistentArmorPieceDto PieceToDto( ThornsEquippedArmorPiece p )
	{
		if ( p.IsEmpty )
			return null;

		return new ThornsPersistentArmorPieceDto
		{
			ItemId = p.ItemId,
			DurabilityRemaining = p.DurabilityRemaining,
			ArmorRollPayload = p.ArmorRollPayload ?? ""
		};
	}

	/// <summary>Host: clone equipped armor for death crate snapshot.</summary>
	public ThornsEquippedArmorPiece[] HostCloneEquippedForDeath()
	{
		if ( !Networking.IsHost )
			return Array.Empty<ThornsEquippedArmorPiece>();

		return new[]
		{
			_equipped[0],
			_equipped[1],
			_equipped[2]
		};
	}

	/// <summary>Host: strip all armor after body snapshot is stored (THORNS doc).</summary>
	public void HostStripAllEquippedForDeath()
	{
		if ( !Networking.IsHost )
			return;

		_equipped[0] = default;
		_equipped[1] = default;
		_equipped[2] = default;
		Log.Info( "[Thorns] Equipped armor stripped (death)" );
		PushArmorStateToOwner();
	}
}

/// <summary>One equipped armor piece — mirror for death crate / persistence (parallel to inventory row fields).</summary>
public readonly record struct ThornsEquippedArmorPiece
{
	public string ItemId { get; init; }
	public float DurabilityRemaining { get; init; }
	public string ArmorRollPayload { get; init; }

	public bool IsEmpty => string.IsNullOrWhiteSpace( ItemId );
}
