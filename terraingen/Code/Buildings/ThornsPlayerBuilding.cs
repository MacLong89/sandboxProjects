namespace Terraingen.Buildings;

using Sandbox.Network;
using Terraingen;
using Terraingen.Combat;
using Terraingen.Physics;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.Rendering;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;

public enum ThornsPlayerBuildPlacementKind
{
	Grid,
	Free
}

public enum ThornsPlayerBuildSnapKind
{
	Foundation,
	Wall,
	Window,
	DoorFrame,
	Ramp,
	Portable
}

public enum ThornsPlayerBuildToolKind
{
	Place,
	Remove,
	Upgrade
}

public sealed class ThornsBuildDefinition
{
	public string Id { get; init; } = "";
	public string DisplayName { get; init; } = "";
	public string RequiredItemId { get; init; } = "";
	public (string ItemId, int Count)[] Costs { get; init; } = Array.Empty<(string, int)>();
	public ThornsPlayerBuildPlacementKind PlacementKind { get; init; }
	public ThornsPlayerBuildSnapKind SnapKind { get; init; }
	public Vector3 Size { get; init; }
	public float FootprintRadius { get; init; } = 48f;
}

public sealed class ThornsBuildMenuEntry
{
	public int SlotIndex { get; init; }
	public string Label { get; init; } = "";
	public string Glyph { get; init; } = "";
	public string StructureId { get; init; } = "";
	public ThornsPlayerBuildToolKind ToolKind { get; init; } = ThornsPlayerBuildToolKind.Place;
}

public static class ThornsPlayerBuildingDefinitions
{
	public const float MaxPlacementDistance = 800f;

	public static readonly ThornsBuildMenuEntry[] Toolbar =
	{
		new() { SlotIndex = 0, Label = "Floor", Glyph = "F", StructureId = "wood_foundation" },
		new() { SlotIndex = 1, Label = "Wall", Glyph = "W", StructureId = "wood_wall" },
		new() { SlotIndex = 2, Label = "Window", Glyph = "N", StructureId = "wood_window" },
		new() { SlotIndex = 3, Label = "Door", Glyph = "D", StructureId = "wood_doorframe" },
		new() { SlotIndex = 4, Label = "Ramp", Glyph = "R", StructureId = "wood_ramp" },
		new() { SlotIndex = 5, Label = "Remove", Glyph = "X", ToolKind = ThornsPlayerBuildToolKind.Remove },
		new() { SlotIndex = 6, Label = "Upgrade", Glyph = "U", ToolKind = ThornsPlayerBuildToolKind.Upgrade }
	};

	static readonly Dictionary<string, ThornsBuildDefinition> ById = new( StringComparer.OrdinalIgnoreCase )
	{
		["wood_foundation"] = new()
		{
			Id = "wood_foundation",
			DisplayName = "Wood Foundation",
			Costs = new[] { ("wood", 20) },
			PlacementKind = ThornsPlayerBuildPlacementKind.Grid,
			SnapKind = ThornsPlayerBuildSnapKind.Foundation,
			Size = new Vector3( ThornsBuildingModule.Cell, ThornsBuildingModule.Cell, ThornsBuildingModule.FloorThickness ),
			FootprintRadius = 48f
		},
		["wood_wall"] = new()
		{
			Id = "wood_wall",
			DisplayName = "Wood Wall",
			Costs = new[] { ("wood", 15) },
			PlacementKind = ThornsPlayerBuildPlacementKind.Grid,
			SnapKind = ThornsPlayerBuildSnapKind.Wall,
			Size = new Vector3( ThornsBuildingModule.WallThickness, ThornsBuildingModule.Cell, ThornsBuildingModule.WallHeight ),
			FootprintRadius = 32f
		},
		["wood_window"] = new()
		{
			Id = "wood_window",
			DisplayName = "Wood Window",
			Costs = new[] { ("wood", 12) },
			PlacementKind = ThornsPlayerBuildPlacementKind.Grid,
			SnapKind = ThornsPlayerBuildSnapKind.Window,
			Size = new Vector3( ThornsBuildingModule.WallThickness, ThornsBuildingModule.Cell, ThornsBuildingModule.WallHeight ),
			FootprintRadius = 32f
		},
		["wood_doorframe"] = new()
		{
			Id = "wood_doorframe",
			DisplayName = "Wood Door Frame",
			Costs = new[] { ("wood", 12) },
			PlacementKind = ThornsPlayerBuildPlacementKind.Grid,
			SnapKind = ThornsPlayerBuildSnapKind.DoorFrame,
			Size = new Vector3( ThornsBuildingModule.WallThickness, ThornsBuildingModule.Cell, ThornsBuildingModule.WallHeight ),
			FootprintRadius = 32f
		},
		["wood_ramp"] = new()
		{
			Id = "wood_ramp",
			DisplayName = "Wood Ramp",
			Costs = new[] { ("wood", 18) },
			PlacementKind = ThornsPlayerBuildPlacementKind.Grid,
			SnapKind = ThornsPlayerBuildSnapKind.Ramp,
			Size = new Vector3(
				ThornsBuildingModule.Cell,
				ThornsBuildingModule.Cell,
				ThornsBuildingModule.WallHeight ),
			FootprintRadius = 32f
		},
		["storage_chest"] = PortableFromCatalog( "storage_chest", "Storage Chest", "storage_chest_kit" ),
		["campfire"] = PortableFromCatalog( "campfire", "Campfire", "campfire_kit" ),
		["workbench"] = PortableFromCatalog( "workbench", "Workbench", "workbench_kit" ),
		["bed"] = PortableFromCatalog( "bed", "Bed", "bed_kit" ),
		["research"] = PortableFromCatalog( "research", "Research Station", "research_kit" ),
		["c4_charge"] = Portable( "c4_charge", "C4 Charge", "c4", new Vector3( 36f, 36f, 20f ) )
	};

	static ThornsBuildDefinition PortableFromCatalog( string id, string displayName, string kitId )
	{
		var size = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( id );
		return Portable( id, displayName, kitId, size );
	}

	static ThornsBuildDefinition Portable( string id, string displayName, string kitId, Vector3 size ) => new()
	{
		Id = id,
		DisplayName = displayName,
		RequiredItemId = kitId,
		PlacementKind = ThornsPlayerBuildPlacementKind.Free,
		SnapKind = ThornsPlayerBuildSnapKind.Portable,
		Size = size,
		FootprintRadius = MathF.Max( 24f, MathF.Max( size.x, size.y ) * 0.42f )
	};

	public static bool TryGet( string id, out ThornsBuildDefinition def ) => ById.TryGetValue( id ?? "", out def );

	public static bool TryGetKitStructure( string itemId, out string structureId )
	{
		structureId = itemId switch
		{
			"storage_chest_kit" => "storage_chest",
			"campfire_kit" => "campfire",
			"workbench_kit" => "workbench",
			"bed_kit" => "bed",
			"research_kit" => "research",
			"c4" => "c4_charge",
			_ => ""
		};

		return !string.IsNullOrWhiteSpace( structureId );
	}

	/// <summary>Maps a hotbar item (kit or placed structure item) to a portable structure id.</summary>
	public static bool TryResolveHotbarPlaceable( string itemId, out string structureId )
	{
		structureId = "";
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		if ( TryGetKitStructure( itemId, out structureId ) )
			return true;

		if ( string.Equals( itemId, "c4", StringComparison.OrdinalIgnoreCase ) )
		{
			structureId = "c4_charge";
			return true;
		}

		if ( TryGet( itemId, out var def )
		     && def.PlacementKind == ThornsPlayerBuildPlacementKind.Free
		     && def.SnapKind == ThornsPlayerBuildSnapKind.Portable )
		{
			structureId = def.Id;
			return true;
		}

		return false;
	}

	public static IReadOnlyList<(string ItemId, int Count)> PlacementCostsForHotbarItem(
		string hotbarItemId,
		ThornsBuildDefinition def )
	{
		if ( def is null )
			return Array.Empty<(string, int)>();

		if ( !string.IsNullOrWhiteSpace( hotbarItemId ) )
		{
			if ( string.Equals( hotbarItemId, def.RequiredItemId, StringComparison.OrdinalIgnoreCase ) )
				return new[] { (def.RequiredItemId, 1) };

			if ( string.Equals( hotbarItemId, def.Id, StringComparison.OrdinalIgnoreCase ) )
				return new[] { (def.Id, 1) };
		}

		return PlacementCosts( def );
	}

	public static IReadOnlyList<(string ItemId, int Count)> PlacementCosts( ThornsBuildDefinition def )
	{
		var costs = new List<(string, int)>( 4 );
		if ( !string.IsNullOrWhiteSpace( def.RequiredItemId ) )
			costs.Add( (def.RequiredItemId, 1) );

		if ( def.Costs is not null )
		{
			foreach ( var cost in def.Costs )
			{
				if ( !string.IsNullOrWhiteSpace( cost.ItemId ) && cost.Count > 0 )
					costs.Add( cost );
			}
		}

		return costs;
	}

	public static bool SupportsUpgrade( ThornsBuildDefinition def ) =>
		def is not null
		&& def.PlacementKind == ThornsPlayerBuildPlacementKind.Grid
		&& def.SnapKind != ThornsPlayerBuildSnapKind.Portable;

	public static IReadOnlyList<(string ItemId, int Count)> UpgradeCosts( ThornsBuildDefinition def, int currentTier )
	{
		if ( !SupportsUpgrade( def ) || currentTier < 0 || currentTier >= ThornsPlacedBuildStructure.MaxMaterialTier )
			return Array.Empty<(string, int)>();

		var baseCount = Math.Max( 8, def.Costs?.FirstOrDefault().Count ?? 8 );
		var itemId = currentTier == 0 ? "stone" : "metal_ore";
		var count = Math.Max( 10, baseCount * 2 );
		return new[] { (itemId, count) };
	}
}

public sealed class ThornsPlacementPreview
{
	public bool Valid;
	public string Reason = "";
	public string StructureId = "";
	public Vector3 Position;
	public Rotation Rotation;
	public ThornsBuildDefinition Definition;
}

[Title( "Thorns Player Building Controller" )]
[Category( "Thorns/Player" )]
public sealed class ThornsPlayerBuildingController : Component
{
	const string GhostName = "Thorns Build Ghost";
	const float TerrainSupportProbeRaise = 220f;

