#nullable disable

using System;
using System.Globalization;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Structured inventory / craft-preview inspect column (header · hero · bars · mods · effects · footer).</summary>
public static class ThornsUiItemInspectPanel
{
	public sealed class FooterModel
	{
		public Action OnEquip;
		public Action OnUse;
		public Action OnModify;
		public Action OnDrop;
		public bool EquipEnabled = true;
		public bool UseEnabled;
		public bool ModifyEnabled;
		public bool DropEnabled;
	}

	public static void Rebuild(
		Panel body,
		in ThornsInventorySlotNet net,
		ThornsItemRegistry.ThornsItemDefinition def,
		bool isCraftPreview,
		FooterModel footer )
	{
		if ( body is null || !body.IsValid )
			return;

		body.DeleteChildren();
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.FlexGrow = 1;
		body.Style.MinHeight = 0;
		body.Style.JustifyContent = Justify.FlexStart;
		body.Style.AlignItems = Align.Stretch;

		var root = ThornsUiPanelAdd.AddChildPanel(body,  "thorns-inv-inspect-root" );
		root.Style.FlexDirection = FlexDirection.Column;
		// Let content define scroll height; flex-grow here squashes the hero into the viewport and clips glyphs.
		root.Style.FlexGrow = 0;
		root.Style.FlexShrink = 0;
		root.Style.Width = Length.Fraction( 1f );

		var head = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.JustifyContent = Justify.SpaceBetween;
		head.Style.AlignItems = Align.FlexStart;
		head.Style.FlexShrink = 0;

		var headLeft = ThornsUiPanelAdd.AddChildPanel(head,  "thorns-inv-inspect-head-left" );
		headLeft.Style.FlexDirection = FlexDirection.Column;
		headLeft.Style.FlexGrow = 1;
		headLeft.Style.MinWidth = 0;

		var title = headLeft.AddChild( new Label(
			ThornsUiInventoryFormatting.SlotPrimaryLine( net ).ToUpperInvariant(),
			"thorns-inv-inspect-title" ) );
		title.Style.PointerEvents = PointerEvents.None;
		title.Style.Overflow = OverflowMode.Visible;
		title.Style.LineHeight = Length.Pixels( 24 );
		if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var titleTint ) )
			title.Style.FontColor = titleTint;
		else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var armorTitleTint ) )
			title.Style.FontColor = armorTitleTint;

		var headRight = ThornsUiPanelAdd.AddChildPanel(head,  "thorns-inv-inspect-head-right" );
		headRight.Style.FlexDirection = FlexDirection.Column;
		headRight.Style.AlignItems = Align.FlexEnd;
		headRight.Style.FlexShrink = 0;
		var meta = headRight.AddChild( new Label(
			$"{net.Quantity} / {def.MaxStack}",
			"thorns-inv-inspect-meta" ) );
		meta.Style.PointerEvents = PointerEvents.None;
		meta.Style.Overflow = OverflowMode.Visible;
		meta.Style.LineHeight = Length.Pixels( 17 );

		var sub = root.AddChild( new Label( "", "thorns-inv-inspect-sub" ) );
		sub.Style.PointerEvents = PointerEvents.None;
		sub.Style.Overflow = OverflowMode.Visible;
		sub.Style.LineHeight = Length.Pixels( 19 );
		if ( def.ItemType == ThornsItemType.Weapon )
		{
			var cid = string.IsNullOrEmpty( def.CombatWeaponDefinitionId ) ? net.ItemId : def.CombatWeaponDefinitionId;
			var w = ThornsWeaponDefinitions.Get( cid?.Trim() ?? "" );
			ThornsUiWeaponInspectFormatting.ResolveWeaponRoll( net, out var rarity, out _, out _ );
			var arch = ThornsUiWeaponInspectFormatting.GetWeaponInspectArchetypeLabel( w, cid?.Trim() ?? "" );
			sub.Text = $"{arch} · {rarity.DisplayName()}";
			var tr = rarity.TintApprox();
			sub.Style.FontColor = new Color( tr.r, tr.g, tr.b, 0.95f );
		}
		else if ( def.ItemType == ThornsItemType.Armor )
		{
			ThornsUiArmorInspectFormatting.ResolveArmorRoll( net, out var ar, out _ );
			sub.Text = $"Armor · {ArmorSlotLabel( def.ArmorSlot )} · {ar.DisplayName()}";
			var atr = ar.TintApprox();
			sub.Style.FontColor = new Color( atr.r, atr.g, atr.b, 0.95f );
		}
		else
		{
			sub.Text = PrettyItemType( def );
			sub.Style.FontColor = new Color( 0.55f, 0.82f, 0.92f, 0.88f );
		}

		if ( isCraftPreview )
		{
			var craftNote = root.AddChild( new Label(
				"Recipe preview — not in your pack",
				"thorns-inv-inspect-craft-note" ) );
			craftNote.Style.PointerEvents = PointerEvents.None;
		}

		var heroRow = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-hero-row" );
		heroRow.Style.FlexDirection = FlexDirection.Row;
		heroRow.Style.JustifyContent = Justify.Center;
		heroRow.Style.AlignItems = Align.Center;
		heroRow.Style.FlexShrink = 0;
		heroRow.Style.Width = Length.Fraction( 1f );
		heroRow.Style.MarginTop = Length.Pixels( 6 );
		heroRow.Style.MarginBottom = Length.Pixels( 4 );

		var iconBox = ThornsUiPanelAdd.AddChildPanel(heroRow,  "thorns-inv-inspect-hero-frame" );
		iconBox.Style.PointerEvents = PointerEvents.None;
		iconBox.Style.Width = Length.Fraction( 1f );
		iconBox.Style.FlexShrink = 0;
		iconBox.Style.FlexDirection = FlexDirection.Column;
		iconBox.Style.JustifyContent = Justify.Center;
		iconBox.Style.AlignItems = Align.Center;
		iconBox.Style.Overflow = OverflowMode.Visible;

		AppendInspectHeroVisual( iconBox, def, net.ItemId );

		{
			var rule = root.AddChild( new Panel() );
			rule.AddClass( "thorns-inv-inspect-rule" );
		}

		if ( def.ItemType == ThornsItemType.Weapon )
			AppendWeaponBars( root, def, net );
		else
			AppendGenericProperties( root, def, net );

		if ( def.ItemType == ThornsItemType.Weapon )
			AppendModsPlaceholder( root );

		AppendEffectsSection( root, def, net );

		var spacer = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-spacer" );
		// Scroll column: avoid flex-grow spacer (clips siblings on some panel layouts).
		spacer.Style.FlexGrow = 0;
		spacer.Style.MinHeight = Length.Pixels( 12 );

		var flavor = root.AddChild( new Label( FlavorLine( def ), "thorns-inv-inspect-flavor" ) );
		flavor.Style.PointerEvents = PointerEvents.None;
		flavor.Style.Overflow = OverflowMode.Visible;
		flavor.Style.LineHeight = Length.Pixels( 18 );
		flavor.Style.MinHeight = Length.Pixels( 20 );

		AppendFooter( root, footer, isCraftPreview );
	}

	static void AppendWeaponBars(
		Panel root,
		ThornsItemRegistry.ThornsItemDefinition def,
		in ThornsInventorySlotNet net )
	{
		var block = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-stats" );
		block.Style.FlexDirection = FlexDirection.Column;
		block.Style.FlexShrink = 0;

		var rows = ThornsUiWeaponInspectFormatting.BuildWeaponInspectBarRows( def, net );
		foreach ( var row in rows )
			AddStatBarRow( block, row.StatKey, row.ValueText, row.Fill01 );
	}

	static void AddStatBarRow( Panel parent, string key, string valueText, float fill01 )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(parent,  "thorns-inv-inspect-stat-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.FlexStart;
		row.Style.MarginBottom = Length.Pixels( 6 );

		var lab = row.AddChild( new Label( key, "thorns-inv-inspect-stat-key" ) );
		lab.Style.PointerEvents = PointerEvents.None;
		lab.Style.Width = Length.Pixels( 86 );
		lab.Style.FlexShrink = 0;

		var track = ThornsUiPanelAdd.AddChildPanel(row,  "thorns-inv-inspect-stat-track" );
		track.Style.FlexGrow = 1;
		track.Style.Height = Length.Pixels( 6 );
		track.Style.MarginLeft = Length.Pixels( 6 );
		track.Style.MarginRight = Length.Pixels( 8 );
		track.Style.MinWidth = Length.Pixels( 40 );

		var fill = ThornsUiPanelAdd.AddChildPanel(track,  "thorns-inv-inspect-stat-fill" );
		fill.Style.Height = Length.Fraction( 1f );
		fill.Style.Width = Length.Fraction( Math.Clamp( fill01, 0.04f, 1f ) );

		var val = row.AddChild( new Label( valueText, "thorns-inv-inspect-stat-val" ) );
		val.Style.PointerEvents = PointerEvents.None;
		val.Style.FlexShrink = 0;
		val.Style.MinWidth = Length.Pixels( 44 );
		val.Style.TextAlign = TextAlign.Right;
	}

	static void AppendGenericProperties(
		Panel root,
		ThornsItemRegistry.ThornsItemDefinition def,
		in ThornsInventorySlotNet net )
	{
		var block = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-stats" );
		block.Style.FlexDirection = FlexDirection.Column;
		block.Style.FlexShrink = 0;

		AddStatBarRow( block, "STACK", $"{net.Quantity}",
			def.MaxStack > 0 ? Math.Clamp( net.Quantity / (float)def.MaxStack, 0.06f, 1f ) : 1f );
		if ( net.HasDurability != 0 && def.ArmorMaxDurability > 0.01f )
			AddStatBarRow( block, "CONDITION", $"{net.Durability:F0}",
				Math.Clamp( net.Durability / def.ArmorMaxDurability, 0.06f, 1f ) );
		if ( net.HasDurability != 0 && def.ItemType == ThornsItemType.Tool && def.ToolMaxDurability > 0.01f )
			AddStatBarRow( block, "CONDITION", $"{net.Durability:F0}",
				Math.Clamp( net.Durability / def.ToolMaxDurability, 0.06f, 1f ) );
		if ( def.ItemType == ThornsItemType.Armor )
		{
			ThornsUiArmorInspectFormatting.ResolveArmorRoll( net, out _, out var drMul );
			var effDr = def.ArmorDamageReductionPercent * drMul;
			AddStatBarRow( block, "MITIGATION", $"{effDr:F1}%",
				Math.Clamp( effDr / ThornsArmorEquipment.MaxTotalDamageReductionPercent, 0.06f, 1f ) );
		}
	}

	static void AppendModsPlaceholder( Panel root )
	{
		var hdr = root.AddChild( new Label( "MODS", "thorns-inv-inspect-section-title" ) );
		hdr.Style.PointerEvents = PointerEvents.None;
		hdr.Style.Overflow = OverflowMode.Visible;
		hdr.Style.LineHeight = Length.Pixels( 19 );
		hdr.Style.MinHeight = Length.Pixels( 24 );

		var grid = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-mods" );
		grid.Style.FlexDirection = FlexDirection.Row;
		grid.Style.JustifyContent = Justify.SpaceBetween;

		AddModSlot( grid, "🔥", "Fire" );
		AddModSlot( grid, "▭", "Magazine" );
		AddModSlot( grid, "◎", "Muzzle" );
		AddModSlot( grid, "⌇", "Grip" );
	}

	static void AddModSlot( Panel grid, string icon, string caption )
	{
		var slot = ThornsUiPanelAdd.AddChildPanel(grid,  "thorns-inv-inspect-mod-slot" );
		slot.Style.FlexDirection = FlexDirection.Column;
		slot.Style.AlignItems = Align.Center;
		slot.Style.PointerEvents = PointerEvents.None;

		var ic = slot.AddChild( new Label( icon, "thorns-inv-inspect-mod-icon" ) );
		ic.Style.PointerEvents = PointerEvents.None;
		slot.AddChild( new Label( caption, "thorns-inv-inspect-mod-cap" ) ).Style.PointerEvents = PointerEvents.None;
	}

	static void AppendEffectsSection(
		Panel root,
		ThornsItemRegistry.ThornsItemDefinition def,
		in ThornsInventorySlotNet net )
	{
		var hdr = root.AddChild( new Label( "EFFECTS", "thorns-inv-inspect-section-title" ) );
		hdr.Style.PointerEvents = PointerEvents.None;
		hdr.Style.Overflow = OverflowMode.Visible;
		hdr.Style.LineHeight = Length.Pixels( 19 );
		hdr.Style.MinHeight = Length.Pixels( 24 );

		var list = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-effects" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.FlexShrink = 0;
		list.Style.Overflow = OverflowMode.Visible;

		var inv = CultureInfo.InvariantCulture;
		if ( def.ItemType == ThornsItemType.Weapon )
		{
			ThornsUiWeaponInspectFormatting.ResolveWeaponRoll( net, out _, out var dmgMul, out var frMul );
			if ( Math.Abs( dmgMul - 1f ) > 0.004f )
				AddEffectRow( list, "●", "Damage tuning",
					$"+{(( dmgMul - 1f ) * 100f).ToString( "F0", inv )}%", "orange" );
			if ( Math.Abs( frMul - 1f ) > 0.004f )
				AddEffectRow( list, "◆", "Fire rate tuning",
					$"+{(( frMul - 1f ) * 100f).ToString( "F0", inv )}%", "cyan" );

			var cid = string.IsNullOrEmpty( def.CombatWeaponDefinitionId ) ? net.ItemId : def.CombatWeaponDefinitionId;
			var w = ThornsWeaponDefinitions.Get( cid?.Trim() ?? "" );
			AddEffectRow( list, "◇", "Headshot multiplier", $"×{w.HeadshotMultiplier.ToString( "F1", inv )}", "blue" );
			var critP = ThornsWeaponDefinitions.ResolveCriticalHitChance( w, cid?.Trim() ?? "" ) * 100f;
			AddEffectRow( list, "✦", "Crit chance (body)", $"{critP.ToString( "F1", inv )}%", "orange" );

			if ( net.HasDurability != 0 && w.MaxDurability > 0.01f )
				AddEffectRow( list, "▪", "Durability", $"{net.Durability:F0} / {w.MaxDurability:F0}", "muted" );
		}
		else if ( def.ItemType == ThornsItemType.Consumable )
		{
			if ( def.HealthRestore > 0.01f )
				AddEffectRow( list, "♥", "Health restore", $"+{def.HealthRestore:F0}", "cyan" );
			if ( def.HungerRestore > 0.01f )
				AddEffectRow( list, "●", "Hunger restore", $"+{def.HungerRestore:F0}", "orange" );
			if ( def.ThirstRestore > 0.01f )
				AddEffectRow( list, "◆", "Thirst restore", $"+{def.ThirstRestore:F0}", "blue" );
			if ( def.UseTimeSeconds > 0.01f )
				AddEffectRow( list, "⌚", "Use time", $"{def.UseTimeSeconds:F1}s", "muted" );
		}
		else if ( def.ItemType == ThornsItemType.Armor )
		{
			ThornsUiArmorInspectFormatting.ResolveArmorRoll( net, out var arRarity, out var drMul );
			var effDr = def.ArmorDamageReductionPercent * drMul;
			AddEffectRow( list, "◇", "Rarity tier", arRarity.DisplayName(), "muted" );
			AddEffectRow( list, "🛡", "Damage reduction (piece)", $"{effDr:F1}%", "cyan" );
			if ( Math.Abs( drMul - 1f ) > 0.004f )
				AddEffectRow( list, "◆", "DR tuning",
					$"+{(( drMul - 1f ) * 100f).ToString( "F0", inv )}%", "orange" );
			AddEffectRow( list, "▪", "Slot", ArmorSlotLabel( def.ArmorSlot ), "muted" );
		}
		else if ( def.ItemType == ThornsItemType.Tool )
		{
			var tip = def.HarvestToolKind == ThornsHarvestToolKind.Axe
				? "Harvest trees and fiber thickets."
				: def.HarvestToolKind == ThornsHarvestToolKind.Pickaxe
					? "Harvest stone outcrops and metal veins."
					: def.HarvestToolKind == ThornsHarvestToolKind.Primitive
						? "Harvest trees and stone only — 1 wood or stone per strike."
						: "Equip on the toolbar to use.";
			var role = def.HarvestToolKind == ThornsHarvestToolKind.None ? "Toolbar" : "Harvest tool";
			AddEffectRowInner( list, "⚒", $"{role}\n{tip}", null, "muted" );
			if ( def.ToolHarvestYieldMultiplier > 1.001f )
				AddEffectRow( list, "▴", "Harvest efficiency", $"+{(def.ToolHarvestYieldMultiplier - 1f) * 100f:F0}% yield vs iron tools", "cyan" );
			else 			if ( def.ToolHarvestYieldMultiplier < 0.999f )
				AddEffectRow( list, "▾", "Harvest efficiency", $"{(def.ToolHarvestYieldMultiplier - 1f) * 100f:F0}% yield vs iron tools", "muted" );
			var toolMeleeCid = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( def.Id );
			if ( !string.IsNullOrEmpty( toolMeleeCid ) )
			{
				var mw = ThornsWeaponDefinitions.Get( toolMeleeCid );
				var tcp = ThornsWeaponDefinitions.ResolveCriticalHitChance( mw, toolMeleeCid ) * 100f;
				AddEffectRow( list, "✦", "Melee crit chance", $"{tcp.ToString( "F1", inv )}%", "orange" );
			}

			if ( net.HasDurability != 0 && def.ToolMaxDurability > 0.01f )
				AddEffectRow(
					list,
					"▪",
					"Durability",
					$"{net.Durability:F0} / {def.ToolMaxDurability:F0}  (~{def.ToolDurabilityLossPerStrike:F2} wear / strike)",
					"muted" );
		}
		else
		{
			AddEffectRow( list, "▪", "Stack limit", $"{def.MaxStack}", "muted" );
		}

		var anyChild = false;
		foreach ( var _ in list.Children )
		{
			anyChild = true;
			break;
		}

		if ( !anyChild )
		{
			var empty = list.AddChild( new Label( "No special bonuses.", "thorns-inv-inspect-effect-empty" ) );
			empty.Style.PointerEvents = PointerEvents.None;
			empty.Style.Overflow = OverflowMode.Visible;
		}
	}

	static void AddEffectRow( Panel list, string bullet, string text, string right, string tone ) =>
		AddEffectRowInner( list, bullet, text, string.IsNullOrWhiteSpace( right ) ? null : right, tone );

	static void AddEffectRowInner( Panel list, string bullet, string text, string right, string tone )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(list,  "thorns-inv-inspect-effect-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.FlexStart;
		row.Style.MarginBottom = Length.Pixels( 7 );
		row.Style.Overflow = OverflowMode.Visible;
		row.Style.MinHeight = Length.Pixels( 20 );

		var b = row.AddChild( new Label( bullet, "thorns-inv-inspect-effect-bullet" ) );
		b.AddClass( $"thorns-inv-inspect-effect-bullet--{tone}" );
		b.Style.PointerEvents = PointerEvents.None;
		b.Style.MarginRight = Length.Pixels( 8 );
		b.Style.MinWidth = Length.Pixels( 18 );
		b.Style.FlexShrink = 0;
		b.Style.Overflow = OverflowMode.Visible;
		b.Style.LineHeight = Length.Pixels( 18 );

		var t = row.AddChild( new Label( text, "thorns-inv-inspect-effect-text" ) );
		t.Style.PointerEvents = PointerEvents.None;
		t.Style.FlexGrow = 1;
		t.Style.FlexShrink = 1;
		t.Style.MinWidth = Length.Pixels( 0 );
		t.Style.Overflow = OverflowMode.Visible;
		t.Style.LineHeight = Length.Pixels( 21 );

		if ( string.IsNullOrEmpty( right ) )
			return;

		var r = row.AddChild( new Label( right, "thorns-inv-inspect-effect-val" ) );
		r.AddClass( $"thorns-inv-inspect-effect-val--{tone}" );
		r.Style.PointerEvents = PointerEvents.None;
		r.Style.FlexShrink = 1;
		r.Style.MinWidth = Length.Pixels( 0 );
		r.Style.Overflow = OverflowMode.Visible;
		r.Style.LineHeight = Length.Pixels( 21 );
	}

	static void AppendFooter( Panel root, FooterModel footer, bool isCraftPreview )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-inv-inspect-actions" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.FlexShrink = 0;
		row.Style.MarginTop = Length.Pixels( 8 );

		var f = footer ?? new FooterModel();
		var craft = isCraftPreview;

		AddFooterBtn( row, "EQUIP", "thorns-inv-inspect-btn", "thorns-inv-inspect-btn--equip", craft || !f.EquipEnabled,
			f.OnEquip );
		AddFooterBtn( row, "USE", "thorns-inv-inspect-btn", "thorns-inv-inspect-btn--use", craft || !f.UseEnabled,
			f.OnUse );
		AddFooterBtn( row, "MODIFY", "thorns-inv-inspect-btn", "thorns-inv-inspect-btn--ghost",
			craft || !f.ModifyEnabled, f.OnModify );
		AddFooterBtn( row, "DROP", "thorns-inv-inspect-btn", "thorns-inv-inspect-btn--ghost", craft || !f.DropEnabled,
			f.OnDrop );
	}

	static void AddFooterBtn( Panel row, string text, string clsPrimary, string clsTone, bool disabled, Action onClick )
	{
		var btn = row.AddChild( new Panel() );
		btn.AddClass( clsPrimary );
		btn.AddClass( clsTone );
		btn.Style.FlexGrow = 1;
		btn.Style.JustifyContent = Justify.Center;
		btn.Style.AlignItems = Align.Center;
		btn.Style.Padding = Length.Pixels( 10 );
		btn.SetClass( "thorns-inv-inspect-btn--disabled", disabled );
		if ( !disabled && onClick is not null )
		{
			btn.Style.PointerEvents = PointerEvents.All;
			btn.AddEventListener( "onmousedown", _ => onClick() );
		}
		else
			btn.Style.PointerEvents = PointerEvents.None;

		btn.AddChild( new Label( text, "thorns-inv-inspect-btn-label" ) ).Style.PointerEvents = PointerEvents.None;
	}

	static void AppendInspectHeroVisual(
		Panel iconBox,
		ThornsItemRegistry.ThornsItemDefinition def,
		string itemId )
	{
		var path = ThornsItemHudIcons.ResolveLoadPath( def, itemId );
		if ( !string.IsNullOrWhiteSpace( path )
		     && ThornsItemHudIcons.TryGetToolbarTexture( path, out var tex )
		     && tex is not null )
		{
			var img = ThornsUiPanelAdd.AddChildPanel(iconBox,  "thorns-inv-inspect-hero-icon-img" );
			img.Style.PointerEvents = PointerEvents.None;
			img.Style.Width = Length.Fraction( 1f );
			img.Style.MinWidth = Length.Pixels( 0 );
			img.Style.Height = Length.Pixels( 232 );
			img.Style.MinHeight = Length.Pixels( 232 );
			img.Style.FlexShrink = 0;
			img.Style.Overflow = OverflowMode.Visible;
			img.Style.BackgroundImage = tex;
			_ = img.Style.Set( "background-size", "contain" );
			_ = img.Style.Set( "background-repeat", "no-repeat" );
			_ = img.Style.Set( "background-position", "center" );
			return;
		}

		var glyphWrap = ThornsUiPanelAdd.AddChildPanel(iconBox,  "thorns-inv-inspect-hero-glyph-wrap" );
		glyphWrap.Style.FlexDirection = FlexDirection.Column;
		glyphWrap.Style.AlignItems = Align.Center;
		glyphWrap.Style.JustifyContent = Justify.Center;
		glyphWrap.Style.Width = Length.Fraction( 1f );
		glyphWrap.Style.MinHeight = Length.Pixels( 200 );
		glyphWrap.Style.FlexShrink = 0;

		var glyph = glyphWrap.AddChild( new Label(
			ThornsUiInventoryFormatting.ItemGlyph( itemId ),
			"thorns-inv-inspect-hero-glyph" ) );
		glyph.Style.PointerEvents = PointerEvents.None;
		glyph.Style.Height = Length.Pixels( 144 );
		glyph.Style.MinHeight = Length.Pixels( 144 );
		glyph.Style.LineHeight = Length.Pixels( 144 );
		glyph.Style.Overflow = OverflowMode.Visible;
	}


	static string PrettyItemType( ThornsItemRegistry.ThornsItemDefinition def ) =>
		def.ItemType switch
		{
			ThornsItemType.Resource => "Resource",
			ThornsItemType.Ammo => "Ammo",
			ThornsItemType.Consumable => "Consumable",
			ThornsItemType.Armor => "Armor",
			ThornsItemType.Tool => "Tool",
			ThornsItemType.Misc => "Misc",
			_ => def.ItemType.ToString()
		};

	static string ArmorSlotLabel( ThornsArmorSlotKind k ) =>
		k switch
		{
			ThornsArmorSlotKind.Helmet => "Head",
			ThornsArmorSlotKind.Chest => "Chest",
			ThornsArmorSlotKind.Pants => "Legs",
			_ => "—"
		};

	static string FlavorLine( ThornsItemRegistry.ThornsItemDefinition def )
	{
		var id = def.Id ?? "";
		var h = id.GetHashCode();
		var pool = new[]
		{
			$"Field notes: {def.DisplayName} is standard Thorn-issue gear.",
			"A balanced piece of kit grown from Thorn-tech.",
			"Issued for survival ops — treat the moving parts kindly."
		};
		return pool[Math.Abs( h ) % pool.Length];
	}
}
