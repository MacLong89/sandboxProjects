#nullable disable

namespace Sandbox;

/// <summary>Local build mode + ghost; host placement authority (THORNS_EVERYTHING_DOCUMENT §12, §5 building).</summary>
[Title( "Thorns — Building (place mode)" )]
[Category( "Thorns" )]
[Icon( "construction" )]
[Order( 72 )]
public sealed class ThornsBuildingController : Component
{
	const string GhostName = "ThornsBuildGhost";

	/// <summary>Crafted kit item — equipping in hotbar enables place mode (ghost + LMB) without opening build (B).</summary>
	public const string StorageChestKitItemId = "storage_chest_kit";

	public const string CampfireKitItemId = "campfire_kit";

	public const string WorkbenchKitItemId = "workbench_kit";

	public const string BedKitItemId = "bed_kit";

	[Property] public float PreviewYawStepDegrees { get; set; } = 90f;

	public bool BuildModeActive { get; private set; }

	public string SelectedStructureDefId { get; private set; }

	public ThornsBuildToolMode ToolMode { get; private set; } = ThornsBuildToolMode.Place;

	public int SelectedBuildToolbarSlot { get; private set; }

	float _previewYawDegrees;

	GameObject _ghostRoot;

	string _ghostPreviewStructureDefId = "";

	readonly List<ThornsPlacedStructure> _ghostPlacementSceneStructures = new();

	readonly List<ThornsPlacedStructure> _ghostPlacementNearStructures = new();

	/// <summary>Increments once per local-owner <see cref="OnUpdate"/> — used to throttle structure enumeration (no <c>Time.Frame</c> in Sandbox).</summary>
	int _ghostPlacementLocalOwnerUpdateOrdinal;

	int _ghostPlacementSceneStructuresLastFullScanOrdinal = int.MinValue;

	/// <summary>When true, <see cref="_ghostRoot"/> was configured for upgrade/remove highlight — must be destroyed before place-mode ghost reuse.</summary>
	bool _ghostUsedForInteractionHighlight;

	const int UpgradeRemoveHighlightIntervalFrames = 6;

	/// <summary>When true, <see cref="_ghostRoot"/> is the C4 charge preview (not a structure mesh).</summary>
	bool _ghostIsC4;

	protected override void OnStart()
	{
		if ( ThornsBuildToolbar.Entries.Length > 0 )
			ApplyToolbarEntry( ThornsBuildToolbar.Entries[0], logSelection: false );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		_ghostPlacementLocalOwnerUpdateOrdinal++;

		ThornsGameplayDiagnostics.TryFlushPeriodicLogs();

		var hud = Components.Get<ThornsDebugHudHost>();
		var hp = Components.Get<ThornsHealth>();
		var shell = Components.Get<ThornsGameShell>();
		if ( shell is { IsValid: true, Enabled: true } && shell.BlocksGameplayShellOverlay )
			return;
		if ( hud.IsValid() && (hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop) )
			return;
		if ( hp.IsValid() && hp.IsDeadState )
			return;

		if ( Input.Keyboard.Pressed( "b" ) )
			SetBuildMode( !BuildModeActive );

		var hotbar = Components.Get<ThornsHotbarEquipment>();
		var c4Equipped = hotbar.IsValid()
		                 && ThornsC4.IsEquippedPlacementItem( hotbar.ClientMirrorActiveItemId );

		var portableKitEquipped = false;
		ThornsPlaceableFurnitureCatalog.Entry equippedKitEntry = default;
		ThornsStructureDefinition equippedKitDef = default;
		if ( !c4Equipped
		     && hotbar.IsValid()
		     && ThornsPlaceableFurnitureCatalog.TryGetKit( hotbar.ClientMirrorActiveItemId, out equippedKitEntry )
		     && ThornsBuildingDefinitions.TryGet( equippedKitEntry.StructureDefId, out equippedKitDef ) )
			portableKitEquipped = true;

		if ( BuildModeActive )
		{
			for ( var i = 0; i < ThornsBuildToolbar.SlotCount; i++ )
			{
				var key = $"{i + 1}";
				if ( Input.Keyboard.Pressed( key ) )
					SelectBuildToolbarSlot( i, playInteractionSfx: true );
			}

			var yawDelta = 0f;
			if ( Input.Keyboard.Pressed( "q" ) )
				yawDelta -= PreviewYawStepDegrees;
			if ( Input.Keyboard.Pressed( "e" ) || Input.Keyboard.Pressed( "r" ) )
				yawDelta += PreviewYawStepDegrees;

			if ( MathF.Abs( yawDelta ) > 0.01f )
			{
				_previewYawDegrees = (_previewYawDegrees + yawDelta) % 360f;
				if ( _previewYawDegrees < 0f )
					_previewYawDegrees += 360f;
				Log.Info( $"[Thorns] Build preview rotate yaw={_previewYawDegrees:F0}°" );
			}

			if ( ToolMode == ThornsBuildToolMode.Place
			     && ThornsBuildingDefinitions.TryGet( SelectedStructureDefId, out var defSel ) )
			{
				if ( _ghostUsedForInteractionHighlight )
				{
					ClientDestroyGhost();
					_ghostUsedForInteractionHighlight = false;
				}

				ClientEnsureGhost();
				ClientUpdateGhostPose( defSel );
			}
			else if ( ToolMode is ThornsBuildToolMode.Upgrade or ThornsBuildToolMode.Remove )
			{
				if ( _ghostPlacementLocalOwnerUpdateOrdinal % UpgradeRemoveHighlightIntervalFrames == 0 )
					ClientUpdateUpgradeRemoveHighlightGhost();
			}
			else
				ClientHideGhostVisual();

			if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
			{
				if ( ToolMode == ThornsBuildToolMode.Remove )
				{
					RequestDemolishFocusedStructure();
					return;
				}

				if ( ToolMode == ThornsBuildToolMode.Upgrade )
				{
					RequestUpgradeFocusedStructure();
					return;
				}

				if ( !ThornsBuildingDefinitions.TryGet( SelectedStructureDefId, out var placeDef ) )
					return;

				if ( !ClientTryComputePlacement( placeDef, out var pos, out var rot, out var snap ) )
				{
					Log.Warning(
						"[Thorns] Placement request skipped — could not resolve preview (camera/eye, distance to aim, or no snap near look)." );
					return;
				}

				if ( !PlacementAllowedCommit( placeDef, snap ) )
				{
					Log.Warning( "[Thorns] Placement request skipped — no valid snap for this piece" );
					return;
				}

				Log.Info( $"[Thorns] Placement request sent def={SelectedStructureDefId} pos={pos} snap={(snap.UsesSocketSnap ? $"{snap.HostSnap.InstanceGuid}:{snap.HostSnap.SocketIndex}" : "terrain_or_free")}" );
				RequestPlaceStructure(
					SelectedStructureDefId,
					snap.UsesSocketSnap,
					snap.UsesSocketSnap ? snap.HostSnap.InstanceGuid : Guid.Empty,
					snap.UsesSocketSnap ? snap.HostSnap.SocketIndex : (ushort)0,
					snap.Channel,
					snap.IncomingPlugIndex,
					snap.OppositeTwinSocketPreview,
					pos,
					rot,
					snap.TerrainKind == ThornsTerrainSeedKind.SlabOnRay,
					_previewYawDegrees );
			}

			return;
		}

		// Hotbar placeable kits: ghost + LMB without opening build palette (B).
		if ( portableKitEquipped )
		{
			ClientTryPortableKitHotbarPlace( equippedKitEntry.StructureDefId, equippedKitDef );
			return;
		}

		if ( c4Equipped )
		{
			ClientEnsureC4Ghost();
			ClientUpdateC4GhostPose();

			if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
			{
				if ( !ClientTryComputeC4Placement( out var pos, out var rot, out var allowed ) )
				{
					Log.Warning( "[Thorns] C4 placement skipped — could not resolve preview (camera/eye or out of range)." );
					return;
				}

				if ( !allowed )
				{
					Log.Warning( "[Thorns] C4 placement skipped — invalid position" );
					return;
				}

				Log.Info( $"[Thorns] C4 placement request pos={pos}" );
				RequestPlaceC4( pos, rot );
			}

			return;
		}

		ClientDestroyGhost();
	}

