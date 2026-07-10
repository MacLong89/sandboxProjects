using System;
using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

public enum ThornsCraftingFilter
{
	All = 0,
	WeaponsAmmo = 1,
	Tool = 2,
	Armor = 3,
	MedicalSustenance = 4,
	Random = 5,
	Placeables = 6
}

/// <summary>Fills the Inventory tab crafting column from <see cref="ThornsCraftingRecipes"/>; calls <see cref="ThornsInventory.RequestCraftRecipe"/>.</summary>
static class ThornsUiShellCraftingList
{
	sealed class CraftRecipeSortRow
	{
		public ThornsCraftingRecipes.ThornsCraftRecipe Recipe;
		public string Title;
		public bool MaterialsOk;
		public bool TierOk;
		public bool CanCraft => MaterialsOk && TierOk;
	}

	/// <summary>Clickable recipe card — left-click updates the right-hand inspect column (output item), like inventory slots.</summary>
	sealed class CraftRecipeRowPanel : Panel
	{
		public Action<string> OnLeftPickInspect;
		public string OutputItemId;

		public CraftRecipeRowPanel()
		{
			Style.PointerEvents = PointerEvents.All;
		}

		public override bool WantsMouseInput() => OnLeftPickInspect is not null;

		protected override void OnMouseDown( MousePanelEvent e )
		{
			base.OnMouseDown( e );
			if ( e.MouseButton != MouseButtons.Left || string.IsNullOrWhiteSpace( OutputItemId ) )
				return;
			OnLeftPickInspect?.Invoke( OutputItemId );
		}
	}

	static readonly Color HudCraftLineOk = new( 0.62f, 0.78f, 0.55f, 1f );
	static readonly Color HudCraftLineMiss = new( 0.92f, 0.55f, 0.45f, 1f );

	public static void Rebuild(
		Panel scrollHost,
		ThornsInventory inv,
		ThornsVitals vitals,
		ThornsPlayerUpgrades upgrades,
		ThornsCraftingFilter filter,
		Action<string> onSelectOutputForInspect )
	{
		scrollHost.DeleteChildren();

		if ( !inv.IsValid() )
		{
			scrollHost.AddChild(
				new Label( "Inventory unavailable.", "thorns-tab-context-placeholder" ) );
			return;
		}

		var tier = upgrades.IsValid()
			? upgrades.GetEffectiveCraftingTier()
			: vitals.IsValid()
				? vitals.CharacterLevel
				: 1;

		var sortedRows = BuildSortedRecipeRows( inv, tier, filter );
		if ( sortedRows.Count == 0 )
		{
			scrollHost.AddChild(
				new Label( "No recipes in this category.", "thorns-tab-context-placeholder" ) );
			return;
		}

		foreach ( var sorted in sortedRows )
		{
			var recipe = sorted.Recipe;
			var title = sorted.Title;
			var tierOk = sorted.TierOk;
			var canCraft = sorted.CanCraft;

			var row = scrollHost.AddChild( new CraftRecipeRowPanel() );
			row.AddClass( $"thorns-inv-craft-recipe-{recipe.Id}" );
			row.AddClass( "thorns-inv-craft-recipe" );
			row.AddClass( canCraft ? "thorns-inv-craft-recipe--ok" : "thorns-inv-craft-recipe--blocked" );
			row.Style.FlexDirection = FlexDirection.Column;
			row.Style.MarginBottom = Length.Pixels( 8 );
			row.OutputItemId = recipe.OutputItemId;
			row.OnLeftPickInspect = onSelectOutputForInspect;

			AppendRecipeHeaderRow( row, recipe.OutputItemId, title, recipe.OutputQuantity );

			foreach ( var ing in recipe.Ingredients )
			{
				var have = inv.ClientMirrorCountItemId( ing.ItemId );
				var okLine = have >= ing.Quantity;
				var shortName = ThornsItemRegistry.ResolveDisplayName( ing.ItemId );
				var ingLbl = row.AddChild( new Label( $"{shortName}: {have} / {ing.Quantity}", "thorns-inv-craft-ing" ) );
				ingLbl.Style.FontColor = okLine ? HudCraftLineOk : HudCraftLineMiss;
				ingLbl.Style.MarginTop = 2;
				ingLbl.Style.PointerEvents = PointerEvents.None;
			}

			if ( !tierOk )
			{
				var tWarn = row.AddChild(
					new Label(
						$"Requires crafting tier ≥ {recipe.RequiredCraftingTier}",
						"thorns-inv-craft-tier-warn" ) );
				tWarn.Style.FontColor = HudCraftLineMiss;
				tWarn.Style.MarginTop = 4;
				tWarn.Style.PointerEvents = PointerEvents.None;
			}

			var rid = recipe.Id;
			var craftBtn = ThornsUiPanelAdd.AddChildPanel(row,  "thorns-inv-craft-btn" );
			craftBtn.AddClass( canCraft ? "thorns-inv-craft-btn--primary" : "thorns-inv-craft-btn--disabled" );
			craftBtn.Style.MarginTop = 6;
			craftBtn.Style.PointerEvents = PointerEvents.All;
			craftBtn.AddEventListener( "onmousedown", _ =>
			{
				if ( !canCraft )
					return;
				Log.Info( $"[Thorns][UI][Shell] Craft recipe={rid}" );
				inv.RequestCraftRecipe( rid );
			} );
			craftBtn.AddChild( new Label( canCraft ? "Craft" : "Can't craft", "thorns-inv-craft-btn-label" ) );
		}
	}

