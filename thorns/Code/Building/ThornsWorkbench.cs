#nullable disable

namespace Sandbox;

/// <summary>
/// Placed <c>workbench</c> — repair durable gear: damaged item (col 1), metal/cloth/leather (col 2), repaired output (col 3).
/// </summary>
[Title( "Thorns — Workbench" )]
[Category( "Thorns/Building" )]
[Icon( "handyman" )]
public sealed class ThornsWorkbench : Component
{
	public const int InputSlot = 0;
	public const int MaterialSlotStart = 1;
	public const int MaterialSlotCount = 8;
	public const int OutputSlot = MaterialSlotStart + MaterialSlotCount;
	public const int SlotCount = OutputSlot + 1;

	public const float InteractionRange = ThornsBuildingVisuals.WorkbenchInteractionUseRange;
	public const float ProcessSecondsPerRepair = 12f;

	/// <summary>Below this fraction of max durability the bench accepts repairs.</summary>
	public const float LowDurabilityFraction = 0.985f;

	public static readonly string[] AllowedRepairMaterialIds = { "metal", "cloth", "leather_scrap" };

	public static readonly Dictionary<Guid, ThornsWorkbench> ActiveByStructureId = new();

	readonly ThornsInventorySlot[] _slots = new ThornsInventorySlot[SlotCount];

	ThornsPlacedStructure _placed;
	ThornsInventory _lastInteractInventory;
	double _nextUiPushTime;
	double _processEndTime;
	string _processingLabel = "";
	ThornsInventorySlot _pendingRepair = ThornsInventorySlot.Empty;

	public Guid StructureInstanceId => _placed.IsValid() ? _placed.InstanceId : Guid.Empty;

	public bool HostIsProcessing => _processEndTime > Time.Now;

