namespace Terraingen.UI.Screens;



using Sandbox.UI;

using Terraingen.GameData;

using Terraingen.Player;

using Terraingen.UI;

using Terraingen.UI.Components;

using Terraingen.UI.Core;



public sealed partial class ThornsInventoryScreen : ThornsScreenBase

{

	readonly List<ThornsItemSlot> _inventorySlots = new();

	readonly List<ThornsItemSlot> _hotbarSlots = new();

	readonly List<ThornsItemSlot> _allInspectableSlots = new();

	ThornsItemSlot _headSlot;

	ThornsItemSlot _chestSlot;

	ThornsItemSlot _legsSlot;

	Panel _craftPanel;

	Panel _statsPanel;

	Panel _categoryRow;

	Panel _recipeList;

	Panel _recipeDetail;

	Panel _recipeIngredients;

	Panel _inspectPanel;

	Panel _inspectIconWrap;

	Panel _inspectIcon;

	Panel _inspectStats;

	Panel _inspectMeta;

	Panel _inspectActions;

	Label _inspectTitle;

	Label _inspectTier;

	Label _recipeTitle;

	Label _recipeDetailDesc;

	Label _recipeDetailMeta;

	Panel _recipeDetailIcon;

	Panel _craftQueue;

	Panel _craftBody;

	Label _craftHeaderLabel;

	string _craftSearchQuery = "";

	TextEntry _craftSearchEntry;

	bool _craftExpanded = true;

	string _lastCraftUiKey = "";
	string _lastRecipeListKey = "";
	bool _recipeListReady;
	bool _recipeListRebuildPending;

	ThornsContainerKind? _inspectContainer;

	int _inspectIndex = -1;

	string _inspectItemId = "";

	Panel _armorSlotsCol;
	Panel _inventoryFilterRow;
	string _inventoryFilterKey = "all";
	string _lastSkillsUiKey = "";