	static List<CraftRecipeSortRow> BuildSortedRecipeRows( ThornsInventory inv, int tier, ThornsCraftingFilter filter )
	{
		var rows = new List<CraftRecipeSortRow>();

		foreach ( var recipe in ThornsCraftingRecipes.All )
		{
			var outDef = ThornsItemRegistry.GetOrNull( recipe.OutputItemId );
			if ( !RecipeMatchesFilter( outDef, filter ) )
				continue;

			var materialsOk = true;
			foreach ( var ing in recipe.Ingredients )
			{
				var have = inv.ClientMirrorCountItemId( ing.ItemId );
				if ( have < ing.Quantity )
					materialsOk = false;
			}

			rows.Add( new CraftRecipeSortRow
			{
				Recipe = recipe,
				Title = ThornsItemRegistry.ResolveDisplayName( recipe.OutputItemId ),
				MaterialsOk = materialsOk,
				TierOk = tier >= recipe.RequiredCraftingTier
			} );
		}

		rows.Sort( ( a, b ) =>
		{
			var craftableCompare = b.CanCraft.CompareTo( a.CanCraft );
			if ( craftableCompare != 0 )
				return craftableCompare;

			var tierCompare = a.Recipe.RequiredCraftingTier.CompareTo( b.Recipe.RequiredCraftingTier );
			if ( tierCompare != 0 )
				return tierCompare;

			return string.Compare( a.Title, b.Title, StringComparison.OrdinalIgnoreCase );
		} );

		return rows;
	}

	static bool IsPlaceableRecipeRow( CraftRecipeSortRow row ) =>
		ThornsItemRegistry.IsPlaceableKitItem( row.Recipe.OutputItemId );

	static bool RecipeMatchesFilter( ThornsItemRegistry.ThornsItemDefinition def, ThornsCraftingFilter filter )
	{
		if ( filter == ThornsCraftingFilter.All )
			return true;

		return filter switch
		{
			ThornsCraftingFilter.WeaponsAmmo => def is not null
			                                    && (def.ItemType == ThornsItemType.Weapon
			                                        || def.ItemType == ThornsItemType.Ammo),
			ThornsCraftingFilter.Tool => def is not null && def.ItemType == ThornsItemType.Tool,
			ThornsCraftingFilter.Armor => def is not null && def.ItemType == ThornsItemType.Armor,
			ThornsCraftingFilter.MedicalSustenance => def is not null && def.ItemType == ThornsItemType.Consumable,
			ThornsCraftingFilter.Random => def is null || def.ItemType == ThornsItemType.Resource,
			ThornsCraftingFilter.Placeables => def is not null && ThornsItemRegistry.IsPlaceableKitItem( def.Id ),
			_ => true
		};
	}

	static void AppendRecipeHeaderRow( Panel row, string outputItemId, string title, int outputQty )
	{
		var header = row.AddChild( new Panel() );
		header.AddClass( "thorns-inv-craft-recipe-header" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.AlignItems = Align.Center;
		header.Style.MarginBottom = Length.Pixels( 4 );
		header.Style.PointerEvents = PointerEvents.None;

		var iconHost = ThornsUiPanelAdd.AddChildPanel(header,  "thorns-inv-craft-recipe-icon" );
		iconHost.Style.FlexShrink = 0;

		var iconFg = ThornsUiPanelAdd.AddChildPanel(iconHost,  "thorns-inv-craft-recipe-icon-img" );
		iconFg.Style.PointerEvents = PointerEvents.None;

		var glyph = iconHost.AddChild( new Label( "", "thorns-inv-craft-recipe-icon-glyph" ) );
		glyph.Style.PointerEvents = PointerEvents.None;

		ThornsItemRegistry.TryGet( outputItemId, out var outDef );
		var iconPath = ThornsItemHudIcons.ResolveLoadPath( outDef, outputItemId );
		if ( ThornsItemHudIcons.TryGetToolbarTexture( iconPath, out var tex ) )
		{
			iconFg.Style.BackgroundImage = tex;
			_ = iconFg.Style.Set( "background-size", "contain" );
			_ = iconFg.Style.Set( "background-repeat", "no-repeat" );
			_ = iconFg.Style.Set( "background-position", "center" );
			glyph.Text = "";
			glyph.SetClass( "thorns-inv-craft-recipe-icon-glyph--hidden", true );
		}
		else
		{
			iconFg.Style.BackgroundImage = null;
			glyph.Text = ThornsUiInventoryFormatting.ItemGlyph( outputItemId );
			glyph.SetClass( "thorns-inv-craft-recipe-icon-glyph--hidden", false );
		}

		var titleLbl = header.AddChild(
			new Label( $"{title} ×{outputQty}", "thorns-inv-craft-recipe-title" ) );
		titleLbl.Style.FlexGrow = 1;
		titleLbl.Style.MarginBottom = 0;
		titleLbl.Style.PointerEvents = PointerEvents.None;
	}
}
