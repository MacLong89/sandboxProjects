#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Thorns game HUD chrome (player card, Rust-style inventory overlay, bottom toolbar dock). Built after gameplay logic in main partial.
/// </summary>
public sealed partial class ThornsDebugHudHost
{
	// Match ThornsGameShell.cs.scss — charcoal surfaces + teal accent (+ warm tint for currency / ammo readouts).
	static readonly Color HudSurfaceCard = new( 16f / 255f, 18f / 255f, 24f / 255f, 0.94f );
	static readonly Color HudSurfaceRaised = new( 22f / 255f, 26f / 255f, 34f / 255f, 0.96f );
	static readonly Color HudBorder = new( 44f / 255f, 54f / 255f, 68f / 255f, 0.92f );
	static readonly Color HudAccentPrimary = new( 82f / 255f, 201f / 255f, 217f / 255f, 1f );
	static readonly Color HudEconomyGold = new( 232f / 255f, 215f / 255f, 175f / 255f, 0.96f );
	static readonly Color HudSlotEmpty = new( 22f / 255f, 26f / 255f, 34f / 255f, 0.95f );
	static readonly Color HudSlotHover = new( 32f / 255f, 38f / 255f, 48f / 255f, 0.96f );
	static readonly Color HudSlotPending = new( 26f / 255f, 52f / 255f, 58f / 255f, 0.95f );
	static readonly Color HudSlotSelectedTb = new( 28f / 255f, 48f / 255f, 54f / 255f, 0.96f );

	const float ToolbarSlotPx = 56f;
	/// <summary>Reserve space for weapon strip + 8 slots + hint + dock margins so inventory never sits under the toolbar.</summary>
	const float InventoryReserveBottomPx = 248f;

	static float HudXpFillFraction( int totalXp, int characterLevel )
	{
		var start = ThornsVitals.CumulativeXpToEnterLevel( characterLevel );
		var next = ThornsVitals.CumulativeXpToEnterLevel( characterLevel + 1 );
		if ( next <= start )
			return 1f;
		return Math.Clamp( (totalXp - start) / (float)(next - start), 0f, 1f );
	}

	/// <summary>Caption + dark track + fill (same pattern as health bar).</summary>
	static void AddHudMeterBarRow( Panel card, string caption, string elementClass, float fill01, Color fillColor, int heightPx = 14, float marginTopPx = 0f )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(card,  elementClass + "-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginBottom = 4;
		if ( marginTopPx > 0f )
			row.Style.MarginTop = marginTopPx;

		var cap = row.AddChild( new Label( caption, elementClass + "-cap" ) );
		cap.Style.FontSize = 10;
		cap.Style.FontWeight = 600;
		cap.Style.FontColor = new Color( 0.72f, 0.7f, 0.65f );
		cap.Style.Width = 56;
		cap.Style.MinWidth = 56;

		var wrap = ThornsUiPanelAdd.AddChildPanel(row,  elementClass + "-wrap" );
		wrap.Style.FlexGrow = 1;
		wrap.Style.Height = heightPx;
		wrap.Style.BackgroundColor = new Color( 0.05f, 0.05f, 0.05f, 1f );
		wrap.Style.Overflow = OverflowMode.Hidden;

		var t = Math.Clamp( fill01, 0f, 1f );
		var fill = ThornsUiPanelAdd.AddChildPanel(wrap,  elementClass + "-fill" );
		fill.Style.Position = PositionMode.Absolute;
		fill.Style.Left = 0;
		fill.Style.Top = 0;
		fill.Style.Bottom = 0;
		fill.Style.Width = Length.Fraction( t );
		fill.Style.BackgroundColor = fillColor;
	}

	/// <summary>Center-screen + reticle: integer px strokes; stack anchored at 50%/50% with −½ margin so center sits on the viewport midpoint.</summary>
	void BuildCrosshair( Panel root, ThornsWeapon weapon )
	{
		var hasWeaponEquipped = weapon is not null && weapon.IsValid() && !string.IsNullOrWhiteSpace( weapon.ClientMirrorCombatDefinitionId );
		var combatIdCross = weapon.ClientMirrorCombatDefinitionId ?? "";
		var meleeEquipped = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( ThornsWeaponDefinitions.Get( combatIdCross ), combatIdCross );
		var fpAllowsAds = !weapon.IsValid() || weapon.ClientMirrorFpPresentationAllowsCombatLayers();
		var ads = hasWeaponEquipped && !meleeEquipped && fpAllowsAds && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		if ( ads )
			return;

		var bump = Math.Clamp( _crosshairBump, 1f, 1.35f );
		GetDebugHudCrosshairStrokePx( bump, out var thick, out var span, out var gap );
		GetCrosshairStackBoxPx( thick, span, gap, out var stackW, out var stackH );
		var ink = new Color( 0.95f, 0.95f, 0.95f, 0.92f );

		var layer = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-crosshair-layer" );
		layer.Style.Position = PositionMode.Absolute;
		layer.Style.Left = 0;
		layer.Style.Top = 0;
		layer.Style.Width = Length.Fraction( 1f );
		layer.Style.Height = Length.Fraction( 1f );
		layer.Style.PointerEvents = PointerEvents.None;

		var stack = ThornsUiPanelAdd.AddChildPanel(layer,  "thorns-crosshair" );
		stack.Style.Position = PositionMode.Absolute;
		stack.Style.Left = Length.Fraction( 0.5f );
		stack.Style.Top = Length.Fraction( 0.5f );
		stack.Style.MarginLeft = Length.Pixels( MathF.Floor( -stackW * 0.5f + 1e-4f ) );
		stack.Style.MarginTop = Length.Pixels( MathF.Floor( -stackH * 0.5f + 1e-4f ) );
		stack.Style.FlexDirection = FlexDirection.Column;
		stack.Style.AlignItems = Align.Center;

		var top = ThornsUiPanelAdd.AddChildPanel(stack,  "thorns-crosshair-arm" );
		top.Style.Width = thick;
		top.Style.Height = span;
		top.Style.BackgroundColor = ink;

		var mid = ThornsUiPanelAdd.AddChildPanel(stack,  "thorns-crosshair-mid" );
		mid.Style.FlexDirection = FlexDirection.Row;
		mid.Style.AlignItems = Align.Center;
		var left = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-crosshair-arm" );
		left.Style.Width = span;
		left.Style.Height = thick;
		left.Style.BackgroundColor = ink;
		var hole = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-crosshair-hole" );
		hole.Style.Width = gap;
		hole.Style.Height = gap;
		var right = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-crosshair-arm" );
		right.Style.Width = span;
		right.Style.Height = thick;
		right.Style.BackgroundColor = ink;

		var bottom = ThornsUiPanelAdd.AddChildPanel(stack,  "thorns-crosshair-arm" );
		bottom.Style.Width = thick;
		bottom.Style.Height = span;
		bottom.Style.BackgroundColor = ink;
	}

