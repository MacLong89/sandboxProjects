#nullable disable

namespace Sandbox;

/// <summary>
/// Placed <c>campfire</c> — fuel column (wood), raw inputs, outputs; host processes 1 unit / <see cref="ProcessSecondsPerItem"/> with fuel.
/// </summary>
[Title( "Thorns — Campfire" )]
[Category( "Thorns/Building" )]
[Icon( "local_fire_department" )]
public sealed class ThornsCampfire : Component
{
	public const int FuelSlotStart = 0;
	public const int FuelSlotCount = 4;
	public const int RawSlotStart = 4;
	public const int RawSlotCount = 8;
	public const int OutputSlotStart = 12;
	public const int OutputSlotCount = 8;
	public const int SlotCount = FuelSlotCount + RawSlotCount + OutputSlotCount;

	public const float InteractionRange = ThornsBuildingVisuals.PlaceableInteractionUseRange;
	public const float ProcessSecondsPerItem = 10f;

	public static readonly Dictionary<Guid, ThornsCampfire> ActiveByStructureId = new();

	readonly ThornsInventorySlot[] _slots = new ThornsInventorySlot[SlotCount];

	ThornsPlacedStructure _placed;

	ThornsInventory _lastInteractInventory;
	double _nextUiPushTime;

	double _processEndTime;
	string _processingInputDisplay = "";
	string _processingOutputDisplay = "";

	public Guid StructureInstanceId => _placed.IsValid() ? _placed.InstanceId : Guid.Empty;

	public bool HostIsProcessing => _processEndTime > Time.Now;

	protected override void OnAwake()
	{
		_placed = Components.Get<ThornsPlacedStructure>();
		if ( _placed.IsValid() && string.Equals( _placed.StructureDefId, "campfire", StringComparison.OrdinalIgnoreCase ) )
			ActiveByStructureId[_placed.InstanceId] = this;
	}

	protected override void OnDestroy()
	{
		var id = StructureInstanceId;
		if ( id != Guid.Empty )
			ActiveByStructureId.Remove( id );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !GameObject.IsValid() )
			return;

