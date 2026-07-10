namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Floating item icon locked to the cursor during inventory drag-drop.</summary>
public static class ThornsInventoryDragGhost
{
	const int GhostSizePx = ThornsUiMetrics.MenuDragGhost;

	static Panel _basis;
	static Panel _coordPanel;
	static Panel _root;
	static Panel _icon;
	static Label _count;

	/// <summary>
	/// Parent for the ghost and basis for <see cref="Panel.ScreenPositionToPanelDelta"/>.
	/// Must match the panel subtree used for pointer hit-testing (menu overlay when open, root otherwise).
	/// </summary>
	public static void EnsureHost( Panel basis, Panel coordPanel = null )
	{
		if ( basis is null || !basis.IsValid )
			return;

		coordPanel ??= basis;
		_basis = basis;
		_coordPanel = coordPanel;

		if ( _root is not null && _root.IsValid && _root.Parent == basis )
			return;

		Destroy();

		_root = new Panel();
		_root.Parent = basis;
		_root.AddClass( "thorns-drag-ghost" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Width = Length.Pixels( GhostSizePx );
		_root.Style.Height = Length.Pixels( GhostSizePx );
		_root.Style.MarginLeft = Length.Pixels( 0 );
		_root.Style.MarginTop = Length.Pixels( 0 );
		_root.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.Apply( _root, ThornsUiPriority.CriticalPopup );
		_root.Style.Display = DisplayMode.None;

		_icon = ThornsUiFactory.AddPanel( _root, "drag-ghost-icon slot-icon" );
		_icon.Style.Width = Length.Percent( 100 );
		_icon.Style.Height = Length.Percent( 100 );
		_icon.Style.PointerEvents = PointerEvents.None;

		_count = ThornsUiFactory.AddLabel( _root, "", "drag-ghost-count" );
		_count.Style.Position = PositionMode.Absolute;
		_count.Style.Right = Length.Pixels( 2 );
		_count.Style.Bottom = Length.Pixels( 0 );
		_count.Style.FontSize = Length.Pixels( 12 );
		_count.Style.PointerEvents = PointerEvents.None;
	}

	public static void Tick()
	{
		if ( _root is null || !_root.IsValid )
			return;

		if ( !ThornsDragState.IsDragging )
		{
			Hide();
			return;
		}

		_root.Style.Display = DisplayMode.Flex;
		UpdatePosition();
		UpdateIcon();
	}

	public static void Hide()
	{
		if ( _root is null || !_root.IsValid )
			return;

		_root.Style.Display = DisplayMode.None;
	}

	public static void Destroy()
	{
		if ( _root is not null && _root.IsValid )
			_root.Delete();

		_basis = null;
		_coordPanel = null;
		_root = null;
		_icon = null;
		_count = null;
	}

	static void UpdatePosition()
	{
		if ( _basis is null || !_basis.IsValid || _root is null || !_root.IsValid )
			return;

		var coord = _coordPanel is { IsValid: true } ? _coordPanel : _basis;
		var screen = coord.PanelPositionToScreenPosition( coord.MousePosition );
		var d = _basis.ScreenPositionToPanelDelta( screen );
		var inner = _basis.Box.RectInner;
		var w = inner.Width;
		var h = inner.Height;
		if ( w < 0.001f || h < 0.001f )
			return;

		var halfFracW = (GhostSizePx / w) * 0.5f;
		var halfFracH = (GhostSizePx / h) * 0.5f;
		_root.Style.Left = Length.Fraction( d.x - halfFracW );
		_root.Style.Top = Length.Fraction( d.y - halfFracH );
	}

	static void UpdateIcon()
	{
		var itemId = ThornsDragState.ItemId;
		if ( string.IsNullOrWhiteSpace( itemId ) )
		{
			_icon.Style.BackgroundImage = null;
			_count.Text = "";
			return;
		}

		var def = ThornsDefinitionRegistry.GetItem( itemId );
		var iconPath = def?.IconPath ?? ThornsIconManifest.ResolveItemPath( itemId );
		ThornsIconCache.ApplyToPanel( _icon, iconPath ?? "" );

		_count.Text = ThornsDragState.StackCount > 1 ? ThornsDragState.StackCount.ToString() : "";
	}
}