	public static ThornsPlayerBuildingController Local { get; private set; }

	[Property] public float PreviewYawStepDegrees { get; set; } = 90f;

	public bool BuildMenuOpen { get; private set; }
	public int SelectedSlot { get; private set; }
	public string SelectedStructureId { get; private set; } = "wood_foundation";
	public ThornsPlayerBuildToolKind SelectedToolKind { get; private set; } = ThornsPlayerBuildToolKind.Place;
	public ThornsPlacementPreview CurrentPreview { get; private set; } = new();
	public ThornsPlacedBuildStructure FocusedStructure { get; private set; }
	public string ModifyStatus { get; private set; } = "";
	public bool ModifyTargetValid { get; private set; }
	public bool IsHotbarPlaceModeActive { get; private set; }
	public string HotbarPlaceStructureId { get; private set; } = "";
	public bool UsesPrimaryFireForPlacement => BuildMenuOpen || IsHotbarPlaceModeActive;

	float _previewYaw;
	GameObject _ghost;
	string _ghostStructureId = "";

	bool IsOwnerDead()
	{
		var health = Components.Get<ThornsPlayerHealth>();
		return health.IsValid() && ( !health.IsAlive || health.IsDeadState );
	}

	protected override void OnStart()
	{
		if ( IsLocallyControlled() )
			Local = this;
	}

	protected override void OnDestroy()
	{
		if ( Local == this )
			Local = null;

		ClearHotbarPlaceMode();
		DestroyGhost();
	}

	/// <summary>Called from <see cref="Terraingen.UI.Core.ThornsGameplayUiHost"/> before hotbar slot input.</summary>
	public void TickBuildInput()
	{
		if ( !Game.IsPlaying || !IsLocallyControlled() )
			return;

		var health = Components.Get<ThornsPlayerHealth>();
		if ( health.IsValid() && ( !health.IsAlive || health.IsDeadState ) )
		{
			ForceCloseBuildMode();
			return;
		}

		if ( Terraingen.UI.Core.ThornsUiInputGate.BlocksBuildInput )
		{
			if ( BuildMenuOpen )
				SetBuildMenuOpen( false );

			ClearHotbarPlaceMode();
			DestroyGhost();
			return;
		}

		if ( ThornsKeybindService.Pressed( "Build" ) )
		{
			var opening = !BuildMenuOpen;
			SetBuildMenuOpen( opening );
			if ( opening )
				Components.Get<ThornsPlayerGameplay>()?.RequestCompleteJournalTask( "goal_explore_controls", "build" );
		}

		if ( BuildMenuOpen )
			TickBuildMenuMode();
		else
			TickHotbarKitMode();
	}

	void TickBuildMenuMode()
	{
		IsHotbarPlaceModeActive = false;
		HotbarPlaceStructureId = "";

		for ( var i = 0; i < ThornsPlayerBuildingDefinitions.Toolbar.Length; i++ )
		{
			if ( Input.Pressed( $"Slot{i + 1}" ) )
				SelectSlot( i );
		}

		TickRotationInput();
		if ( SelectedToolKind == ThornsPlayerBuildToolKind.Place )
		{
			UpdatePreviewForStructure( SelectedStructureId );
			if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
				TryRequestPlaceCurrent();
		}
		else
		{
			UpdateModifyPreview();
			if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
				TryRequestModifyCurrent();
		}

		NotifyBuildMenuUiChanged();
	}

	void TickHotbarKitMode()
	{
		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarItemId( out var itemId )
		     || !ThornsPlayerBuildingDefinitions.TryResolveHotbarPlaceable( itemId, out var structureId ) )
		{
			ClearHotbarPlaceMode();
			return;
		}

		IsHotbarPlaceModeActive = true;
		HotbarPlaceStructureId = structureId;
		TickRotationInput();
		UpdatePreviewForStructure( structureId );

