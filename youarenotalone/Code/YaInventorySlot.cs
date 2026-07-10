namespace Sandbox;

public struct YaInventorySlot
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

	public static YaInventorySlot Empty => new();

	public bool EqualsStackIdentity( YaInventorySlot other )
	{
		if ( ItemId != other.ItemId )
			return false;
		var a = string.IsNullOrEmpty( WeaponInstanceId ) ? "" : WeaponInstanceId;
		var b = string.IsNullOrEmpty( other.WeaponInstanceId ) ? "" : other.WeaponInstanceId;
		return a == b;
	}
}

public struct YaInventorySlotNet
{
	public string ItemId;
	public int Quantity;
	public int HasDurability;
	public float Durability;
	public string WeaponInstanceId;
	public int WeaponLoadedAmmo;
	public string WeaponRollPayload;
	public string ArmorRollPayload;
}