	public ThornsInventoryScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }



	protected override void Build()

	{

		AddClass( "inventory-screen inventory-screen-concept" );

		Style.Position = PositionMode.Relative;
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.PaddingBottom = Length.Pixels( 0 );
		Style.BackgroundColor = Color.Transparent;

		_contentRow = ThornsUiFactory.AddPanel( this, "inventory-content-row" );
		_contentRow.Style.FlexDirection = FlexDirection.Row;
		_contentRow.Style.FlexGrow = 1;
		_contentRow.Style.FlexShrink = 1;
		_contentRow.Style.MinHeight = Length.Pixels( 0 );
		_contentRow.Style.Width = Length.Percent( 100 );

		BuildArmorColumn();
		ThornsTheme.CreateWoodColumnDivider( _contentRow );
		BuildInventoryCenterColumn();
		ThornsTheme.CreateWoodColumnDivider( _contentRow );
		BuildCraftColumn();

		ApplyFixedConceptLayout();
	}

	public override void OnShown()
	{
		base.OnShown();
		ThornsDefinitionRegistry.EnsureInitialized();
		EnsurePlayerCraftListShowsAll();
		RebuildInspect();
	}

	void EnsurePlayerCraftListShowsAll()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var inv = ThornsUiClientState.Snapshot.Inventory;
		var atHandStation = ThornsUiClientState.Snapshot.Craft?.NearestStation == ThornsCraftStationKind.Hand;
		if ( !atHandStation )
			return;

		if ( string.Equals( inv.ActiveCraftCategoryId, ThornsMenuSnapshotHelpers.AllCraftCategoryId,
			     StringComparison.OrdinalIgnoreCase ) )
			return;

		ThornsPlayerGameplay.Local?.SetCraftUiState(
			inv.CraftPanelExpanded,
			ThornsMenuSnapshotHelpers.AllCraftCategoryId,
			null );
	}

	public override void Tick()
	{
		base.Tick();

		if ( _recipeListRebuildPending )
		{
			_recipeListRebuildPending = false;
			_recipeListReady = true;
			RebuildRecipeList();
		}

		if ( _appliedConceptSlotPx > 0 )
			return;

		EnsureConceptLayoutSizing();
	}



	ThornsItemSlot CreateSlot( Panel parent, ThornsContainerKind container, int index, bool isHotbar = false )

	{

		var slot = new ThornsItemSlot( parent, container, index, Rebuild, isHotbar, OnSlotSelected, menuLayout: true );

		_allInspectableSlots.Add( slot );

		return slot;

	}

	ThornsItemSlot CreateArmorSlot( Panel parent, ThornsContainerKind container )
	{
		var slot = CreateSlot( parent, container, 0 );
		slot.Style.FlexShrink = 0;
		return slot;
	}

	void OnSlotSelected( ThornsContainerKind container, int index, string itemId )

	{

		if ( string.IsNullOrEmpty( itemId ) )

		{

			_inspectContainer = null;

			_inspectIndex = -1;

			_inspectItemId = "";

		}

		else

		{

			_inspectContainer = container;

			_inspectIndex = index;

			_inspectItemId = itemId;

		}



		UpdateSlotHighlights();

		RebuildInspect();

	}



	void UpdateSlotHighlights()

	{

		var activeHotbar = ThornsUiClientState.HasSnapshot
			? Math.Clamp( ThornsUiClientState.Snapshot.Inventory.ActiveHotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 )
			: 0;

		foreach ( var slot in _allInspectableSlots )
		{
			if ( slot.Container == ThornsContainerKind.Hotbar )
			{
				slot.SetSelected( slot.Index == activeHotbar );
				continue;
			}

			var selected = _inspectContainer == slot.Container
				&& _inspectIndex == slot.Index
				&& !string.IsNullOrEmpty( _inspectItemId );

			slot.SetSelected( selected );
		}

	}



	protected override void OnRevision( UiRevisionChannel channel, int revision )

	{

		_ = revision;

		if ( !ThornsUiClientState.HasSnapshot )

			return;



		switch ( channel )

		{

			case UiRevisionChannel.Vitals:

				RebuildStats();

				break;

			case UiRevisionChannel.Skills:

				RebuildSkillsPanelsIfChanged();
				RebuildRecipeList();
				UpdateCraftFooterButton();

				break;

			case UiRevisionChannel.Craft:

				_craftExpanded = ThornsUiClientState.Snapshot.Inventory.CraftPanelExpanded;

				_craftPanel?.SetClass( "thorns-craft-collapsed", !_craftExpanded );

				RebuildCraftPanelsIfChanged();

				RebuildCraftQueue();

				break;

			case UiRevisionChannel.Inventory:

				_lastRecipeListKey = "";
				Rebuild();

				break;

			case UiRevisionChannel.Hotbar:

				UpdateSlotHighlights();

				break;

		}

	}



	public override void OnShown( bool firstShow )
	{
		if ( firstShow )
		{
			Rebuild();
			return;
		}

		if ( !ThornsUiClientState.HasSnapshot )
			return;

		RefreshAllSlots();
		RebuildStats();
		UpdateSlotHighlights();
		RebuildCraftQueue();
	}



	public override void Rebuild()

	{

		ThornsWeaponAttachmentPicker.Close();

		if ( !ThornsUiClientState.HasSnapshot )

			return;



		_craftExpanded = ThornsUiClientState.Snapshot.Inventory.CraftPanelExpanded;

		_craftPanel?.SetClass( "thorns-craft-collapsed", !_craftExpanded );



		RefreshAllSlots();



		RefreshExplorerPortrait();

		RebuildSkillsPanelsIfChanged();

		RebuildStats();

		RebuildInspect();

		UpdateSlotHighlights();

		ApplyInventoryFilter();

		RebuildCraftPanelsIfChanged();

		RebuildCraftQueue();

	}



	void RefreshAllSlots()

	{

		foreach ( var s in _inventorySlots )

			s.Refresh();

		foreach ( var s in _hotbarSlots )

			s.Refresh();

		_headSlot?.Refresh();

		_chestSlot?.Refresh();

		_legsSlot?.Refresh();

	}



	string BuildCraftUiKey()

	{

		var inv = ThornsUiClientState.Snapshot.Inventory;

		return $"{inv.ActiveCraftCategoryId}|{inv.SelectedRecipeId}|{inv.CraftPanelExpanded}";

	}

	string BuildRecipeListKey()
	{
		var inv = ThornsUiClientState.Snapshot.Inventory;
		return $"{inv.ActiveCraftCategoryId}|{_craftSearchQuery}";
	}



	void RebuildCraftPanelsIfChanged()

	{

		var key = BuildCraftUiKey();

		var keyChanged = !string.Equals( key, _lastCraftUiKey, StringComparison.Ordinal );

		if ( keyChanged )

		{

			_lastCraftUiKey = key;

			RebuildCraftCategories();

		}

		if ( !_recipeListReady )
		{
			_recipeListRebuildPending = true;
			return;
		}

		var recipeKey = BuildRecipeListKey();
		if ( !string.Equals( recipeKey, _lastRecipeListKey, StringComparison.Ordinal ) )
		{
			_lastRecipeListKey = recipeKey;
			RebuildRecipeList();
		}

	}



	void RebuildInspect()

	{

		if ( _inspectPanel is null || !_inspectPanel.IsValid )

			return;



		_inspectStats?.DeleteChildren( true );
		_inspectMeta?.DeleteChildren( true );
		_inspectActions?.DeleteChildren( true );
		_inspectWeaponStats?.DeleteChildren( true );
		_inspectWeaponMeta?.DeleteChildren( true );
		_inspectWeaponLeftMeta?.DeleteChildren( true );
		_inspectWeaponActions?.DeleteChildren( true );



		if ( string.IsNullOrEmpty( _inspectItemId ) )

		{

			SetInspectLayout( weaponLayout: false );
			_inspectTitle.Text = "SELECT AN ITEM";
			_inspectTitle.Style.FontColor = ThornsTheme.Accent;

			_inspectIcon.Style.BackgroundImage = null;

			ThornsInventoryInspectUi.PopulateEmpty( _inspectStats, _inspectMeta, _inspectTier,
				"Click a slot in your inventory or quickbar to inspect it." );
			ClearWeaponAttachmentSlots();

			return;

		}



		var def = ThornsDefinitionRegistry.GetItem( _inspectItemId );

		if ( def is null )

		{

			SetInspectLayout( weaponLayout: false );
			_inspectTitle.Text = _inspectItemId.ToUpper();
			_inspectTitle.Style.FontColor = ThornsTheme.Accent;

			ThornsInventoryInspectUi.PopulateEmpty( _inspectStats, _inspectMeta, _inspectTier, "Unknown item." );
			ClearWeaponAttachmentSlots();

			return;

		}



		var inspectDto = FindInspectSlotDto();
		var stack = FindInspectStack();
		var isWeapon = def.Category == ThornsItemCategory.Weapon && inspectDto is not null;
		SetInspectLayout( isWeapon );

		if ( isWeapon )
		{
			var weaponTier = inspectDto.ItemTier > 0 ? inspectDto.ItemTier : inspectDto.WeaponTier;
			_inspectWeaponTitle.Style.FontColor = Terraingen.Combat.ThornsWeaponTierVisuals.TitleTint( weaponTier );
			_inspectWeaponTitle.Text = def.DisplayName.ToUpper();
			_inspectWeaponDesc.Text = def.Description ?? "";

			var modelPath = !string.IsNullOrWhiteSpace( def.WorldModelAsset )
				? def.WorldModelAsset
				: def.ViewModelAsset;
			if ( !string.IsNullOrWhiteSpace( modelPath ) )
				_inspectWeaponPreview.SetSpeciesModel( modelPath, CraftItemIconPath( def.Id ), caption: null );
			else
				_inspectWeaponPreview.SetPortrait( CraftItemIconPath( def.Id ), def.DisplayName );

			ThornsInventoryInspectUi.Populate(
				_inspectWeaponStats,
				_inspectWeaponLeftMeta,
				_inspectWeaponTier,
				def,
				inspectDto,
				stack,
				useWeaponBarLayout: true );

			BindWeaponAttachmentSlots();
			RebuildWeaponInspectActions();
			return;
		}

		_inspectTitle.Style.FontColor = ThornsTheme.Accent;
		if ( inspectDto is not null && ThornsItemTier.SupportsTiering( def ) )
		{
			var tier = inspectDto.ItemTier > 0 ? inspectDto.ItemTier : inspectDto.WeaponTier;
			if ( tier > 0 )
				_inspectTitle.Style.FontColor = Terraingen.Combat.ThornsWeaponTierVisuals.TitleTint( tier );
		}

		_inspectTitle.Text = def.DisplayName.ToUpper();

		ThornsIconCache.ApplyToPanel( _inspectIcon, CraftItemIconPath( def.Id ) );

		ThornsInventoryInspectUi.Populate( _inspectStats, _inspectMeta, _inspectTier, def, inspectDto, stack );

		ClearWeaponAttachmentSlots();
		RebuildInspectActions();

	}

	void SetInspectLayout( bool weaponLayout )
	{
		_inspectCompactLayout?.SetClass( "hidden", weaponLayout );
		_inspectWeaponLayout?.SetClass( "hidden", !weaponLayout );
		if ( _inspectCompactLayout is not null && _inspectCompactLayout.IsValid )
		{
			_inspectCompactLayout.Style.Display = weaponLayout ? DisplayMode.None : DisplayMode.Flex;
			_inspectCompactLayout.Style.ZIndex = weaponLayout ? 0 : 1;
		}
		if ( _inspectWeaponLayout is not null && _inspectWeaponLayout.IsValid )
		{
			_inspectWeaponLayout.Style.Display = weaponLayout ? DisplayMode.Flex : DisplayMode.None;
			_inspectWeaponLayout.Style.ZIndex = weaponLayout ? 1 : 0;
		}
	}

	void BindWeaponAttachmentSlots()
	{
		if ( _inspectContainer is null || _weaponAttachmentSlots.Count == 0 )
		{
			ThornsInventoryInspectContext.ClearWeaponInspect();
			return;
		}

		ThornsInventoryInspectContext.SyncWeaponInspect( _inspectContainer, _inspectIndex );
		ThornsAttachmentDragDebug.Write(
			$"BindWeaponAttachmentSlots weapon={_inspectContainer}[{_inspectIndex}] slots={_weaponAttachmentSlots.Count}" );

		foreach ( var slot in _weaponAttachmentSlots )
		{
			slot.BindWeapon( _inspectContainer.Value, _inspectIndex );
			slot.Refresh();
		}
	}

	void ClearWeaponAttachmentSlots()
	{
		ThornsInventoryInspectContext.ClearWeaponInspect();

		foreach ( var slot in _weaponAttachmentSlots )
			slot.ClearWeapon();
	}

	void RebuildWeaponInspectActions()
	{
		if ( _inspectWeaponActions is null || !_inspectWeaponActions.IsValid )
			return;

		_inspectWeaponActions.DeleteChildren( true );

		if ( _inspectContainer is null || string.IsNullOrEmpty( _inspectItemId ) )
			return;

		if ( _inspectContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.WorldLoot )
			return;

		var stack = FindInspectStack();
		if ( stack.IsEmpty )
			return;

		ThornsUiFactory.AddClickable( _inspectWeaponActions, "thorns-btn-primary inspect-drop-btn", "DROP", () =>
			ThornsPlayerGameplay.Local?.RequestDropFromSlot( _inspectContainer.Value, _inspectIndex, 1 ) );
		TryAddUpgradeButton( _inspectWeaponActions );
	}

	void TryAddUpgradeButton( Panel actionsRoot )
	{
		if ( actionsRoot is null || !actionsRoot.IsValid || _inspectContainer is null || string.IsNullOrEmpty( _inspectItemId ) )
			return;

		if ( ThornsUiClientState.Snapshot.Workbench?.IsOpen != true )
			return;

		var def = ThornsDefinitionRegistry.GetItem( _inspectItemId );
		var stack = FindInspectStack();
		if ( def is null || stack.IsEmpty || !Terraingen.GameData.ThornsItemTier.CanUpgrade( stack, def ) )
			return;

		var nextTier = Terraingen.GameData.ThornsItemTier.NextTier( stack, def );
		var tierName = Terraingen.Combat.ThornsWeaponTierVisuals.TierName( nextTier );
		var btn = ThornsUiFactory.AddClickable( actionsRoot, "thorns-btn-secondary inspect-upgrade-btn", $"UPGRADE → {tierName.ToUpperInvariant()}", () =>
			ThornsPlayerGameplay.Local?.RequestUpgradeItem( new Terraingen.GameData.ThornsUpgradeItemRequest
			{
				Container = _inspectContainer.Value,
				Index = _inspectIndex
			} ) );
		btn.Style.MarginRight = Length.Pixels( 8 );
	}

	void RebuildInspectActions()
	{
		if ( _inspectActions is null || !_inspectActions.IsValid )
			return;

		_inspectActions.DeleteChildren( true );

		if ( _inspectContainer is null || string.IsNullOrEmpty( _inspectItemId ) )
			return;

		if ( _inspectContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.WorldLoot )
			return;

		var stack = FindInspectStack();
		if ( stack.IsEmpty )
			return;

		var kind = _inspectContainer.Value;
		var index = _inspectIndex;

		if ( ThornsSurvivalConsumables.IsConsumable( _inspectItemId ) )
		{
			var useBtn = ThornsUiFactory.AddClickable( _inspectActions, "thorns-btn-primary inspect-use-btn", "USE", () =>
				ThornsPlayerGameplay.Local?.RequestConsumeFromSlot( kind, index ) );
			useBtn.Style.MarginRight = Length.Pixels( 8 );
		}

		ThornsUiFactory.AddClickable( _inspectActions, "thorns-btn-primary inspect-drop-btn", "DROP", () =>
			ThornsPlayerGameplay.Local?.RequestDropFromSlot( kind, index, 1 ) );
		TryAddUpgradeButton( _inspectActions );
	}



	ThornsInventorySlotDto FindInspectSlotDto()
	{
		if ( _inspectContainer is null || !ThornsUiClientState.HasSnapshot )
			return null;

		return ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s =>
			s.Container == _inspectContainer
			&& s.Index == (_inspectContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs ? 0 : _inspectIndex) );
	}

	ThornsItemStack FindInspectStack()

	{

		if ( _inspectContainer is null || !ThornsUiClientState.HasSnapshot )

			return ThornsItemStack.EmptyStack;



		var match = ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s =>

			s.Container == _inspectContainer && s.Index == (_inspectContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs ? 0 : _inspectIndex) );



		if ( match is null || string.IsNullOrEmpty( match.ItemId ) )

			return ThornsItemStack.EmptyStack;



		return new ThornsItemStack
		{
			ItemId = match.ItemId,
			Count = match.Count,
			HasDurability = match.HasDurability,
			Durability = match.Durability,
			WeaponLoadedAmmo = match.WeaponLoadedAmmo,
			ItemTier = match.ItemTier > 0 ? match.ItemTier : match.WeaponTier,
			StatRoll = match.StatRoll,
			AttachmentId0 = match.WeaponAttachmentIds?.Count > 0 ? match.WeaponAttachmentIds[0] : "",
			AttachmentId1 = match.WeaponAttachmentIds?.Count > 1 ? match.WeaponAttachmentIds[1] : "",
			AttachmentId2 = match.WeaponAttachmentIds?.Count > 2 ? match.WeaponAttachmentIds[2] : ""
		};

	}



	void RebuildStats()
	{
		RebuildArmorAttributes();
	}



	void RefreshExplorerPortrait()
	{
		// Character preview removed from concept armor column layout.
	}



	void RebuildCraftCategories()

	{

		if ( _categoryRow is null || !_categoryRow.IsValid )

			return;



		_categoryRow.DeleteChildren( true );

		var active = ThornsMenuSnapshotHelpers.NormalizeCraftCategoryId(
			ThornsUiClientState.Snapshot.Inventory.ActiveCraftCategoryId );

		foreach ( var cat in ThornsMenuSnapshotHelpers.GetCraftCategories() )

		{

			var captured = cat;

			var btn = ThornsUiFactory.AddClickable( _categoryRow, "craft-cat-btn",
				() => ThornsPlayerGameplay.Local?.SetCraftUiState( _craftExpanded, captured, null ) );

			btn.SetClass( "active", string.Equals( captured, active, StringComparison.OrdinalIgnoreCase ) );
			btn.Tooltip = BuildCraftCategoryTooltip( captured );
			btn.Style.FlexGrow = 1f;
			btn.Style.FlexShrink = 1f;
			btn.Style.FlexBasis = Length.Pixels( 0 );
			btn.Style.MinWidth = Length.Pixels( 0 );
			btn.Style.MaxHeight = Length.Pixels( ThornsUiMetrics.MenuCraftCatIcon + 8 );
			btn.Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuCraftCatIcon + 8 );
			btn.Style.FlexDirection = FlexDirection.Row;
			btn.Style.AlignItems = Align.Center;
			btn.Style.JustifyContent = Justify.Center;

			var icon = ThornsUiFactory.AddPanel( btn, "craft-cat-icon slot-icon" );
			icon.Style.Width = Length.Pixels( ThornsUiMetrics.MenuCraftCatIcon );
			icon.Style.Height = Length.Pixels( ThornsUiMetrics.MenuCraftCatIcon );
			icon.Style.MinWidth = Length.Pixels( ThornsUiMetrics.MenuCraftCatIcon );
			icon.Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuCraftCatIcon );
			icon.Style.FlexShrink = 0;
			var iconKey = string.Equals( captured, ThornsMenuSnapshotHelpers.AllCraftCategoryId, StringComparison.OrdinalIgnoreCase )
				? "inventory"
				: captured;
			ThornsIconCache.ApplyToPanel( icon, ThornsIconRegistry.CraftCategoryIcon( iconKey ) );

		}

	}

	static string BuildCraftCategoryTooltip( string categoryId )
	{
		var count = ThornsMenuSnapshotHelpers.CountRecipesForCategory( categoryId );
		if ( string.Equals( categoryId, ThornsMenuSnapshotHelpers.AllCraftCategoryId, StringComparison.OrdinalIgnoreCase ) )
			return $"All recipes ({count})";

		return $"{TitleCaseCategory( categoryId )} ({count})";
	}

	static string TitleCaseCategory( string categoryId )
	{
		if ( string.IsNullOrWhiteSpace( categoryId ) )
			return "All";

		return char.ToUpperInvariant( categoryId[0] ) + categoryId[1..];
	}



	void RebuildRecipeList()

	{

		if ( _recipeList is null || !_recipeList.IsValid )

			return;

		ThornsDefinitionRegistry.EnsureInitialized();

		_recipeList.DeleteChildren( true );

		var category = ResolveRecipeListCategory();

		var search = _craftSearchQuery?.Trim() ?? "";

		var selectedId = ThornsUiClientState.Snapshot.Inventory.SelectedRecipeId;
		var recipeCount = 0;
		var totalRecipes = ThornsMenuSnapshotHelpers.CountRecipesForCategory( ThornsMenuSnapshotHelpers.AllCraftCategoryId );

		foreach ( var recipe in ThornsMenuSnapshotHelpers.EnumerateRecipesForCategory( category )

			         .Where( r => string.IsNullOrWhiteSpace( search )
			                      || r.DisplayName.Contains( search, StringComparison.OrdinalIgnoreCase )
			                      || r.OutputItemId.Contains( search, StringComparison.OrdinalIgnoreCase ) ) )

		{
			recipeCount++;
			var captured = recipe;

			var meetsTier = ThornsMenuSnapshotHelpers.MeetsRecipeCraftTier( captured );
			var hasStation = ThornsMenuSnapshotHelpers.HasCraftStation( captured );
			var hasIngredients = ThornsMenuSnapshotHelpers.HasRecipeIngredients( captured );
			var canCraft = meetsTier && hasStation && hasIngredients;

			var card = ThornsUiFactory.AddPanel( _recipeList, "recipe-card" );

			card.SetClass( "selected", captured.Id == selectedId );

			card.SetClass( "craftable", canCraft );
			card.SetClass( "locked", !meetsTier );
			card.SetClass( "blocked-station", meetsTier && !hasStation );
			card.SetClass( "blocked-mats", meetsTier && hasStation && !hasIngredients );

			card.Style.FlexDirection = FlexDirection.Row;

			card.Style.AlignItems = Align.Center;
			card.Style.FlexShrink = 0;
			card.Style.Width = Length.Percent( 100 );
			card.Style.Position = PositionMode.Relative;
			card.Style.MarginBottom = Length.Pixels( 4 );

			var selectArea = ThornsUiFactory.AddClickable( card, "recipe-card-select", () =>

				ThornsPlayerGameplay.Local?.SetCraftUiState( _craftExpanded, category, captured.Id ) );

			selectArea.Style.FlexDirection = FlexDirection.Row;

			selectArea.Style.AlignItems = Align.Center;

			selectArea.Style.FlexGrow = 1;

			selectArea.Style.MinWidth = Length.Pixels( 0 );

			var icon = ThornsUiFactory.AddPanel( selectArea, "recipe-card-icon slot-icon" );

			icon.Style.PointerEvents = PointerEvents.None;

			icon.Style.FlexShrink = 0;

			ThornsIconCache.ApplyToPanel( icon, CraftItemIconPath( captured.OutputItemId ) );

			var body = ThornsUiFactory.AddPanel( selectArea, "recipe-card-body" );

			body.Style.FlexDirection = FlexDirection.Column;

			body.Style.FlexGrow = 1;

			body.Style.MinWidth = Length.Pixels( 0 );

			body.Style.PointerEvents = PointerEvents.None;

			ThornsUiFactory.AddPassiveLabel( body, captured.DisplayName, "recipe-card-title" );

			var reqLabel = ThornsUiFactory.AddPassiveLabel( body,
				ThornsCraftProgression.FormatRequiredTier( captured.RequiredCraftTier ),
				"recipe-card-tier-req" );
			reqLabel.SetClass( "met", meetsTier );
			reqLabel.SetClass( "locked", !meetsTier );

			if ( !canCraft )
			{
				var status = ThornsUiFactory.AddPassiveLabel( body,
					ThornsMenuSnapshotHelpers.DescribeCraftBlock( captured ),
					"recipe-card-status" );
				status.SetClass( "locked", !meetsTier );
				status.SetClass( "blocked-station", meetsTier && !hasStation );
				status.SetClass( "blocked-mats", meetsTier && hasStation && !hasIngredients );
			}

			var mats = ThornsUiFactory.AddPanel( body, "recipe-card-mats" );

			mats.Style.FlexDirection = FlexDirection.Row;

			mats.Style.FlexWrap = Wrap.Wrap;

			foreach ( var ing in captured.Ingredients )

			{

				var owned = ThornsMenuSnapshotHelpers.CountItem( ing.ItemId );

				var ok = owned >= ing.Count;

				var chip = ThornsUiFactory.AddPanel( mats, "recipe-card-mat" );

				chip.SetClass( "met", ok );

				chip.SetClass( "short", !ok );

				var ingIcon = ThornsUiFactory.AddPanel( chip, "recipe-card-mat-icon slot-icon" );

				ingIcon.Style.PointerEvents = PointerEvents.None;

				ThornsIconCache.ApplyToPanel( ingIcon, CraftItemIconPath( ing.ItemId ) );

				ThornsUiFactory.AddPassiveLabel( chip, $"{owned}/{ing.Count}", "recipe-card-mat-qty" );

			}

			var craftBtn = ThornsUiFactory.AddClickable( card, "thorns-btn-primary recipe-card-craft-btn", "CRAFT",

				() => RequestCraftRecipe( captured.Id ) );

			craftBtn.SetClass( "disabled", !canCraft );

			craftBtn.Style.FlexShrink = 0;

		}

		if ( recipeCount == 0 )
		{
			var emptyText = string.IsNullOrWhiteSpace( search )
				? "No recipes in this category."
				: $"No recipes match \"{search}\".";
			ThornsTheme.CreateMuted( _recipeList, emptyText );
		}

		UpdateCraftHeaderLabel( recipeCount, totalRecipes, category );
		UpdateCraftFooterButton();
	}

	static string ResolveRecipeListCategory()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return ThornsMenuSnapshotHelpers.AllCraftCategoryId;

		return ThornsMenuSnapshotHelpers.NormalizeCraftCategoryId(
			ThornsUiClientState.Snapshot.Inventory.ActiveCraftCategoryId );
	}

	void UpdateCraftHeaderLabel( int visibleCount, int totalCount, string category )
	{
		if ( _craftHeaderLabel is null || !_craftHeaderLabel.IsValid )
			return;

		var suffix = string.Equals( category, ThornsMenuSnapshotHelpers.AllCraftCategoryId,
			StringComparison.OrdinalIgnoreCase )
			? $" ({visibleCount})"
			: $" ({visibleCount}/{totalCount})";
		_craftHeaderLabel.Text = $"CRAFTING{suffix}";
	}

	void UpdateCraftFooterButton()
	{
		if ( _craftFooterBtn is null || !_craftFooterBtn.IsValid )
			return;

		var recipeId = ThornsUiClientState.Snapshot?.Inventory?.SelectedRecipeId;
		var recipe = ThornsDefinitionRegistry.GetRecipe( recipeId );
		var canCraft = recipe is not null && ThornsMenuSnapshotHelpers.CanCraftRecipe( recipe );
		_craftFooterBtn.SetClass( "disabled", !canCraft );
		_craftFooterBtn.SetClass( "ready", canCraft );
	}



	void RebuildRecipeDetail()

	{

		if ( _recipeDetail is null || !_recipeDetail.IsValid )

			return;



		_recipeIngredients.DeleteChildren( true );



		var selected = ThornsDefinitionRegistry.GetRecipe( ThornsUiClientState.Snapshot.Inventory.SelectedRecipeId );

		if ( _recipeTitle.IsValid() )
			_recipeTitle.Text = selected?.DisplayName.ToUpper() ?? "SELECT RECIPE";

		if ( selected is null )
		{
			SetRecipeDetailDesc( "" );
			SetRecipeDetailMeta( "" );
			if ( _recipeDetailIcon is not null && _recipeDetailIcon.IsValid )
				_recipeDetailIcon.Style.BackgroundImage = null;
			ThornsTheme.CreateMuted( _recipeIngredients, "Choose a recipe from the list." );
			return;
		}

		var output = ThornsDefinitionRegistry.GetItem( selected.OutputItemId );

		if ( _recipeDetailIcon is not null && _recipeDetailIcon.IsValid )
		{
			if ( string.IsNullOrWhiteSpace( selected.OutputItemId ) )
				_recipeDetailIcon.Style.BackgroundImage = null;
			else
				ThornsIconCache.ApplyToPanel( _recipeDetailIcon, CraftItemIconPath( selected.OutputItemId ) );
		}

		var desc = selected.Description;
		if ( string.IsNullOrWhiteSpace( desc ) )
			desc = output?.Description ?? "";
		SetRecipeDetailDesc( desc );
		SetRecipeDetailMeta(
			$"{ThornsCraftProgression.FormatRequiredTier( selected.RequiredCraftTier )} · {selected.Station} · {selected.CraftSeconds:0}s" );

		ThornsTheme.CreateHeader( _recipeIngredients, "MATERIALS" );

		foreach ( var ing in selected.Ingredients )
		{
			var def = ThornsDefinitionRegistry.GetItem( ing.ItemId );
			var owned = ThornsMenuSnapshotHelpers.CountItem( ing.ItemId );
			var ok = owned >= ing.Count;
			var displayName = def?.DisplayName ?? ing.ItemId;

			var row = ThornsUiFactory.AddPanel( _recipeIngredients, "recipe-ingredient-row" );
			row.SetClass( "met", ok );
			row.SetClass( "short", !ok );

			var ingIcon = ThornsUiFactory.AddPanel( row, "ingredient-icon slot-icon" );
			ingIcon.Style.PointerEvents = PointerEvents.None;
			ThornsIconCache.ApplyToPanel( ingIcon, CraftItemIconPath( ing.ItemId ) );

			var label = $"{displayName} {owned}/{ing.Count}";
			ThornsUiFactory.AddPassiveLabel( row, label, "ingredient-line" );
		}



		var canCraft = ThornsMenuSnapshotHelpers.CanCraftRecipe( selected );

		ThornsTheme.CreateMuted( _recipeIngredients, ThornsMenuSnapshotHelpers.DescribeCraftBlock( selected ) );

	}

	static string CraftItemIconPath( string itemId ) =>
		string.IsNullOrWhiteSpace( itemId ) ? "" : ThornsIconManifest.ResolveItemPath( itemId );

	void SetRecipeDetailDesc( string text )
	{
		if ( _recipeDetailDesc is null || !_recipeDetailDesc.IsValid )
			return;

		var show = !string.IsNullOrWhiteSpace( text );
		_recipeDetailDesc.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
		_recipeDetailDesc.Text = text;
	}

	void SetRecipeDetailMeta( string text )
	{
		if ( _recipeDetailMeta is null || !_recipeDetailMeta.IsValid )
			return;

		var show = !string.IsNullOrWhiteSpace( text );
		_recipeDetailMeta.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
		_recipeDetailMeta.Text = text;
	}

	void RebuildCraftQueue()

	{

		if ( _craftQueue is null || !_craftQueue.IsValid )

			return;



		_craftQueue.DeleteChildren( true );

		ThornsUiFactory.AddLabel( _craftQueue, "CRAFTING QUEUE", "craft-queue-title thorns-header" );

		var queue = ThornsUiClientState.Snapshot.Craft.Queue;

		if ( queue.Count == 0 )
		{
			ThornsUiFactory.AddPassiveLabel( _craftQueue, "Queue empty.", "craft-queue-empty thorns-muted" );
			return;
		}

		var list = ThornsUiFactory.AddPanel( _craftQueue, "craft-queue-list" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.FlexGrow = 1;
		list.Style.MinHeight = Length.Pixels( 0 );
		list.Style.Overflow = OverflowMode.Scroll;

		for ( var i = 0; i < queue.Count; i++ )
		{
			var entry = queue[i];
			var recipe = ThornsDefinitionRegistry.GetRecipe( entry.RecipeId );
			var item = ThornsDefinitionRegistry.GetItem( entry.OutputItemId );
			var name = item?.DisplayName ?? entry.OutputItemId;
			var totalSeconds = Math.Max( 0.5f, recipe?.CraftSeconds ?? entry.SecondsRemaining );
			var progress = 1f - Math.Clamp( entry.SecondsRemaining / totalSeconds, 0f, 1f );
			var isActive = i == 0;

			var row = ThornsUiFactory.AddPanel( list, "craft-queue-row" );
			row.SetClass( "active", isActive );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.FlexShrink = 0;

			var icon = ThornsUiFactory.AddPanel( row, "craft-queue-icon slot-icon" );
			icon.Style.FlexShrink = 0;
			icon.Style.PointerEvents = PointerEvents.None;
			if ( !string.IsNullOrWhiteSpace( entry.OutputItemId ) )
				ThornsIconCache.ApplyToPanel( icon, CraftItemIconPath( entry.OutputItemId ) );

			var body = ThornsUiFactory.AddPanel( row, "craft-queue-row-body" );
			body.Style.FlexDirection = FlexDirection.Column;
			body.Style.FlexGrow = 1;
			body.Style.MinWidth = Length.Pixels( 0 );

			var titleRow = ThornsUiFactory.AddPanel( body, "craft-queue-row-title" );
			titleRow.Style.FlexDirection = FlexDirection.Row;
			titleRow.Style.AlignItems = Align.Center;
			titleRow.Style.JustifyContent = Justify.SpaceBetween;

			ThornsUiFactory.AddPassiveLabel( titleRow, name, "craft-queue-item-name" );
			ThornsUiFactory.AddPassiveLabel( titleRow, $"×{entry.QuantityRemaining}", "craft-queue-qty" );

			var meta = isActive ? "Crafting" : "Queued";
			ThornsUiFactory.AddPassiveLabel( body, $"{meta} · {entry.SecondsRemaining:0.0}s", "craft-queue-time thorns-muted" );

			var track = ThornsUiFactory.AddPanel( body, "craft-queue-progress thorns-progress" );
			var fill = ThornsUiFactory.AddPanel( track, "fill craft-queue-progress-fill" );
			fill.Style.Width = Length.Percent( progress * 100f );
		}
	}

}