		if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
			TryRequestPlaceCurrent( itemId );
	}

	void ClearHotbarPlaceMode()
	{
		IsHotbarPlaceModeActive = false;
		HotbarPlaceStructureId = "";
		DestroyGhost();
	}

	void TickRotationInput()
	{
		if ( !UsesPrimaryFireForPlacement )
			return;

		if ( !IsHotbarPlaceModeActive && SelectedToolKind != ThornsPlayerBuildToolKind.Place )
			return;

		var delta = 0f;
		if ( Input.Pressed( "BuildRotateLeft" ) )
			delta -= PreviewYawStepDegrees;
		if ( Input.Pressed( "BuildRotateRight" ) )
			delta += PreviewYawStepDegrees;

		if ( MathF.Abs( delta ) <= 0.01f )
			return;

		_previewYaw = (_previewYaw + delta) % 360f;
		if ( _previewYaw < 0f )
			_previewYaw += 360f;

		var structureId = IsHotbarPlaceModeActive ? HotbarPlaceStructureId : SelectedStructureId;
		if ( !string.IsNullOrWhiteSpace( structureId ) )
			UpdatePreviewForStructure( structureId );
	}

	public void ForceCloseBuildMode() => SetBuildMenuOpen( false );

	public void SetBuildMenuOpen( bool open )
	{
		if ( BuildMenuOpen == open )
			return;

		BuildMenuOpen = open;
		if ( open )
		{
			SelectSlot( Math.Clamp( SelectedSlot, 0, ThornsPlayerBuildingDefinitions.Toolbar.Length - 1 ) );
		}
		else
		{
			ClearHotbarPlaceMode();
			DestroyGhost();
			ClearModifyPreview();
		}

		NotifyBuildMenuUiChanged();
	}

	public void SelectSlot( int index )
	{
		if ( index < 0 || index >= ThornsPlayerBuildingDefinitions.Toolbar.Length )
			return;

		SelectedSlot = index;
		var entry = ThornsPlayerBuildingDefinitions.Toolbar[index];
		SelectedToolKind = entry.ToolKind;
		if ( SelectedToolKind == ThornsPlayerBuildToolKind.Place )
			SelectedStructureId = entry.StructureId;
		NotifyBuildMenuUiChanged();
	}

	static void NotifyBuildMenuUiChanged()
	{
		UiRevisionBus.Publish( UiRevisionChannel.BuildMenu );
		UiRevisionBus.Publish( UiRevisionChannel.Hotbar );
	}

	void UpdatePreviewForStructure( string structureId )
	{
		ClearModifyPreview( destroyGhost: false );
		CurrentPreview = ComputePreview( structureId );
		UpdateGhost( CurrentPreview );
	}

	ThornsPlacementPreview ComputePreview( string structureId )
	{
		var preview = new ThornsPlacementPreview { StructureId = structureId };
		if ( !ThornsPlayerBuildingDefinitions.TryGet( structureId, out var def ) )
			return Invalid( preview, "unknown" );

		preview.Definition = def;

		if ( def.SnapKind == ThornsPlayerBuildSnapKind.Portable )
		{
			if ( !TryResolvePortableAimPoint( out var portableAim ) )
				return Invalid( preview, "aim" );

			return ComputePortablePreview( preview, portableAim, def, _previewYaw );
		}

		if ( !TryResolvePlacementAimPoint( out var aimPoint ) )
			return Invalid( preview, "aim" );

		Vector3 snapPos;
		Rotation snapRot;

		if ( def.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
		{
			if ( TryFindSnapForPiece( aimPoint, def, out snapPos, out snapRot ) )
			{
				preview.Position = snapPos;
				preview.Rotation = snapRot * Rotation.FromYaw( _previewYaw );
				preview.Valid = IsPlacementClear( preview.Position, def, ignoreAttached: false );
				preview.Reason = preview.Valid ? "" : "blocked";
				return preview;
			}

			return ComputeFoundationPreview( preview, aimPoint, def );
		}

		if ( TryFindSnapForPiece( aimPoint, def, out snapPos, out snapRot ) )
		{
			preview.Position = snapPos;
			preview.Rotation = snapRot * Rotation.FromYaw( _previewYaw );
			// AUDIT FIX: ghost preview must mirror host ownership support (no foreign-base green ghost).
			preview.Valid = IsPlacementClear( preview.Position, def, ignoreAttached: true )
			                && HostHasPlacementSupport( def, preview.Position, ResolveLocalPlacerAccountKey() );
			preview.Reason = preview.Valid ? "" : "blocked";
			return preview;
		}

		var hintPos = aimPoint;
		var hintRot = Rotation.FromYaw( _previewYaw );
		if ( TryResolveSmoothFoundationPlacement( aimPoint, out var snappedHint, out var snappedRot, _previewYaw ) )
		{
			hintPos = snappedHint;
			hintRot = snappedRot;
		}

		return InvalidAt( preview, hintPos, hintRot, "no snap" );
	}

	ThornsPlacementPreview ComputeFoundationPreview( ThornsPlacementPreview preview, Vector3 aimPoint, ThornsBuildDefinition def )
	{
		if ( !TryResolveSmoothFoundationPlacement( aimPoint, out var snapped, out var rotation, _previewYaw, useViewAlignedGroundYaw: true ) )
			return Invalid( preview, "ground" );

		preview.Position = snapped;
		preview.Rotation = rotation;
		preview.Valid = IsPlacementClear( snapped, def, ignoreAttached: false );
		preview.Reason = preview.Valid ? "" : "blocked";
		return preview;
	}

	/// <summary>Horizontal look yaw for free placement (floors, placeables — follows camera, not world N/S/E/W).</summary>
	float ResolveViewYawDegrees()
	{
		var controller = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( controller.IsValid() )
			return controller.EyeAngles.yaw;

		if ( !ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out _, out var forward, useScreenCenter: true ) )
			return 0f;

		forward = forward.WithZ( 0f );
		if ( forward.Length < 0.001f )
			return 0f;

		return MathF.Atan2( forward.y, forward.x ).RadianToDegree();
	}

	ThornsPlacementPreview ComputePortablePreview( ThornsPlacementPreview preview, Vector3 aimPoint, ThornsBuildDefinition def, float manualYawOffsetDegrees )
	{
		var position = aimPoint;
		if ( TryResolvePortableSurfaceZ( aimPoint.x, aimPoint.y, aimPoint, out var surfaceZ ) )
			position = new Vector3( aimPoint.x, aimPoint.y, surfaceZ );

		preview.Position = position;
		preview.Rotation = Rotation.FromYaw( ResolveViewYawDegrees() + manualYawOffsetDegrees );

		if ( ThornsPlaceableFurnitureCatalog.UsesCatalogPresentation( preview.StructureId ) )
		{
			ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface(
				preview.StructureId,
				ref preview.Position,
				preview.Rotation );
		}

		preview.Valid = IsPlacementClear( preview.Position, def, ignoreAttached: true );
		preview.Reason = preview.Valid ? "" : "blocked";
		return preview;
	}

	/// <summary>Smooth floor placement: crosshair XY with terrain or deck height (no grid snap).</summary>
	bool TryResolveSmoothFoundationPlacement(
		Vector3 aimPoint,
		out Vector3 position,
		out Rotation rotation,
		float manualYawOffsetDegrees = 0f,
		bool useViewAlignedGroundYaw = false )
	{
		position = default;
		rotation = Rotation.Identity;

		if ( TryResolveWallTopFoundationPlacement( aimPoint, out position, out rotation, manualYawOffsetDegrees ) )
			return true;

		if ( !TryResolveFoundationSurfaceZ( aimPoint.x, aimPoint.y, aimPoint, out var surfaceZ ) )
			return false;

		position = new Vector3( aimPoint.x, aimPoint.y, surfaceZ );
		var yaw = useViewAlignedGroundYaw
			? ResolveViewYawDegrees() + manualYawOffsetDegrees
			: manualYawOffsetDegrees;
		rotation = Rotation.FromYaw( yaw );
		return true;
	}

	static bool TryResolveFoundationSurfaceZ( float worldX, float worldY, Vector3 aimPoint, out float surfaceZ )
	{
		if ( TryGetFoundationVerticalStackBottom( worldX, worldY, aimPoint, out surfaceZ ) )
			return true;

		if ( TryGetWallTopFloorBottomNear( worldX, worldY, aimPoint, out surfaceZ ) )
			return true;

		if ( TryGetFoundationDeckBottomNear( worldX, worldY, aimPoint, out surfaceZ ) )
			return true;

		return TrySampleFoundationGroundZ( worldX, worldY, out surfaceZ );
	}

	/// <summary>Place a floor on the story deck directly above an existing floor (same XY cell).</summary>
	static bool TryGetFoundationVerticalStackBottom( float worldX, float worldY, Vector3 aimPoint, out float floorBottomZ )
	{
		floorBottomZ = 0f;
		var bestScore = float.MaxValue;
		var found = false;
		var deckTolerance = ThornsBuildingModule.FloorThickness + ThornsBuildingModule.WallHeight * 0.35f;
		var minAimAboveBottom = ThornsBuildingModule.FloorThickness * 0.35f;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			if ( hostDef.SnapKind != ThornsPlayerBuildSnapKind.Foundation )
				continue;

			var hostPos = placed.GameObject.WorldPosition;
			if ( !IsPointOnFoundationCell( worldX, worldY, hostPos ) )
				continue;

			if ( aimPoint.z < hostPos.z + minAimAboveBottom )
				continue;

			var floorTopZ = hostPos.z + ThornsBuildingModule.FloorThickness;
			var deckZ = FoundationStoryDeckZ( hostPos.z );

			if ( MathF.Abs( aimPoint.z - deckZ ) <= deckTolerance )
			{
				floorBottomZ = deckZ;
				return true;
			}

			if ( aimPoint.z < floorTopZ - ThornsBuildingModule.FloorThickness * 0.65f )
				continue;

			if ( aimPoint.z >= deckZ + deckTolerance )
				continue;

			var score = MathF.Abs( aimPoint.z - floorTopZ ) + MathF.Abs( deckZ - aimPoint.z ) * 0.05f;
			if ( score >= bestScore )
				continue;

			bestScore = score;
			floorBottomZ = deckZ;
			found = true;
		}

		return found;
	}

	static bool TryGetFoundationDeckBottomNear( float worldX, float worldY, Vector3 aimPoint, out float floorBottomZ )
	{
		floorBottomZ = 0f;
		if ( !TryGetFoundationDeckSurfaceNear( worldX, worldY, aimPoint, out var floorTopZ ) )
			return false;

		floorBottomZ = floorTopZ - ThornsBuildingModule.FloorThickness;
		return true;
	}

	bool TryResolvePortableAimPoint( out Vector3 worldPoint )
	{
		worldPoint = default;
		if ( !TryResolvePlacementAimPoint( out var sample, preferFoundationFloor: true ) )
			return false;

		if ( !TryResolvePortableSurfaceZ( sample.x, sample.y, sample, out var surfaceZ ) )
			return false;

		worldPoint = new Vector3( sample.x, sample.y, surfaceZ );
		return true;
	}

	bool TryResolvePlacementAimPoint( out Vector3 worldPoint, bool preferFoundationFloor = false )
	{
		worldPoint = default;
		if ( !ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out var origin, out var forward, useScreenCenter: true ) )
			return false;

		var rayDir = forward.Normal;
		var maxDist = ThornsPlayerBuildingDefinitions.MaxPlacementDistance;
		var end = origin + rayDir * maxDist;

		var trace = Scene.Trace.Ray( origin, end )
			.IgnoreGameObjectHierarchy( GameObject );

		if ( _ghost.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _ghost );

		var sceneHit = trace.Run();

		if ( preferFoundationFloor && sceneHit.Hit && TryGetFoundationPlacementPoint( sceneHit, out var foundationPoint ) )
		{
			worldPoint = foundationPoint;
			return true;
		}

		var terrain = ThornsTerrainCache.Current;
		if ( ThornsTerrainSurface.TryIntersectAlongRay( terrain, origin, rayDir, maxDist, out var terrainPoint ) )
		{
			if ( sceneHit.Hit && TryGetPlayerStructureFromTrace( sceneHit, out _ ) )
			{
				var sceneAlong = Vector3.Dot( sceneHit.HitPosition - origin, rayDir );
				var terrainAlong = Vector3.Dot( terrainPoint - origin, rayDir );
				if ( sceneAlong + 12f < terrainAlong )
				{
					worldPoint = sceneHit.HitPosition;
					return true;
				}
			}

			worldPoint = terrainPoint;
			return true;
		}

		if ( sceneHit.Hit )
		{
			worldPoint = sceneHit.HitPosition;
			return true;
		}

		if ( terrain is not null && terrain.IsValid()
		     && ThornsTerrainSurface.TrySnapToTerrain( terrain, end + Vector3.Up * TerrainSupportProbeRaise, out var ground ) )
		{
			worldPoint = ground;
			return true;
		}

		return false;
	}

	static bool TryGetPlayerStructureFromTrace( SceneTraceResult hit, out ThornsPlacedBuildStructure structure )
	{
		structure = null;
		if ( !hit.Hit || !hit.GameObject.IsValid() )
			return false;

		structure = hit.GameObject.Components.Get<ThornsPlacedBuildStructure>( FindMode.EverythingInSelfAndParent );
		return structure.IsValid();
	}

	static bool TryGetFoundationPlacementPoint( SceneTraceResult hit, out Vector3 point )
	{
		point = default;
		if ( !hit.Hit || !hit.GameObject.IsValid() )
			return false;

		var structure = hit.GameObject.Components.Get<ThornsPlacedBuildStructure>( FindMode.EverythingInSelfAndParent );
		if ( !structure.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( structure.StructureId, out var def ) )
			return false;

		if ( def.SnapKind != ThornsPlayerBuildSnapKind.Foundation )
			return false;

		var hostPos = structure.GameObject.WorldPosition;
		point = new Vector3( hit.HitPosition.x, hit.HitPosition.y, hostPos.z + ThornsBuildingModule.FloorThickness );
		return true;
	}

	static bool TryResolvePortableSurfaceZ( float worldX, float worldY, Vector3 aimPoint, out float surfaceZ )
	{
		if ( TryGetFoundationFloorTopAtPoint( worldX, worldY, out surfaceZ ) )
			return true;

		if ( TryGetFoundationDeckSurfaceNear( worldX, worldY, aimPoint, out surfaceZ ) )
			return true;

		return TrySampleFoundationGroundZ( worldX, worldY, out surfaceZ );
	}

	static bool IsPointOnFoundationCell( float worldX, float worldY, Vector3 hostPos )
	{
		var half = ThornsBuildingModule.Cell * 0.5f;
		return MathF.Abs( worldX - hostPos.x ) <= half + 0.5f
		       && MathF.Abs( worldY - hostPos.y ) <= half + 0.5f;
	}

	static bool TryGetFoundationFloorTopAtPoint( float worldX, float worldY, out float floorTopZ )
	{
		floorTopZ = 0f;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			if ( hostDef.SnapKind != ThornsPlayerBuildSnapKind.Foundation )
				continue;

			var hostPos = placed.GameObject.WorldPosition;
			if ( !IsPointOnFoundationCell( worldX, worldY, hostPos ) )
				continue;

			floorTopZ = hostPos.z + ThornsBuildingModule.FloorThickness;
			return true;
		}

		return false;
	}

	static bool TryGetFoundationDeckSurfaceNear( float worldX, float worldY, Vector3 aimPoint, out float floorTopZ )
	{
		floorTopZ = 0f;
		var cell = ThornsBuildingModule.Cell;
		var deckZTolerance = ThornsBuildingModule.FloorThickness + ThornsBuildingModule.WallHeight * 0.5f;
		var bestScore = float.MaxValue;
		var found = false;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			if ( hostDef.SnapKind != ThornsPlayerBuildSnapKind.Foundation )
				continue;

			var hostPos = placed.GameObject.WorldPosition;
			var topZ = hostPos.z + ThornsBuildingModule.FloorThickness;
			if ( MathF.Abs( aimPoint.z - topZ ) > deckZTolerance )
				continue;

			var xyDist = (hostPos.WithZ( 0f ) - new Vector3( worldX, worldY, 0f )).Length;
			if ( xyDist > cell * 1.05f )
				continue;

			var score = xyDist + MathF.Abs( aimPoint.z - topZ ) * 0.15f;
			if ( score >= bestScore )
				continue;

			bestScore = score;
			floorTopZ = topZ;
			found = true;
		}

		return found;
	}

	static bool TrySampleFoundationGroundZ( float worldX, float worldY, out float groundZ )
	{
		groundZ = 0f;
		var terrain = ThornsTerrainCache.Current;
		if ( terrain is null || !terrain.IsValid() )
			return false;

		if ( !ThornsTerrainSurface.TryRaycastGround( terrain, worldX, worldY, out var hit ) )
			return false;

		groundZ = hit.z;
		return true;
	}

	/// <summary>True when placing on/continuing an elevated floor grid (not bare terrain).</summary>
	static bool IsNearFoundationFloorHeight( Vector3 position )
	{
		var cell = ThornsBuildingModule.Cell;
		var zTolerance = ThornsBuildingModule.FloorThickness + ThornsBuildingModule.WallHeight * 0.5f;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			if ( hostDef.SnapKind != ThornsPlayerBuildSnapKind.Foundation )
				continue;

			var hostPos = placed.GameObject.WorldPosition;
			if ( (hostPos - position).WithZ( 0f ).Length > cell * 1.05f )
				continue;

			if ( MathF.Abs( hostPos.z - position.z ) <= zTolerance )
				return true;

			var deckZ = FoundationStoryDeckZ( hostPos.z );
			if ( MathF.Abs( deckZ - position.z ) <= zTolerance )
				return true;
		}

		return false;
	}


	bool TryFindSnapForPiece( Vector3 aimPoint, ThornsBuildDefinition def, out Vector3 position, out Rotation rotation )
	{
		position = default;
		rotation = default;
		var best = float.MaxValue;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			if ( hostDef.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
				ConsiderFoundationHost( placed, def, aimPoint, ref best, ref position, ref rotation );
		}

		return best < float.MaxValue;
	}

	void ConsiderFoundationHost( ThornsPlacedBuildStructure host, ThornsBuildDefinition incoming, Vector3 aimPoint, ref float best, ref Vector3 position, ref Rotation rotation )
	{
		var cell = ThornsBuildingModule.Cell;
		var hostPos = host.GameObject.WorldPosition;
		var anchorPos = hostPos;

		if ( incoming.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
		{
			if ( !TryGetFoundationStoryZBand( aimPoint, hostPos.z, out var placementZ ) )
				return;

			anchorPos = hostPos.WithZ( placementZ );
		}

		var candidates = incoming.SnapKind switch
		{
			ThornsPlayerBuildSnapKind.Foundation => new[]
			{
				(anchorPos + host.GameObject.WorldRotation * new Vector3( cell, 0f, 0f ), host.GameObject.WorldRotation),
				(anchorPos + host.GameObject.WorldRotation * new Vector3( -cell, 0f, 0f ), host.GameObject.WorldRotation),
				(anchorPos + host.GameObject.WorldRotation * new Vector3( 0f, cell, 0f ), host.GameObject.WorldRotation),
				(anchorPos + host.GameObject.WorldRotation * new Vector3( 0f, -cell, 0f ), host.GameObject.WorldRotation)
			},
			ThornsPlayerBuildSnapKind.Wall or ThornsPlayerBuildSnapKind.Window or ThornsPlayerBuildSnapKind.DoorFrame => new[]
			{
				(hostPos + host.GameObject.WorldRotation * new Vector3( 0f, cell * 0.5f, FoundationWallCenterZ() ), host.GameObject.WorldRotation),
				(hostPos + host.GameObject.WorldRotation * new Vector3( cell * 0.5f, 0f, FoundationWallCenterZ() ), host.GameObject.WorldRotation * Rotation.FromYaw( 90f )),
				(hostPos + host.GameObject.WorldRotation * new Vector3( 0f, -cell * 0.5f, FoundationWallCenterZ() ), host.GameObject.WorldRotation),
				(hostPos + host.GameObject.WorldRotation * new Vector3( -cell * 0.5f, 0f, FoundationWallCenterZ() ), host.GameObject.WorldRotation * Rotation.FromYaw( 90f ))
			},
			ThornsPlayerBuildSnapKind.Ramp => new[]
			{
				(hostPos + host.GameObject.WorldRotation * new Vector3( 0f, 0f, FoundationRampCenterZ() ), host.GameObject.WorldRotation)
			},
			_ => Array.Empty<(Vector3, Rotation)>()
		};

		foreach ( var candidate in candidates )
		{
			if ( incoming.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
				ConsiderFoundationEdgeCandidate( candidate.Item1, candidate.Item2, aimPoint, ref best, ref position, ref rotation );
			else
				ConsiderCandidate( candidate.Item1, candidate.Item2, aimPoint, ref best, ref position, ref rotation );
		}

		if ( incoming.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
			ConsiderFoundationStackOnHost( host, aimPoint, 0f, ref best, ref position, ref rotation );
	}

	static bool TryFindFoundationEdgeSnap(
		Vector3 aimPoint,
		float maxXyDistance,
		out Vector3 position,
		out Rotation rotation,
		float yawDegrees = 0f )
	{
		position = default;
		rotation = Rotation.FromYaw( yawDegrees );
		var best = float.MaxValue;
		var found = false;
		var cell = ThornsBuildingModule.Cell;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			if ( hostDef.SnapKind != ThornsPlayerBuildSnapKind.Foundation )
				continue;

			var hostPos = placed.GameObject.WorldPosition;
			if ( !TryGetFoundationStoryZBand( aimPoint, hostPos.z, out var placementZ ) )
				continue;

			var anchorPos = hostPos.WithZ( placementZ );
			var hostRot = placed.GameObject.WorldRotation;
			var offsets = new[]
			{
				new Vector3( cell, 0f, 0f ),
				new Vector3( -cell, 0f, 0f ),
				new Vector3( 0f, cell, 0f ),
				new Vector3( 0f, -cell, 0f )
			};

			foreach ( var localOffset in offsets )
			{
				var candidatePos = anchorPos + hostRot * localOffset;
				var candidateRot = hostRot * Rotation.FromYaw( yawDegrees );
				var xyDist = (candidatePos.WithZ( 0f ) - aimPoint.WithZ( 0f )).Length;
				if ( xyDist > maxXyDistance || xyDist >= best )
					continue;

				best = xyDist;
				position = candidatePos;
				rotation = candidateRot;
				found = true;
			}

			ConsiderFoundationStackOnHost( placed, aimPoint, yawDegrees, ref best, ref position, ref rotation );
			if ( best < float.MaxValue )
				found = true;
		}

		return found;
	}

	static void ConsiderFoundationStackOnHost(
		ThornsPlacedBuildStructure host,
		Vector3 aimPoint,
		float yawDegrees,
		ref float best,
		ref Vector3 position,
		ref Rotation rotation )
	{
		var hostPos = host.GameObject.WorldPosition;
		if ( !IsPointOnFoundationCell( aimPoint.x, aimPoint.y, hostPos ) )
			return;

		var floorTopZ = hostPos.z + ThornsBuildingModule.FloorThickness;
		if ( aimPoint.z < floorTopZ - ThornsBuildingModule.FloorThickness * 0.65f )
			return;

		var deckZ = FoundationStoryDeckZ( hostPos.z );
		if ( aimPoint.z >= deckZ + ThornsBuildingModule.FloorThickness + ThornsBuildingModule.WallHeight * 0.35f )
			return;

		var candidatePos = hostPos.WithZ( deckZ );
		var candidateRot = host.GameObject.WorldRotation * Rotation.FromYaw( yawDegrees );
		ConsiderFoundationEdgeCandidate( candidatePos, candidateRot, aimPoint, ref best, ref position, ref rotation );
	}

	static void ConsiderFoundationEdgeCandidate(
		Vector3 candidatePos,
		Rotation candidateRot,
		Vector3 aimPoint,
		ref float best,
		ref Vector3 position,
		ref Rotation rotation )
	{
		var xyDist = (candidatePos.WithZ( 0f ) - aimPoint.WithZ( 0f )).Length;
		if ( xyDist > ThornsBuildingModule.Cell * 0.75f || xyDist >= best )
			return;

		best = xyDist;
		position = candidatePos;
		rotation = candidateRot;
	}

	static float FoundationStoryDeckZ( float floorBottomZ ) =>
		floorBottomZ + ThornsBuildingModule.StoryHeightWorld;

	static bool TryGetFoundationStoryZBand( Vector3 aimPoint, float floorBottomZ, out float placementZ )
	{
		placementZ = floorBottomZ;
		var zTolerance = ThornsBuildingModule.FloorThickness + ThornsBuildingModule.WallHeight * 0.35f;
		var floorTopZ = floorBottomZ + ThornsBuildingModule.FloorThickness;
		var deckZ = FoundationStoryDeckZ( floorBottomZ );

		if ( aimPoint.z >= floorTopZ - ThornsBuildingModule.FloorThickness * 0.65f )
		{
			if ( MathF.Abs( aimPoint.z - deckZ ) <= zTolerance || aimPoint.z < deckZ + zTolerance )
			{
				placementZ = deckZ;
				return true;
			}
		}

		var nearLower = MathF.Abs( aimPoint.z - floorBottomZ ) <= zTolerance;
		var nearDeck = MathF.Abs( aimPoint.z - deckZ ) <= zTolerance;
		if ( !nearLower && !nearDeck )
			return false;

		placementZ = nearDeck && !nearLower ? deckZ : floorBottomZ;
		return true;
	}

	static bool TryResolveWallTopFoundationPlacement( Vector3 aimPoint, out Vector3 position, out Rotation rotation, float yawDegrees )
	{
		position = default;
		rotation = Rotation.FromYaw( yawDegrees );
		return TryFindFoundationEdgeSnap(
			aimPoint,
			ThornsBuildingModule.Cell * 0.75f,
			out position,
			out rotation,
			yawDegrees );
	}

	static bool TryGetWallTopFloorBottomNear( float worldX, float worldY, Vector3 aimPoint, out float floorBottomZ )
	{
		floorBottomZ = 0f;
		if ( !TryResolveWallTopFoundationPlacement(
			     new Vector3( worldX, worldY, aimPoint.z ),
			     out var snapped,
			     out _,
			     0f ) )
			return false;

		floorBottomZ = snapped.z;
		return true;
	}

	static float FoundationWallCenterZ() =>
		ThornsBuildingModule.FloorThickness + ThornsBuildingModule.WallHeight * 0.5f;

	static float FoundationRampCenterZ() =>
		ThornsProcBuildingRampGeometry.RampSeatPivotLocalZFromFoundationCenter();

	static void ConsiderCandidate( Vector3 candidatePos, Rotation candidateRot, Vector3 aimPoint, ref float best, ref Vector3 position, ref Rotation rotation )
	{
		var d = (candidatePos - aimPoint).Length;
		if ( d > 86f || d >= best )
			return;

		best = d;
		position = candidatePos;
		rotation = candidateRot;
	}

	static bool IsWallLike( ThornsPlayerBuildSnapKind kind ) =>
		kind is ThornsPlayerBuildSnapKind.Wall or ThornsPlayerBuildSnapKind.Window or ThornsPlayerBuildSnapKind.DoorFrame;

	bool IsPlacementClear( Vector3 position, ThornsBuildDefinition def, bool ignoreAttached )
	{
		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var other ) )
				continue;

			if ( ignoreAttached && ShouldAllowAttachedOverlap( def, other ) )
				continue;

			if ( (placed.GameObject.WorldPosition - position).Length < (def.FootprintRadius + other.FootprintRadius) * 0.72f )
				return false;
		}

		return true;
	}

	static bool ShouldAllowAttachedOverlap( ThornsBuildDefinition incoming, ThornsBuildDefinition other )
	{
		if ( incoming.PlacementKind == ThornsPlayerBuildPlacementKind.Free && other.PlacementKind == ThornsPlayerBuildPlacementKind.Grid )
			return true;

		if ( incoming.SnapKind == ThornsPlayerBuildSnapKind.Foundation && IsWallLike( other.SnapKind ) )
			return true;

		if ( IsWallLike( incoming.SnapKind ) && other.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
			return true;

		if ( incoming.SnapKind == ThornsPlayerBuildSnapKind.Ramp && other.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
			return true;

		return false;
	}

	static ThornsPlacementPreview Invalid( ThornsPlacementPreview preview, string reason )
	{
		preview.Valid = false;
		preview.Reason = reason;
		return preview;
	}

	static ThornsPlacementPreview InvalidAt( ThornsPlacementPreview preview, Vector3 position, Rotation rotation, string reason )
	{
		preview.Position = position;
		preview.Rotation = rotation;
		preview.Valid = false;
		preview.Reason = reason;
		return preview;
	}

	void TryRequestPlaceCurrent( string hotbarItemId = "" )
	{
		var structureId = IsHotbarPlaceModeActive ? HotbarPlaceStructureId : SelectedStructureId;
		var preview = ComputePreview( structureId );
		CurrentPreview = preview;
		UpdateGhost( preview );

		if ( preview is null || !preview.Valid || preview.Definition is null )
		{
			PushPlacementErrorSfxToOwner();
			return;
		}

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestPlace( preview.StructureId, preview.Position, preview.Rotation, hotbarItemId ?? "", _previewYaw );
		else
			HostTryPlace( preview.StructureId, preview.Position, preview.Rotation, Connection.Local, hotbarItemId ?? "", _previewYaw );
	}

	void TryRequestModifyCurrent()
	{
		if ( !ModifyTargetValid || !FocusedStructure.IsValid() || string.IsNullOrWhiteSpace( FocusedStructure.InstanceKey ) )
			return;

		if ( SelectedToolKind == ThornsPlayerBuildToolKind.Remove )
		{
			if ( Networking.IsActive && !Networking.IsHost )
				RpcRequestRemove( FocusedStructure.InstanceKey );
			else
				HostTryRemove( FocusedStructure.InstanceKey, Connection.Local );
			return;
		}

		if ( SelectedToolKind == ThornsPlayerBuildToolKind.Upgrade )
		{
			if ( Networking.IsActive && !Networking.IsHost )
				RpcRequestUpgrade( FocusedStructure.InstanceKey );
			else
				HostTryUpgrade( FocusedStructure.InstanceKey, Connection.Local );
		}
	}

	[Rpc.Host]
	void RpcRequestPlace( string structureId, Vector3 position, Rotation rotation, string hotbarItemId, float previewYawDegrees )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryPlace( structureId, position, rotation, Rpc.Caller, hotbarItemId ?? "", previewYawDegrees );
	}

	[Rpc.Host]
	void RpcRequestRemove( string instanceKey )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryRemove( instanceKey, Rpc.Caller );
	}

	[Rpc.Host]
	void RpcRequestUpgrade( string instanceKey )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryUpgrade( instanceKey, Rpc.Caller );
	}

	void HostTryPlace(
		string structureId,
		Vector3 position,
		Rotation rotation,
		Connection owner,
		string hotbarItemId = "",
		float previewYawDegrees = float.NaN )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( IsOwnerDead() )
			return;

		if ( !HostTryPlaceInternal( structureId, position, rotation, owner, hotbarItemId, previewYawDegrees ) )
			PushPlacementErrorSfxToOwner();
	}

	bool HostTryPlaceInternal(
		string structureId,
		Vector3 position,
		Rotation rotation,
		Connection owner,
		string hotbarItemId,
		float previewYawDegrees )
	{
		if ( !ThornsPlayerBuildingDefinitions.TryGet( structureId, out var def ) )
			return false;

		if ( !HostTryValidatePlacementTransform( structureId, position, rotation, previewYawDegrees, out position, out rotation ) )
			return false;

		if ( def.SnapKind == ThornsPlayerBuildSnapKind.Foundation
		     && !IsNearFoundationFloorHeight( position )
		     && TryResolveFoundationSurfaceZ( position.x, position.y, position, out var foundationGroundZ ) )
			position = new Vector3( position.x, position.y, foundationGroundZ );

		if ( def.SnapKind == ThornsPlayerBuildSnapKind.Portable
		     && TryResolvePortableSurfaceZ( position.x, position.y, position, out var portableSurfaceZ ) )
			position = new Vector3( position.x, position.y, portableSurfaceZ );

		if ( ThornsPlaceableFurnitureCatalog.UsesCatalogPresentation( structureId ) )
		{
			ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface(
				structureId,
				ref position,
				rotation );
		}

		if ( !string.IsNullOrWhiteSpace( hotbarItemId )
		     && ( !ThornsPlayerBuildingDefinitions.TryResolveHotbarPlaceable( hotbarItemId, out var expectedStructure )
		          || !string.Equals( expectedStructure, structureId, StringComparison.OrdinalIgnoreCase ) ) )
			return false;

		if ( (position - GameObject.WorldPosition).Length > ThornsPlayerBuildingDefinitions.MaxPlacementDistance * 1.2f )
			return false;

		if ( !IsPlacementClear( position, def, ignoreAttached: true ) )
			return false;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
			return false;

		// AUDIT FIX: pass placer account so we cannot snap walls/ramps onto foreign foundations.
		var placerKey = ThornsBuildingOwnership.ResolveOwnerAccountKey( owner, gameplay );
		if ( !HostHasPlacementSupport( def, position, placerKey ) )
			return false;

		var costs = ThornsPlayerBuildingDefinitions.PlacementCostsForHotbarItem( hotbarItemId, def );
		if ( !string.IsNullOrWhiteSpace( hotbarItemId ) )
		{
			if ( !gameplay.TryGetActiveHotbarIndex( out var hotbarIndex )
			     || !gameplay.HostTryConsumePlacementCostsPreferHotbar( hotbarIndex, costs ) )
				return false;
		}
		else if ( !gameplay.HostTryConsumeItems( costs ) )
			return false;

		if ( string.Equals( structureId, "c4_charge", StringComparison.OrdinalIgnoreCase ) )
		{
			var placer = owner is null
				? GameObject
				: Scene.GetAllComponents<ThornsPlayerGameplay>()
					.FirstOrDefault( g => string.Equals(
						g.AccountKey,
						Terraingen.Multiplayer.ThornsPersistenceIdentity.GetStableAccountKey( owner ),
						StringComparison.OrdinalIgnoreCase ) )?.GameObject ?? GameObject;

			var charge = ThornsExplosiveCharge.SpawnHost( Scene, owner, position, rotation, placer );
			if ( charge is null || !charge.IsValid() )
			{
				foreach ( var cost in costs )
					gameplay.HostGrantHarvestItem( cost.ItemId, cost.Count );
				return false;
			}

			gameplay.PushClientToastToOwner( "C4 planted — 3 seconds!", "warning", 3f );
			PushPlacementSfxToOwner();
			return true;
		}

		var placed = ThornsPlacedBuildStructure.SpawnHost( Scene, owner, structureId, position, rotation );
		if ( placed is null || !placed.IsValid() )
		{
			foreach ( var cost in costs )
				gameplay.HostGrantHarvestItem( cost.ItemId, cost.Count );
			return false;
		}

		// AUDIT FIX: prefer gameplay AccountKey if connection identity was empty.
		if ( string.IsNullOrWhiteSpace( placed.OwnerAccountKey )
		     && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			placed.OwnerAccountKey = gameplay.AccountKey.Trim();

		gameplay.HostNotifyStructurePlaced( structureId );
		if ( string.Equals( structureId, "bed", StringComparison.OrdinalIgnoreCase )
		     && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
		{
			var yaw = placed.GameObject.WorldRotation.Angles().yaw;
			ThornsWorldPersistence.Instance?.HostSetBedSpawn( gameplay.AccountKey, placed.GameObject.WorldPosition, yaw );
			gameplay.PushClientToastToOwner( "Respawn point set to your bed.", "info" );
		}
		PushPlacementSfxToOwner();
		ThornsWorldPersistence.RequestSave();
		return true;
	}

	void HostTryRemove( string instanceKey, Connection caller )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsOwnerDead() )
			return;

		if ( !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var target ) )
			return;

		if ( !HostCanModify( target, caller ) )
			return;

		if ( !HostIsWithinModifyDistance( target ) )
			return;

		target.GameObject.Destroy();
		PushDemolishSfxToOwner();
		ThornsWorldPersistence.RequestSave();
	}

	void HostTryUpgrade( string instanceKey, Connection caller )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsOwnerDead() )
			return;

		if ( !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var target ) )
			return;

		if ( !HostCanModify( target, caller ) || !ThornsPlayerBuildingDefinitions.TryGet( target.StructureId, out var def ) )
			return;

		if ( !HostIsWithinModifyDistance( target ) )
			return;

		if ( !ThornsPlayerBuildingDefinitions.SupportsUpgrade( def ) || target.MaterialTier >= ThornsPlacedBuildStructure.MaxMaterialTier )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
			return;

		var costs = ThornsPlayerBuildingDefinitions.UpgradeCosts( def, target.MaterialTier );
		if ( !gameplay.HostTryConsumeItems( costs ) )
			return;

		target.MaterialTier = Math.Clamp( target.MaterialTier + 1, 0, ThornsPlacedBuildStructure.MaxMaterialTier );
		target.HostRefreshHealthAfterUpgrade();
		target.HostBroadcastMaterialTier();

		gameplay.HostNotifyMilestoneEvent( "fortify" );

		ThornsWorldPersistence.RequestSave();
	}

	void PushPlacementSfxToOwner() => ThornsGameplaySfx.PlayBuildMenuOrPlace( GameObject );

	void PushPlacementErrorSfxToOwner()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
			ThornsGameplaySfx.PlayPlacementError( GameObject );
		else
			ThornsGameplaySfx.PlayAtPawnEar( GameObject, ThornsGameplaySfx.PlacementError, 0.9f );
	}

	void PushDemolishSfxToOwner() => ThornsGameplaySfx.PlayDemolish( GameObject );

	static bool HostCanModify( ThornsPlacedBuildStructure target, Connection caller )
	{
		if ( target is null || !target.IsValid() )
			return false;

		if ( !Networking.IsActive )
			return true;

		if ( caller is null )
			return false;

		// AUDIT FIX: blank OwnerAccountKey used to mean "anyone may demolish/upgrade".
		// Now requires owner match or guild — see ThornsBuildingOwnership.
		var callerKey = Terraingen.Multiplayer.ThornsPersistenceIdentity.GetStableAccountKey( caller );
		return ThornsBuildingOwnership.HostAccountMayUseStructure( target.OwnerAccountKey, callerKey );
	}

	bool HostIsWithinModifyDistance( ThornsPlacedBuildStructure target )
	{
		if ( target is null || !target.IsValid() )
			return false;

		var maxDist = ThornsPlayerBuildingDefinitions.MaxPlacementDistance * 1.2f;
		return (target.GameObject.WorldPosition - GameObject.WorldPosition).Length <= maxDist;
	}

	const float MaxPlacementPositionErrorInches = 52f;
	const float MaxPlacementYawErrorDegrees = 14f;

	/// <summary>Recompute placement from server aim; reject client transforms that diverge.</summary>
	bool HostTryValidatePlacementTransform(
		string structureId,
		Vector3 clientPosition,
		Rotation clientRotation,
		float previewYawDegrees,
		out Vector3 position,
		out Rotation rotation )
	{
		position = clientPosition;
		rotation = clientRotation;

		var savedYaw = _previewYaw;
		_previewYaw = float.IsNaN( previewYawDegrees ) ? clientRotation.Angles().yaw : previewYawDegrees;

		var preview = ComputePreview( structureId );
		_previewYaw = savedYaw;

		if ( !preview.Valid || preview.Definition is null )
			return false;

		if ( Vector3.DistanceBetween( clientPosition, preview.Position ) > MaxPlacementPositionErrorInches )
			return false;

		var yawDelta = MathF.Abs( NormalizeYawDelta( clientRotation.Angles().yaw - preview.Rotation.Angles().yaw ) );
		if ( yawDelta > MaxPlacementYawErrorDegrees )
			return false;

		position = preview.Position;
		rotation = preview.Rotation;
		return true;
	}

	static float NormalizeYawDelta( float degrees )
	{
		var wrapped = (degrees + 180f) % 360f;
		if ( wrapped < 0f )
			wrapped += 360f;

		return wrapped - 180f;
	}

	string ResolveLocalPlacerAccountKey()
	{
		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay.IsValid() && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			return gameplay.AccountKey.Trim();

		if ( Networking.IsActive && Network.Owner is not null )
			return Terraingen.Multiplayer.ThornsPersistenceIdentity.GetStableAccountKey( Network.Owner ) ?? "";

		return Terraingen.Multiplayer.ThornsPersistenceIdentity.GetStableAccountKey( GameObject ) ?? "";
	}

	/// <summary>
	/// AUDIT FIX: support geometry must be owned by the placer (or guild).
	/// Previously any nearby foundation counted — enabling building onto rivals' bases.
	/// Preview now also calls this so foreign snaps show red ghosts.
	/// </summary>
	static bool HostHasPlacementSupport( ThornsBuildDefinition def, Vector3 position, string placerAccountKey )
	{
		if ( def.PlacementKind == ThornsPlayerBuildPlacementKind.Free || def.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
			return true;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var hostDef ) )
				continue;

			// Ownership gate before geometry — avoids "valid snap" that authorizes onto enemies.
			if ( !ThornsBuildingOwnership.HostAccountMayUseAsPlacementSupport( placed.OwnerAccountKey, placerAccountKey ) )
				continue;

			var d = (placed.GameObject.WorldPosition - position).Length;
			if ( hostDef.SnapKind == ThornsPlayerBuildSnapKind.Foundation )
			{
				if ( def.SnapKind is ThornsPlayerBuildSnapKind.Wall or ThornsPlayerBuildSnapKind.Window or ThornsPlayerBuildSnapKind.DoorFrame
				     && d < ThornsBuildingModule.Cell * 0.82f )
					return true;

				if ( def.SnapKind == ThornsPlayerBuildSnapKind.Ramp && d < ThornsBuildingModule.Cell * 0.35f )
					return true;
			}

			if ( def.SnapKind == ThornsPlayerBuildSnapKind.Foundation && IsWallLike( hostDef.SnapKind )
			     && d < ThornsBuildingModule.Cell * 0.82f )
				return true;
		}

		return false;
	}

	void UpdateGhost( ThornsPlacementPreview preview )
	{
		if ( preview is null || preview.Definition is null )
		{
			DestroyGhost();
			return;
		}

		if ( !preview.Valid && preview.Position.LengthSquared < 1f )
		{
			DestroyGhost( resetPreview: false );
			return;
		}

		EnsureGhost( preview.StructureId );
		if ( !_ghost.IsValid() )
			return;

		_ghost.WorldPosition = preview.Position;
		_ghost.WorldRotation = preview.Rotation;
		var tint = preview.Valid ? new Color( 0.35f, 0.86f, 0.45f, 0.42f ) : new Color( 0.95f, 0.28f, 0.22f, 0.52f );
		if ( string.Equals( preview.StructureId, "c4_charge", StringComparison.OrdinalIgnoreCase ) )
		{
			tint = preview.Valid
				? new Color( 1f, 0.42f, 0.08f, 0.58f )
				: new Color( 0.95f, 0.12f, 0.08f, 0.62f );
		}
		foreach ( var renderer in _ghost.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Tint = tint;
		}
	}

	void UpdateModifyPreview()
	{
		CurrentPreview = new ThornsPlacementPreview();
		ModifyTargetValid = TryFindStructureAlongLook( out var target );
		FocusedStructure = target;

		if ( !ModifyTargetValid || !target.IsValid() )
		{
			ModifyStatus = SelectedToolKind == ThornsPlayerBuildToolKind.Remove
				? "Aim at a structure or placeable to remove"
				: "Aim at a structure to upgrade";
			DestroyGhost( resetPreview: false );
			return;
		}

		if ( !ThornsPlayerBuildingDefinitions.TryGet( target.StructureId, out var def ) )
		{
			ModifyStatus = "Unknown structure";
			ModifyTargetValid = false;
			DestroyGhost( resetPreview: false );
			return;
		}

		if ( SelectedToolKind == ThornsPlayerBuildToolKind.Upgrade )
		{
			if ( !ThornsPlayerBuildingDefinitions.SupportsUpgrade( def ) )
			{
				ModifyStatus = "Cannot upgrade this";
				ModifyTargetValid = false;
			}
			else if ( target.MaterialTier >= ThornsPlacedBuildStructure.MaxMaterialTier )
			{
				ModifyStatus = "Already fully upgraded";
				ModifyTargetValid = false;
			}
			else
			{
				var costs = ThornsPlayerBuildingDefinitions.UpgradeCosts( def, target.MaterialTier );
				ModifyStatus = $"Left click upgrade: {FormatCosts( costs )}";
			}
		}
		else
		{
			ModifyStatus = $"Left click remove {def.DisplayName}";
		}

		UpdateModifyGhost( target, SelectedToolKind == ThornsPlayerBuildToolKind.Remove
			? new Color( 0.95f, 0.25f, 0.18f, 0.52f )
			: new Color( 0.95f, 0.74f, 0.2f, 0.48f ) );
	}

	static string FormatCosts( IReadOnlyList<(string ItemId, int Count)> costs )
	{
		if ( costs is null || costs.Count == 0 )
			return "no cost";

		return string.Join( ", ", costs.Select( c => $"{c.Count} {c.ItemId}" ) );
	}

	void UpdateModifyGhost( ThornsPlacedBuildStructure target, Color tint )
	{
		if ( !target.IsValid() )
			return;

		var key = $"{SelectedToolKind}:{target.StructureId}:{target.MaterialTier}";
		if ( !_ghost.IsValid() || !string.Equals( _ghostStructureId, key, StringComparison.OrdinalIgnoreCase ) )
		{
			DestroyGhost( resetPreview: false );
			_ghostStructureId = key;
			_ghost = Scene.CreateObject( true );
			_ghost.Name = GhostName;
			_ghost.Tags.Add( "placement_ghost" );
			var tier = SelectedToolKind == ThornsPlayerBuildToolKind.Upgrade
				? Math.Clamp( target.MaterialTier + 1, 0, ThornsPlacedBuildStructure.MaxMaterialTier )
				: target.MaterialTier;
			ThornsPlacedBuildStructure.ApplyVisual( _ghost, target.StructureId, ghost: true, materialTier: tier );
		}

		_ghost.WorldPosition = target.GameObject.WorldPosition;
		_ghost.WorldRotation = target.GameObject.WorldRotation;
		foreach ( var renderer in _ghost.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Tint = tint;
		}
	}

	bool TryFindStructureAlongLook( out ThornsPlacedBuildStructure target )
	{
		target = null;
		if ( !ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out var origin, out var forward, useScreenCenter: true ) )
			return false;

		var rayDir = forward.Normal;
		var end = origin + rayDir * ThornsPlayerBuildingDefinitions.MaxPlacementDistance;
		var trace = Scene.Trace.Ray( origin, end )
			.IgnoreGameObjectHierarchy( GameObject );

		if ( _ghost.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _ghost );

		var hit = trace.Run();
		TryPickStructureAlongRay( origin, rayDir, out var registryTarget );

		if ( hit.Hit && TryGetPlayerStructureFromTrace( hit, out var traceTarget ) )
		{
			if ( !registryTarget.IsValid() )
			{
				target = traceTarget;
				return true;
			}

			var traceAlong = Vector3.Dot( hit.HitPosition - origin, rayDir );
			var registryAlong = Vector3.Dot( registryTarget.GameObject.WorldPosition - origin, rayDir );
			target = traceAlong <= registryAlong + 16f ? traceTarget : registryTarget;
			return true;
		}

		target = registryTarget;
		return target.IsValid();
	}

	bool TryPickStructureAlongRay( Vector3 origin, Vector3 rayDir, out ThornsPlacedBuildStructure target )
	{
		target = null;
		var bestAlong = float.MaxValue;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() )
				continue;

			if ( !ThornsPlayerBuildingDefinitions.TryGet( placed.StructureId, out var def ) )
				continue;

			var to = placed.GameObject.WorldPosition - origin;
			var along = Vector3.Dot( to, rayDir );
			if ( along < 0f || along > ThornsPlayerBuildingDefinitions.MaxPlacementDistance )
				continue;

			var closest = origin + rayDir * along;
			var dist = (placed.GameObject.WorldPosition - closest).Length;
			var pickRadius = MathF.Max( 56f, def.FootprintRadius + 28f );
			if ( dist > pickRadius || along >= bestAlong )
				continue;

			bestAlong = along;
			target = placed;
		}

		return target.IsValid();
	}

	void ClearModifyPreview( bool destroyGhost = true )
	{
		FocusedStructure = null;
		ModifyStatus = "";
		ModifyTargetValid = false;
		if ( destroyGhost )
			DestroyGhost( resetPreview: false );
	}

	void EnsureGhost( string structureId )
	{
		if ( _ghost.IsValid() && string.Equals( _ghostStructureId, structureId, StringComparison.OrdinalIgnoreCase ) )
			return;

		DestroyGhost();
		_ghostStructureId = structureId;
		_ghost = Scene.CreateObject( true );
		_ghost.Name = GhostName;
		_ghost.Tags.Add( "placement_ghost" );
		ThornsPlacedBuildStructure.ApplyVisual( _ghost, structureId, ghost: true );
	}

	void DestroyGhost( bool resetPreview = true )
	{
		if ( _ghost.IsValid() )
			_ghost.Destroy();
		_ghost = null;
		_ghostStructureId = "";
		if ( resetPreview )
			CurrentPreview = new ThornsPlacementPreview();
	}

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocalConnectionOwner( this );
}