	void BuildPlayerHudTopLeft(
		Panel root,
		ThornsHealth health,
		ThornsArmorEquipment armor,
		ThornsVitals vitals,
		ThornsPlayerUpgrades upgrades,
		ThornsPlayerMilestones milestones,
		ThornsWallet wallet )
	{
		var top = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-player-card-v2" );
		top.Style.Position = PositionMode.Absolute;
		top.Style.Top = 14;
		top.Style.Left = 14;
		top.Style.FlexDirection = FlexDirection.Column;
		top.Style.PointerEvents = PointerEvents.None;

		if ( !health.IsValid() )
			return;

		var card = ThornsUiPanelAdd.AddChildPanel(top,  "player-card-inner" );
		card.Style.BackgroundColor = HudSurfaceCard;
		card.Style.BorderWidth = 1;
		card.Style.BorderColor = HudBorder;
		card.Style.Padding = 10;
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MinWidth = HealthBarWidth;

		var wrap = ThornsUiPanelAdd.AddChildPanel(card,  "health-bar-wrap-v2" );
		wrap.Style.Width = HealthBarWidth - 24;
		wrap.Style.Height = 18;
		wrap.Style.BackgroundColor = new Color( 0.05f, 0.05f, 0.05f, 1f );
		wrap.Style.Overflow = OverflowMode.Hidden;
		wrap.Style.MarginBottom = 6;

		var t = health.MaxHealth > 0.01f ? Math.Clamp( health.CurrentHealth / health.MaxHealth, 0f, 1f ) : 0f;
		var fill = ThornsUiPanelAdd.AddChildPanel(wrap,  "health-bar-fill-v2" );
		fill.Style.Position = PositionMode.Absolute;
		fill.Style.Left = 0;
		fill.Style.Top = 0;
		fill.Style.Bottom = 0;
		fill.Style.Width = Length.Fraction( t );
		fill.Style.BackgroundColor = health.IsAlive
			? new Color( 0.45f, 0.72f, 0.32f, 1f )
			: new Color( 0.75f, 0.22f, 0.2f, 1f );

		var hp = card.AddChild( new Label( $"{health.CurrentHealth:F0} / {health.MaxHealth:F0}", "hp-readout-v2" ) );
		hp.Style.FontSize = 13;
		hp.Style.FontWeight = 700;
		hp.Style.FontColor = new Color( 0.92f, 0.9f, 0.85f );

		if ( wallet.IsValid() )
		{
			var goldLbl = card.AddChild( new Label( $"Gold {wallet.Gold}", "gold-readout-v2" ) );
			goldLbl.Style.FontSize = 11;
			goldLbl.Style.FontWeight = 700;
			goldLbl.Style.FontColor = HudEconomyGold;
			goldLbl.Style.MarginBottom = 4;
		}

		if ( vitals.IsValid() )
		{
			var mountIx = health.Components.Get<ThornsWildlifeMountInteractor>();
			var riding = mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty;

			var foodHue = new Color( 0.88f, 0.52f, 0.18f, 1f );
			var waterHue = new Color( 0.28f, 0.62f, 0.95f, 1f );
			var staminaHue = new Color( 0.92f, 0.84f, 0.22f, 1f );
			var xpHue = new Color( 0.62f, 0.38f, 0.92f, 1f );

			var foodF = vitals.MaxHunger > 0.01f ? Math.Clamp( vitals.Hunger / vitals.MaxHunger, 0f, 1f ) : 0f;
			var waterF = vitals.MaxThirst > 0.01f ? Math.Clamp( vitals.Thirst / vitals.MaxThirst, 0f, 1f ) : 0f;
			var stamF = vitals.MaxStamina > 0.01f ? Math.Clamp( vitals.Stamina / vitals.MaxStamina, 0f, 1f ) : 0f;
			var xpF = HudXpFillFraction( vitals.TotalXp, vitals.CharacterLevel );

			AddHudMeterBarRow( card, "Food", "food-bar-v2", foodF, foodHue, 14, 6f );
			AddHudMeterBarRow( card, "Water", "water-bar-v2", waterF, waterHue );
			if ( !riding )
				AddHudMeterBarRow( card, "Stamina", "stamina-bar-v2", stamF, staminaHue );
			AddHudMeterBarRow( card, "XP", "xp-bar-v2", xpF, xpHue );

			var xpLeg = card.AddChild( new Label( $"Level {vitals.CharacterLevel}", "xp-legend-v2" ) );
			xpLeg.Style.FontSize = 10;
			xpLeg.Style.FontColor = new Color( 0.7f, 0.66f, 0.78f );
			xpLeg.Style.MarginBottom = 2;

			if ( upgrades.IsValid() )
			{
				var prog = card.AddChild( new Label(
					$"UP · {upgrades.UnspentUpgradePoints}    Craft T{upgrades.GetEffectiveCraftingTier()} · miner R{upgrades.MinerRank} lumber R{upgrades.LumberjackRank}",
					"upgrades-legend-v2" ) );
				prog.Style.FontSize = 9;
				prog.Style.FontColor = new Color( 0.55f, 0.52f, 0.72f );
				prog.Style.MarginBottom = 2;
			}

			if ( milestones.IsValid() )
			{
				var done = milestones.ClientCompletedGoalsCount();
				var n = ThornsMilestoneDefinitions.Count;
				var msTxt = n > 0
					? $"Journal goals: {done}/{n} complete"
					: "Journal goals: —";

				var msLeg = card.AddChild( new Label( msTxt, "milestone-legend-v2" ) );
				msLeg.Style.FontSize = 9;
				msLeg.Style.FontColor = new Color( 0.58f, 0.62f, 0.72f );
				msLeg.Style.MarginBottom = 2;
			}

			var moveLine = card.AddChild( new Label(
				riding
					? "Riding — tame sets your pace (Jump hops; crouch or Use on tame to dismount)"
					: $"{(vitals.ServerSprinting ? "SPRINT" : "walk")}  {(vitals.ServerCrouching ? "crouch" : "stand")}",
				"move-state-v2" ) );
			moveLine.Style.FontSize = 10;
			moveLine.Style.FontColor = new Color( 0.55f, 0.58f, 0.62f );
			moveLine.Style.MarginTop = 2;
		}

		if ( armor.IsValid() && !ShowFullInventory )
		{
			var dr = armor.GetClientUiTotalDrPercentCapped();
			var drLbl = card.AddChild( new Label( $"Armor DR (cap) {dr:F0}%", "armor-dr-v2" ) );
			drLbl.Style.FontSize = 11;
			drLbl.Style.FontColor = HudAccentPrimary;
			drLbl.Style.MarginTop = 4;
		}
	}

