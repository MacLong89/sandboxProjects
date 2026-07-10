namespace Terraingen.UI.Core;



using Sandbox.UI;

using Terraingen.Combat;

using Terraingen.GameData;

using Terraingen.Player;

using Terraingen.UI;



/// <summary>Structured item inspector rows for the inventory tab.</summary>

public static class ThornsInventoryInspectUi

{

	public static void Populate(

		Panel statsRoot,

		Panel metaRoot,

		Label tierLabel,

		ThornsItemDefinition def,

		ThornsInventorySlotDto inspectDto,

		ThornsItemStack stack,

		bool useWeaponBarLayout = false )

	{

		statsRoot?.DeleteChildren( true );

		metaRoot?.DeleteChildren( true );



		if ( statsRoot is null || !statsRoot.IsValid )

			return;



		if ( tierLabel is not null && tierLabel.IsValid )

		{

			tierLabel.Text = "";

			tierLabel.Style.Display = DisplayMode.None;

		}



		if ( def is null )

			return;



		if ( !stack.IsEmpty && stack.Count > 1 && !useWeaponBarLayout )

			AddStatRow( statsRoot, "Stack", $"×{stack.Count}", emphasize: false );



		var tier = ResolveInspectTier( inspectDto, stack, def );

		if ( tier > 0 && ThornsItemTier.SupportsTiering( def ) )

		{

			PopulateTierLabel( tierLabel, tier );

			var quality = (int)(ThornsItemTier.ResolveStatMultiplier( stack, def ) * 100f);

			AddStatRow( statsRoot, "Tier", ThornsWeaponTierVisuals.TierDisplayName( tier ), emphasize: true );

			AddStatRow( statsRoot, "Quality", $"{quality}%", emphasize: false );

		}



		if ( def.Category == ThornsItemCategory.Weapon && inspectDto is not null )

		{

			if ( useWeaponBarLayout )

			{

				foreach ( var row in ThornsWeaponInspectFormatting.BuildWeaponInspectBarRows( def, inspectDto, stack ) )

					AddStatBarRow( statsRoot, row.StatKey, row.ValueText, row.Fill01 );

			}

			else

			{

				foreach ( var row in ThornsWeaponInspectFormatting.BuildWeaponStatRows( def, inspectDto, stack ) )

				{

					if ( string.Equals( row.Label, "Tier", StringComparison.OrdinalIgnoreCase ) )

						continue;



					AddStatRow( statsRoot, row.Label, row.Value, row.Emphasize );

				}

			}

		}

		else if ( def.Category == ThornsItemCategory.Tool )

		{

			if ( def.ToolMaxDurability > 0.001f )

			{

				var max = def.ToolMaxDurability * ThornsItemTier.ResolveStatMultiplier( stack, def );

				AddStatRow( statsRoot, "Durability", $"{stack.Durability:F0} / {max:F0}" );

			}



			if ( def.HarvestToolKind != ThornsHarvestToolKind.None )

				AddStatRow( statsRoot, "Type", def.HarvestToolKind.ToString() );

		}

		else if ( def.Category == ThornsItemCategory.Armor )

		{

			var protection = (int)(ThornsItemTier.ResolveArmorProtection( stack, def ) * 100f );

			AddStatRow( statsRoot, "Protection", $"{protection}%" );

		}



		if ( !useWeaponBarLayout && !string.IsNullOrWhiteSpace( def.Description ) )

			AddDescription( statsRoot, def.Description );



		PopulateMeta( metaRoot, def );

	}



	static int ResolveInspectTier( ThornsInventorySlotDto inspectDto, in ThornsItemStack stack, ThornsItemDefinition def )

	{

		if ( inspectDto?.ItemTier > 0 )

			return inspectDto.ItemTier;



		if ( inspectDto?.WeaponTier > 0 )

			return inspectDto.WeaponTier;



		return ThornsItemTier.ResolveTier( stack, def );

	}



	static void PopulateTierLabel( Label tierLabel, int tier )

	{

		if ( tierLabel is null || !tierLabel.IsValid )

			return;



		tierLabel.Text = ThornsWeaponTierVisuals.TierName( tier ).ToUpperInvariant();

		tierLabel.Style.Display = DisplayMode.Flex;

		tierLabel.Style.FontColor = ThornsWeaponTierVisuals.TitleTint( tier );

	}



	public static void PopulateEmpty( Panel statsRoot, Panel metaRoot, Label tierLabel, string message )

	{

		statsRoot?.DeleteChildren( true );

		metaRoot?.DeleteChildren( true );

		if ( tierLabel is not null && tierLabel.IsValid )

		{

			tierLabel.Text = "";

			tierLabel.Style.Display = DisplayMode.None;

		}



		if ( statsRoot is null || !statsRoot.IsValid )

			return;



		var hint = ThornsUiFactory.AddPassiveLabel( statsRoot, message, "inspect-empty-hint thorns-muted" );

		hint.Style.WhiteSpace = WhiteSpace.Normal;

		hint.Style.LineHeight = Length.Pixels( 20 );

	}



	static void PopulateMeta( Panel metaRoot, ThornsItemDefinition def )