[Title( "Thorns Placed Build Structure" )]
[Category( "Terrain/Buildings" )]
public sealed class ThornsPlacedBuildStructure : Component
{
	public const int MaxMaterialTier = 2;

	[SkipHotload]
	public static readonly HashSet<ThornsPlacedBuildStructure> Registry = new();

	[Sync( SyncFlags.FromHost )] public string StructureId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string OwnerAccountKey { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string InstanceKey { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public int MaterialTier { get; set; }
	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; }
	[Sync( SyncFlags.FromHost )] public float MaxHealth { get; set; }

	protected override void OnStart()
	{
		Registry.Add( this );
		HostEnsureHealthInitialized();
		ApplyCurrentVisual();
		if ( string.Equals( StructureId, "wood_doorframe", StringComparison.OrdinalIgnoreCase )
		     && !Components.Get<ThornsPlayerDoor>().IsValid() )
			HostEnsureDoorComponent();
	}

	protected override void OnEnabled() => Registry.Add( this );

	public void HostEnsureStorageComponent()
	{
		if ( !ThornsPlacedStructureStorage.IsStorageStructure( StructureId ) )
			return;

		HostSyncWorldContainer( ThornsPlacedStructureStorage.EnsureOn( this ) );
	}

	public void HostEnsureDoorComponent( bool doorOpen = false )
	{
		if ( !string.Equals( StructureId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
			return;

		ThornsPlayerDoor.HostEnsureOnDoorframe( this, doorOpen );
	}

	internal void HostSyncWorldContainer( ThornsPlacedStructureStorage storage = null )
	{
		if ( !ThornsPlacedStructureStorage.IsStorageStructure( StructureId )
		     || string.IsNullOrWhiteSpace( InstanceKey ) )
			return;

		storage ??= Components.Get<ThornsPlacedStructureStorage>();
		if ( !storage.IsValid() )
			return;

		Terraingen.World.ThornsWorldLootContainerService.Instance?.HostRegisterStructureStorage( InstanceKey, storage );
	}

	protected override void OnDestroy()
	{
		Registry.Remove( this );
		if ( !string.IsNullOrWhiteSpace( InstanceKey ) )
			Terraingen.World.ThornsWorldLootContainerService.Instance?.HostUnregister(
				Terraingen.World.ThornsWorldLootContainerService.StructureKey( InstanceKey ) );
	}

	public static ThornsPlacedBuildStructure SpawnHost(
		Scene scene,
		Connection owner,
		string structureId,
		Vector3 position,
		Rotation rotation,
		string instanceKey = null )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid )
			return null;

		var go = scene.CreateObject( true );
		go.Name = $"Thorns Player Structure {structureId}";
		go.WorldPosition = position;
		go.WorldRotation = rotation;
		go.Tags.Add( "thorns_structure" );
		go.NetworkMode = NetworkMode.Object;

		var placed = go.Components.Create<ThornsPlacedBuildStructure>();
		placed.StructureId = structureId;
		// AUDIT FIX: never leave OwnerAccountKey blank when we have any identity — blank used to be "public".
		placed.OwnerAccountKey = ThornsBuildingOwnership.ResolveOwnerAccountKey( owner, gameplayFallback: null );
		if ( string.IsNullOrWhiteSpace( placed.OwnerAccountKey ) && owner is null && Networking.IsActive )
			Log.Warning( $"[Thorns Building] Placed '{structureId}' with blank OwnerAccountKey while networked — structure will not be publicly editable." );
		placed.InstanceKey = string.IsNullOrWhiteSpace( instanceKey )
			? Guid.NewGuid().ToString( "N" )
			: instanceKey.Trim();
		Registry.Add( placed );
		placed.ApplyCurrentVisual();
		placed.HostEnsureStorageComponent();
		placed.HostEnsureHealthInitialized();
		placed.HostEnsureDoorComponent();

		if ( Networking.IsActive )
		{
			var opts = new NetworkSpawnOptions
			{
				Owner = Connection.Host,
				OrphanedMode = NetworkOrphaned.Host
			};
			go.NetworkSpawn( opts );
		}

		return placed;
	}

	public static bool TryFindByInstanceKey( string instanceKey, out ThornsPlacedBuildStructure placed )
	{
		placed = null;
		if ( string.IsNullOrWhiteSpace( instanceKey ) )
			return false;

		foreach ( var candidate in Registry )
		{
			if ( candidate.IsValid() && string.Equals( candidate.InstanceKey, instanceKey, StringComparison.OrdinalIgnoreCase ) )
			{
				placed = candidate;
				return true;
			}
		}

		return false;
	}

	public void ApplyCurrentVisual()
	{
		ApplyVisual( GameObject, StructureId, ghost: false, MaterialTier );
		if ( string.Equals( StructureId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
			Components.Get<ThornsPlayerDoor>()?.RefreshPanelTier( MaterialTier );
	}

	public void HostEnsureHealthInitialized( float? savedHealth = null )
	{
		if ( !ThornsStructureHealthRules.HasHealth( StructureId ) )
		{
			CurrentHealth = MaxHealth = 0f;
			return;
		}

		MaxHealth = ThornsStructureHealthRules.ResolveMaxHealth( StructureId, MaterialTier );
		if ( savedHealth.HasValue && savedHealth.Value > 0f )
			CurrentHealth = MathF.Min( savedHealth.Value, MaxHealth );
		else if ( CurrentHealth <= 0f || CurrentHealth > MaxHealth )
			CurrentHealth = MaxHealth;
	}

	public void HostRefreshHealthAfterUpgrade()
	{
		MaxHealth = ThornsStructureHealthRules.ResolveMaxHealth( StructureId, MaterialTier );
		CurrentHealth = MaxHealth;
	}

	public void HostTakeStructureDamage( float amount, GameObject attackerRoot )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0f )
			return;

		if ( !ThornsStructureHealthRules.HasHealth( StructureId ) )
			return;

		if ( MaxHealth <= 0f )
			HostEnsureHealthInitialized();

		CurrentHealth = MathF.Max( 0f, CurrentHealth - amount );
		if ( CurrentHealth > 0.01f )
			return;

		GameObject.Destroy();
		Terraingen.Multiplayer.ThornsWorldPersistence.RequestSave();
	}

	/// <summary>Host-only: apply tier material locally and push visual to remote clients.</summary>
	public void HostBroadcastMaterialTier()
	{
		ApplyCurrentVisual();
		if ( Networking.IsActive && ThornsMultiplayer.IsHostOrOffline )
			RpcApplyMaterialTier( MaterialTier );
	}

	[Rpc.Broadcast]
	void RpcApplyMaterialTier( int materialTier )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		MaterialTier = Math.Clamp( materialTier, 0, MaxMaterialTier );
		ApplyCurrentVisual();
	}