	void SetBuildMode( bool on )
	{
		if ( BuildModeActive == on )
			return;

		BuildModeActive = on;
		Log.Info( $"[Thorns] Build mode {(BuildModeActive ? "entered" : "exited")}" );

		if ( BuildModeActive )
		{
			ClientEnsureGhost();
			SelectBuildToolbarSlot( 0, playInteractionSfx: false );
		}
		else
		{
			ClientDestroyGhost();
			Components.Get<ThornsDebugHudHost>()?.RequestHudRebuild();
		}

		var weapon = Components.Get<ThornsWeapon>();
		if ( weapon.IsValid() )
			weapon.ApplyLocalFpWeaponDrawForBuildMode( BuildModeActive );
	}

	/// <summary>HUD / keyboard: pick a build dock slot (floor, wall, … remove, upgrade).</summary>
	public void SelectBuildToolbarSlot( int slotIndex, bool playInteractionSfx = true )
	{
		if ( slotIndex < 0 || slotIndex >= ThornsBuildToolbar.SlotCount )
			return;

		var entry = ThornsBuildToolbar.Entries[slotIndex];
		ApplyToolbarEntry( entry, logSelection: true );
		Components.Get<ThornsDebugHudHost>()?.RequestHudRebuild();

		if ( playInteractionSfx && BuildModeActive )
			ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );
	}

	void ApplyToolbarEntry( ThornsBuildToolbarEntry entry, bool logSelection )
	{
		SelectedBuildToolbarSlot = entry.SlotIndex;

		var prevTool = ToolMode;
		var prevStructureId = SelectedStructureDefId;

		switch ( entry.Kind )
		{
			case ThornsBuildToolbarSlotKind.PlaceStructure:
				ToolMode = ThornsBuildToolMode.Place;
				SelectedStructureDefId = entry.StructureDefId;
				break;
			case ThornsBuildToolbarSlotKind.Remove:
				ToolMode = ThornsBuildToolMode.Remove;
				break;
			case ThornsBuildToolbarSlotKind.Upgrade:
				ToolMode = ThornsBuildToolMode.Upgrade;
				break;
		}

		if ( logSelection )
			Log.Info( $"[Thorns] Build toolbar: slot={entry.SlotIndex} label={entry.Label} mode={ToolMode} piece={(ToolMode == ThornsBuildToolMode.Place ? SelectedStructureDefId : "-")}" );

		if ( BuildModeActive )
		{
			var toolChanged = prevTool != ToolMode;
			var placePieceChanged = ToolMode == ThornsBuildToolMode.Place
			                        && entry.Kind == ThornsBuildToolbarSlotKind.PlaceStructure
			                        && !string.Equals( entry.StructureDefId, prevStructureId, StringComparison.Ordinal );
			if ( toolChanged || placePieceChanged )
				ClientDestroyGhost();
		}
	}

	public void DebugSelectStructureById( string id )
	{
		if ( !ThornsBuildingDefinitions.TryGet( id, out _ ) )
			return;

		for ( var i = 0; i < ThornsBuildToolbar.Entries.Length; i++ )
		{
			var e = ThornsBuildToolbar.Entries[i];
			if ( e.Kind == ThornsBuildToolbarSlotKind.PlaceStructure && e.StructureDefId == id )
			{
				SelectBuildToolbarSlot( i, playInteractionSfx: BuildModeActive );
				Log.Info( $"[Thorns] Build structure selected (debug): {id}" );
				return;
			}
		}

		SelectedStructureDefId = id;
		ToolMode = ThornsBuildToolMode.Place;
		Log.Info( $"[Thorns] Build structure selected (debug, non-toolbar id): {id}" );
		Components.Get<ThornsDebugHudHost>()?.RequestHudRebuild();
	}

	void ClientHideGhostVisual()
	{
		if ( !_ghostRoot.IsValid() )
			return;
		foreach ( var mr in _ghostRoot.Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( mr.IsValid() )
				mr.Enabled = false;
		}
	}

	/// <summary>Gold/red translucent shell on the structure under the look ray (same resolver as host demolish/upgrade).</summary>
	void ClientUpdateUpgradeRemoveHighlightGhost()
	{
		if ( !_ghostRoot.IsValid() )
		{
			_ghostRoot = new GameObject( true, GhostName );
			_ghostRoot.WorldPosition = GameObject.WorldPosition;
		}

		_ghostUsedForInteractionHighlight = true;

		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
		{
			ClientHideGhostVisual();
			return;
		}

		if ( !HostTryFindOwnedStructureAlongLook(
			     GameObject,
			     scene,
			     GameObject.Network.OwnerId,
			     ThornsBuildingDefinitions.MaxPlacementDistance,
			     out var ps,
			     out _ ) )
		{
			ClientHideGhostVisual();
			return;
		}

		var upgrade = ToolMode == ThornsBuildToolMode.Upgrade;
		var defId = ps.StructureDefId;
		var previewTier = ps.MaterialTier;
		if ( upgrade && HostCanUpgradeStructure( defId ) && previewTier < (int)ThornsBuildingMaterialTier.Metal )
			previewTier++;

		var mr = ClientResolveGhostModelRenderer( defId );
		if ( !mr.IsValid() )
			return;

		mr.Enabled = true;
		mr.Model = ThornsBuildingVisuals.StructureModel( defId, previewTier );
		if ( string.Equals( defId, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyStorageChestVisual( mr,
				upgrade ? new Color( 1f, 0.82f, 0.2f, 0.45f ) : new Color( 0.95f, 0.3f, 0.22f, 0.48f ) );
		else
			mr.Tint = upgrade
				? new Color( 1f, 0.82f, 0.2f, 0.42f )
				: new Color( 0.92f, 0.28f, 0.22f, 0.45f );

		_ghostRoot.WorldPosition = ps.GameObject.WorldPosition;
		_ghostRoot.WorldRotation = ps.GameObject.WorldRotation;
		_ghostRoot.LocalScale = ps.GameObject.LocalScale * (upgrade ? 1.045f : 1.03f );
	}

	void ClientTryPortableKitHotbarPlace( string structureDefId, ThornsStructureDefinition def )
	{
		var kitYaw = 0f;
		if ( Input.Keyboard.Pressed( "q" ) )
			kitYaw -= PreviewYawStepDegrees;
		if ( Input.Keyboard.Pressed( "r" ) )
			kitYaw += PreviewYawStepDegrees;

		if ( MathF.Abs( kitYaw ) > 0.01f )
		{
			_previewYawDegrees = (_previewYawDegrees + kitYaw) % 360f;
			if ( _previewYawDegrees < 0f )
				_previewYawDegrees += 360f;
		}

		ClientEnsureGhost( structureDefId );
		ClientUpdateGhostPose( def );

		if ( !Input.Pressed( "Attack1" ) && !Input.Pressed( "attack1" ) )
			return;

		if ( !ClientTryComputePlacement( def, out var pos, out var rot, out var snap ) )
		{
			Log.Warning(
				$"[Thorns] {def.DisplayName} kit placement skipped — could not resolve preview (camera/eye, distance, or no snap near look)." );
			return;
		}

		if ( !PlacementAllowedCommit( def, snap ) )
		{
			Log.Warning( $"[Thorns] {def.DisplayName} kit placement skipped — invalid snap" );
			return;
		}

		RequestPlaceStructure(
			structureDefId,
			snap.UsesSocketSnap,
			snap.UsesSocketSnap ? snap.HostSnap.InstanceGuid : Guid.Empty,
			snap.UsesSocketSnap ? snap.HostSnap.SocketIndex : (ushort)0,
			snap.Channel,
			snap.IncomingPlugIndex,
			snap.OppositeTwinSocketPreview,
			pos,
			rot,
			snap.TerrainKind == ThornsTerrainSeedKind.SlabOnRay,
			_previewYawDegrees );
	}

	void ClientEnsureGhost( string structureDefId = null )
	{
		var defId = string.IsNullOrWhiteSpace( structureDefId ) ? SelectedStructureDefId : structureDefId.Trim();
		if ( _ghostRoot.IsValid() && !_ghostIsC4
		     && string.Equals( _ghostPreviewStructureDefId, defId, StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( _ghostRoot.IsValid() )
			ClientDestroyGhost();

		_ghostIsC4 = false;
		_ghostPreviewStructureDefId = defId;

		_ghostRoot = new GameObject( true, GhostName );
		_ghostRoot.WorldPosition = GameObject.WorldPosition;

		var ghostTint = new Color( 0.35f, 0.85f, 0.45f, 0.45f );
		var mr = ClientResolveGhostModelRenderer( defId );
		if ( !mr.IsValid() )
			return;

		if ( string.Equals( defId, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyStorageChestVisual( mr, ghostTint );
		else if ( string.Equals( defId, "bed", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyBedVisual( mr, ghostTint );
		else
		{
			mr.Model = ThornsBuildingVisuals.StructureModel( defId );
			mr.Tint = ghostTint;
		}
	}

	void ClientDestroyGhost()
	{
		if ( !_ghostRoot.IsValid() )
			return;

		_ghostRoot.Destroy();
		_ghostRoot = default;
		_ghostPreviewStructureDefId = "";
		_ghostUsedForInteractionHighlight = false;
		_ghostIsC4 = false;
	}

	void ClientEnsureC4Ghost()
	{
		if ( _ghostRoot.IsValid() && _ghostIsC4 )
			return;

		ClientDestroyGhost();
		_ghostIsC4 = true;
		_ghostRoot = new GameObject( true, GhostName );
		_ghostRoot.WorldPosition = GameObject.WorldPosition;
		_ghostRoot.LocalScale = Vector3.One * ThornsC4.WorldVisualScale;

		var mr = _ghostRoot.Components.Create<ModelRenderer>();
		var c4Model = Model.Load( ThornsC4.ModelPath );
		mr.Model = c4Model;
		mr.Tint = new Color( 0.92f, 0.28f, 0.22f, 0.42f );
		ThornsModelMaterialUvScale.ApplyForScaledModel( mr, _ghostRoot, c4Model, ThornsC4.ModelPath );
	}

	void ClientUpdateC4GhostPose()
	{
		if ( !_ghostRoot.IsValid() || !_ghostIsC4 )
			return;

		if ( !ClientTryComputeC4Placement( out var pos, out var rot, out var allowed ) )
			return;

		_ghostRoot.WorldPosition = pos;
		_ghostRoot.WorldRotation = rot;

		var mr = _ghostRoot.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		if ( !mr.IsValid() )
			return;

		mr.Enabled = true;
		mr.Tint = allowed
			? new Color( 0.92f, 0.28f, 0.22f, 0.42f )
			: new Color( 0.95f, 0.35f, 0.25f, 0.55f );
	}

	bool ClientTryComputeC4Placement( out Vector3 worldPosition, out Rotation worldRotation, out bool allowed )
	{
		worldPosition = default;
		worldRotation = default;
		allowed = false;

		if ( !ThornsC4.TryResolvePlantPosition( GameObject, out worldPosition ) )
			return false;

		if ( !TryGetLocalEye( out _, out var eyeRot ) )
			return false;

		worldRotation = ClientYawFromEye( eyeRot );
		allowed = true;
		return true;

		static Rotation ClientYawFromEye( Rotation eyeRot )
		{
			var planar = new Vector3( eyeRot.Forward.x, eyeRot.Forward.y, 0f );
			if ( planar.Length <= 0.001f )
				return Rotation.Identity;
			return Rotation.LookAt( planar.Normal, Vector3.Up );
		}
	}

	[Rpc.Host]
	public void RequestPlaceC4( Vector3 worldPosition, Rotation worldRotation )
	{
		_ = worldRotation;
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null )
		{
			Log.Warning( "[Thorns] C4 place rejected: no_caller" );
			return;
		}

		if ( Rpc.Caller.Id != GameObject.Network.OwnerId )
		{
			Log.Warning( "[Thorns] C4 place rejected: not_owner" );
			return;
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Warning( "[Thorns] C4 place rejected: dead" );
			return;
		}

		var hb = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		if ( !hb.IsValid() || !inv.IsValid() )
		{
			Log.Warning( "[Thorns] C4 place rejected: missing_components" );
			return;
		}

		var slot = hb.ServerGetSelectedHotbarIndex();
		if ( slot < 0 || slot >= ThornsInventory.HotbarSlotCount )
		{
			Log.Warning( "[Thorns] C4 place rejected: no_hotbar_slot" );
			return;
		}

		if ( !inv.TryGetHostSlot( slot, out var hotbarSlot ) || hotbarSlot.IsEmpty
		     || !ThornsC4.IsEquippedPlacementItem( hotbarSlot.ItemId ) )
		{
			Log.Warning( "[Thorns] C4 place rejected: hotbar_not_c4" );
			return;
		}

		if ( !ThornsC4.HostValidatePlantPosition( GameObject, worldPosition, out var plantPos ) )
		{
			Log.Warning( "[Thorns] C4 place rejected: invalid_position" );
			return;
		}

		if ( inv.ServerRemoveItem( slot, 1 ) <= 0 )
		{
			Log.Warning( "[Thorns] C4 place rejected: remove_failed" );
			return;
		}

		ThornsC4Charge.HostSpawn( GameObject.Scene, plantPos, GameObject, Rpc.Caller.Id );
		ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );
		RpcOwnerPlayBuildMenuOrPlaceSfx();
		Log.Info( $"[Thorns] C4 placed pos={plantPos} owner={Rpc.Caller.Id}" );
	}

	ModelRenderer ClientResolveGhostModelRenderer( string defId )
	{
		if ( !_ghostRoot.IsValid() )
			return default;

		if ( string.Equals( defId, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
		{
			ThornsBuildingVisuals.DestroyBedOffsetChildIfPresent( _ghostRoot );
			return ThornsBuildingVisuals.GetOrCreateStorageChestOffsetModelRenderer( _ghostRoot );
		}

		if ( string.Equals( defId, "bed", StringComparison.OrdinalIgnoreCase ) )
		{
			ThornsBuildingVisuals.DestroyStorageChestOffsetChildIfPresent( _ghostRoot );
			return ThornsBuildingVisuals.GetOrCreateBedOffsetModelRenderer( _ghostRoot );
		}

		ThornsBuildingVisuals.DestroyStorageChestOffsetChildIfPresent( _ghostRoot );
		ThornsBuildingVisuals.DestroyBedOffsetChildIfPresent( _ghostRoot );

		var mr = _ghostRoot.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		if ( mr.IsValid() )
			return mr;

		return _ghostRoot.Components.Create<ModelRenderer>();
	}

	void ClientUpdateGhostPose( ThornsStructureDefinition def )
	{
		if ( !_ghostRoot.IsValid() )
			return;

		if ( !ClientTryComputePlacement( def, out var pos, out var rot, out var snap ) )
			return;

		var allowed = PlacementAllowedCommit( def, snap );
		_ghostRoot.WorldPosition = pos;
		_ghostRoot.WorldRotation = rot;
		_ghostRoot.LocalScale = ThornsBuildingVisuals.StructureLocalScale( def.Id );

		var mr = ClientResolveGhostModelRenderer( def.Id );
		if ( !mr.IsValid() )
			return;

		mr.Enabled = true;
		var ghostTint = allowed
			? new Color( 0.35f, 0.85f, 0.45f, 0.35f )
			: new Color( 0.95f, 0.35f, 0.25f, 0.4f );
		if ( string.Equals( def.Id, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyStorageChestVisual( mr, ghostTint );
		else if ( string.Equals( def.Id, "bed", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyBedVisual( mr, ghostTint );
		else
		{
			mr.Model = ThornsBuildingVisuals.StructureModel( def.Id );
			mr.Tint = ghostTint;
			var ghostPath = mr.Model.IsValid() ? mr.Model.Name : def.Id;
			if ( ThornsPlaceableFurnitureCatalog.TryGet( def.Id, out var entry ) )
				ghostPath = entry.ModelPath;
			ThornsModelMaterialUvScale.ApplyForScaledModel( mr, _ghostRoot, mr.Model, ghostPath );
		}
	}

	/// <summary>Whether the current preview can be committed (ghost may still show a fallback pose when false).</summary>
	static bool PlacementAllowedCommit( ThornsStructureDefinition def, in ThornsPlacementSuggestion snap )
	{
		if ( def.PlacementKind == ThornsPlacementKind.Free )
			return true;

		if ( def.Id == "wood_foundation" && snap.TerrainKind == ThornsTerrainSeedKind.SlabOnRay && !snap.UsesSocketSnap )
			return true;

		return snap.UsesSocketSnap;
	}

	bool ClientTryComputePlacement( ThornsStructureDefinition def, out Vector3 worldPosition, out Rotation worldRotation,
		out ThornsPlacementSuggestion snapOut )
	{
		ThornsGameplayDiagnostics.BumpGhostPlacementResolve();

		worldPosition = default;
		worldRotation = default;
		snapOut = new ThornsPlacementSuggestion();

		if ( !TryGetLocalEye( out var eyePos, out var eyeRot ) )
			return false;

		var dir = eyeRot.Forward.Normal;

		var traceDistance = 4000f;
		var tr = ThornsTraceUtility.RunRay(
			Scene,
			new Ray( eyePos, dir ),
			traceDistance,
			ThornsTraceProfile.BuildingPlacementView,
			GameObject );

		Vector3 bumped;
		if ( tr.Hit )
			bumped = ThornsBuildingSnap.BumpFromTrace( tr.HitPosition, tr.Normal );
		else
		{
			// Sky / gaps / upward aims: no hit along the look ray, but socket snaps (e.g. ceiling floor on wall tops above
			// another roof) still need an aim anchor and a wider structure query.
			bumped = eyePos + dir * MathF.Min( 1400f, traceDistance * 0.45f );
		}

		if ( def.PlacementKind == ThornsPlacementKind.Free )
		{
			worldPosition = bumped;
			worldRotation = ClientYawFromEye( eyeRot ) * Rotation.FromYaw( _previewYawDegrees );
			if ( tr.Hit && tr.Normal.z >= 0.55f )
				ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( def.Id, ref worldPosition, worldRotation );
			snapOut.UsesSocketSnap = false;
			snapOut.TerrainKind = ThornsTerrainSeedKind.NotTerrain;
			return DistanceWithinPlacementReach( worldPosition );
		}

		var gatherRadius = tr.Hit
			? MathF.Max( ThornsSnapResolver.SocketSearchRadiusUnits * 8f, 400f )
			: MathF.Max( ThornsSnapResolver.SocketSearchRadiusUnits * 40f, ThornsBuildingDefinitions.MaxPlacementDistance * 2.5f );

		var hosts = GatherStructuresNearPlacementForGhost( Scene, bumped, gatherRadius );

		if ( !ThornsSnapResolver.ClientTrySolve( bumped, hosts, def.Id, out snapOut ) )
		{
			worldPosition = bumped;
			worldRotation = ClientYawFromEye( eyeRot ) * Rotation.FromYaw( _previewYawDegrees );
			snapOut.UsesSocketSnap = false;
			snapOut.TerrainKind = ThornsTerrainSeedKind.NotTerrain;
			return DistanceWithinPlacementReach( worldPosition );
		}

		if ( string.Equals( def.Id, "wood_foundation", StringComparison.OrdinalIgnoreCase )
		     && snapOut.TerrainKind == ThornsTerrainSeedKind.SlabOnRay
		     && !snapOut.UsesSocketSnap )
		{
			ThornsBuildingTerrainSurface.ClampFoundationTerrainSlabToSurface(
				Scene,
				GameObject,
				in tr,
				bumped,
				ref snapOut );
		}

		worldPosition = snapOut.ProposedWorldPosition;
		worldRotation = snapOut.ProposedWorldRotation * Rotation.FromYaw( _previewYawDegrees );
		return DistanceWithinPlacementReach( worldPosition );

		bool DistanceWithinPlacementReach( Vector3 p ) =>
			(p - GameObject.WorldPosition).Length <= ThornsBuildingDefinitions.MaxPlacementDistance;

		static Rotation ClientYawFromEye( Rotation eyeRot )
		{
			var planar = new Vector3( eyeRot.Forward.x, eyeRot.Forward.y, 0f );

			if ( planar.Length <= 0.001f )
				planar = new Vector3( 1f, 0f, 0f );
			else
				planar = planar.Normal;

			return Rotation.LookAt( planar, Vector3.Up );
		}
	}

	void EnsureGhostPlacementSceneStructuresCache( Scene scene )
	{
		var o = _ghostPlacementLocalOwnerUpdateOrdinal;
		if ( o == _ghostPlacementSceneStructuresLastFullScanOrdinal )
			return;

		if ( _ghostPlacementSceneStructuresLastFullScanOrdinal >= 0
		     && o - _ghostPlacementSceneStructuresLastFullScanOrdinal < 2 )
			return;

		_ghostPlacementSceneStructures.Clear();
		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( ps.IsValid() && ps.GameObject.IsValid() && ps.GameObject.Scene == scene )
				_ghostPlacementSceneStructures.Add( ps );
		}

		_ghostPlacementSceneStructuresLastFullScanOrdinal = o;
		ThornsGameplayDiagnostics.BumpPlacedStructureFullScan();
	}

	List<ThornsPlacedStructure> GatherStructuresNearPlacementForGhost( Scene scene, Vector3 aimWorld, float radius )
	{
		EnsureGhostPlacementSceneStructuresCache( scene );
		_ghostPlacementNearStructures.Clear();
		foreach ( var ps in _ghostPlacementSceneStructures )
		{
			var d = (ps.GameObject.WorldPosition - aimWorld).Length;

			if ( d <= radius )
				_ghostPlacementNearStructures.Add( ps );
		}

		return _ghostPlacementNearStructures;
	}

	bool TryGetLocalEye( out Vector3 worldPos, out Rotation worldRot )
	{
		worldPos = default;
		worldRot = default;

		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( GameObject, out worldPos, out worldRot ) )
			return true;

		foreach ( var ch in GameObject.Children )
		{
			if ( ch.Name != "View" )
				continue;
			if ( ch.Components.Get<CameraComponent>( FindMode.EnabledInSelf ) is not { } cam || !cam.IsValid() || !cam.Enabled )
				continue;
			worldPos = cam.GameObject.WorldPosition;
			worldRot = cam.GameObject.WorldRotation;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Picks the <see cref="ThornsPlacedStructure"/> whose collider the player is actually looking at (physics ray),
	/// piercing terrain/world hits until a structure is found. Falls back to legacy centre-on-ray scoring if no hit.
	/// </summary>
	static bool HostTryFindOwnedStructureAlongLook(
		GameObject pawnRoot,
		Scene scene,
		Guid callerId,
		float maxRange,
		out ThornsPlacedStructure target,
		out string failReason )
	{
		target = default;
		failReason = "";

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
		{
			failReason = "no_eye";
			return false;
		}

		var dir = rot.Forward.Normal;

		if ( scene is null || !scene.IsValid() )
		{
			failReason = "no_scene";
			return false;
		}

		{
			var origin = eye;
			var remaining = maxRange;
			const int maxPasses = 28;
			for ( var pass = 0; pass < maxPasses && remaining > 0.25f; pass++ )
			{
				var hit = ThornsTraceUtility.WithIgnoredRoots(
						ThornsTraceUtility.PrepareRay( scene, new Ray( origin, dir ), remaining,
							ThornsTraceProfile.BuildingStructurePickPiercing ),
						pawnRoot,
						null )
					.Run();
				if ( !hit.Hit || !hit.GameObject.IsValid() )
					break;

				var psHit = hit.GameObject.Components.GetInAncestorsOrSelf<ThornsPlacedStructure>( true );
				if ( psHit.IsValid() )
				{
					if ( !ThornsStructureOwnership.HostCallerOwnsStructure( callerId, psHit ) )
					{
						failReason = "not_owner";
						return false;
					}

					var distPlayer = (pawnRoot.WorldPosition - psHit.GameObject.WorldPosition).Length;
					if ( distPlayer > ThornsBuildingDefinitions.MaxPlacementDistance )
					{
						failReason = "distance";
						return false;
					}

					target = psHit;
					return true;
				}

				var traveled = (hit.HitPosition - origin).Length;
				if ( traveled < 0.01f )
					break;

				origin = hit.HitPosition + dir * 2.5f;
				remaining -= traveled + 2.5f;
			}
		}

		float bestT = float.MaxValue;
		float bestLat = float.MaxValue;
		ThornsPlacedStructure best = default;

		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( !ps.IsValid() )
				continue;

			if ( !ThornsStructureOwnership.HostCallerOwnsStructure( callerId, ps ) )
				continue;

			if ( !ThornsBuildingDefinitions.TryGet( ps.StructureDefId, out var defPick ) )
				continue;

			var p = ps.GameObject.WorldPosition;
			var to = p - eye;
			var t = Vector3.Dot( to, dir );

			if ( t < 0f || t > maxRange )
				continue;

			var onRay = eye + dir * t;
			var lateral = (p - onRay).Length;
			var thresh = MathF.Max( defPick.FootprintRadius, 52f );

			if ( lateral > thresh * 1.4f )
				continue;

			if ( t < bestT - 0.5f || (MathF.Abs( t - bestT ) <= 0.5f && lateral < bestLat - 0.25f) )
			{
				bestT = t;
				bestLat = lateral;
				best = ps;
			}
		}

		if ( best is null || !best.IsValid() )
		{
			failReason = "not_structure";
			return false;
		}

		var distPlayer2 = (pawnRoot.WorldPosition - best.GameObject.WorldPosition).Length;

		if ( distPlayer2 > ThornsBuildingDefinitions.MaxPlacementDistance )
		{
			failReason = "distance";
			return false;
		}

		target = best;
		return true;
	}

	[Rpc.Host]
	public void RequestDemolishFocusedStructure()
	{
		Log.Info( $"[Thorns] Demolish request caller={Rpc.Caller?.Id}" );

		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null )
		{
			Log.Warning( "[Thorns] Demolish rejected: no_caller" );
			return;
		}

		if ( Rpc.Caller.Id != GameObject.Network.OwnerId )
		{
			Log.Warning( "[Thorns] Demolish rejected: not_owner" );
			return;
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Warning( "[Thorns] Demolish rejected: dead" );
			return;
		}

		if ( !HostTryFindOwnedStructureAlongLook( GameObject, GameObject.Scene, Rpc.Caller.Id, ThornsBuildingDefinitions.MaxPlacementDistance, out var ps, out var why ) )
		{
			Log.Warning( $"[Thorns] Demolish rejected: {why}" );
			return;
		}

		var id = ps.StructureDefId;
		if ( id == "base_core" )
			ThornsBuildingAuthority.HostUnregisterPlacedBaseCore( Rpc.Caller.Id );

		if ( ThornsBuildingDefinitions.TryGet( id, out var refundDef ) )
		{
			var invRefund = Components.Get<ThornsInventory>();
			if ( invRefund.IsValid() )
				HostRefundDemolishCosts( invRefund, refundDef );
			else
				Log.Warning( "[Thorns] Demolish: no inventory — materials not refunded" );
		}

		Log.Info( $"[Thorns] Structure demolished instance={ps.InstanceId} def={id}" );
		ps.GameObject.Destroy();
		ThornsWorldPersistence.HostNotifyStructureDestroyedByDemolish();
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
		NotifyOwnerBuildMenuOrPlaceSfx();
	}

	static void HostRefundDemolishCosts( ThornsInventory inv, ThornsStructureDefinition def )
	{
		if ( !inv.IsValid() )
			return;

		if ( !string.IsNullOrEmpty( def.RequiredPlacementItemId ) )
			_ = inv.ServerAddItem( def.RequiredPlacementItemId, 1, suppressOwnerSnapshot: true, suppressMilestoneRecord: true );

		foreach ( var c in ThornsBuildingDefinitions.PlacementResourceCosts( def ) )
			_ = inv.ServerAddItem( c.ItemId, c.Quantity, suppressOwnerSnapshot: true, suppressMilestoneRecord: true );

		inv.HostPushInventorySnapshotToOwner();
		Log.Info(
			$"[Thorns] Demolish refund applied def={def.Id} (+placement gate if any)" );
	}

	[Rpc.Host]
	public void RequestUpgradeFocusedStructure()
	{
		Log.Info( $"[Thorns] Upgrade request caller={Rpc.Caller?.Id}" );

		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || Rpc.Caller.Id != GameObject.Network.OwnerId )
		{
			Log.Warning( "[Thorns] Upgrade rejected: caller" );
			return;
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Warning( "[Thorns] Upgrade rejected: dead" );
			return;
		}

		if ( !HostTryFindOwnedStructureAlongLook( GameObject, GameObject.Scene, Rpc.Caller.Id, ThornsBuildingDefinitions.MaxPlacementDistance, out var ps, out var why ) )
		{
			Log.Warning( $"[Thorns] Upgrade rejected: {why}" );
			return;
		}

		if ( !HostCanUpgradeStructure( ps.StructureDefId ) )
		{
			Log.Warning( $"[Thorns] Upgrade rejected: not_upgradeable def={ps.StructureDefId}" );
			return;
		}

		var fromTier = ps.MaterialTier;
		if ( fromTier >= (int)ThornsBuildingMaterialTier.Metal )
		{
			Log.Warning( $"[Thorns] Upgrade rejected: max_tier def={ps.StructureDefId}" );
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			Log.Warning( "[Thorns] Upgrade rejected: no_inventory" );
			return;
		}

		var toTier = fromTier + 1;
		var cost = HostUpgradeCostFor( ps.StructureDefId, fromTier );
		if ( cost.Quantity <= 0 )
		{
			Log.Warning( $"[Thorns] Upgrade rejected: bad_cost def={ps.StructureDefId} tier={fromTier}" );
			return;
		}

		if ( inv.ServerCountItemId( cost.ItemId ) < cost.Quantity )
		{
			Log.Warning( $"[Thorns] Upgrade rejected: missing_resource:{cost.ItemId} need={cost.Quantity} have={inv.ServerCountItemId( cost.ItemId )}" );
			return;
		}

		if ( inv.ServerRemoveItemId( cost.ItemId, cost.Quantity, suppressOwnerSnapshot: true ) != cost.Quantity )
		{
			Log.Warning( $"[Thorns] Upgrade rejected: payment_failed:{cost.ItemId}" );
			return;
		}
		inv.HostPushInventorySnapshotToOwner();

		var healthRatio = ps.MaxHealthSync > 0.001f ? Math.Clamp( ps.CurrentHealth / ps.MaxHealthSync, 0f, 1f ) : 1f;
		ps.UpgradeTierPlaceholder = toTier;
		ThornsBuildingDurability.HostApplyMaxHealthFromDurability( ps, refillToFull: false );
		ps.CurrentHealth = MathF.Max( 1f, ps.MaxHealthSync * healthRatio );
		ps.HostApplyVisualTier();

		Log.Info( $"[Thorns] Structure upgraded instance={ps.InstanceId} def={ps.StructureDefId} tier={fromTier}->{toTier} hp={ps.CurrentHealth:F1}/{ps.MaxHealthSync:F1}" );
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
		RpcOwnerPlayBuildMenuOrPlaceSfx();

		if ( inv.IsValid() )
			inv.GameObject.Components.Get<ThornsPlayerMilestones>()?.HostRecordEvent( ThornsMilestoneEventTokens.StructureUpgraded );
	}

	static bool HostCanUpgradeStructure( string structureDefId ) =>
		ThornsBuildingDefinitions.SupportsMaterialTierUpgrade( structureDefId );

	static (string ItemId, int Quantity) HostUpgradeCostFor( string structureDefId, int fromTier ) =>
		ThornsBuildingDefinitions.GetUpgradeCostForTier( structureDefId, fromTier );

	[Rpc.Host]
	public void RequestPlaceStructure(
		string structureDefId,
		bool usesSocketSnap,
		Guid hostInstanceId,
		ushort hostSocketIndex,
		ThornsSnapChannel snapChannel,
		ushort incomingPlugIndexIgnored,
		ushort oppositeTwinSocketPreview,
		Vector3 clientWorldPosition,
		Rotation clientWorldRotation,
		bool terrainFoundationSeed,
		float previewYawDegrees )
	{
		_ = incomingPlugIndexIgnored;

		Log.Info(
			$"[Thorns] Placement request received def={structureDefId} pos={clientWorldPosition} caller={Rpc.Caller?.Id} snap={usesSocketSnap}" );

		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null )
		{
			Log.Warning( "[Thorns] Placement rejected: no_caller" );
			RpcPlacementResult( false, "no_caller" );
			return;
		}

		var ownerId = GameObject.Network.OwnerId;

		if ( Rpc.Caller.Id != ownerId )
		{
			Log.Warning( "[Thorns] Placement rejected: not_owner" );
			RpcPlacementResult( false, "not_owner" );
			return;
		}

		var health = Components.Get<ThornsHealth>();

		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Warning( "[Thorns] Placement rejected: dead" );
			RpcPlacementResult( false, "dead" );
			return;
		}

		if ( !ThornsBuildingDefinitions.TryGet( structureDefId, out var def ) )
		{
			Log.Warning( "[Thorns] Placement rejected: unknown_structure" );
			RpcPlacementResult( false, "unknown_structure" );
			return;
		}

		var pawnDist = (clientWorldPosition - GameObject.WorldPosition).Length;

		if ( pawnDist > ThornsBuildingDefinitions.MaxPlacementDistance )
		{
			Log.Warning(
				$"[Thorns] Placement rejected: distance d={pawnDist:F1} max={ThornsBuildingDefinitions.MaxPlacementDistance}" );
			RpcPlacementResult( false, "distance" );
			return;
		}

		if ( structureDefId == "base_core" && ThornsBuildingAuthority.HostHasBaseCore( Rpc.Caller.Id ) )
		{
			Log.Warning( "[Thorns] Base Core placement rejected: base_core_already_placed" );
			RpcPlacementResult( false, "base_core_already_placed" );
			return;
		}

		if ( !HostTryResolveAuthoritativePose(
			     structureDefId,
			     def,
			     usesSocketSnap,
			     hostInstanceId,
			     hostSocketIndex,
			     snapChannel,
			     clientWorldPosition,
			     clientWorldRotation,
			     terrainFoundationSeed,
			     previewYawDegrees,
			     Rpc.Caller.Id,
			     GameObject.Scene,
			     GameObject,
			     out var authoritativePosition,
			     out var authoritativeRotation ) )
		{
			Log.Warning( "[Thorns] Placement rejected: authoritative_pose_failed" );
			RpcPlacementResult( false, "snap_replay_failed" );
			return;
		}

		var ignoreHostForOverlap =
			usesSocketSnap && hostInstanceId != Guid.Empty ? (Guid?)hostInstanceId : null;

		if ( !HostValidateOverlap( Scene,
			     authoritativePosition,
			     authoritativeRotation,
			     def,
			     ignoreHostForOverlap,
			     usesSocketSnap,
			     snapChannel ) )
		{
			Log.Warning( "[Thorns] Placement rejected: overlap" );
			RpcPlacementResult( false, "overlap" );
			return;
		}

		var inv = Components.Get<ThornsInventory>();

		if ( !inv.IsValid() )
		{
			Log.Warning( "[Thorns] Placement rejected: no_inventory" );
			RpcPlacementResult( false, "no_inventory" );
			return;
		}

		if ( !string.IsNullOrEmpty( def.RequiredPlacementItemId ) &&
		     inv.ServerCountItemId( def.RequiredPlacementItemId ) < 1 )
		{
			Log.Warning( $"[Thorns] Placement rejected: missing_gate_item id={def.RequiredPlacementItemId}" );
			RpcPlacementResult( false, "missing_gate_item" );
			return;
		}

		foreach ( var c in ThornsBuildingDefinitions.PlacementResourceCosts( def ) )
		{
			if ( inv.ServerCountItemId( c.ItemId ) < c.Quantity )
			{
				Log.Warning(
					$"[Thorns] Placement rejected: insufficient {c.ItemId} need={c.Quantity} have={inv.ServerCountItemId( c.ItemId )}" );
				RpcPlacementResult( false, $"missing_resource:{c.ItemId}" );
				return;
			}
		}

		if ( !HostTryConsumeCosts( inv, def, out var payReason ) )
		{
			Log.Warning( $"[Thorns] Placement rejected: payment_failed {payReason}" );
			RpcPlacementResult( false, payReason );
			return;
		}

		Log.Info( $"[Thorns] Placement resources consumed def={structureDefId}" );

		var spawn = ThornsPlacedStructure.SpawnHost(
			GameObject.Scene,
			Rpc.Caller.Id,
			ThornsPersistenceIdentity.GetStableAccountKey( Rpc.Caller ),
			structureDefId,
			authoritativePosition,
			authoritativeRotation,
			out var spawnFail );

		if ( !spawn.IsValid() )
		{
			Log.Error( $"[Thorns] Placement spawn failed: {spawnFail} — refunding costs" );
			HostRefundCosts( inv, def );
			RpcPlacementResult( false, "spawn_failed" );
			return;
		}

		if ( usesSocketSnap &&
		     !HostTryLedgerAfterSpawn( hostInstanceId, spawn, structureDefId, snapChannel, hostSocketIndex,
			     oppositeTwinSocketPreview, Rpc.Caller.Id ) )
		{
			Log.Warning( "[Thorns] Placement rejected — socket ledger contention; spawned structure destroyed" );
			spawn.GameObject.Destroy();
			ThornsWorldPersistence.HostNotifyStructureDestroyedByDemolish();
			HostRefundCosts( inv, def );
			RpcPlacementResult( false, "socket_occupied" );
			return;
		}

		if ( structureDefId == "base_core" )
		{
			ThornsBuildingAuthority.HostRegisterPlacedBaseCore( Rpc.Caller.Id, authoritativePosition );
			Log.Info( $"[Thorns] Base Core placed owner={Rpc.Caller.Id} pos={authoritativePosition}" );
		}

		Log.Info( $"[Thorns] Structure spawned ok instance={spawn.InstanceId}" );
		Log.Info(
			$"[Thorns] Placeable placed def={structureDefId} instance={spawn.InstanceId} pos={authoritativePosition} yaw={authoritativeRotation.Angles().yaw:F1} snap={usesSocketSnap} terrainSeed={terrainFoundationSeed}" );

		var milestones = Components.Get<ThornsPlayerMilestones>();
		if ( milestones.IsValid() )
			milestones.HostRecordStructurePlaced( structureDefId );

		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();

		RpcPlacementResult( true, "" );
	}

	static bool HostTryLedgerAfterSpawn(
		Guid hostInstanceId,
		ThornsPlacedStructure spawned,
		string placedStructureDefId,
		ThornsSnapChannel snapChannel,
		int hostSocketIndex,
		ushort twinClientOpposite,
		Guid placementCallerConnectionId )
	{
		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( hostInstanceId, out var ledgerHostStructure ) ||
		     !ThornsStructureOwnership.HostCallerOwnsStructure( placementCallerConnectionId, ledgerHostStructure ) )
			return false;

		switch ( snapChannel )
		{
			case ThornsSnapChannel.FoundationEdgeMate when placedStructureDefId == "wood_foundation":
			{
				var twin =
					(ushort)Math.Clamp( ThornsSnapResolver.OppositeEdgeIndex( hostSocketIndex ), 0, 65535 );

				if ( twinClientOpposite != twin )
					Log.Warning( $"[Thorns] twin hint mismatch client={twinClientOpposite} server={twin}" );

				var hostBind =
					new ThornsPlacementSocketBind( ledgerHostStructure.InstanceId,
						(ushort)Math.Clamp( hostSocketIndex,
							0,
							65535 ) );

				var spawnedBind =
					new ThornsPlacementSocketBind(
						spawned.InstanceId,
						twin );

				return ThornsBuildingSocketLedger.HostTryOccupyPair( hostBind, spawnedBind, spawned.InstanceId );
			}
			default:
			{
				var bind =
					new ThornsPlacementSocketBind(
						ledgerHostStructure.InstanceId,
						(ushort)Math.Clamp( hostSocketIndex,
							0,
							65535 ) );

				return ThornsBuildingSocketLedger.HostTryOccupy( bind, spawned.InstanceId );
			}
		}
	}

	static bool HostTryConsumeCosts( ThornsInventory inv, ThornsStructureDefinition def, out string failReason )
	{
		failReason = "";
		var removed = new List<(string Id, int Qty)>();

		if ( !string.IsNullOrEmpty( def.RequiredPlacementItemId ) )
		{
			var r = inv.ServerRemoveItemId( def.RequiredPlacementItemId, 1, suppressOwnerSnapshot: true );
			if ( r != 1 )
			{
				HostRefundList( inv, removed );
				failReason = "pay_gate_failed";
				return false;
			}

			removed.Add( (def.RequiredPlacementItemId, 1) );
		}

		foreach ( var c in ThornsBuildingDefinitions.PlacementResourceCosts( def ) )
		{
			var r = inv.ServerRemoveItemId( c.ItemId, c.Quantity, suppressOwnerSnapshot: true );
			if ( r != c.Quantity )
			{
				HostRefundList( inv, removed );
				failReason = $"pay_failed:{c.ItemId}";
				return false;
			}

			removed.Add( (c.ItemId, c.Quantity) );
		}

		inv.HostPushInventorySnapshotToOwner();
		return true;
	}

	static void HostRefundCosts( ThornsInventory inv, ThornsStructureDefinition def )
	{
		if ( !inv.IsValid() )
			return;

		if ( !string.IsNullOrEmpty( def.RequiredPlacementItemId ) )
			_ = inv.ServerAddItem( def.RequiredPlacementItemId, 1, suppressOwnerSnapshot: true );

		foreach ( var c in ThornsBuildingDefinitions.PlacementResourceCosts( def ) )
			_ = inv.ServerAddItem( c.ItemId, c.Quantity, suppressOwnerSnapshot: true );

		inv.HostPushInventorySnapshotToOwner();
	}

	static void HostRefundList( ThornsInventory inv, List<(string Id, int Qty)> removed )
	{
		foreach ( var (id, qty) in removed )
			_ = inv.ServerAddItem( id, qty, suppressOwnerSnapshot: true );

		inv.HostPushInventorySnapshotToOwner();
	}

	static bool HostTryResolveAuthoritativePose(
		string structureDefId,
		ThornsStructureDefinition def,
		bool usesSocketSnap,
		Guid hostInstanceId,
		ushort hostSocketIndex,
		ThornsSnapChannel snapChannel,
		Vector3 clientWorldPosition,
		Rotation clientWorldRotation,
		bool terrainFoundationSeed,
		float previewYawDegrees,
		Guid callerConnectionId,
		Scene scene,
		GameObject pawnRoot,
		out Vector3 authoritativePos,
		out Rotation authoritativeRot )
	{
		authoritativePos = default;
		authoritativeRot = default;

		var previewMultiply = Rotation.FromYaw( previewYawDegrees );

		bool ReplayMatchesClient( Vector3 posAuth, Rotation baseRotSansPreviewMultiply )
		{
			var posTol = snapChannel == ThornsSnapChannel.FloorSeatOnWallTop
				? ThornsSnapResolver.AuthoritativePoseTolerance * 14f
				: ThornsSnapResolver.AuthoritativePoseTolerance * 5f;

			if ( (posAuth - clientWorldPosition).Length > posTol )
				return false;

			var fwdServer = (baseRotSansPreviewMultiply * previewMultiply).Forward.Normal;

			var fwdClient = clientWorldRotation.Forward.Normal;

			var minDot = snapChannel == ThornsSnapChannel.FloorSeatOnWallTop ? 0.88f : 0.962f;

			return Vector3.Dot( fwdServer, fwdClient ) >= minDot;
		}

		if ( def.PlacementKind == ThornsPlacementKind.Free )
		{
			authoritativePos = clientWorldPosition;
			authoritativeRot = clientWorldRotation;
			ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface(
				structureDefId,
				ref authoritativePos,
				authoritativeRot );

			return true;
		}

		if ( terrainFoundationSeed )
		{
			if ( structureDefId != "wood_foundation" )
				return false;

			var noLook = default( SceneTraceResult );
			float cz;
			if ( scene is not null && scene.IsValid()
			     && ThornsBuildingTerrainSurface.TryGetSupportWorldZ(
				     scene,
				     pawnRoot,
				     in noLook,
				     clientWorldPosition,
				     out var zSurf ) )
				cz = ThornsBuildingTerrainSurface.FoundationSlabCentreZFromSupportWorldZ( zSurf );
			else
				cz = ThornsSnapStory.KzBandFromElevation( clientWorldPosition.z ) * ThornsBuildingModule.Cell +
				     ThornsBuildingModule.FloorThickness * 0.5f;

			authoritativePos = new Vector3( clientWorldPosition.x,
				clientWorldPosition.y,
				cz );

			authoritativeRot = Rotation.Identity * previewMultiply;

			return ReplayMatchesClient( authoritativePos, Rotation.Identity );
		}

		if ( !usesSocketSnap )
		{
			Log.Warning(
				"[Thorns] Authoritative resolver: grid RPC missing routing (terrain flag off but no socket)." );
			return false;
		}

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( hostInstanceId, out var hostPs ) )
			return false;

		if ( !ThornsStructureOwnership.HostCallerOwnsStructure( callerConnectionId, hostPs ) )
			return false;

		if ( !ThornsSnapResolver.HostTryReplaySnap( hostPs,
				  hostSocketIndex,
				  structureDefId,
				  snapChannel,
				  out var replayPos,
				  out var replayRotSansPreview ) )
			return false;

		authoritativePos = replayPos;
		authoritativeRot = replayRotSansPreview * previewMultiply;

		return ReplayMatchesClient( replayPos, replayRotSansPreview );
	}

	/// <param name="ignoreOverlapWithInstanceId">Host snapped into — seat midpoints sit inside that footprint sphere.</param>
	static bool HostValidateOverlap( Scene scene, Vector3 candidatePos, Rotation candidateRot,
		ThornsStructureDefinition candidateDef,
		Guid? ignoreOverlapWithInstanceId,
		bool usesSocketSnap,
		ThornsSnapChannel snapChannel )
	{
		_ = candidateRot;

		// Hotbar kits (chest / campfire / workbench): sphere-vs-placed-structure rejects valid interior spots (bed,
		// base_core, tight proc rooms). Validity is aim trace + distance — not conservative footprint cages.
		if ( ThornsBuildingDefinitions.IsPortableKitPlaceableId( candidateDef.Id ) )
			return true;

		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( !ps.IsValid() )
				continue;

			if ( ignoreOverlapWithInstanceId.HasValue && ps.InstanceId == ignoreOverlapWithInstanceId.Value )
				continue;

			if ( !ThornsBuildingDefinitions.TryGet( ps.StructureDefId, out var otherDef ) )
				continue;

			if ( HostShouldIgnoreSkinOverlapForSocketSnap(
				     candidateDef,
				     ps.StructureDefId,
				     candidatePos,
				     ps.GameObject.WorldPosition,
				     usesSocketSnap,
				     snapChannel ) )
				continue;

			if ( HostShouldSkipFootprintOverlapForMixedStructureKinds(
				     candidateDef,
				     otherDef,
				     candidatePos,
				     ps.GameObject.WorldPosition ) )
				continue;

			if ( HostShouldSkipFoundationEdgeMateKitOnHostSlab(
				     candidateDef,
				     otherDef,
				     candidatePos,
				     ps,
				     ignoreOverlapWithInstanceId,
				     usesSocketSnap,
				     snapChannel,
				     scene ) )
				continue;

			var d = (ps.GameObject.WorldPosition - candidatePos).Length;
			var sep = candidateDef.FootprintRadius + otherDef.FootprintRadius;

			if ( d < sep * 0.95f )
				return false;
		}

		return true;
	}

	/// <summary>
	/// Edge-mated foundation: chest/campfire/workbench on the host slab must not block the neighbour tile
	/// (conservative kit spheres still clip the shared edge when props sit near the seam).
	/// </summary>
	static bool HostShouldSkipFoundationEdgeMateKitOnHostSlab(
		ThornsStructureDefinition candidateDef,
		ThornsStructureDefinition otherDef,
		Vector3 candidatePos,
		ThornsPlacedStructure otherPs,
		Guid? ignoreHostInstanceId,
		bool usesSocketSnap,
		ThornsSnapChannel snapChannel,
		Scene scene )
	{
		if ( !usesSocketSnap || snapChannel != ThornsSnapChannel.FoundationEdgeMate )
			return false;

		if ( candidateDef.SnapKind != ThornsBuildingSnapKind.Foundation )
			return false;

		if ( !ThornsBuildingDefinitions.IsPortableKitPlaceableId( otherDef.Id ) )
			return false;

		if ( !ignoreHostInstanceId.HasValue || ignoreHostInstanceId.Value == otherPs.InstanceId )
			return false;

		if ( !scene.IsValid() )
			return false;

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( ignoreHostInstanceId.Value, out var hostPs )
		     || !hostPs.IsValid()
		     || hostPs.StructureDefId != "wood_foundation" )
			return false;

		if ( !ThornsBuildingDefinitions.TryGet( hostPs.StructureDefId, out var hostDef ) )
			return false;

		var hostFlat = hostPs.GameObject.WorldPosition.WithZ( 0 );
		var kitFlat = otherPs.GameObject.WorldPosition.WithZ( 0 );
		var candFlat = candidatePos.WithZ( 0 );

		if ( (kitFlat - hostFlat).Length > hostDef.FootprintRadius * 1.08f )
			return false;

		if ( (candFlat - hostFlat).Length > ThornsBuildingModule.Cell * 1.15f )
			return false;

		return true;
	}

	/// <summary>
	/// Footprint spheres are conservative — skip when placing free props on modules, or when grid pieces intentionally
	/// share volume (wall on slab, ramp on foundation). Still run sphere checks for same-kind collisions (two slabs, two walls).
	/// </summary>
	static bool HostShouldSkipFootprintOverlapForMixedStructureKinds(
		ThornsStructureDefinition candidateDef,
		ThornsStructureDefinition otherDef,
		Vector3 candidatePos,
		Vector3 otherPos )
	{
		_ = candidatePos;
		_ = otherPos;

		if ( ThornsBuildingDefinitions.IsPortableKitPlaceableId( candidateDef.Id )
		     && ThornsBuildingDefinitions.IsPortableKitPlaceableId( otherDef.Id ) )
			return true;

		// Chest / campfire / workbench on foundations — sphere rejects valid use.
		if ( candidateDef.PlacementKind == ThornsPlacementKind.Free
		     && otherDef.PlacementKind == ThornsPlacementKind.Grid )
			return true;

		if ( candidateDef.PlacementKind != ThornsPlacementKind.Grid || otherDef.PlacementKind != ThornsPlacementKind.Grid )
			return false;

		var a = candidateDef.SnapKind;
		var b = otherDef.SnapKind;

		if ( a == ThornsBuildingSnapKind.Foundation && b == ThornsBuildingSnapKind.Foundation )
			return false;

		if ( a == b )
			return false;

		static bool IsVerticalShell( ThornsBuildingSnapKind k ) =>
			k is ThornsBuildingSnapKind.Wall
				or ThornsBuildingSnapKind.Window
				or ThornsBuildingSnapKind.DoorFrame
				or ThornsBuildingSnapKind.DoorPanel;

		if ( a == ThornsBuildingSnapKind.Foundation && (b == ThornsBuildingSnapKind.Ramp || IsVerticalShell( b )) )
			return true;
		if ( b == ThornsBuildingSnapKind.Foundation && (a == ThornsBuildingSnapKind.Ramp || IsVerticalShell( a )) )
			return true;

		if ( a == ThornsBuildingSnapKind.Ramp && IsVerticalShell( b ) )
			return true;
		if ( b == ThornsBuildingSnapKind.Ramp && IsVerticalShell( a ) )
			return true;

		if ( IsVerticalShell( a ) && IsVerticalShell( b ) )
			return true;

		return false;
	}

	/// <summary>
	/// Ceiling floors and foundation-top ramps are centered in the cell while perimeter walls sit ~half a cell away on XY.
	/// Sphere overlap then rejects valid snaps (48+32 &gt; ~50). Skip only nearby vertical wall skins, not distant structures.
	/// </summary>
	static bool HostShouldIgnoreSkinOverlapForSocketSnap(
		ThornsStructureDefinition candidateDef,
		string otherDefId,
		Vector3 candidatePos,
		Vector3 otherPos,
		bool usesSocketSnap,
		ThornsSnapChannel snapChannel )
	{
		if ( !usesSocketSnap )
			return false;

		if ( otherDefId is not ("wood_wall" or "wood_window" or "wood_doorframe") )
			return false;

		var dx = candidatePos.x - otherPos.x;
		var dy = candidatePos.y - otherPos.y;
		var horiz = MathF.Sqrt( dx * dx + dy * dy );
		if ( horiz > ThornsBuildingModule.Cell * 0.62f )
			return false;

		if ( snapChannel == ThornsSnapChannel.FloorSeatOnWallTop && candidateDef.Id == "wood_foundation" )
			return true;

		if ( snapChannel == ThornsSnapChannel.RampSeatOnFoundationTop && candidateDef.Id == "wood_ramp" )
			return true;

		return false;
	}

	void NotifyOwnerBuildMenuOrPlaceSfx()
	{
		if ( !Networking.IsActive )
		{
			ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );
			return;
		}

		var local = Connection.Local;
		if ( local is not null && local.Id == GameObject.Network.OwnerId )
			ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );

		RpcOwnerPlayBuildMenuOrPlaceSfx();
	}

	[Rpc.Owner]
	void RpcOwnerPlayBuildMenuOrPlaceSfx()
	{
		ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );
	}

	[Rpc.Owner]
	void RpcPlacementResult( bool ok, string reason )
	{
		if ( ok )
		{
			Log.Info( "[Thorns] Placement accepted (owner notify)" );
			ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );
		}
		else
		{
			Log.Warning( $"[Thorns] Placement rejected (owner notify): {reason}" );
		}
	}

	protected override void OnDestroy()
	{
		ClientDestroyGhost();
	}
}