	{

		if ( metaRoot is null || !metaRoot.IsValid || def is null )

			return;



		var parts = new List<string> { def.Category.ToString() };

		if ( def.WeightKg > 0f )

			parts.Add( $"{def.WeightKg:0.##} kg" );

		if ( def.EquipSlot != ThornsEquipSlot.None )

			parts.Add( def.EquipSlot.ToString() );

		if ( def.MaxStack > 1 )

			parts.Add( $"Stacks to {def.MaxStack}" );



		var meta = ThornsUiFactory.AddPassiveLabel( metaRoot, string.Join( " · ", parts ), "inspect-meta-line thorns-muted" );

		meta.Style.WhiteSpace = WhiteSpace.NoWrap;

		meta.Style.Overflow = OverflowMode.Hidden;

		meta.Style.TextOverflow = TextOverflow.Ellipsis;

		meta.Style.FontSize = Length.Pixels( 10 );

		meta.Style.FontWeight = 400;

	}



	static void AddStatRow( Panel parent, string label, string value, bool emphasize = false )

	{

		var row = ThornsUiFactory.AddPanel( parent, "inspect-stat-row" );

		row.Style.FlexDirection = FlexDirection.Row;

		row.Style.JustifyContent = Justify.SpaceBetween;

		row.Style.AlignItems = Align.Center;

		row.Style.FlexShrink = 0;

		row.Style.MinHeight = Length.Pixels( 24 );

		row.Style.PaddingTop = Length.Pixels( 2 );

		row.Style.PaddingBottom = Length.Pixels( 2 );



		var labelEl = ThornsUiFactory.AddPassiveLabel( row, label, "inspect-stat-label thorns-muted" );

		labelEl.Style.FlexShrink = 0;

		labelEl.Style.MarginRight = Length.Pixels( 16 );



		var valueEl = ThornsUiFactory.AddPassiveLabel( row, value, emphasize ? "inspect-stat-value inspect-stat-emphasis" : "inspect-stat-value" );

		valueEl.Style.TextAlign = TextAlign.Right;

		valueEl.Style.FlexGrow = 1;

		valueEl.Style.FlexShrink = 1;

		valueEl.Style.WhiteSpace = WhiteSpace.Normal;

	}



	static void AddStatBarRow( Panel parent, string key, string valueText, float fill01 )

	{

		var row = ThornsUiFactory.AddPanel( parent, "inspect-stat-bar-row" );

		row.Style.FlexDirection = FlexDirection.Row;

		row.Style.AlignItems = Align.Center;

		row.Style.FlexShrink = 0;

		row.Style.MinHeight = Length.Pixels( 18 );

		row.Style.MarginBottom = Length.Pixels( 2 );

		row.Style.Overflow = OverflowMode.Hidden;

		row.Style.Width = Length.Percent( 100 );



		var labelEl = ThornsUiFactory.AddPassiveLabel( row, key, "inspect-stat-bar-key thorns-muted" );

		labelEl.Style.Width = Length.Pixels( 64 );

		labelEl.Style.MinWidth = Length.Pixels( 64 );

		labelEl.Style.MaxWidth = Length.Pixels( 64 );

		labelEl.Style.FlexShrink = 0;

		labelEl.Style.FontSize = Length.Pixels( 9 );

		labelEl.Style.Overflow = OverflowMode.Hidden;

		labelEl.Style.TextOverflow = TextOverflow.Ellipsis;

		labelEl.Style.WhiteSpace = WhiteSpace.NoWrap;



		var track = ThornsUiFactory.AddPanel( row, "inspect-stat-bar-track" );

		track.Style.FlexGrow = 1;

		track.Style.FlexShrink = 1;

		track.Style.Height = Length.Pixels( 6 );

		track.Style.MarginLeft = Length.Pixels( 4 );

		track.Style.MarginRight = Length.Pixels( 6 );

		track.Style.MinWidth = Length.Pixels( 24 );

		track.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.35f );

		track.Style.Overflow = OverflowMode.Hidden;



		var fill = ThornsUiFactory.AddPanel( track, "inspect-stat-bar-fill" );

		fill.Style.Height = Length.Percent( 100 );

		fill.Style.Width = Length.Fraction( Math.Clamp( fill01, 0.04f, 1f ) );

		fill.Style.BackgroundColor = new Color( 0.72f, 0.58f, 0.28f, 0.95f );



		var valueEl = ThornsUiFactory.AddPassiveLabel( row, valueText, "inspect-stat-bar-value" );

		valueEl.Style.FlexShrink = 0;

		valueEl.Style.Width = Length.Pixels( 52 );

		valueEl.Style.MinWidth = Length.Pixels( 52 );

		valueEl.Style.MaxWidth = Length.Pixels( 52 );

		valueEl.Style.TextAlign = TextAlign.Right;

		valueEl.Style.FontSize = Length.Pixels( 9 );

		valueEl.Style.FontWeight = 700;

		valueEl.Style.Overflow = OverflowMode.Hidden;

		valueEl.Style.TextOverflow = TextOverflow.Ellipsis;

		valueEl.Style.WhiteSpace = WhiteSpace.NoWrap;

	}



	static void AddDescription( Panel parent, string text )

	{

		var block = ThornsUiFactory.AddPanel( parent, "inspect-description-block" );

		block.Style.MarginTop = Length.Pixels( 10 );

		block.Style.FlexShrink = 0;



		var label = ThornsUiFactory.AddPassiveLabel( block, text, "inspect-description thorns-muted" );

		label.Style.WhiteSpace = WhiteSpace.Normal;

		label.Style.LineHeight = Length.Pixels( 18 );

	}

}