	protected override void OnAwake()
	{
		_placed = Components.Get<ThornsPlacedStructure>();
		if ( _placed.IsValid() && string.Equals( _placed.StructureDefId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			ActiveByStructureId[_placed.InstanceId] = this;
	}

	protected override void OnDestroy()
	{
		var id = StructureInstanceId;
		if ( id != Guid.Empty )
			ActiveByStructureId.Remove( id );

		if ( Networking.IsHost && !_pendingRepair.IsEmpty )
		{
			// Mid-repair teardown: return pending item to input if possible
			ref var inp = ref _slots[InputSlot];
			if ( inp.IsEmpty )
				inp = _pendingRepair;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !GameObject.IsValid() )
			return;

		HostTickProcessing();
	}

	public static bool TryGetForStructure( Guid instanceId, out ThornsWorkbench wb ) =>
		ActiveByStructureId.TryGetValue( instanceId, out wb ) && wb.IsValid();

	internal void HostPushSnapshotToOwner( ThornsInventory ownerInv )
	{
		if ( !Networking.IsHost || ownerInv is null || !ownerInv.IsValid() )
			return;

		_lastInteractInventory = ownerInv;

		var payload = new ThornsInventorySlotNet[SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		var prog = HostComputeProgress01();
		var remaining = HostComputeRemainingSeconds();
		ownerInv.ClientReceiveWorkbenchSnapshot(
			StructureInstanceId.ToString( "D" ),
			payload,
			_processingLabel ?? "",
			prog,
			remaining );
	}

	float HostComputeProgress01()
	{
		if ( !HostIsProcessing || ProcessSecondsPerRepair <= 0.01f )
			return 0f;
		var start = _processEndTime - ProcessSecondsPerRepair;
		var t = (Time.Now - start) / ProcessSecondsPerRepair;
		return Math.Clamp( (float)t, 0f, 1f );
	}

	float HostComputeRemainingSeconds()
	{
		if ( !HostIsProcessing )
			return 0f;
		return Math.Max( 0f, (float)(_processEndTime - Time.Now) );
	}

	void HostTickProcessing()
	{
		if ( _processEndTime > 0 && Time.Now >= _processEndTime )
			HostCompleteRepairCycle();

		if ( _processEndTime <= Time.Now )
			TryStartNextRepairCycle();

		if ( _lastInteractInventory.IsValid() && HostIsProcessing && Time.Now >= _nextUiPushTime )
		{
			_nextUiPushTime = Time.Now + 0.12;
			HostPushSnapshotToOwner( _lastInteractInventory );
		}
	}

	void HostCompleteRepairCycle()
	{
		_processEndTime = 0;
		_processingLabel = "";

		if ( _pendingRepair.IsEmpty || !ThornsItemRegistry.TryGet( _pendingRepair.ItemId, out var def ) )
		{
			_pendingRepair = ThornsInventorySlot.Empty;
			if ( _lastInteractInventory.IsValid() )
				HostPushSnapshotToOwner( _lastInteractInventory );
			return;
		}

		var max = HostGetMaxDurabilityForItem( _pendingRepair, def );
		if ( max <= 0.001f )
		{
			_pendingRepair = ThornsInventorySlot.Empty;
			if ( _lastInteractInventory.IsValid() )
				HostPushSnapshotToOwner( _lastInteractInventory );
			return;
		}

		var repaired = _pendingRepair;
		repaired.HasDurability = true;
		repaired.Durability = max;
		_pendingRepair = ThornsInventorySlot.Empty;

		if ( !HostTryDepositOutput( repaired ) )
		{
			// Should be rare — refund materials roughly by re-adding to mat slots (best-effort)
			ref var inp = ref _slots[InputSlot];
			if ( inp.IsEmpty )
				inp = repaired;
			HostRefundLastMaterialCostsBestEffort();
		}

		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
		if ( _lastInteractInventory.IsValid() )
			HostPushSnapshotToOwner( _lastInteractInventory );
	}

	int _lastCostMetal;
	int _lastCostCloth;
	int _lastCostLeather;

	void HostRefundLastMaterialCostsBestEffort()
	{
		TryAddMaterialLoose( "metal", _lastCostMetal );
		TryAddMaterialLoose( "cloth", _lastCostCloth );
		TryAddMaterialLoose( "leather_scrap", _lastCostLeather );
		_lastCostMetal = _lastCostCloth = _lastCostLeather = 0;
	}

	void TryAddMaterialLoose( string itemId, int qty )
	{
		if ( qty <= 0 || !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return;

		for ( var i = 0; i < MaterialSlotCount && qty > 0; i++ )
		{
			ref var s = ref _slots[MaterialSlotStart + i];
			if ( s.IsEmpty )
			{
				var put = Math.Min( qty, def.MaxStack );
				s = new ThornsInventorySlot { ItemId = itemId, Quantity = put };
				qty -= put;
				continue;
			}

			if ( string.Equals( s.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
			{
				var space = def.MaxStack - s.Quantity;
				var put = Math.Min( qty, space );
				if ( put > 0 )
				{
					s.Quantity += put;
					qty -= put;
				}
			}
		}
	}

	void TryStartNextRepairCycle()
	{
		if ( HostIsProcessing )
			return;

		ref var inp = ref _slots[InputSlot];
		if ( inp.IsEmpty || inp.Quantity != 1 )
			return;

		if ( !ThornsItemRegistry.TryGet( inp.ItemId, out var def ) )
			return;

		var max = HostGetMaxDurabilityForItem( inp, def );
		if ( max <= 0.001f || !inp.HasDurability )
			return;

		if ( inp.Durability >= max * LowDurabilityFraction )
			return;

		if ( !TryGetRepairMaterialCosts( def, out var cm, out var cc, out var cl ) )
			return;

		if ( !HostOutputRegionEmpty() )
			return;

		if ( !HostHasMaterialCosts( cm, cc, cl ) )
			return;

		if ( !HostConsumeMaterialCosts( cm, cc, cl ) )
			return;

		_lastCostMetal = cm;
		_lastCostCloth = cc;
		_lastCostLeather = cl;

		_pendingRepair = inp;
		inp = ThornsInventorySlot.Empty;

		_processEndTime = Time.Now + ProcessSecondsPerRepair;
		_processingLabel = ThornsItemRegistry.TryGet( _pendingRepair.ItemId, out var d2 ) ? d2.DisplayName : _pendingRepair.ItemId;
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();

		if ( _lastInteractInventory.IsValid() )
			HostPushSnapshotToOwner( _lastInteractInventory );
	}

	static bool TryGetRepairMaterialCosts( ThornsItemRegistry.ThornsItemDefinition def, out int metal, out int cloth,
		out int leather )
	{
		metal = cloth = leather = 0;
		switch ( def.ItemType )
		{
			case ThornsItemType.Armor:
				metal = 2;
				cloth = 3;
				return true;
			case ThornsItemType.Tool when def.ToolMaxDurability > 0.001f:
				metal = 3;
				cloth = 1;
				return true;
			case ThornsItemType.Weapon:
				metal = 4;
				cloth = 2;
				leather = 1;
				return true;
			default:
				return false;
		}
	}

	public static float HostGetMaxDurabilityForItem( ThornsInventorySlot s, ThornsItemRegistry.ThornsItemDefinition def )
	{
		if ( def.ItemType == ThornsItemType.Weapon && !string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) )
		{
			var w = ThornsWeaponDefinitions.Get( def.CombatWeaponDefinitionId );
			return w.MaxDurability;
		}

		if ( def.ItemType == ThornsItemType.Tool && def.ToolMaxDurability > 0.001f )
			return def.ToolMaxDurability;

		if ( def.ItemType == ThornsItemType.Armor && def.ArmorMaxDurability > 0.001f )
			return def.ArmorMaxDurability;

		return 0f;
	}

	bool HostOutputRegionEmpty()
	{
		ref var o = ref _slots[OutputSlot];
		return o.IsEmpty;
	}

	bool HostHasMaterialCosts( int needMetal, int needCloth, int needLeather ) =>
		HostCountMaterial( "metal" ) >= needMetal
		       && HostCountMaterial( "cloth" ) >= needCloth
		       && HostCountMaterial( "leather_scrap" ) >= needLeather;

	int HostCountMaterial( string id )
	{
		var n = 0;
		for ( var i = 0; i < MaterialSlotCount; i++ )
		{
			ref var s = ref _slots[MaterialSlotStart + i];
			if ( !s.IsEmpty && string.Equals( s.ItemId, id, StringComparison.OrdinalIgnoreCase ) )
				n += s.Quantity;
		}

		return n;
	}

	bool HostConsumeMaterialCosts( int needMetal, int needCloth, int needLeather ) =>
		HostRemoveMaterialTotal( "metal", needMetal )
		       && HostRemoveMaterialTotal( "cloth", needCloth )
		       && HostRemoveMaterialTotal( "leather_scrap", needLeather );

	bool HostRemoveMaterialTotal( string id, int need )
	{
		if ( need <= 0 )
			return true;

		var remaining = need;
		for ( var i = 0; i < MaterialSlotCount && remaining > 0; i++ )
		{
			ref var s = ref _slots[MaterialSlotStart + i];
			if ( s.IsEmpty || !string.Equals( s.ItemId, id, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var take = Math.Min( remaining, s.Quantity );
			s.Quantity -= take;
			remaining -= take;
			if ( s.Quantity <= 0 )
				s = ThornsInventorySlot.Empty;
		}

		return remaining <= 0;
	}

	bool HostTryDepositOutput( ThornsInventorySlot item )
	{
		ref var o = ref _slots[OutputSlot];
		if ( !o.IsEmpty )
			return false;

		o = item;
		return true;
	}

	internal bool HostApplyTransfer(
		bool fromBench,
		int fromIdx,
		bool toBench,
		int toIdx,
		ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( HostIsProcessing )
		{
			if ( fromBench && IsOutputIndex( fromIdx ) && !toBench )
			{
				ref var c = ref _slots[fromIdx];
				ref var p = ref playerInv.HostGetSlotRef( toIdx );
				if ( !TryMoveOrMergeOnto( ref c, ref p ) )
					SwapSlots( ref c, ref p );
				playerInv.HostPushInventorySnapshotToOwner();
				ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
				HostPushSnapshotToOwner( playerInv );
				return true;
			}

			return false;
		}

		if ( fromBench == toBench )
		{
			if ( !fromBench )
				return false;
			if ( !IsBenchIndex( fromIdx ) || !IsBenchIndex( toIdx ) || fromIdx == toIdx )
				return false;
			if ( !HostAllowSameGridMove( fromIdx, toIdx ) )
				return false;

			ref var a = ref _slots[fromIdx];
			ref var b = ref _slots[toIdx];
			SwapSlots( ref a, ref b );
			ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
			HostPushSnapshotToOwner( playerInv );
			return true;
		}

		if ( fromBench )
		{
			if ( !IsBenchIndex( fromIdx ) || !playerInv.HostIsValidInventorySlot( toIdx ) )
				return false;

			ref var c = ref _slots[fromIdx];
			ref var p = ref playerInv.HostGetSlotRef( toIdx );
			if ( !TryMoveOrMergeOnto( ref c, ref p ) )
				SwapSlots( ref c, ref p );
		}
		else
		{
			if ( !playerInv.HostIsValidInventorySlot( fromIdx ) || !IsBenchIndex( toIdx ) )
				return false;

			ref var p = ref playerInv.HostGetSlotRef( fromIdx );
			ref var c = ref _slots[toIdx];

			if ( IsOutputIndex( toIdx ) )
				return false;

			if ( IsInputIndex( toIdx ) )
			{
				if ( !p.IsEmpty && !HostSlotIsAllowedRepairInput( p ) )
					return false;
			}
			else if ( IsMaterialIndex( toIdx ) )
			{
				if ( !p.IsEmpty && !HostSlotIsAllowedMaterial( p ) )
					return false;
			}

			if ( !TryMoveOrMergeOnto( ref p, ref c ) )
				SwapSlots( ref p, ref c );
		}

		playerInv.HostPushInventorySnapshotToOwner();
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
		HostPushSnapshotToOwner( playerInv );
		return true;
	}

	static bool HostSlotIsAllowedMaterial( ThornsInventorySlot p )
	{
		if ( p.IsEmpty )
			return true;

		foreach ( var id in AllowedRepairMaterialIds )
		{
			if ( string.Equals( p.ItemId, id, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	static bool HostSlotIsAllowedRepairInput( ThornsInventorySlot p )
	{
		if ( p.IsEmpty || p.Quantity != 1 || !ThornsItemRegistry.TryGet( p.ItemId, out var def ) )
			return false;

		if ( !p.HasDurability )
			return false;

		var max = HostGetMaxDurabilityForItem( p, def );
		if ( max <= 0.001f )
			return false;

		return p.Durability < max * LowDurabilityFraction;
	}

	internal bool HostTryQuickTransfer( bool fromBench, int fromIdx, ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( HostIsProcessing )
			return false;

		if ( fromBench )
		{
			if ( !IsBenchIndex( fromIdx ) )
				return false;

			ref var src = ref _slots[fromIdx];
			if ( src.IsEmpty )
				return false;

			var toIdx = HostFindQuickDepositPlayerSlot( src, playerInv );
			if ( toIdx < 0 )
				return false;

			return HostApplyTransfer( true, fromIdx, false, toIdx, playerInv );
		}

		if ( !playerInv.HostIsValidInventorySlot( fromIdx ) )
			return false;

		ref var psrc = ref playerInv.HostGetSlotRef( fromIdx );
		if ( psrc.IsEmpty )
			return false;

		var benchTo = HostFindQuickDepositBenchSlot( psrc );
		if ( benchTo < 0 )
			return false;

		return HostApplyTransfer( false, fromIdx, true, benchTo, playerInv );
	}

	int HostFindQuickDepositBenchSlot( ThornsInventorySlot src )
	{
		if ( src.IsEmpty || !ThornsItemRegistry.TryGet( src.ItemId, out var def ) )
			return -1;

		if ( HostSlotIsAllowedRepairInput( src ) )
			return InputSlot;

		if ( HostSlotIsAllowedMaterial( src ) )
			return HostFindStackSlotInRegion( MaterialSlotStart, MaterialSlotCount, src );

		return -1;
	}

	int HostFindStackSlotInRegion( int start, int count, ThornsInventorySlot src )
	{
		for ( var i = 0; i < count; i++ )
		{
			ref var c = ref _slots[start + i];
			if ( c.IsEmpty )
				continue;

			if ( src.ItemId != c.ItemId || !src.EqualsStackIdentity( c ) )
				continue;

			var space = ThornsItemRegistry.GetOrNull( src.ItemId ) is { } d ? d.MaxStack - c.Quantity : 0;
			if ( space > 0 )
				return start + i;
		}

		for ( var i = 0; i < count; i++ )
		{
			ref var c = ref _slots[start + i];
			if ( c.IsEmpty )
				return start + i;
		}

		return -1;
	}

	static int HostFindQuickDepositPlayerSlot( ThornsInventorySlot src, ThornsInventory inv )
	{
		if ( src.IsEmpty || !ThornsItemRegistry.TryGet( src.ItemId, out var def ) )
			return -1;

		for ( var i = 0; i < ThornsInventory.TotalSlots; i++ )
		{
			ref var p = ref inv.HostGetSlotRef( i );
			if ( p.IsEmpty )
				continue;

			if ( src.ItemId != p.ItemId || !src.EqualsStackIdentity( p ) )
				continue;

			var space = def.MaxStack - p.Quantity;
			if ( space > 0 )
				return i;
		}

		for ( var i = 0; i < ThornsInventory.TotalSlots; i++ )
		{
			ref var p = ref inv.HostGetSlotRef( i );
			if ( p.IsEmpty )
				return i;
		}

		return -1;
	}

	static bool IsInputIndex( int i ) => i == InputSlot;

	static bool IsMaterialIndex( int i ) => i >= MaterialSlotStart && i < MaterialSlotStart + MaterialSlotCount;

	static bool IsOutputIndex( int i ) => i == OutputSlot;

	static bool IsBenchIndex( int i ) => i >= 0 && i < SlotCount;

	static bool HostAllowSameGridMove( int fromIdx, int toIdx )
	{
		if ( IsInputIndex( fromIdx ) && IsInputIndex( toIdx ) )
			return true;
		if ( IsMaterialIndex( fromIdx ) && IsMaterialIndex( toIdx ) )
			return true;
		return IsOutputIndex( fromIdx ) && IsOutputIndex( toIdx );
	}

	static void SwapSlots( ref ThornsInventorySlot a, ref ThornsInventorySlot b ) =>
		(a, b) = (b, a);

	static bool TryMoveOrMergeOnto( ref ThornsInventorySlot from, ref ThornsInventorySlot onto )
	{
		if ( from.IsEmpty )
			return false;

		if ( onto.IsEmpty )
		{
			onto = from;
			from = ThornsInventorySlot.Empty;
			return true;
		}

		if ( !ThornsItemRegistry.TryGet( from.ItemId, out var def ) )
			return false;

		if ( from.ItemId != onto.ItemId || !from.EqualsStackIdentity( onto ) )
			return false;

		var space = def.MaxStack - onto.Quantity;
		if ( space <= 0 )
			return false;

		var put = Math.Min( from.Quantity, space );
		onto.Quantity += put;
		from.Quantity -= put;
		if ( from.Quantity <= 0 )
			from = ThornsInventorySlot.Empty;

		return true;
	}

	public static bool HostValidatePlayerUseRange( GameObject pawnRoot, ThornsWorkbench wb, float range )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || wb is null || !wb.IsValid() )
			return false;

		var d = (pawnRoot.WorldPosition - wb.GameObject.WorldPosition).Length;
		return d <= range;
	}

	public static bool HostValidatePlayerUseAllowed( GameObject pawnRoot, ThornsWorkbench wb, float range )
	{
		if ( !HostValidatePlayerUseRange( pawnRoot, wb, range ) )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, wb.GameObject, range );
	}

	internal ThornsInventorySlotNet[] HostSnapshotSlotsForPersistence()
	{
		var payload = new ThornsInventorySlotNet[SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		return payload;
	}

	internal void HostRestoreSlotsFromPersistence( ThornsInventorySlotNet[] src )
	{
		if ( !Networking.IsHost || src is null )
			return;

		for ( var i = 0; i < SlotCount; i++ )
			_slots[i] = i < src.Length ? ThornsInventory.SlotFromNet( src[i] ) : ThornsInventorySlot.Empty;

		_processEndTime = 0;
		_pendingRepair = ThornsInventorySlot.Empty;
		_processingLabel = "";
		_lastCostMetal = _lastCostCloth = _lastCostLeather = 0;
	}
}
