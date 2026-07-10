namespace Terraingen.World;

using Terraingen.Buildings;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;

/// <summary>Random weapon, armor, and ammo rolls for supply drops.</summary>
public static class ThornsAirdropLootTables
{
	public const float AttachmentRollChance = 0.15f;

	static readonly string[] Weapons = { "m4", "mp5", "shotgun", "sniper", "usp", "m9_bayonet" };
	static readonly string[] Armor =
	[
		"scrap_chest", "scrap_head", "scrap_legs", "kevlar_head", "kevlar_chest", "kevlar_legs"
	];

	static readonly (string Id, int Min, int Max)[] AmmoPools =
	[
		( "rifle_ammo", 24, 60 ),
		( "smg_ammo", 30, 72 ),
		( "shotgun_ammo", 12, 28 ),
		( "sniper_ammo", 10, 24 ),
		( "pistol_ammo", 18, 48 )
	];

	public static HashSet<string> CollectAllPossibleItemIds()
	{
		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var weapon in Weapons )
			ids.Add( weapon );

		foreach ( var armor in Armor )
			ids.Add( armor );

		foreach ( var pool in AmmoPools )
			ids.Add( pool.Id );

		foreach ( var attachmentId in ThornsAttachmentItemIds.EnabledInGame )
			ids.Add( attachmentId );

		return ids;
	}

	public static List<ThornsBuildingLoot> Roll( Random rng )
	{
		var result = new List<ThornsBuildingLoot>( 5 );
		var rollCount = rng.Next( 3, 6 );

		for ( var i = 0; i < rollCount; i++ )
		{
			var pick = rng.NextSingle();
			if ( pick < 0.15f )
				TryAddAttachment( result, rng );
			else if ( pick < 0.50f )
				TryAddAmmo( result, rng );
			else if ( pick < 0.80f )
				TryAddWeapon( result, rng );
			else
				TryAddArmor( result, rng );
		}

		EnsureAtLeastOneWeapon( result, rng );

		return result;
	}

	static void TryAddAttachment( List<ThornsBuildingLoot> result, Random rng )
	{
		var itemId = ThornsWeaponAttachmentRoll.RollLooseAttachmentItemId( rng );
		if ( !IsValidItem( itemId ) || Contains( result, itemId ) )
			return;

		result.Add( new ThornsBuildingLoot( itemId, 1 ) );
	}

	static void EnsureAtLeastOneWeapon( List<ThornsBuildingLoot> result, Random rng )
	{
		if ( ContainsWeapon( result ) )
			return;

		for ( var attempt = 0; attempt < Weapons.Length * 2; attempt++ )
		{
			var id = Weapons[rng.Next( Weapons.Length )];
			if ( !IsValidItem( id ) || Contains( result, id ) )
				continue;

			result.Add( new ThornsBuildingLoot( id, 1 ) );
			return;
		}

		foreach ( var id in Weapons )
		{
			if ( !IsValidItem( id ) )
				continue;

			result.Add( new ThornsBuildingLoot( id, 1 ) );
			return;
		}
	}

	static bool ContainsWeapon( List<ThornsBuildingLoot> result )
	{
		for ( var i = 0; i < result.Count; i++ )
		{
			var def = ThornsDefinitionRegistry.GetItem( result[i].ItemId );
			if ( def?.Category == ThornsItemCategory.Weapon )
				return true;
		}

		return false;
	}

	static void TryAddAmmo( List<ThornsBuildingLoot> result, Random rng )
	{
		var pool = AmmoPools[rng.Next( AmmoPools.Length )];
		if ( !IsValidItem( pool.Id ) )
			pool = AmmoPools[0];

		var count = rng.Next( pool.Min, pool.Max + 1 );
		MergeOrAdd( result, pool.Id, count );
	}

	static void TryAddWeapon( List<ThornsBuildingLoot> result, Random rng )
	{
		for ( var attempt = 0; attempt < 6; attempt++ )
		{
			var id = Weapons[rng.Next( Weapons.Length )];
			if ( !IsValidItem( id ) )
				continue;

			if ( !Contains( result, id ) )
				result.Add( new ThornsBuildingLoot( id, 1 ) );

			if ( rng.NextSingle() < AttachmentRollChance )
				TryAddAttachment( result, rng );

			return;
		}

		TryAddAmmo( result, rng );
	}

	static void TryAddArmor( List<ThornsBuildingLoot> result, Random rng )
	{
		for ( var attempt = 0; attempt < 6; attempt++ )
		{
			var id = Armor[rng.Next( Armor.Length )];
			if ( !IsValidItem( id ) )
				continue;

			if ( !Contains( result, id ) )
				result.Add( new ThornsBuildingLoot( id, 1 ) );

			return;
		}

		TryAddAmmo( result, rng );
	}

	static void MergeOrAdd( List<ThornsBuildingLoot> result, string itemId, int count )
	{
		for ( var i = 0; i < result.Count; i++ )
		{
			if ( result[i].ItemId != itemId )
				continue;

			var entry = result[i];
			result[i] = new ThornsBuildingLoot( itemId, entry.Count + count );
			return;
		}

		result.Add( new ThornsBuildingLoot( itemId, count ) );
	}

	static bool Contains( List<ThornsBuildingLoot> result, string itemId )
	{
		for ( var i = 0; i < result.Count; i++ )
		{
			if ( result[i].ItemId == itemId )
				return true;
		}

		return false;
	}

	static bool IsValidItem( string itemId ) =>
		!string.IsNullOrWhiteSpace( itemId ) && ThornsDefinitionRegistry.GetItem( itemId ) is not null;
}
