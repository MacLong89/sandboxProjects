#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>Single inventory cell for <see cref="ThornsDebugHudHost"/> backpack / toolbar when full inventory is open.</summary>
public sealed class InventorySlotPanel : Panel
{
	public int SlotIndex { get; set; }
	public Action<int> MouseDownSlot { get; set; }
	public Action<int> MouseUpSlot { get; set; }
	public Action<int> MouseRightClickSlot { get; set; }
	public Action<int> MouseEnterSlot { get; set; }
	public Action<int> MouseLeaveSlot { get; set; }

	public override bool WantsMouseInput() => true;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton == MouseButtons.Left )
			MouseDownSlot?.Invoke( SlotIndex );
		else if ( e.MouseButton == MouseButtons.Right )
			MouseRightClickSlot?.Invoke( SlotIndex );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		if ( e.MouseButton == MouseButtons.Left )
			MouseUpSlot?.Invoke( SlotIndex );
	}

	protected override void OnMouseOver( MousePanelEvent e )
	{
		base.OnMouseOver( e );
		MouseEnterSlot?.Invoke( SlotIndex );
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		MouseLeaveSlot?.Invoke( SlotIndex );
	}
}