	void BuildInventoryOverlayRust( Panel root, ThornsInventory inv, ThornsHotbarEquipment hotbar, ThornsArmorEquipment armor, ThornsVitals vitals, ThornsPlayerUpgrades upgrades )
	{
		var dim = new InventoryOverlayRootPanel();
		dim.AddClass( "inv-overlay-v2" );
		dim.OnBackdropMouseUp = CancelInventoryDragFromBackdrop;
		root.AddChild( dim );

		_inventoryDim = dim;
		dim.Style.Position = PositionMode.Absolute;
		dim.Style.Left = 0;
		dim.Style.Top = 0;
		dim.Style.Width = Length.Fraction( 1f );
		dim.Style.Height = Length.Fraction( 1f );
		dim.Style.BackgroundColor = new Color( 5f / 255f, 7f / 255f, 10f / 255f, 0.72f );
		dim.Style.PointerEvents = PointerEvents.All;
		dim.Style.FlexDirection = FlexDirection.Column;
		dim.Style.JustifyContent = Justify.Center;
		dim.Style.AlignItems = Align.Center;
		dim.Style.PaddingTop = 44;
		dim.Style.PaddingBottom = InventoryReserveBottomPx;

		if ( ThornsInventoryDragState.FromInventorySlot.HasValue && inv.IsValid()
		     && inv.TryGetClientMirrorSlot( ThornsInventoryDragState.FromInventorySlot.Value, out var dragNet )
		     && dragNet.Quantity > 0 )
		{
			var ghost = new DragIconPanel( dim, armorStyle: false );
			dim.AddChild( ghost );
			ghost.SetGlyph( ItemIcon( dragNet.ItemId ), "inv-drag-ghost-text-v2" );
			ThornsInventoryDragState.SetDragIcon( ghost );
			ghost.UpdateFollowMouse();
		}
		else if ( ThornsInventoryDragState.FromArmorSlot.HasValue && armor.IsValid() )
		{
			armor.GetClientMirrorEquippedPiece( ThornsInventoryDragState.FromArmorSlot.Value, out var armId, out _ );
			if ( !string.IsNullOrWhiteSpace( armId ) )
			{
				var ghost = new DragIconPanel( dim, armorStyle: true );
				dim.AddChild( ghost );
				ghost.SetGlyph( ItemIcon( armId ), "armor-drag-ghost-text-v2" );
				ThornsInventoryDragState.SetDragIcon( ghost );
				ghost.UpdateFollowMouse();
			}
		}

		var shell = ThornsUiPanelAdd.AddChildPanel(dim,  "inv-shell-v2" );
		shell.Style.FlexDirection = FlexDirection.Column;
		shell.Style.BackgroundColor = HudSurfaceCard;
		shell.Style.BorderWidth = 2;
		shell.Style.BorderColor = HudBorder;
		shell.Style.Padding = 18;
		shell.Style.MaxWidth = 1520;
		shell.Style.Width = Length.Fraction( 0.97f );
		shell.Style.MinHeight = Length.Fraction( 0.42f );
		shell.Style.MaxHeight = Length.Fraction( 0.76f );
		shell.Style.PointerEvents = PointerEvents.All;

		shell.AddEventListener( "onmouseup", e =>
		{
			if ( !ThornsInventoryDragState.IsDragging )
				return;

			if ( e.Target != shell )
				return;

			CancelInventoryDragFromBackdrop();
		} );

		var titleRow = ThornsUiPanelAdd.AddChildPanel(shell,  "inv-title-row-v2" );
		titleRow.Style.FlexDirection = FlexDirection.Row;
		titleRow.Style.JustifyContent = Justify.SpaceBetween;
		titleRow.Style.AlignItems = Align.Center;
		titleRow.Style.Width = Length.Fraction( 1f );
		titleRow.Style.MarginBottom = 6;

		var title = titleRow.AddChild( new Label( "INVENTORY", "inv-title-v2" ) );
		title.Style.FontSize = 18;
		title.Style.FontWeight = 900;
		title.Style.LetterSpacing = 8;
		title.Style.FontColor = new Color( 0.88f, 0.84f, 0.76f );

		var close = ThornsUiPanelAdd.AddClickableLabel(titleRow,  "Close", () =>
		{
			ShowFullInventory = false;
			ClearPendingMoveSlot();
			RequestHudRebuild();
		} );
		close.Style.PointerEvents = PointerEvents.All;
		close.Style.Padding = 6;

		var help = shell.AddChild( new Label(
			"Release on another slot to move · release on same slot to inspect · Shift-click inv piece → equip armor · Shift-click armor → unequip · Drag armor onto backpack slots. Craft is server-validated.",
			"inv-help-v2" ) );
		help.Style.FontSize = 12;
		help.Style.FontColor = new Color( 0.52f, 0.5f, 0.46f );
		help.Style.MarginBottom = 12;

		var body = ThornsUiPanelAdd.AddChildPanel(shell,  "inv-body-row-v2" );
		body.Style.FlexDirection = FlexDirection.Row;
		body.Style.Width = Length.Fraction( 1f );
		body.Style.AlignItems = Align.Stretch;

		if ( armor.IsValid() )
			BuildRustArmorColumnV2( body, armor );

		var midRow = ThornsUiPanelAdd.AddChildPanel(body,  "inv-craft-mid-v2" );
		midRow.Style.FlexDirection = FlexDirection.Row;
		midRow.Style.Width = Length.Fraction( 1f );
		midRow.Style.FlexGrow = 1;
		midRow.Style.AlignItems = Align.Stretch;

		var invColumn = ThornsUiPanelAdd.AddChildPanel(midRow,  "inv-main-v2" );
		invColumn.Style.FlexDirection = FlexDirection.Column;
		invColumn.Style.FlexGrow = 1;
		invColumn.Style.MinWidth = 420;

		BuildBackpackGridV2( invColumn, inv, armor );

		var craftCol = ThornsUiPanelAdd.AddChildPanel(midRow,  "craft-panel-wrap-v2" );
		craftCol.Style.FlexDirection = FlexDirection.Column;
		craftCol.Style.Width = Length.Pixels( 340 );
		craftCol.Style.MinWidth = 300;
		craftCol.Style.MarginLeft = 12;

		BuildCraftingPanelV2( craftCol, inv, vitals, upgrades );
	}

	static readonly Color HudCraftOkBorder = new( 0.32f, 0.52f, 0.3f, 0.95f );
	static readonly Color HudCraftNoBorder = new( 0.52f, 0.28f, 0.26f, 0.95f );
	static readonly Color HudCraftLineOk = new( 0.62f, 0.78f, 0.55f, 1f );
	static readonly Color HudCraftLineMiss = new( 0.92f, 0.55f, 0.45f, 1f );