	public static void ApplyVisual( GameObject go, string structureId, bool ghost, int materialTier = 0 )
	{
		if ( !go.IsValid() || !ThornsPlayerBuildingDefinitions.TryGet( structureId, out var def ) )
			return;

		ClearRenderers( go );

		var material = BuildMaterial( def, materialTier );
		var tint = ghost ? new Color( 0.35f, 0.86f, 0.45f, 0.42f ) : Color.White;

		switch ( def.SnapKind )
		{
			case ThornsPlayerBuildSnapKind.Foundation:
				SpawnBox( go, "foundation", ThornsBuildingModule.BoxBottomAlignedLocalCenter( def.Size ), def.Size, material, tint, !ghost );
				break;
			case ThornsPlayerBuildSnapKind.Wall:
				SpawnSolidWall( go, material, tint, !ghost, materialTier );
				break;
			case ThornsPlayerBuildSnapKind.Window:
				SpawnWindowWall( go, material, tint, !ghost, materialTier );
				break;
			case ThornsPlayerBuildSnapKind.DoorFrame:
				SpawnDoorFrame( go, material, tint, !ghost, materialTier );
				break;
			case ThornsPlayerBuildSnapKind.Ramp:
				SpawnRamp( go, material, tint, !ghost );
				break;
			default:
				if ( string.Equals( structureId, "c4_charge", StringComparison.OrdinalIgnoreCase ) )
					ThornsExplosiveCharge.ApplyVisual( go );
				else
					SpawnPortable( go, structureId, def, tint, !ghost );
				break;
		}
	}

