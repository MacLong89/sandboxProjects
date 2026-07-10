namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.Buildings;
using Terraingen.UI.Core;

public sealed class ThornsBuildMenuHud
{
	readonly Panel _root;
	readonly Label _title;
	readonly Label _status;
	readonly List<Panel> _slots = new();

	public Panel Root => _root;

	public ThornsBuildMenuHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "build-menu-hud thorns-hud-glass" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Bottom = Length.Pixels( ThornsHudSafeZones.Scaled( 108 ) );
		_root.Style.Left = Length.Percent( 50 );
		_root.Style.MarginLeft = Length.Pixels( -310 );
		_root.Style.Width = Length.Pixels( 620 );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.PointerEvents = PointerEvents.All;
		ThornsUiLayer.Apply( _root, ThornsUiPriority.Hotbar );

		_title = ThornsUiFactory.AddLabel( _root, "BUILD", "build-menu-title" );

		var row = ThornsUiFactory.AddPanel( _root, "build-menu-row" );
		row.Style.FlexDirection = FlexDirection.Row;

		foreach ( var entry in ThornsPlayerBuildingDefinitions.Toolbar )
		{
			var slot = ThornsUiFactory.AddClickable( row, "build-menu-slot", () => ThornsPlayerBuildingController.Local?.SelectSlot( entry.SlotIndex ) );
			ThornsUiFactory.AddLabel( slot, entry.Glyph, "build-menu-glyph" );
			ThornsUiFactory.AddLabel( slot, $"{entry.SlotIndex + 1}", "build-menu-key" );
			ThornsUiFactory.AddLabel( slot, entry.Label, "build-menu-label" );
			_slots.Add( slot );
		}

		_status = ThornsUiFactory.AddLabel( _root, "", "build-menu-status" );
		Refresh();
	}

	public void Refresh()
	{
		var controller = ThornsPlayerBuildingController.Local;
		var open = controller is not null && controller.BuildMenuOpen;
		_root.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;
		if ( !open )
			return;

		for ( var i = 0; i < _slots.Count; i++ )
			_slots[i].SetClass( "selected", controller.SelectedSlot == i );

		if ( controller.SelectedToolKind != ThornsPlayerBuildToolKind.Place )
		{
			_title.Text = controller.SelectedToolKind == ThornsPlayerBuildToolKind.Remove ? "BUILD: REMOVE" : "BUILD: UPGRADE";
			_status.Text = controller.ModifyStatus;
			_status.SetClass( "invalid", !controller.ModifyTargetValid );
			return;
		}

		var preview = controller.CurrentPreview;
		var def = preview?.Definition;
		_title.Text = def is null ? "BUILD" : $"BUILD: {def.DisplayName}";
		_status.Text = preview is null || preview.Valid
			? "Q/R rotate  |  Left click place"
			: $"Cannot place: {preview.Reason}";
		_status.SetClass( "invalid", preview is not null && !preview.Valid );
	}
}
