namespace Terraingen.UI;

using Terraingen.GameData;
using Terraingen.UI.Core;

/// <summary>Client-only drag and drop state for inventory UI.</summary>
public static class ThornsDragState
{
	static Vector2 _pressPosition;
	static bool _suppressNextClick;

	public static bool IsDragging { get; private set; }
	public static bool PointerMoved { get; private set; }
	public static ThornsContainerKind FromContainer { get; private set; }
	public static int FromIndex { get; private set; }
	public static string ItemId { get; private set; } = "";
	public static int StackCount { get; private set; }

	public static void Begin( ThornsContainerKind container, int index, string itemId, int stackCount = 1 )
	{
		IsDragging = true;
		PointerMoved = false;
		_suppressNextClick = false;
		_pressPosition = Mouse.Position;
		FromContainer = container;
		FromIndex = index;
		ItemId = itemId ?? "";
		StackCount = Math.Max( 1, stackCount );
		ClearWeaponAttachmentDrag();
		ThornsAttachmentDragDebug.Write( $"Begin drag {container}[{index}] item={ItemId}" );
	}

	public static void UpdatePointer()
	{
		if ( !IsDragging || PointerMoved )
			return;

		if ( (Mouse.Position - _pressPosition).Length > 4f )
			PointerMoved = true;
	}

	public static void MarkDropHandled() => _suppressNextClick = true;

	public static bool ConsumeClickSuppression()
	{
		if ( !_suppressNextClick )
			return false;

		_suppressNextClick = false;
		return true;
	}

	public static void Clear()
	{
		IsDragging = false;
		PointerMoved = false;
		StackCount = 0;
		ItemId = "";
		ClearWeaponAttachmentDrag();
	}

	public static bool IsWeaponAttachmentDrag { get; private set; }
	public static ThornsContainerKind WeaponContainer { get; private set; }
	public static int WeaponIndex { get; private set; }
	public static int WeaponAttachmentSlotIndex { get; private set; } = -1;

	public static void BeginWeaponAttachment(
		ThornsContainerKind weaponContainer,
		int weaponIndex,
		int attachmentSlotIndex,
		string itemId )
	{
		IsDragging = true;
		PointerMoved = false;
		_suppressNextClick = false;
		_pressPosition = Mouse.Position;
		FromContainer = weaponContainer;
		FromIndex = weaponIndex;
		ItemId = itemId ?? "";
		StackCount = 1;
		IsWeaponAttachmentDrag = true;
		WeaponContainer = weaponContainer;
		WeaponIndex = weaponIndex;
		WeaponAttachmentSlotIndex = attachmentSlotIndex;
	}

	public static void ClearWeaponAttachmentDrag()
	{
		IsWeaponAttachmentDrag = false;
		WeaponContainer = default;
		WeaponIndex = -1;
		WeaponAttachmentSlotIndex = -1;
	}
}