		HostTickProcessing();
	}

	public static bool TryGetForStructure( Guid instanceId, out ThornsCampfire cf ) =>
		ActiveByStructureId.TryGetValue( instanceId, out cf ) && cf.IsValid();

	/// <param name="presentOverlay">True only for explicit E/open — periodic craft updates must not pop the UI when the player walked away or closed it.</param>
	internal void HostPushSnapshotToOwner( ThornsInventory ownerInv, bool presentOverlay )
	{
		if ( !Networking.IsHost || ownerInv is null || !ownerInv.IsValid() )
			return;

		_lastInteractInventory = ownerInv;

		var payload = new ThornsInventorySlotNet[SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		var prog = HostComputeProgress01();
		var remaining = HostComputeRemainingSeconds();
		ownerInv.ClientReceiveCampfireSnapshot(
			StructureInstanceId.ToString( "D" ),
			payload,
			_processingInputDisplay ?? "",
			_processingOutputDisplay ?? "",
			prog,
			remaining,
			presentOverlay );
	}

	/// <summary>Host: stop periodic snapshot pushes to this inventory (client closed the campfire UI).</summary>
	internal void HostClearLastInteractInventoryIf( ThornsInventory inv )
	{
		if ( inv is not null && inv.IsValid() && _lastInteractInventory == inv )
			_lastInteractInventory = null;
	}

	float HostComputeProgress01()
	{
		if ( !HostIsProcessing || ProcessSecondsPerItem <= 0.01f )
			return 0f;
		var start = _processEndTime - ProcessSecondsPerItem;
		var t = (Time.Now - start) / ProcessSecondsPerItem;
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
			HostCompleteProcessingCycle();

		if ( _processEndTime <= Time.Now )
			TryStartNextProcessingCycle();

		if ( _lastInteractInventory.IsValid() && HostIsProcessing && Time.Now >= _nextUiPushTime )
		{
			_nextUiPushTime = Time.Now + 0.12;
			HostPushSnapshotToOwner( _lastInteractInventory, presentOverlay: false );
		}
	}

	void HostCompleteProcessingCycle()
	{
		_processEndTime = 0;
		if ( string.IsNullOrWhiteSpace( _pendingOutputItemId ) )
		{
			_processingInputDisplay = "";
			_processingOutputDisplay = "";
			return;
		}

		if ( HostTryDepositOutput( _pendingOutputItemId, _pendingOutputQty ) )
			ThornsWorldPersistence.HostNotifyWorldStructuresDirty();

		_pendingOutputItemId = "";
		_pendingOutputQty = 0;
		_processingInputDisplay = "";
		_processingOutputDisplay = "";

		if ( _lastInteractInventory.IsValid() )
			HostPushSnapshotToOwner( _lastInteractInventory, presentOverlay: false );
	}

	string _pendingOutputItemId = "";
	int _pendingOutputQty;

	void TryStartNextProcessingCycle()
	{
		if ( !HostConsumeOneFuelWood() )
			return;

		if ( !HostTryTakeNextRawUnit( out var inputId ) )
			return;

		if ( !ThornsCampfireRecipes.TryGetOutputForInput( inputId, out var outId, out var qty ) || qty <= 0 )
			return;

		if ( !HostCanFitOutput( outId, qty ) )
		{
			HostRefundWoodOne();
			HostRefundRawOne( inputId );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( outId, out var outDef ) )
			return;

		_pendingOutputItemId = outId;
		_pendingOutputQty = qty;
		_processEndTime = Time.Now + ProcessSecondsPerItem;
		if ( ThornsItemRegistry.TryGet( inputId, out var inDef ) )
			_processingInputDisplay = inDef.DisplayName;
		_processingOutputDisplay = outDef.DisplayName;
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();

		if ( _lastInteractInventory.IsValid() )
			HostPushSnapshotToOwner( _lastInteractInventory, presentOverlay: false );
	}

	bool HostConsumeOneFuelWood()
	{
		for ( var i = 0; i < FuelSlotCount; i++ )
		{
			ref var s = ref _slots[FuelSlotStart + i];
			if ( s.IsEmpty || !ThornsCampfireRecipes.IsValidFuelItemId( s.ItemId ) )
				continue;

			s.Quantity--;
			if ( s.Quantity <= 0 )
				s = ThornsInventorySlot.Empty;

			return true;
		}

		return false;
	}

	void HostRefundWoodOne()
	{
		for ( var i = 0; i < FuelSlotCount; i++ )
		{
			ref var s = ref _slots[FuelSlotStart + i];
			if ( s.IsEmpty )
				continue;

			if ( !ThornsCampfireRecipes.IsValidFuelItemId( s.ItemId ) )
				continue;

			if ( ThornsItemRegistry.TryGet( s.ItemId, out var def ) && s.Quantity < def.MaxStack )
			{
				s.Quantity++;
				return;
			}
		}

		for ( var j = 0; j < FuelSlotCount; j++ )
		{
			ref var s = ref _slots[FuelSlotStart + j];
			if ( s.IsEmpty )
			{
				s = new ThornsInventorySlot { ItemId = "wood", Quantity = 1 };
				return;
			}
		}
	}

	void HostRefundRawOne( string inputId )
	{
		for ( var i = 0; i < RawSlotCount; i++ )
		{
			ref var s = ref _slots[RawSlotStart + i];
			if ( s.IsEmpty && ThornsItemRegistry.TryGet( inputId, out _ ) )
			{
				s = new ThornsInventorySlot { ItemId = inputId, Quantity = 1 };
				return;
			}

			if ( !s.IsEmpty && string.Equals( s.ItemId, inputId, StringComparison.OrdinalIgnoreCase ) )
			{
				if ( ThornsItemRegistry.TryGet( s.ItemId, out var def ) )
				{
					var space = def.MaxStack - s.Quantity;
					if ( space > 0 )
					{
						s.Quantity++;
						return;
					}
				}
			}
		}

		for ( var j = 0; j < RawSlotCount; j++ )
		{
			ref var s = ref _slots[RawSlotStart + j];
			if ( s.IsEmpty )
			{
				s = new ThornsInventorySlot { ItemId = inputId, Quantity = 1 };
				return;
			}
		}
	}

	bool HostTryTakeNextRawUnit( out string inputId )
	{
		inputId = "";
		for ( var i = 0; i < RawSlotCount; i++ )
		{
			ref var s = ref _slots[RawSlotStart + i];
			if ( s.IsEmpty || !ThornsCampfireRecipes.IsRawInputProcessable( s.ItemId ) )
				continue;

			inputId = s.ItemId;
			s.Quantity--;
			if ( s.Quantity <= 0 )
				s = ThornsInventorySlot.Empty;

			return true;
		}

		return false;
	}

	bool HostCanFitOutput( string itemId, int quantity )
	{
		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		for ( var i = 0; i < OutputSlotCount; i++ )
		{
			ref var s = ref _slots[OutputSlotStart + i];
			if ( s.IsEmpty )
				return true;

			if ( string.Equals( s.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
			{
				var probe = new ThornsInventorySlot { ItemId = itemId, Quantity = 1 };
				if ( s.EqualsStackIdentity( probe ) )
				{
					var space = def.MaxStack - s.Quantity;
					if ( space >= quantity )
						return true;
				}
			}
		}

		return false;
	}

	bool HostTryDepositOutput( string itemId, int quantity )
	{
		if ( quantity <= 0 || !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		for ( var i = 0; i < OutputSlotCount; i++ )
		{
			ref var s = ref _slots[OutputSlotStart + i];
			if ( s.IsEmpty )
			{
				s = new ThornsInventorySlot { ItemId = itemId, Quantity = quantity };
				return true;
			}

			if ( string.Equals( s.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
			{
				var probe = new ThornsInventorySlot { ItemId = itemId, Quantity = 1 };
				if ( !s.EqualsStackIdentity( probe ) )
					continue;

				var space = def.MaxStack - s.Quantity;
				var put = Math.Min( quantity, space );
				if ( put > 0 )
				{
					s.Quantity += put;
					return true;
				}
			}
		}

		return false;
	}

	internal bool HostApplyTransfer(
		bool fromCampfire,
		int fromIdx,
		bool toCampfire,
		int toIdx,
		ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( fromCampfire == toCampfire )
		{
			if ( !fromCampfire )
				return false;
			if ( !IsCampfireIndex( fromIdx ) || !IsCampfireIndex( toIdx ) || fromIdx == toIdx )
				return false;

			if ( !HostAllowSameGridMove( fromIdx, toIdx ) )
				return false;

			ref var a = ref _slots[fromIdx];
			ref var b = ref _slots[toIdx];
			SwapSlots( ref a, ref b );
			ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
			HostPushSnapshotToOwner( playerInv, presentOverlay: false );
			return true;
		}

		if ( fromCampfire )
		{
			if ( !IsCampfireIndex( fromIdx ) || !playerInv.HostIsValidInventorySlot( toIdx ) )
				return false;

			ref var c = ref _slots[fromIdx];
			ref var p = ref playerInv.HostGetSlotRef( toIdx );
			if ( !TryMoveOrMergeOnto( ref c, ref p ) )
				SwapSlots( ref c, ref p );
		}
		else
		{
			if ( !playerInv.HostIsValidInventorySlot( fromIdx ) || !IsCampfireIndex( toIdx ) )
				return false;

			ref var p = ref playerInv.HostGetSlotRef( fromIdx );
			ref var c = ref _slots[toIdx];

			if ( IsFuelIndex( toIdx ) )
			{
				if ( !p.IsEmpty && !ThornsCampfireRecipes.IsValidFuelItemId( p.ItemId ) )
					return false;
			}
			else if ( IsRawIndex( toIdx ) )
			{
				if ( !p.IsEmpty && !ThornsCampfireRecipes.IsRawInputProcessable( p.ItemId ) )
					return false;
			}
			else if ( IsOutputIndex( toIdx ) )
				return false;

			if ( !TryMoveOrMergeOnto( ref p, ref c ) )
				SwapSlots( ref p, ref c );
		}

		playerInv.HostPushInventorySnapshotToOwner();
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
		HostPushSnapshotToOwner( playerInv, presentOverlay: false );
		return true;
	}

	static bool IsFuelIndex( int i ) => i >= FuelSlotStart && i < FuelSlotStart + FuelSlotCount;

	static bool IsRawIndex( int i ) => i >= RawSlotStart && i < RawSlotStart + RawSlotCount;

	static bool IsOutputIndex( int i ) => i >= OutputSlotStart && i < OutputSlotStart + OutputSlotCount;

	static bool IsCampfireIndex( int i ) => i >= 0 && i < SlotCount;

	static bool HostAllowSameGridMove( int fromIdx, int toIdx )
	{
		var fF = IsFuelIndex( fromIdx );
		var tF = IsFuelIndex( toIdx );
		if ( fF && tF )
			return true;
		var fR = IsRawIndex( fromIdx );
		var tR = IsRawIndex( toIdx );
		if ( fR && tR )
			return true;
		var fO = IsOutputIndex( fromIdx );
		var tO = IsOutputIndex( toIdx );
		return fO && tO;
	}

	internal bool HostTryQuickTransfer( bool fromCampfire, int fromIdx, ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( fromCampfire )
		{
			if ( !IsCampfireIndex( fromIdx ) )
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

		var campTo = HostFindQuickDepositCampfireSlot( psrc );
		if ( campTo < 0 )
			return false;

		return HostApplyTransfer( false, fromIdx, true, campTo, playerInv );
	}

	int HostFindQuickDepositCampfireSlot( ThornsInventorySlot src )
	{
		if ( src.IsEmpty || !ThornsItemRegistry.TryGet( src.ItemId, out var def ) )
			return -1;

		if ( ThornsCampfireRecipes.IsValidFuelItemId( src.ItemId ) )
			return HostFindStackSlotInRegion( FuelSlotStart, FuelSlotCount, src );

		if ( ThornsCampfireRecipes.IsRawInputProcessable( src.ItemId ) )
			return HostFindStackSlotInRegion( RawSlotStart, RawSlotCount, src );

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

	public static bool HostValidatePlayerUseRange( GameObject pawnRoot, ThornsCampfire campfire, float range )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || campfire is null || !campfire.IsValid() )
			return false;

		var d = (pawnRoot.WorldPosition - campfire.GameObject.WorldPosition).Length;
		return d <= range;
	}

	/// <summary>Host: distance plus view toward the campfire (matches client E-press selection).</summary>
	public static bool HostValidatePlayerUseAllowed( GameObject pawnRoot, ThornsCampfire campfire, float range )
	{
		if ( !HostValidatePlayerUseRange( pawnRoot, campfire, range ) )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, campfire.GameObject, range );
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
		_pendingOutputItemId = "";
		_pendingOutputQty = 0;
		_processingInputDisplay = "";
		_processingOutputDisplay = "";
	}
}