	void BuildCraftingPanelV2( Panel craftRoot, ThornsInventory inv, ThornsVitals vitals, ThornsPlayerUpgrades upgrades )
	{
		if ( !inv.IsValid() )
			return;

		var head = craftRoot.AddChild( new Label( "CRAFTING", "craft-head-v2" ) );
		head.Style.FontSize = 14;
		head.Style.FontWeight = 900;
		head.Style.LetterSpacing = 4;
		head.Style.FontColor = new Color( 0.88f, 0.82f, 0.72f, 1f );
		head.Style.MarginBottom = 6;

		var tier = upgrades.IsValid() ? upgrades.GetEffectiveCraftingTier() : (vitals.IsValid() ? vitals.CharacterLevel : 1);
		var tierLbl = craftRoot.AddChild( new Label( $"Tier gate (craft upgrade): T{tier}", "craft-tier-v2" ) );
		tierLbl.Style.FontSize = 11;
		tierLbl.Style.FontColor = new Color( 0.5f, 0.48f, 0.45f, 1f );
		tierLbl.Style.MarginBottom = 10;

		var scroll = ThornsUiPanelAdd.AddChildPanel(craftRoot,  "craft-scroll-v2" );
		scroll.Style.FlexDirection = FlexDirection.Column;
		scroll.Style.MaxHeight = Length.Pixels( 420 );
		scroll.Style.Overflow = OverflowMode.Scroll;
		scroll.Style.PointerEvents = PointerEvents.All;

		foreach ( var recipe in ThornsCraftingRecipes.All )
		{
			var title = ThornsItemRegistry.ResolveDisplayName( recipe.OutputItemId );

			var materialsOk = true;
			foreach ( var ing in recipe.Ingredients )
			{
				var have = inv.ClientMirrorCountItemId( ing.ItemId );
				if ( have < ing.Quantity )
					materialsOk = false;
			}

			var tierOk = tier >= recipe.RequiredCraftingTier;
			var canCraft = materialsOk && tierOk;

			var row = ThornsUiPanelAdd.AddChildPanel(scroll,  $"craft-recipe-{recipe.Id}-v2" );
			row.Style.FlexDirection = FlexDirection.Column;
			row.Style.MarginBottom = 8;
			row.Style.Padding = 8;
			row.Style.BorderWidth = 1;
			row.Style.BorderColor = canCraft ? HudCraftOkBorder : HudCraftNoBorder;
			row.Style.BackgroundColor = new Color( 0.09f, 0.08f, 0.07f, 0.92f );

			var titleLbl = row.AddChild( new Label( $"{title} ×{recipe.OutputQuantity}", "craft-title-v2" ) );
			titleLbl.Style.FontSize = 12;
			titleLbl.Style.FontWeight = 800;
			titleLbl.Style.FontColor = new Color( 0.92f, 0.9f, 0.85f, 1f );

			foreach ( var ing in recipe.Ingredients )
			{
				var have = inv.ClientMirrorCountItemId( ing.ItemId );
				var okLine = have >= ing.Quantity;
				var shortName = ThornsItemRegistry.ResolveDisplayName( ing.ItemId );
				var ingLbl = row.AddChild( new Label( $"{shortName}: {have} / {ing.Quantity}", "craft-ing-v2" ) );
				ingLbl.Style.FontSize = 11;
				ingLbl.Style.FontColor = okLine ? HudCraftLineOk : HudCraftLineMiss;
				ingLbl.Style.MarginTop = 2;
			}

			if ( !tierOk )
			{
				var tWarn = row.AddChild( new Label( $"Requires tier ≥ {recipe.RequiredCraftingTier}", "craft-tier-warn-v2" ) );
				tWarn.Style.FontSize = 10;
				tWarn.Style.FontColor = HudCraftLineMiss;
				tWarn.Style.MarginTop = 4;
			}

			var rid = recipe.Id;
			var craftBtn = ThornsUiPanelAdd.AddClickableLabel(row,  canCraft ? "Craft" : "Can't craft", () =>
			{
				if ( !canCraft )
					return;
				Log.Info( $"[Thorns][UI] Craft button recipe={rid}" );
				inv.RequestCraftRecipe( rid );
				RequestHudRebuild();
			} );
			craftBtn.Style.MarginTop = 6;
			craftBtn.Style.Padding = 6;
			craftBtn.Style.PointerEvents = PointerEvents.All;
		}
	}

	void BuildRustArmorColumnV2( Panel shell, ThornsArmorEquipment armor )
	{
		var col = ThornsUiPanelAdd.AddChildPanel(shell,  "armor-col-v2" );
		col.Style.FlexDirection = FlexDirection.Column;
		col.Style.MarginRight = 14;
		col.Style.MinWidth = 198;
		col.Style.MaxWidth = 220;
		col.Style.JustifyContent = Justify.SpaceBetween;

		var head = col.AddChild( new Label( "ARMOR", "armor-head-v2" ) );
		head.Style.FontSize = 13;
		head.Style.FontWeight = 900;
		head.Style.LetterSpacing = 5;
		head.Style.FontColor = new Color( 0.88f, 0.84f, 0.76f );
		head.Style.MarginBottom = 10;

		var slotsWrap = ThornsUiPanelAdd.AddChildPanel(col,  "armor-slots-wrap-v2" );
		slotsWrap.Style.FlexDirection = FlexDirection.Column;
		slotsWrap.Style.JustifyContent = Justify.Center;

		var titles = new[] { "Head", "Body", "Legs" };
		for ( var i = 0; i < 3; i++ )
		{
			armor.GetClientMirrorEquippedPiece( i, out var id, out var dur );

			var slot = ThornsUiPanelAdd.AddChildPanel(slotsWrap,  $"armor-slot-v2-{i}" );
			slot.Style.Width = 88;
			slot.Style.Height = 88;
			slot.Style.MarginBottom = i < 2 ? 12 : 0;
			slot.Style.BackgroundColor = HudSlotEmpty;
			slot.Style.BorderWidth = 1;
			slot.Style.BorderColor = HudBorder;
			slot.Style.FlexDirection = FlexDirection.Column;
			slot.Style.JustifyContent = Justify.Center;
			slot.Style.AlignItems = Align.Center;
			slot.Style.PointerEvents = PointerEvents.All;
			var armorSlotIdx = i;
			slot.AddEventListener( "onmousedown", () =>
			{
				if ( !ShowFullInventory )
					return;

				if ( Input.Keyboard.Down( "Shift" ) )
				{
					armor.GetClientMirrorEquippedPiece( armorSlotIdx, out var aid, out _ );
					if ( !string.IsNullOrWhiteSpace( aid ) && armor.IsValid() )
						armor.RequestUnequipArmor( armorSlotIdx );
					RequestHudRebuild();
					return;
				}

				BeginDragArmorSlot( armorSlotIdx, armor );
			} );

			slot.AddEventListener( "onmouseup", () => HandleArmorSlotMouseUp( armorSlotIdx, armor ) );

			var cap = slot.AddChild( new Label( titles[i], "armor-cap-v2" ) );
			cap.Style.FontSize = 10;
			cap.Style.FontColor = new Color( 0.42f, 0.4f, 0.38f );
			cap.Style.MarginBottom = 2;

			var iconL = slot.AddChild( new Label(
				string.IsNullOrWhiteSpace( id ) ? "—" : ItemIcon( id ),
				"armor-icon-v2" ) );
			iconL.Style.FontSize = 24;
			iconL.Style.FontColor = Color.White;
			iconL.Style.TextAlign = TextAlign.Center;

			var durL = slot.AddChild( new Label(
				string.IsNullOrWhiteSpace( id ) || dur <= 0 ? "" : $"{dur:F0}",
				"armor-dur-v2" ) );
			durL.Style.FontSize = 9;
			durL.Style.FontColor = new Color( 0.6f, 0.58f, 0.55f );
		}

		var dr = armor.GetClientUiTotalDrPercentCapped();
		var fx = col.AddChild( new Label( $"TOTAL DR (CAP)\n{dr:F0}%", "armor-fx-v2" ) );
		fx.Style.FontSize = 13;
		fx.Style.FontWeight = 700;
		fx.Style.FontColor = HudAccentPrimary;
		fx.Style.MarginTop = 10;
	}

