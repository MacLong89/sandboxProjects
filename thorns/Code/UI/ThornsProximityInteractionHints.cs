namespace Sandbox;

/// <summary>
/// Owner client: single focused proximity prompt, presented as a stable crosshair-adjacent interaction affordance.
/// </summary>
[Title( "Thorns - Proximity interaction hints" )]
[Category( "Thorns/UI" )]
[Icon( "touch_app" )]
[Order( 81 )]
public sealed class ThornsProximityInteractionHints : Component
{
	const float StructureSearchHoriz = 420f;
	const float FocusMaxAngleDegrees = 55f;
	const float FocusAngleScoreWeight = 10f;
	const float HintUpdateIntervalSeconds = 0.15f;

	float _nextHintUpdateTime;
	ThornsGameShell _cachedShell;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( Time.Now < _nextHintUpdateTime )
			return;

		_nextHintUpdateTime = Time.Now + HintUpdateIntervalSeconds;

		if ( !_cachedShell.IsValid() )
			_cachedShell = Components.Get<ThornsGameShell>();

		if ( !_cachedShell.IsValid() || !_cachedShell.Enabled )
			return;

		var hint = ResolveHint();
		_cachedShell.SetGameplayInteractionHint( hint.Text );
	}

	HintCandidate ResolveHint()
	{
		if ( GameplayHintsBlocked() )
			return default;

		if ( ThornsTameHoldHudBridge.Phase != ThornsTameHudPhase.Hidden )
			return default;

		var pos = GameObject.WorldPosition;
		var scene = GameObject.Scene;
		var best = default( HintCandidate );
		var focus = ResolveFocusContext( pos );

		TryGetRadioHint( scene, pos, focus, ref best );
		TryGetLootCrateHint( pos, focus, ref best );
		TryGetProcFurnitureLootHint( pos, focus, ref best );
		TryGetDeathCrateHint( pos, focus, ref best );
		TryGetDoorHint( pos, focus, ref best );
		TryGetPlaceableStructureHint( pos, focus, ref best );
		TryGetHarvestHint( scene, pos, focus, ref best );

		return best;
	}

	bool GameplayHintsBlocked()
	{
		if ( ThornsHarvestInteractor.LocalOwnerGameplayInputBlocked( GameObject ) )
			return true;

		if ( !_cachedShell.IsValid() )
			_cachedShell = Components.Get<ThornsGameShell>();

		if ( _cachedShell is { IsValid: true, Enabled: true }
		     && (_cachedShell.MenuOpen || _cachedShell.BlocksGameplayShellOverlay || _cachedShell.RadioShopUiOpen
		         || _cachedShell.StorageChestUiOpen || _cachedShell.CampfireUiOpen || _cachedShell.WorkbenchUiOpen) )
			return true;

		var hp = Components.Get<ThornsHealth>();
		return hp.IsValid() && (hp.IsDeadState || !hp.IsAlive);
	}

	readonly struct HintCandidate
	{
		public readonly string Text;
		public readonly GameObject Target;
		public readonly Vector3 AnchorWorld;
		public readonly float Distance;
		public readonly float Score;
		public readonly bool HasAnchor;

		public HintCandidate( string text, GameObject target, Vector3 anchorWorld, float distance, float score )
		{
			Text = text ?? "";
			Target = target;
			AnchorWorld = anchorWorld;
			Distance = distance;
			Score = score;
			HasAnchor = !string.IsNullOrWhiteSpace( text );
		}
	}

	readonly struct FocusContext
	{
		public readonly bool Valid;
		public readonly Vector3 Origin;
		public readonly Vector3 Forward;

		public FocusContext( Vector3 origin, Vector3 forward )
		{
			Valid = forward.LengthSquared > 0.001f;
			Origin = origin;
			Forward = forward.Normal;
		}
	}

	FocusContext ResolveFocusContext( Vector3 fallbackOrigin )
	{
		var cam = Components.GetInDescendantsOrSelf<CameraComponent>( true );
		if ( cam.IsValid() && cam.Enabled )
			return new FocusContext( cam.GameObject.WorldPosition, cam.GameObject.WorldRotation.Forward );

		return new FocusContext( fallbackOrigin + Vector3.Up * 52f, GameObject.WorldRotation.Forward );
	}

	static void ConsiderHint( ref HintCandidate best, string text, GameObject target, Vector3 fallbackAnchor, float distance, FocusContext focus )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return;

		var score = distance;
		if ( focus.Valid )
		{
			var toTarget = fallbackAnchor - focus.Origin;
			var len = toTarget.Length;
			if ( len > 0.001f )
			{
				var dot = Math.Clamp( Vector3.Dot( focus.Forward, toTarget / len ), -1f, 1f );
				var angle = MathF.Acos( dot ).RadianToDegree();
				if ( angle > FocusMaxAngleDegrees )
					return;

				score += angle * FocusAngleScoreWeight;
			}
		}

		if ( best.HasAnchor && score >= best.Score )
			return;

		best = new HintCandidate( text, target, fallbackAnchor, distance, score );
	}

	static void TryGetRadioHint( Scene scene, Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		var station = ThornsRadioStation.FindNearest( scene, pos, StructureSearchHoriz );
		if ( !station.IsValid() || station.StationId == Guid.Empty || !station.HostIsInRange( pos ) )
			return;

		var d = (station.GameObject.WorldPosition - pos).Length;
		ConsiderHint( ref best, $"Press {ThornsHotTipKeys.Use} - open radio shop", station.GameObject, station.GameObject.WorldPosition, d, focus );
	}

	static void TryGetLootCrateHint( Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		foreach ( var c in ThornsLootCrate.ActiveById.Values )
		{
			if ( !c.IsValid() || c.CrateId == Guid.Empty )
				continue;

			var maxD = Math.Max( 32f, c.InteractionRadius );
			var d = (c.GameObject.WorldPosition - pos).Length;
			if ( d > maxD )
				continue;

			ConsiderHint( ref best, $"Press {ThornsHotTipKeys.Use} - open loot crate", c.GameObject, c.GameObject.WorldPosition, d, focus );
		}
	}

	static void TryGetProcFurnitureLootHint( Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		foreach ( var c in ThornsFurnitureContainer.ActiveByContainerId.Values )
		{
			if ( !c.IsValid() || !c.IsProcLootSync || c.ContainerId == Guid.Empty )
				continue;

			var maxD = Math.Max( 32f, ThornsFurnitureContainer.InteractionRange );
			var d = (c.GameObject.WorldPosition - pos).Length;
			if ( d > maxD )
				continue;

			ConsiderHint(
				ref best,
				PlaceableOpenHint( c.StructureDefIdSync ),
				c.GameObject,
				c.GameObject.WorldPosition,
				d,
				focus );
		}
	}

	static string PlaceableOpenHint( string structureDefId ) =>
		$"Press {ThornsHotTipKeys.Use} - Open {FormatFurnitureHintName( structureDefId )}";

	static string PlaceableUseHint( string structureDefId ) =>
		$"Press {ThornsHotTipKeys.Use} - Use {FormatFurnitureHintName( structureDefId )}";

	static string FormatFurnitureHintName( string structureDefId )
	{
		if ( string.IsNullOrWhiteSpace( structureDefId ) )
			return "Furniture";

		return ThornsPlaceableFurnitureCatalog.FormatDisplayName( structureDefId );
	}

	static void TryGetDeathCrateHint( Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		foreach ( var c in ThornsDeathCrate.ActiveById.Values )
		{
			if ( !c.IsValid() || c.CrateId == Guid.Empty )
				continue;

			var maxD = Math.Max( 32f, c.InteractionRadius );
			var d = (c.GameObject.WorldPosition - pos).Length;
			if ( d > maxD )
				continue;

			ConsiderHint( ref best, $"Press {ThornsHotTipKeys.Use} - loot death crate", c.GameObject, c.GameObject.WorldPosition, d, focus );
		}
	}

	static void TryGetDoorHint( Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		foreach ( var door in ThornsPlayerDoor.ActiveByFrameId.Values )
		{
			if ( !door.IsValid() )
				continue;

			var d = (door.GameObject.WorldPosition - pos).Length;
			if ( d > ThornsPlayerDoor.InteractionRange )
				continue;

			ConsiderHint( ref best, $"Press {ThornsHotTipKeys.Use} - open door", door.GameObject, door.GameObject.WorldPosition, d, focus );
		}
	}

	static void TryGetPlaceableStructureHint( Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		foreach ( var chest in ThornsStorageChest.ActiveByStructureId.Values )
		{
			if ( !chest.IsValid() )
				continue;

			var d = (chest.GameObject.WorldPosition - pos).Length;
			if ( d > ThornsStorageChest.InteractionRange )
				continue;

			ConsiderHint( ref best, PlaceableOpenHint( "storage_chest" ), chest.GameObject, chest.GameObject.WorldPosition, d, focus );
		}

		foreach ( var furniture in ThornsFurnitureContainer.ActiveByContainerId.Values )
		{
			if ( !furniture.IsValid() || furniture.IsProcLootSync )
				continue;

			var d = (furniture.GameObject.WorldPosition - pos).Length;
			if ( d > ThornsFurnitureContainer.InteractionRange )
				continue;

			ConsiderHint(
				ref best,
				PlaceableOpenHint( furniture.StructureDefIdSync ),
				furniture.GameObject,
				furniture.GameObject.WorldPosition,
				d,
				focus );
		}

		foreach ( var campfire in ThornsCampfire.ActiveByStructureId.Values )
		{
			if ( !campfire.IsValid() )
				continue;

			var d = (campfire.GameObject.WorldPosition - pos).Length;
			if ( d > ThornsCampfire.InteractionRange )
				continue;

			ConsiderHint( ref best, PlaceableUseHint( "campfire" ), campfire.GameObject, campfire.GameObject.WorldPosition, d, focus );
		}

		foreach ( var workbench in ThornsWorkbench.ActiveByStructureId.Values )
		{
			if ( !workbench.IsValid() )
				continue;

			var d = (workbench.GameObject.WorldPosition - pos).Length;
			if ( d > ThornsWorkbench.InteractionRange )
				continue;

			ConsiderHint( ref best, PlaceableUseHint( "workbench" ), workbench.GameObject, workbench.GameObject.WorldPosition, d, focus );
		}
	}

	void TryGetHarvestHint( Scene scene, Vector3 pos, FocusContext focus, ref HintCandidate best )
	{
		var node = ThornsResourceNode.FindNearestHarvestable( scene, pos, StructureSearchHoriz );
		if ( !node.IsValid() || !node.ClientIsWithinHarvestHintRange( pos ) )
			return;

		var hint = node.ResourceKind switch
		{
			ThornsResourceKind.Wood => "LMB - punch to harvest wood",
			ThornsResourceKind.Stone => "LMB - punch to harvest stone",
			ThornsResourceKind.MetalOre => ClientHasStonePickaxe()
				? "LMB - mine ore"
				: "Equip a pickaxe to mine ore (LMB)",
			ThornsResourceKind.Fiber => "",
			_ => "LMB - harvest"
		};
		if ( string.IsNullOrWhiteSpace( hint ) )
			return;

		var d = ThornsResourceNode.ClientHarvestDistanceToNode( node, pos );
		ConsiderHint( ref best, hint, node.GameObject, node.GameObject.WorldPosition, d, focus );
	}

	bool ClientHasStonePickaxe()
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return false;

		for ( var i = 0; i < ThornsInventory.HotbarSlotCount; i++ )
		{
			if ( !inv.TryGetClientMirrorSlot( i, out var slot ) || slot.IsEmpty )
				continue;

			if ( ThornsItemRegistry.TryGet( slot.ItemId, out var def )
			     && def.ItemType == ThornsItemType.Tool
			     && def.HarvestToolKind == ThornsHarvestToolKind.Pickaxe )
				return true;
		}

		return false;
	}
}
