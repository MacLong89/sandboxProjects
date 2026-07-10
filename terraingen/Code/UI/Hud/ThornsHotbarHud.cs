namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

public sealed class ThornsHotbarHud
{
	readonly Panel _root;
	readonly Panel _slotsRow;
	readonly ThornsHudXpStrip _xpStrip;
	readonly List<ThornsItemSlot> _slots = new();

	public ThornsHotbarHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "hotbar-hud" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Bottom = Length.Pixels( ThornsHudTheme.HotbarBottomPx );
		_root.Style.Left = Length.Percent( 50 );
		_root.Style.MarginLeft = Length.Pixels( ThornsHudTheme.HotbarMarginLeftPx );
		_root.Style.Width = Length.Pixels( ThornsHudTheme.HotbarRootWidthPx );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.AlignItems = Align.Center;

		_slotsRow = ThornsUiFactory.AddPanel( _root, "hotbar-slots hotbar-slots-clean" );
		_slotsRow.Style.FlexDirection = FlexDirection.Row;
		_slotsRow.Style.JustifyContent = Justify.Center;

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
			_slots.Add( new ThornsItemSlot( _slotsRow, ThornsContainerKind.Hotbar, i, Refresh, isHotbar: true ) );

		var xpWrap = ThornsUiFactory.AddPanel( _root, "hotbar-xp-wrap" );
		xpWrap.Style.Width = Length.Pixels( ThornsHudTheme.HotbarXpWidthPx );
		xpWrap.Style.MaxWidth = Length.Pixels( ThornsHudTheme.HotbarXpWidthPx );
		_xpStrip = new ThornsHudXpStrip( xpWrap );
	}

	public void Refresh()
	{
		var buildMenuOpen = ThornsPlayerBuildingController.Local?.BuildMenuOpen == true;
		var hideForScopedAds = ThornsSniperScopeHudState.HideGameplayHotbar;
		_root.Style.Display = buildMenuOpen || hideForScopedAds ? DisplayMode.None : DisplayMode.Flex;
		if ( buildMenuOpen || hideForScopedAds )
			return;

		foreach ( var slot in _slots )
			slot.Refresh();

		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var skills = ThornsUiClientState.Snapshot.Skills ?? new();
		var level = Math.Max( 1, skills.PlayerLevel );

		var totalXp = Math.Max( 0, skills.TotalXp );
		var floor = (level - 1) * ThornsPlayerGameplay.XpPerLevel;
		var inLevel = totalXp - floor;
		var toNext = ThornsPlayerGameplay.XpPerLevel;
		_xpStrip.Set( inLevel, toNext );

		HighlightActiveSlot();
	}

	void HighlightActiveSlot()
	{
		var active = 0;
		if ( ThornsUiClientState.HasSnapshot )
		{
			var inventory = ThornsUiClientState.Snapshot.Inventory;
			if ( inventory is not null )
				active = Math.Clamp( inventory.ActiveHotbarIndex, 0, _slots.Count - 1 );
		}

		for ( var i = 0; i < _slots.Count; i++ )
			_slots[i].SetSelected( i == active );
	}
}
