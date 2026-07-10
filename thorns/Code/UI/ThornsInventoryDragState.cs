#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Shared drag state for legacy full-screen inventory (<see cref="ThornsDebugHudHost"/>).
/// Server moves still go through inventory RPCs only.
/// </summary>
public static class ThornsInventoryDragState
{
	public static int? FromInventorySlot { get; private set; }
	public static int? FromArmorSlot { get; private set; }
	public static InventorySlotPanel FromPanel { get; private set; }
	public static DragIconPanel DragIcon { get; private set; }

	public static bool IsDragging => FromInventorySlot.HasValue || FromArmorSlot.HasValue;

	public static void BeginInventoryDrag( int slotIndex, InventorySlotPanel panel, DragIconPanel icon )
	{
		Clear();
		FromInventorySlot = slotIndex;
		FromPanel = panel;
		if ( panel is { IsValid: true } )
			panel.AddClass( "dragging" );

		if ( icon is not null )
			SetDragIcon( icon );
	}

	public static void BeginArmorDrag( int armorSlotIndex )
	{
		Clear();
		FromArmorSlot = armorSlotIndex;
	}

	public static void SetDragIcon( DragIconPanel icon )
	{
		if ( DragIcon is not null && DragIcon.IsValid && DragIcon != icon )
			DragIcon.Delete();

		DragIcon = icon;
	}

	/// <summary>Invoked when the HUD tree is rebuilt; panel references may be invalid.</summary>
	public static void NotifyPanelsDestroyed()
	{
		DragIcon = null;
		FromPanel = null;
	}

	public static void Clear()
	{
		if ( FromPanel is not null && FromPanel.IsValid )
			FromPanel.RemoveClass( "dragging" );

		FromPanel = null;
		FromInventorySlot = null;
		FromArmorSlot = null;

		if ( DragIcon is not null && DragIcon.IsValid )
			DragIcon.Delete();

		DragIcon = null;
	}
}