	static void ClearRenderers( GameObject go )
	{
		foreach ( var child in go.Children.ToArray() )
		{
			if ( child.IsValid() && child.Name == "ThornsDoorHinge" )
				continue;

			child.Destroy();
		}

		foreach ( var renderer in go.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf ) )
			renderer.Destroy();

		foreach ( var collider in go.Components.GetAll<BoxCollider>( FindMode.EverythingInSelf ) )
			collider.Destroy();

		foreach ( var collider in go.Components.GetAll<ModelCollider>( FindMode.EverythingInSelf ) )
			collider.Destroy();
	}

	static Model _devBoxModel;

	static Model DevBoxModel
	{
		get
		{
			if ( _devBoxModel is null || !_devBoxModel.IsValid )
				_devBoxModel = Model.Load( "models/dev/box.vmdl" );
			return _devBoxModel;
		}
	}

	static Material BuildMaterial( ThornsBuildDefinition def, int materialTier = 0 )
	{
		var name = def.SnapKind == ThornsPlayerBuildSnapKind.Portable
			? "wood"
			: Math.Clamp( materialTier, 0, MaxMaterialTier ) switch
			{
				1 => "stone_brick",
				2 => "sheet_metal",
				_ => "barn_wood"
			};
		var mat = Material.Load( $"materials/building_materials/{name}.vmat" );
		return mat is not null && mat.IsValid() ? mat : Material.Load( "materials/default/default.vmat" );
	}

	static string FacadeMaterialSlug( int materialTier ) =>
		Math.Clamp( materialTier, 0, MaxMaterialTier ) switch
		{
			1 => "stone_brick",
			2 => "sheet_metal",
			_ => "barn_wood"
		};

	static void SpawnSolidWall( GameObject parent, Material material, Color tint, bool solid, int materialTier ) =>
		SpawnFacadeWallMesh(
			parent,
			"solid_wall",
			ThornsBuildingWallMesh.GetSolidWall( FacadeMaterialSlug( materialTier ), ThornsBuildingModule.Cell ),
			material,
			tint,
			solid );

	static void SpawnWindowWall( GameObject parent, Material material, Color tint, bool solid, int materialTier ) =>
		SpawnFacadeWallMesh(
			parent,
			"window",
			ThornsBuildingWallMesh.GetWindowWall( FacadeMaterialSlug( materialTier ), ThornsBuildingModule.Cell ),
			material,
			tint,
			solid );

	static void SpawnDoorFrame( GameObject parent, Material material, Color tint, bool solid, int materialTier ) =>
		SpawnFacadeWallMesh(
			parent,
			"doorframe",
			ThornsBuildingWallMesh.GetDoorFrameWall( FacadeMaterialSlug( materialTier ), ThornsBuildingModule.Cell ),
			material,
			tint,
			solid );

	static void SpawnFacadeWallMesh(
		GameObject parent,
		string name,
		Model model,
		Material material,
		Color tint,
		bool solid )
	{
		if ( !model.IsValid() || model.IsError )
		{
			SpawnBox(
				parent,
				name,
				Vector3.Zero,
				new Vector3(
					ThornsBuildingModule.WallThickness,
					ThornsBuildingModule.Cell,
					ThornsBuildingModule.WallHeight ),
				material,
				tint,
				solid );
			return;
		}

		var visual = parent.Scene.CreateObject( true );
		visual.Name = name;
		visual.Parent = parent;
		visual.LocalPosition = Vector3.Zero;
		visual.LocalRotation = Rotation.Identity;
		visual.LocalScale = Vector3.One;

		var renderer = visual.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.MaterialOverride = material;
		renderer.Tint = tint;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		if ( !solid )
			return;

		visual.Tags.Add( "thorns_structure" );
		TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( visual, model );
	}

	static void SpawnRamp( GameObject parent, Material material, Color tint, bool solid )
	{
		var model = ThornsBuildingRampMesh.GetOrCreate( "wood" );
		if ( !model.IsValid || model.IsError )
		{
			var fallback = SpawnBox(
				parent,
				"ramp",
				Vector3.Zero,
				new Vector3(
					ThornsProcBuildingRampGeometry.RampRunWorld,
					ThornsProcBuildingRampGeometry.RampSpanYWorld,
					ThornsProcBuildingRampGeometry.RampRiseWorld ),
				material,
				tint,
				solid );
			fallback.LocalRotation = Rotation.FromYaw( 90f );
			fallback.LocalPosition = new Vector3( 0f, 0f, ThornsProcBuildingRampGeometry.RampSeatPivotLocalZFromFoundationCenter() );
			return;
		}

		var visual = parent.Scene.CreateObject( true );
		visual.Name = "ramp";
		visual.Parent = parent;
		visual.LocalPosition = Vector3.Zero;
		visual.LocalRotation = Rotation.FromYaw( 90f );
		visual.LocalScale = Vector3.One;

		var renderer = visual.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.MaterialOverride = material;
		renderer.Tint = tint;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		if ( solid )
		{
			visual.Tags.Add( "thorns_structure" );
			TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( visual, model );
		}
	}

	static void SpawnPortable( GameObject parent, string structureId, ThornsBuildDefinition def, Color tint, bool solid )
	{
		if ( ThornsPlaceableFurnitureCatalog.UsesCatalogPresentation( structureId ) )
		{
			SpawnCatalogPortable( parent, structureId, tint, solid );
			return;
		}

		var model = ThornsPlaceableModels.LoadStructureModel( structureId );
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
		{
			SpawnBox( parent, structureId, ThornsBuildingModule.BoxBottomAlignedLocalCenter( def.Size ), def.Size, BuildMaterial( def ), tint, solid );
			return;
		}

		var scale = ScaleModelToSize( model, def.Size );
		var bounds = ResolveBounds( model );
		var center = (bounds.Mins + bounds.Maxs) * 0.5f;
		var visual = parent.Scene.CreateObject( true );
		visual.Name = structureId;
		visual.Parent = parent;
		visual.LocalPosition = new Vector3(
			-center.x * scale.x,
			-center.y * scale.y,
			-bounds.Mins.z * scale.z );
		visual.LocalScale = scale;

		var renderer = visual.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Tint = tint;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		if ( solid )
		{
			visual.Tags.Add( "thorns_structure" );
			TerraingenAnchoredPhysics.EnsureSolidTags( visual );
			var collider = visual.Components.GetOrCreate<BoxCollider>();
			collider.Center = bounds.Center;
			collider.Scale = bounds.Size;
			collider.Static = true;
			collider.Enabled = true;
		}
	}

	static void SpawnCatalogPortable( GameObject parent, string structureId, Color tint, bool solid )
	{
		var worldSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( structureId );
		var model = ThornsPlaceableFurniturePresentation.LoadModel( structureId );
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
		{
			var effectiveSize = ThornsPlaceableFurnitureScale.EffectiveWorldSizeInches( worldSize, structureId );
			ThornsPlayerBuildingDefinitions.TryGet( structureId, out var fallbackDef );
			SpawnBox(
				parent,
				structureId,
				ThornsBuildingModule.BoxBottomAlignedLocalCenter( effectiveSize ),
				effectiveSize,
				BuildMaterial( fallbackDef ),
				tint,
				solid );
			return;
		}

		ThornsPlaceableFurniturePresentation.Apply( parent, structureId );

		foreach ( var renderer in parent.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf ) )
			renderer.Tint = tint;

		var collider = parent.Components.Get<BoxCollider>();
		if ( collider.IsValid() )
		{
			collider.Enabled = solid;
			collider.Static = solid;
		}

		if ( !solid )
			return;

		parent.Tags.Add( "thorns_structure" );
		TerraingenAnchoredPhysics.EnsureSolidTags( parent );
	}

	static GameObject SpawnBox( GameObject parent, string name, Vector3 localPos, Vector3 worldSize, Material material, Color tint, bool solid )
	{
		var child = parent.Scene.CreateObject( true );
		child.Name = name;
		child.Parent = parent;
		child.LocalPosition = localPos;
		child.LocalScale = ThornsBuildingModule.ScaleBoxToWorldAxes( worldSize.x, worldSize.y, worldSize.z );

		var renderer = child.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.MaterialOverride = material;
		renderer.Tint = tint;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		if ( solid )
		{
			TerraingenAnchoredPhysics.EnsureSolidTags( child );
			child.Tags.Add( "thorns_structure" );
			TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( child, DevBoxModel );
		}

		return child;
	}

	static Vector3 ScaleModelToSize( Model model, Vector3 worldSize )
	{
		var b = ResolveBounds( model );
		return new Vector3(
			worldSize.x / Math.Max( 1f, b.Size.x ),
			worldSize.y / Math.Max( 1f, b.Size.y ),
			worldSize.z / Math.Max( 1f, b.Size.z ) );
	}

	static BBox ResolveBounds( Model model )
	{
		if ( model.IsValid && model.Bounds.Size.LengthSquared > 1e-8f )
			return model.Bounds;

		return new BBox( new Vector3( -25f, -25f, -25f ), new Vector3( 25f, 25f, 25f ) );
	}
}
