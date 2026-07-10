namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed class ThornsLootFeedHud
{
	readonly Panel _root;

	Action<UiRevisionChannel, int> _onRevision;

	public ThornsLootFeedHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "loot-feed-hud" );
		_root.Style.Position = PositionMode.Absolute;
		if ( ThornsHudClassicChrome.IsActive )
			_root.AddClass( "loot-feed-hud-classic" );

		ApplySafePosition();
		_root.Style.Width = Length.Pixels( ThornsHudTheme.LootFeedMaxWidthPx );
		_root.Style.MaxWidth = Length.Pixels( ThornsHudTheme.LootFeedMaxWidthPx );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.AlignItems = Align.FlexStart;
		_root.Style.JustifyContent = Justify.FlexStart;
		_root.Style.Overflow = OverflowMode.Hidden;
		_root.Style.ZIndex = ThornsUiLayer.ZIndex( ThornsUiPriority.Toast );

		_onRevision = OnRevision;
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel == UiRevisionChannel.LootFeed )
			Refresh();
	}

	static Label AddLootFeedLabel( Panel row, string text )
	{
		var label = ThornsUiFactory.AddLabel( row, text, "loot-feed-name" );
		label.Style.FlexGrow = 0;
		label.Style.FlexShrink = 0;
		label.Style.WhiteSpace = WhiteSpace.NoWrap;
		label.Style.Overflow = OverflowMode.Visible;
		return label;
	}

	void ApplySafePosition()
	{
		_root.Style.Left = Length.Pixels( ThornsHudSafeZones.LeftMiddleColumnLeftPx );
		_root.Style.Right = Length.Auto;
		_root.Style.Bottom = Length.Auto;
		_root.Style.Top = Length.Pixels( ThornsHudSafeZones.LeftMiddleLootFeedTopPx );
	}

	public void Refresh()
	{
		if ( !_root.IsValid )
			return;

		ApplySafePosition();
		_root.DeleteChildren( true );

		var entries = ThornsLootFeedBus.Active;
		if ( entries.Count == 0 )
			return;

		if ( entries.Count > 1 )
		{
			var header = ThornsUiFactory.AddLabel( _root, "Collected", "loot-feed-header thorns-muted" );
			header.Style.MarginBottom = Length.Pixels( 4 );
			header.Style.FontSize = Length.Pixels( 11 );
			header.Style.LetterSpacing = Length.Pixels( 1 );
		}

		foreach ( var entry in entries )
		{
			var row = ThornsUiFactory.AddPanel( _root, "loot-feed-row" );
			ThornsHudTheme.ApplyHudGlass( row );
			row.Style.Width = Length.Auto;
			row.Style.MaxWidth = Length.Pixels( ThornsHudTheme.LootFeedMaxWidthPx );
			row.Style.Overflow = OverflowMode.Hidden;
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.JustifyContent = Justify.FlexStart;
			row.Style.FlexShrink = 0;
			row.Style.FlexGrow = 0;

			var icon = ThornsUiFactory.AddPanel( row, "loot-feed-icon" );
			icon.Style.Width = Length.Pixels( 28 );
			icon.Style.Height = Length.Pixels( 28 );
			icon.Style.FlexGrow = 0;
			icon.Style.FlexShrink = 0;

			if ( entry.Kind == ThornsLootFeedKind.Tame )
			{
				ThornsIconCache.ApplyToPanel( icon, ThornsTameCatalog.CreaturePortraitPath( entry.SpeciesKey ), addSlotIconClass: false );
				var tameLabel = AddLootFeedLabel(
					row,
					$"+{entry.Count} {ThornsTameCatalog.FormatTamePickupLabel( entry.Tier, entry.SpeciesName )}" );
				tameLabel.Style.FontColor = ThornsTameCatalog.TierColor( entry.Tier );
				continue;
			}

			var def = ThornsDefinitionRegistry.GetItem( entry.ItemId );
			var title = string.IsNullOrWhiteSpace( def?.DisplayName ) ? entry.ItemId : def.DisplayName;
			var iconPath = def?.IconPath ?? ThornsIconManifest.ResolveItemPath( entry.ItemId );
			ThornsIconCache.ApplyToPanel( icon, iconPath ?? "", addSlotIconClass: false );
			AddLootFeedLabel( row, $"+{entry.Count} {title}" );
		}
	}
}
