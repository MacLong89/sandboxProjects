namespace Sandbox;

/// <summary>
/// Single inventory cell — THORNS_EVERYTHING_DOCUMENT fixed 38 slots; supports death crate serialization and weapon row state later.
/// </summary>
public struct ThornsInventorySlot
{
	public string ItemId;
	public int Quantity;
	public bool HasDurability;
	public float Durability;
	public string WeaponInstanceId;
	public int WeaponLoadedAmmo;
	public string WeaponRollPayload;
	public string ArmorRollPayload;

	public bool IsEmpty => string.IsNullOrWhiteSpace( ItemId ) || Quantity <= 0;

	public static ThornsInventorySlot Empty => new();

	public bool EqualsStackIdentity( ThornsInventorySlot other )
	{
		if ( ItemId != other.ItemId )
			return false;

		var a = string.IsNullOrEmpty( WeaponInstanceId ) ? "" : WeaponInstanceId;
		var b = string.IsNullOrEmpty( other.WeaponInstanceId ) ? "" : other.WeaponInstanceId;
		if ( a != b )
			return false;

		var wr = WeaponRollPayload ?? "";
		var ow = other.WeaponRollPayload ?? "";
		if ( wr != ow )
			return false;

		var ar = ArmorRollPayload ?? "";
		var oa = other.ArmorRollPayload ?? "";
		return ar == oa;
	}
}

/// <summary>Wire payload for owner-only snapshot RPC (mirror only — not authoritative).</summary>
public struct ThornsInventorySlotNet
{
	public string ItemId;
	public int Quantity;

	public readonly bool IsEmpty => Quantity <= 0 || string.IsNullOrWhiteSpace( ItemId );
	public int HasDurability;
	public float Durability;
	public string WeaponInstanceId;
	public int WeaponLoadedAmmo;
	public string WeaponRollPayload;
	public string ArmorRollPayload;
}

/// <summary>Single-slot delta for owner inventory mirror RPCs.</summary>
public struct ThornsInventorySlotChangeNet
{
	public int SlotIndex;
	public ThornsInventorySlotNet Slot;
}
