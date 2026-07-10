namespace Terraingen.World;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Loot table picks for enemy death crates (animals and bandits).</summary>
public static class ThornsEnemyLootTables
{
	public const float BloomedGunDropChance = 0.20f;
	public const int EpicWeaponTier = 3;
	public const int LegendaryWeaponTier = 4;

	static readonly string[] NormalAnimalDrops = { "raw_meat", "water", "cloth", "animal_hide" };

	/// <summary>Normal animals drop one random resource from raw meat, water, or cloth. Bloomed bosses may bonus-roll epic/legendary guns.</summary>
	public static (List<ThornsItemStack> Items, string Title) BuildAnimalLoot( ThornsAnimalBrain brain )
	{
		var species = brain?.Species;
		var objectSeed = brain is null ? 0 : brain.GameObject.Id.GetHashCode();
		var seed = HashCode.Combine( brain?.SpeciesId ?? 0, objectSeed, (int)(Time.Now * 1000 ) );
		var rng = new Random( seed );
		var stacks = new List<ThornsItemStack>( 2 );

		var itemId = NormalAnimalDrops[rng.Next( NormalAnimalDrops.Length )];
		MergeOrAdd( stacks, itemId, RollNormalAnimalCount( itemId, rng ) );

		if ( brain?.IsBloomed == true && rng.NextSingle() < BloomedGunDropChance
		     && TryRollBloomedGun( rng, out var gunStack ) )
			stacks.Add( gunStack );

		var display = species is null || string.IsNullOrWhiteSpace( species.DisplayName )
			? "Animal"
			: species.DisplayName;
		var title = brain?.IsBloomed == true ? $"Bloomed {display} Loot" : $"{display} Loot";
		return ( stacks, title );
	}

	public static (string LootTable, int LootSeed, string Title) ResolveBandit( ThornsBanditBrain brain )
	{
		var seed = HashCode.Combine( (int)brain.BanditType, brain.GroupId, brain.GameObject.Id.GetHashCode(), (int)(Time.Now * 1000) );
		var lootTable = brain.BanditType switch
		{
			ThornsBanditType.CityDefender => Pick( seed, "military_armory", "military_ammo", "military_gear" ),
			ThornsBanditType.AirdropDefender => Pick( seed, "military_gear", "military_ammo", "military_medical" ),
			_ => Pick( seed, "salvage_pile", "ruin_scrap", "home_clutter" )
		};

		return ( lootTable, seed, "Bandit Loot" );
	}

	public const float BanditAttachmentDropChance = 0.22f;

	public static List<ThornsItemStack> RollStacks( string lootTable, int lootSeed )
	{
		var rng = new Random( lootSeed );
		var rolls = ThornsBuildingLootTables.Roll( lootTable, rng ).ToList();
		if ( rolls.Count > 0 )
			return ConvertToStacks( rolls, lootTable, rng );

		rolls = ThornsBuildingLootTables.Roll( "kitchen_fridge", rng ).ToList();
		return ConvertToStacks( rolls, "kitchen_fridge", rng );
	}

	static List<ThornsItemStack> ConvertToStacks( List<ThornsBuildingLoot> loot, string lootTable, Random rng )
	{
		var stacks = new List<ThornsItemStack>( loot.Count + 1 );
		foreach ( var entry in loot )
		{
			if ( string.IsNullOrWhiteSpace( entry.ItemId ) || entry.Count <= 0 )
				continue;

			var stack = new ThornsItemStack { ItemId = entry.ItemId.Trim(), Count = entry.Count };
			if ( ThornsItemRegistry.TryGet( stack.ItemId, out var def ) && ThornsItemTier.SupportsTiering( def ) )
			{
				var rollMul = IsMilitaryTable( lootTable ) ? 1.25f : 1f;
				ThornsInventoryWeaponState.PrepareWorldLootStack( ref stack, rng, rollMul, IsMilitaryTable( lootTable ) );
			}

			stacks.Add( stack );
		}

		if ( IsMilitaryTable( lootTable ) && rng.NextSingle() < BanditAttachmentDropChance )
		{
			var attachmentId = ThornsWeaponAttachmentRoll.RollLooseAttachmentItemId( rng );
			if ( ThornsItemRegistry.TryGet( attachmentId, out _ ) )
				stacks.Add( new ThornsItemStack { ItemId = attachmentId, Count = 1 } );
		}

		return stacks;
	}

	static bool IsMilitaryTable( string lootTable ) => lootTable is
		"military_armory" or "military_ammo" or "military_gear" or "military_medical"
		or "military_briefing" or "military_crate" or "military_intel" or "military_locker"
		or "military_mess" or "military" or "weapons" or "ammo";

	static string Pick( int seed, params string[] options )
	{
		if ( options is null or { Length: 0 } )
			return "kitchen_fridge";

		var index = Math.Abs( seed ) % options.Length;
		return options[index];
	}

	static int RollNormalAnimalCount( string itemId, Random rng ) => itemId switch
	{
		"cloth" or "animal_hide" => rng.Next( 2, 7 ),
		"raw_meat" or "water" => rng.Next( 1, 4 ),
		_ => 1
	};

	static bool TryRollBloomedGun( Random rng, out ThornsItemStack stack )
	{
		stack = default;
		var pool = GetAllInventoryGunIds();
		if ( pool.Count == 0 )
			return false;

		var gunId = pool[rng.Next( pool.Count )];
		if ( !ThornsItemRegistry.TryGet( gunId, out var def ) )
			return false;

		stack = new ThornsItemStack
		{
			ItemId = gunId,
			Count = 1,
			ItemTier = rng.Next( 2 ) == 0 ? EpicWeaponTier : LegendaryWeaponTier
		};
		stack.StatRoll = ThornsItemTier.RollStatRollForTier( rng, stack.ItemTier );
		ThornsInventoryWeaponState.PrepareWorldLootStack( ref stack, rng, 1.5f, premiumTable: true );
		return true;
	}

	static List<string> GetAllInventoryGunIds()
	{
		ThornsDefinitionRegistry.EnsureInitialized();
		var result = new List<string>( 8 );
		foreach ( var item in ThornsDefinitionRegistry.AllItems.Values )
		{
			if ( item.Category == ThornsItemCategory.Weapon )
				result.Add( item.Id );
		}

		return result;
	}

	static List<string> GetInventoryGunIdsByTier( int weaponTier )
	{
		ThornsDefinitionRegistry.EnsureInitialized();
		var result = new List<string>( 4 );
		foreach ( var item in ThornsDefinitionRegistry.AllItems.Values )
		{
			if ( item.Category != ThornsItemCategory.Weapon )
				continue;

			var combatId = string.IsNullOrWhiteSpace( item.CombatWeaponDefinitionId )
				? item.Id
				: item.CombatWeaponDefinitionId;
			if ( ThornsWeaponTierVisuals.ResolveWeaponTier( combatId ) != weaponTier )
				continue;

			result.Add( item.Id );
		}

		return result;
	}

	static void MergeOrAdd( List<ThornsItemStack> stacks, string itemId, int count )
	{
		for ( var i = 0; i < stacks.Count; i++ )
		{
			if ( stacks[i].ItemId != itemId )
				continue;

			var entry = stacks[i];
			stacks[i] = new ThornsItemStack { ItemId = itemId, Count = entry.Count + count };
			return;
		}

		stacks.Add( new ThornsItemStack { ItemId = itemId, Count = count } );
	}
}