	void BuildBackpackGridV2( Panel invColumn, ThornsInventory inv, ThornsArmorEquipment armor )
	{
		var grid = ThornsUiPanelAdd.AddChildPanel(invColumn,  "backpack-grid-v2" );
		grid.Style.FlexDirection = FlexDirection.Column;
		grid.Style.Width = Length.Fraction( 1f );
		grid.Style.PointerEvents = PointerEvents.None;

		const int columns = 6;
		var cellW = Length.Fraction( 1f / columns );
		var count = ThornsInventory.BackpackSlotCount;
		var start = ThornsInventory.HotbarSlotCount;

		for ( var rowStart = 0; rowStart < count; rowStart += columns )
		{
			var row = ThornsUiPanelAdd.AddChildPanel(grid,  "backpack-row-v2" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.Width = Length.Fraction( 1f );
			row.Style.PointerEvents = PointerEvents.None;
			row.Style.MarginBottom = 5;

			for ( var c = 0; c < columns; c++ )
			{
				var n = rowStart + c;
				if ( n >= count )
					break;

				var idx = start + n;
				ThornsInventorySlotNet net = default;
				_ = inv.TryGetClientMirrorSlot( idx, out net );

				var draggingSource = ThornsInventoryDragState.FromInventorySlot == idx;
				var hovered = _hoverSlot == idx;
				var dropTarget = ThornsInventoryDragState.IsDragging && hovered;
				var inspectSel = _overlayInspectSlot == idx;
				InventorySlotPanel cell = null;
				cell = row.AddChild( new InventorySlotPanel
				{
					SlotIndex = idx,
					MouseDownSlot = _ => HandleInventoryGridMouseDown( idx, inv, armor, cell ),
					MouseUpSlot = _ => HandleInventorySlotMouseUp( idx, inv, armor ),
					MouseRightClickSlot = useIdx =>
					{
						Log.Info( $"[Thorns][UI] Right-click use slot {useIdx}" );
						if ( inv.IsValid() )
							inv.RequestUseItemFromSlot( useIdx );
					},
					MouseEnterSlot = overIdx => _hoverSlot = overIdx,
					MouseLeaveSlot = leaveIdx =>
					{
						if ( _hoverSlot == leaveIdx )
							_hoverSlot = null;
					}
				} );
				if ( inspectSel )
					cell.AddClass( "inspect-selected" );
				if ( dropTarget )
					cell.AddClass( "drop-target" );
				_slotPanels[idx] = cell;
				cell.Style.Width = cellW;
				cell.Style.MinHeight = 56;
				cell.Style.MarginRight = c < columns - 1 ? 4 : 0;
				cell.Style.BackgroundColor = draggingSource ? HudSlotPending : hovered ? HudSlotHover : HudSlotEmpty;
				cell.Style.BorderWidth = 1;
				cell.Style.BorderColor = draggingSource || inspectSel ? HudAccentPrimary : HudBorder;
				cell.Style.FlexDirection = FlexDirection.Column;
				cell.Style.Padding = 6;
				cell.Style.JustifyContent = Justify.Center;
				cell.Style.PointerEvents = PointerEvents.All;

				var main = cell.AddChild( new Label( SlotPrimaryLine( net ), "bp-slot-main-v2" ) );
				main.Style.FontSize = 18;
				main.Style.FontColor = new Color( 0.92f, 0.9f, 0.85f );
				main.Style.TextAlign = TextAlign.Center;
				main.Style.PointerEvents = PointerEvents.None;

				var sub = cell.AddChild( new Label( SlotSecondaryLine( net ), "bp-slot-sub-v2" ) );
				sub.Style.FontSize = 9;
				sub.Style.FontColor = new Color( 0.55f, 0.53f, 0.5f );
				sub.Style.TextAlign = TextAlign.Center;
				sub.Style.PointerEvents = PointerEvents.None;
			}
		}
	}

	void BuildToolbarDockBuildMode( Panel root, ThornsBuildingController build, ThornsInventory inv )
	{
		if ( build is null || !build.IsValid() )
			return;

		var dock = ThornsUiPanelAdd.AddChildPanel(root,  "toolbar-dock-build" );
		dock.Style.Position = PositionMode.Absolute;
		dock.Style.Bottom = 12;
		dock.Style.Left = 0;
		dock.Style.Width = Length.Fraction( 1f );
		dock.Style.JustifyContent = Justify.Center;
		dock.Style.AlignItems = Align.Center;
		dock.Style.FlexDirection = FlexDirection.Column;
		dock.Style.PointerEvents = PointerEvents.All;

		var rail = ThornsUiPanelAdd.AddChildPanel(dock,  "toolbar-build-rail-v2" );
		rail.Style.FlexDirection = FlexDirection.Column;
		rail.Style.AlignItems = Align.Center;
		rail.Style.BackgroundColor = HudSurfaceCard;
		rail.Style.BorderTopWidth = 2;
		rail.Style.BorderTopColor = HudAccentPrimary;
		rail.Style.BorderBottomWidth = 1;
		rail.Style.BorderBottomColor = HudBorder;
		rail.Style.Padding = 12;
		rail.Style.MinWidth = 640;
		rail.Style.MaxWidth = Length.Fraction( 0.96f );

		var title = rail.AddChild( new Label( "BUILD MODE", "toolbar-build-title-v2" ) );
		title.Style.FontSize = 12;
		title.Style.FontWeight = 900;
		title.Style.LetterSpacing = 3;
		title.Style.FontColor = HudAccentPrimary;
		title.Style.MarginBottom = 6;
		title.Style.TextAlign = TextAlign.Center;

		var keysLine = rail.AddChild( new Label( ThornsBuildModeHudCopy.BuildToolbarKeysLine(), "toolbar-build-keys-v2" ) );
		keysLine.Style.FontSize = 11;
		keysLine.Style.FontColor = new Color( 0.78f, 0.8f, 0.74f, 1f );
		keysLine.Style.TextAlign = TextAlign.Center;
		keysLine.Style.MarginBottom = 8;
		keysLine.Style.WhiteSpace = WhiteSpace.Normal;

		var slotRow = ThornsUiPanelAdd.AddChildPanel(rail,  "toolbar-build-slots-v2" );
		slotRow.Style.FlexDirection = FlexDirection.Row;
		slotRow.Style.JustifyContent = Justify.Center;
		slotRow.Style.FlexWrap = Wrap.Wrap;
		slotRow.Style.PointerEvents = PointerEvents.All;
		slotRow.Style.MarginBottom = 8;

		for ( var i = 0; i < ThornsBuildToolbar.Entries.Length; i++ )
		{
			var entry = ThornsBuildToolbar.Entries[i];
			var idx = i;
			var selected = build.SelectedBuildToolbarSlot == entry.SlotIndex;

			var cell = ThornsUiPanelAdd.AddChildPanel(slotRow,  $"toolbar-build-slot-{i}-v2" );
			cell.Style.Width = ToolbarSlotPx + 8;
			cell.Style.Height = ToolbarSlotPx + 18;
			cell.Style.MarginLeft = i == 0 ? 0 : 4;
			cell.Style.MarginBottom = 4;
			cell.Style.BackgroundColor = selected ? HudSlotSelectedTb : HudSlotEmpty;
			cell.Style.BorderWidth = selected ? 2 : 1;
			cell.Style.BorderColor = selected ? HudAccentPrimary : HudBorder;
			cell.Style.FlexDirection = FlexDirection.Column;
			cell.Style.JustifyContent = Justify.Center;
			cell.Style.AlignItems = Align.Center;
			cell.Style.Padding = 4;
			cell.Style.PointerEvents = PointerEvents.All;

			cell.AddEventListener( "mousedown", () =>
			{
				Log.Info( $"[Thorns][UI] Build toolbar click slot={idx}" );
				build.SelectBuildToolbarSlot( idx, build.BuildModeActive );
			} );

			var key = cell.AddChild( new Label( $"{i + 1}", "tb-build-key-v2" ) );
			key.Style.FontSize = 9;
			key.Style.FontColor = new Color( 0.42f, 0.48f, 0.4f, 1f );

			var cap = cell.AddChild( new Label( entry.Label, "tb-build-cap-v2" ) );
			cap.Style.FontSize = 10;
			cap.Style.FontWeight = 700;
			cap.Style.FontColor = new Color( 0.88f, 0.86f, 0.8f, 1f );
			cap.Style.MarginTop = 2;
			cap.Style.TextAlign = TextAlign.Center;
		}

		var statusText = ThornsBuildModeHudCopy.BuildActionStatusLine( build, inv );
		var statusOk = !statusText.Contains( "(not enough)", StringComparison.Ordinal );
		var status = rail.AddChild( new Label( statusText, "toolbar-build-status-v2" ) );
		status.Style.FontSize = 12;
		status.Style.FontWeight = 600;
		status.Style.FontColor = statusOk
			? new Color( 0.82f, 0.86f, 0.78f, 1f )
			: new Color( 0.95f, 0.52f, 0.44f, 1f );
		status.Style.TextAlign = TextAlign.Center;
		status.Style.WhiteSpace = WhiteSpace.Normal;
		status.Style.MarginBottom = 6;

		var footer = rail.AddChild( new Label( ThornsBuildModeHudCopy.BuildFooter( build.ToolMode ), "toolbar-build-hint-v2" ) );
		footer.Style.FontSize = 11;
		footer.Style.FontColor = new Color( 0.58f, 0.62f, 0.56f, 1f );
		footer.Style.TextAlign = TextAlign.Center;
	}

	void BuildToolbarDockV2( Panel root, ThornsInventory inv, ThornsHotbarEquipment hotbar, ThornsWeapon weapon, ThornsArmorEquipment armor )
	{
		var dock = ThornsUiPanelAdd.AddChildPanel(root,  "toolbar-dock-v2" );
		dock.Style.Position = PositionMode.Absolute;
		dock.Style.Bottom = 12;
		dock.Style.Left = 0;
		dock.Style.Width = Length.Fraction( 1f );
		dock.Style.JustifyContent = Justify.Center;
		dock.Style.AlignItems = Align.Center;
		dock.Style.FlexDirection = FlexDirection.Column;
		dock.Style.PointerEvents = PointerEvents.All;

		var rail = ThornsUiPanelAdd.AddChildPanel(dock,  "toolbar-rail-v2" );
		rail.Style.FlexDirection = FlexDirection.Column;
		rail.Style.AlignItems = Align.Center;
		rail.Style.BackgroundColor = HudSurfaceCard;
		rail.Style.BorderTopWidth = 2;
		rail.Style.BorderTopColor = HudAccentPrimary;
		rail.Style.BorderBottomWidth = 1;
		rail.Style.BorderBottomColor = HudBorder;
		rail.Style.Padding = 10;
		rail.Style.MinWidth = 520;

		BuildWeaponReadoutV2( rail, weapon, hotbar, inv );

		var hotbarBarRow = ThornsUiPanelAdd.AddChildPanel(rail,  "toolbar-hotbar-bar-row-v2" );
		hotbarBarRow.Style.FlexDirection = FlexDirection.Row;
		hotbarBarRow.Style.AlignItems = Align.Center;
		hotbarBarRow.Style.JustifyContent = Justify.Center;
		hotbarBarRow.Style.MarginTop = 8;
		hotbarBarRow.Style.PointerEvents = PointerEvents.All;

		var slotRow = ThornsUiPanelAdd.AddChildPanel(hotbarBarRow,  "toolbar-slots-v2" );
		slotRow.Style.FlexDirection = FlexDirection.Row;
		slotRow.Style.JustifyContent = Justify.Center;
		slotRow.Style.PointerEvents = PointerEvents.All;

		if ( ThornsWeapon.HudShouldShowGunAmmoCounters( weapon ) )
		{
			var loaded = weapon.ClientMirrorLoadedAmmo;
			var reserve = weapon.ClientMirrorReserveAmmo;
			var mini = hotbarBarRow.AddChild(
				new Label( $"{loaded} / {reserve}", "toolbar-ammo-mini-v2" ) );
			mini.Style.PointerEvents = PointerEvents.None;
		}

		for ( var i = 0; i < ThornsInventory.HotbarSlotCount; i++ )
		{
			var idx = i;
			var selected = hotbar.IsValid() && hotbar.ClientMirrorSelectedHotbar == idx;
			ThornsInventorySlotNet net = default;
			_ = inv.TryGetClientMirrorSlot( idx, out net );

			var draggingSource = ThornsInventoryDragState.FromInventorySlot == idx;
			var hovered = _hoverSlot == idx;
			var inspectSel = _overlayInspectSlot == idx;

			InventorySlotPanel cell = null;
			cell = slotRow.AddChild( new InventorySlotPanel
			{
				SlotIndex = idx,
				MouseDownSlot = _ =>
				{
					if ( !ShowFullInventory )
					{
						Log.Info( $"[Thorns][UI] Toolbar select slot {idx}" );
						if ( hotbar.IsValid() )
							hotbar.RequestSelectHotbarSlot( idx );
						return;
					}

					HandleInventoryGridMouseDown( idx, inv, armor, cell );
				},
				MouseUpSlot = _ =>
				{
					if ( !ShowFullInventory )
						return;

					HandleInventorySlotMouseUp( idx, inv, armor );
				},
				MouseRightClickSlot = useIdx =>
				{
					Log.Info( $"[Thorns][UI] Right-click use toolbar slot {useIdx}" );
					if ( inv.IsValid() )
						inv.RequestUseItemFromSlot( useIdx );
				},
				MouseEnterSlot = overIdx => _hoverSlot = overIdx,
				MouseLeaveSlot = leaveIdx =>
				{
					if ( _hoverSlot == leaveIdx )
						_hoverSlot = null;
				}
			} );
			if ( inspectSel )
				cell.AddClass( "inspect-selected" );
			if ( ThornsInventoryDragState.IsDragging && hovered )
				cell.AddClass( "drop-target" );
			_slotPanels[idx] = cell;

			cell.Style.Width = ToolbarSlotPx;
			cell.Style.Height = ToolbarSlotPx;
			cell.Style.MarginLeft = i == 0 ? 0 : 5;
			cell.Style.BackgroundColor = draggingSource
				? HudSlotPending
				: selected
					? HudSlotSelectedTb
					: hovered
						? HudSlotHover
						: HudSlotEmpty;
			cell.Style.BorderWidth = selected ? 2 : 1;
			cell.Style.BorderColor = selected ? HudAccentPrimary : HudBorder;
			cell.Style.FlexDirection = FlexDirection.Column;
			cell.Style.JustifyContent = Justify.Center;
			cell.Style.AlignItems = Align.Center;
			cell.Style.Padding = 4;
			cell.Style.PointerEvents = PointerEvents.All;

			var key = cell.AddChild( new Label( $"{idx + 1}", "tb-key-v2" ) );
			key.Style.FontSize = 9;
			key.Style.FontColor = new Color( 0.45f, 0.42f, 0.38f );
			key.Style.PointerEvents = PointerEvents.None;

			var stack = cell.AddChild( new Panel() );
			stack.AddClass( "tb-slot-stack-v2" );
			stack.Style.FlexDirection = FlexDirection.Column;
			stack.Style.AlignItems = Align.Center;
			stack.Style.JustifyContent = Justify.Center;
			stack.Style.FlexGrow = 1;
			stack.Style.PointerEvents = PointerEvents.None;

			var iconWrap = stack.AddChild( new Panel() );
			iconWrap.AddClass( "tb-slot-icon-wrap-v2" );
			iconWrap.Style.PointerEvents = PointerEvents.None;

			var iconFg = iconWrap.AddChild( new Panel() );
			iconFg.AddClass( "tb-slot-icon-fg-v2" );
			iconFg.Style.PointerEvents = PointerEvents.None;

			var glyph = iconWrap.AddChild( new Label( "", "tb-slot-icon-glyph-v2" ) );
			glyph.Style.TextAlign = TextAlign.Center;
			glyph.Style.PointerEvents = PointerEvents.None;

			var qty = stack.AddChild( new Label( "", "tb-slot-qty-v2" ) );
			qty.Style.TextAlign = TextAlign.Center;
			qty.Style.PointerEvents = PointerEvents.None;

			ThornsItemHudIcons.BindDebugToolbarHotbarCell( iconFg, glyph, qty, net );
		}

		var hint = rail.AddChild( new Label( "TAB inventory  ·  use/E consume (hotbar)  ·  right-click slot use  ·  F1 dev  ·  ESC close", "toolbar-hint-v2" ) );
		hint.Style.FontSize = 11;
		hint.Style.FontColor = new Color( 0.55f, 0.52f, 0.48f );
		hint.Style.MarginTop = 8;
		hint.Style.PointerEvents = PointerEvents.None;
	}

	void BuildWeaponReadoutV2( Panel rail, ThornsWeapon weapon, ThornsHotbarEquipment hotbar, ThornsInventory inv )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(rail,  "weapon-row-v2" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.JustifyContent = Justify.Center;
		row.Style.PointerEvents = PointerEvents.All;

		var itemId = hotbar.IsValid() ? hotbar.ClientMirrorActiveItemId : "";
		var defName = string.IsNullOrEmpty( weapon?.ClientMirrorCombatDefinitionId )
			? "—"
			: ShortDisplayName( weapon.ClientMirrorCombatDefinitionId );
		if ( !string.IsNullOrEmpty( itemId ) && ThornsItemRegistry.TryGet( itemId, out var def ) && def.ItemType == ThornsItemType.Weapon )
			defName = def.DisplayName;

		var nameLbl = row.AddChild( new Label( defName.ToUpperInvariant(), "weapon-name-v2" ) );
		nameLbl.Style.FontSize = 14;
		nameLbl.Style.FontWeight = 900;
		nameLbl.Style.LetterSpacing = 3;
		nameLbl.Style.FontColor = new Color( 0.9f, 0.86f, 0.78f );
		nameLbl.Style.MarginRight = 16;

		var combatIdHud = weapon.IsValid() ? weapon.ClientMirrorCombatDefinitionId ?? "" : "";
		var combatDefForHud = weapon.IsValid() ? ThornsWeaponDefinitions.Get( combatIdHud ) : null;
		var meleeForHud = combatDefForHud is not null
			&& ThornsWeaponDefinitions.TreatsAsMeleeWeapon( combatDefForHud, combatIdHud );

		var clip = weapon?.ClientMirrorLoadedAmmo ?? 0;
		var res = weapon?.ClientMirrorReserveAmmo ?? 0;
		var ammoText = meleeForHud || clip < 0 ? "Melee" : $"{clip}  /  {res}";
		var ammoLbl = row.AddChild( new Label( ammoText, "ammo-readout-v2" ) );
		ammoLbl.Style.FontSize = 22;
		ammoLbl.Style.FontWeight = 900;
		ammoLbl.Style.FontColor = HudEconomyGold;
		ammoLbl.Style.MarginRight = 12;

		if ( weapon.IsValid() && weapon.ClientMirrorReloading )
		{
			var rel = row.AddChild( new Label( "RELOADING", "reload-tag-v2" ) );
			rel.Style.FontSize = 11;
			rel.Style.FontColor = new Color( 0.45f, 0.75f, 1f );
			rel.Style.MarginRight = 8;
		}

		if ( weapon.IsValid() && weapon.ClientMirrorWeaponBroken )
		{
			var br = row.AddChild( new Label( "BROKEN", "broken-tag-v2" ) );
			br.Style.FontSize = 11;
			br.Style.FontColor = new Color( 1f, 0.35f, 0.35f );
			br.Style.MarginRight = 8;
		}

		if ( !(weapon.IsValid() && meleeForHud) )
		{
			var reloadBtn = ThornsUiPanelAdd.AddClickableLabel(row,  "Reload", () =>
			{
				Log.Info( "[Thorns] UI: reload" );
				if ( weapon.IsValid() )
					weapon.DebugUiSendReloadIntent();
			} );
			reloadBtn.Style.Padding = 8;
			reloadBtn.Style.PointerEvents = PointerEvents.All;
		}
	}

	void BuildRadioShopOverlay( Panel root, ThornsInventory inv, ThornsRadioShopInteractor shop )
	{
		var layer = ThornsUiPanelAdd.AddChildPanel(root,  "radio-shop-layer" );
		layer.Style.Position = PositionMode.Absolute;
		layer.Style.Left = 0;
		layer.Style.Top = 0;
		layer.Style.Width = Length.Fraction( 1f );
		layer.Style.Height = Length.Fraction( 1f );
		layer.Style.BackgroundColor = new Color( 5f / 255f, 7f / 255f, 10f / 255f, 0.72f );
		layer.Style.JustifyContent = Justify.Center;
		layer.Style.AlignItems = Align.Center;
		layer.Style.PointerEvents = PointerEvents.All;
		layer.Style.ZIndex = 55;

		var card = ThornsUiPanelAdd.AddChildPanel(layer,  "radio-shop-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.Padding = 16;
		card.Style.MinWidth = Length.Pixels( 440 );
		card.Style.MaxWidth = Length.Pixels( 560 );
		card.Style.MaxHeight = Length.Pixels( 520 );
		card.Style.BackgroundColor = HudSurfaceCard;
		card.Style.BorderWidth = 2;
		card.Style.BorderColor = HudAccentPrimary;

		var head = ThornsUiPanelAdd.AddChildPanel(card,  "radio-shop-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.JustifyContent = Justify.SpaceBetween;
		head.Style.AlignItems = Align.Center;
		head.Style.MarginBottom = 8;

		var title = head.AddChild( new Label( "RADIO OUTPOST", "radio-shop-title" ) );
		title.Style.FontSize = 16;
		title.Style.FontWeight = 900;
		title.Style.LetterSpacing = 5;
		title.Style.FontColor = new Color( 0.92f, 0.86f, 0.68f, 1f );

		var close = ThornsUiPanelAdd.AddClickableLabel(head,  "Close", () =>
		{
			SetRadioShopOpen( false );
			RequestHudRebuild();
		} );
		close.Style.Padding = 6;

		var metalCount = inv.IsValid()
			? inv.ClientMirrorCountItemId( ThornsRadioShopCatalog.CurrencyItemId )
			: 0;
		var goldLine = card.AddChild( new Label(
			inv.IsValid() ? $"Metal in inventory: {metalCount}" : "Metal in inventory: —",
			"radio-shop-gold" ) );
		goldLine.Style.FontSize = 13;
		goldLine.Style.FontWeight = 700;
		goldLine.Style.FontColor = HudEconomyGold;
		goldLine.Style.MarginBottom = 10;

		var hint = card.AddChild( new Label(
			ThornsInteractionPromptText.Format( "Press Use (E) near the radio to open · Esc closes" ),
			"radio-shop-hint" ) );
		hint.Style.FontSize = 10;
		hint.Style.FontColor = new Color( 0.55f, 0.54f, 0.5f, 1f );
		hint.Style.MarginBottom = 8;

		var scroll = ThornsUiPanelAdd.AddChildPanel(card,  "radio-shop-scroll" );
		scroll.Style.FlexDirection = FlexDirection.Column;
		scroll.Style.Overflow = OverflowMode.Scroll;
		scroll.Style.MaxHeight = Length.Pixels( 260 );

		var ids = _radioCatalogItemIds;
		var prices = _radioCatalogBuyPrices;
		var maxB = _radioCatalogMaxBuy;
		var n = ids is not null ? ids.Length : 0;
		if ( n == 0 )
		{
			scroll.AddChild( new Label( "No offers loaded — reopen from the station.", "radio-shop-empty" ) );
		}
		else
		{
			var stationId = RadioShopStationId;
			for ( var i = 0; i < n; i++ )
			{
				var slot = i;
				var itemId = ids[i];
				var unit = i < prices.Length ? prices[i] : 0;
				var cap = i < maxB.Length ? maxB[i] : 1;
				var def = ThornsItemRegistry.GetOrNull( itemId );
				var titleTxt = def?.DisplayName ?? itemId;

				var row = ThornsUiPanelAdd.AddChildPanel(scroll,  $"radio-offer-{i}" );
				row.Style.FlexDirection = FlexDirection.Row;
				row.Style.JustifyContent = Justify.SpaceBetween;
				row.Style.AlignItems = Align.Center;
				row.Style.MarginBottom = 6;
				row.Style.Padding = 6;
				row.Style.BorderBottomWidth = 1;
				row.Style.BorderBottomColor = HudBorder;

				var txt = row.AddChild( new Label( $"{titleTxt}  —  {unit} metal each  (max {cap})", "radio-offer-txt" ) );
				txt.Style.FontSize = 11;
				txt.Style.FontColor = new Color( 0.88f, 0.85f, 0.78f, 1f );
				txt.Style.FlexGrow = 1;

				var btnRow = ThornsUiPanelAdd.AddChildPanel(row,  "radio-offer-btns" );
				btnRow.Style.FlexDirection = FlexDirection.Row;

				void TryBuy( int q )
				{
					if ( !shop.IsValid() || stationId == Guid.Empty )
						return;
					var qq = Math.Clamp( q, 1, Math.Max( 1, cap ) );
					shop.RequestRadioBuy( stationId, slot, qq );
					RequestHudRebuild();
				}

				var b1 = ThornsUiPanelAdd.AddClickableLabel(btnRow,  "×1", () => TryBuy( 1 ) );
				b1.Style.MarginLeft = 4;
				b1.Style.Padding = 4;
				var b5 = ThornsUiPanelAdd.AddClickableLabel(btnRow,  "×5", () => TryBuy( 5 ) );
				b5.Style.MarginLeft = 4;
				b5.Style.Padding = 4;
			}
		}

		var sellHead = card.AddChild( new Label( "Sell from inventory (×1)", "radio-sell-head" ) );
		sellHead.Style.FontSize = 12;
		sellHead.Style.FontWeight = 800;
		sellHead.Style.MarginTop = 12;
		sellHead.Style.MarginBottom = 6;
		sellHead.Style.FontColor = new Color( 0.78f, 0.76f, 0.7f, 1f );

		var sellRowWrap = ThornsUiPanelAdd.AddChildPanel(card,  "radio-sell-grid" );
		sellRowWrap.Style.FlexDirection = FlexDirection.Column;
		sellRowWrap.Style.MaxHeight = Length.Pixels( 140 );
		sellRowWrap.Style.Overflow = OverflowMode.Scroll;

		var sid = RadioShopStationId;
		for ( var s = 0; s < ThornsInventory.TotalSlots; s++ )
		{
			var si = s;
			ThornsInventorySlotNet net = default;
			if ( inv.IsValid() )
				_ = inv.TryGetClientMirrorSlot( si, out net );
			var hasItem = !string.IsNullOrEmpty( net.ItemId ) && net.Quantity > 0;
			var sellBlocked = hasItem && ThornsRadioShopCatalog.IsMetalTradeBlockedFromRadioShop( net.ItemId );
			string label = $"Slot {s}: empty";
			if ( hasItem )
			{
				var d = ThornsItemRegistry.GetOrNull( net.ItemId );
				var nm = d?.DisplayName ?? net.ItemId;
				if ( sellBlocked )
					label = $"{nm} ×{net.Quantity}  —  not sold here";
				else
				{
					var sellEa = ThornsRadioShopCatalog.ClientEstimateSellMetalDisplay( net );
					label = $"{nm} ×{net.Quantity}  →  ~{sellEa} metal / unit";
				}
			}

			var sRow = ThornsUiPanelAdd.AddChildPanel(sellRowWrap,  $"radio-sell-{s}" );
			sRow.Style.FlexDirection = FlexDirection.Row;
			sRow.Style.JustifyContent = Justify.SpaceBetween;
			sRow.Style.MarginBottom = 4;

			var sellLbl = sRow.AddChild( new Label( label, "radio-sell-line" ) );
			sellLbl.Style.FontSize = 10;
			if ( hasItem && !sellBlocked )
			{
				var sb = ThornsUiPanelAdd.AddClickableLabel(sRow,  "Sell ×1", () =>
				{
					if ( !shop.IsValid() || sid == Guid.Empty )
						return;
					shop.RequestRadioSell( sid, si, 1 );
					RequestHudRebuild();
				} );
				sb.Style.Padding = 4;
			}
			else if ( hasItem )
			{
				_ = sRow.AddChild( new Label( "—", "radio-sell-line" ) );
			}
			else
			{
				_ = sRow.AddChild( new Label( "—", "radio-sell-line" ) );
			}
		}
	}
}
