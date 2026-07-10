using System;

namespace Sandbox;

/// <summary>Host-only drops when wild (untamed) wildlife dies — small Hunter-cache style crate; bosses may drop a legendary rolled weapon.</summary>
public static class ThornsWildlifeLoot
{
	static readonly string[] BossLegendaryGunCandidates =
	{
		"m4",
		"mp5",
		"shotgun",
		"sniper",
		"m9_bayonet"
	};

	public static void HostTrySpawnLootCrateOnWildlifeKill( Scene scene, Vector3 deathWorldPosition, ThornsWildlifeIdentity wid )
	{
		if ( !Networking.IsHost || scene is null || !scene.IsValid() )
			return;

		if ( wid is null || !wid.IsValid() || wid.HostIsTamed )
			return;

		var rng = new Random( HashCode.Combine( wid.Species, wid.WildlifeId, (int)(deathWorldPosition.x * 1000f) ) );
		var grid = BuildHarvestGrid( wid.Species, wid.IsBossWildlifeSync, rng );
		if ( !GridHasAny( grid ) )
			return;

		var pos = deathWorldPosition + Vector3.Up * 12f;
		ThornsLootCrate.SpawnHostWithGrid( scene, pos, ThornsLootCrateKind.HunterCache, grid );
	}

	public static bool GridHasAny( ThornsInventorySlot[] grid )
	{
		if ( grid is null )
			return false;
		foreach ( var s in grid )
		{
			if ( !s.IsEmpty )
				return true;
		}

		return false;
	}

	/// <summary>Uniform among <paramref name="candidates"/> entries that exist in <see cref="ThornsItemRegistry"/>.</summary>
	static string PickRandomRegisteredGunId( Random rng, ReadOnlySpan<string> candidates )
	{
		Span<int> ok = stackalloc int[candidates.Length];
		var n = 0;
		for ( var i = 0; i < candidates.Length; i++ )
		{
			if ( ThornsItemRegistry.TryGet( candidates[i], out _ ) )
				ok[n++] = i;
		}

		if ( n <= 0 )
			return null;

		return candidates[ok[rng.Next( n )]];
	}

	static bool TryPushLegendaryRolledGun( Random rng, ThornsInventorySlot[] grid, ref int ix )
	{
		if ( ix >= grid.Length )
			return false;

		var gunId = PickRandomRegisteredGunId( rng, BossLegendaryGunCandidates );
		if ( string.IsNullOrEmpty( gunId ) || !ThornsItemRegistry.TryGet( gunId, out var witem ) )
			return false;

		var combatId = string.IsNullOrEmpty( witem.CombatWeaponDefinitionId ) ? witem.Id : witem.CombatWeaponDefinitionId;
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		var rarity = ThornsLootRarity.Legendary;
		var ( dmg, fr ) = ThornsGearRoll.RollWeaponMultipliers( rng, rarity );
		grid[ix++] = new ThornsInventorySlot
		{
			ItemId = witem.Id,
			Quantity = 1,
			HasDurability = true,
			Durability = wdef.MaxDurability,
			WeaponInstanceId = Guid.NewGuid().ToString( "D" ),
			WeaponLoadedAmmo = wdef.ClipSize,
			WeaponRollPayload = ThornsGearRoll.EncodeWeapon( rarity, dmg, fr ),
			ArmorRollPayload = ""
		};
		return true;
	}

	static ThornsInventorySlot[] BuildHarvestGrid( ThornsWildlifeSpeciesKind species, bool isBossWildlife, Random rng )
	{
		var grid = new ThornsInventorySlot[ThornsLootCrate.LootGridSlots];
		var ix = 0;

		void Push( string itemId, int qty )
		{
			if ( ix >= grid.Length || qty <= 0 )
				return;
			if ( !ThornsItemRegistry.TryGet( itemId, out _ ) )
				return;

			qty = Math.Clamp( qty, 1, 2 );
			grid[ix++] = new ThornsInventorySlot
			{
				ItemId = itemId,
				Quantity = qty,
				HasDurability = false,
				Durability = 0f,
				WeaponRollPayload = "",
				ArmorRollPayload = ""
			};
		}

		Push( "raw_meat", rng.Next( 1, 3 ) );

		var addSecond = rng.NextDouble() < 0.62;
		if ( addSecond && ix < grid.Length )
		{
			var u = rng.NextDouble();
			switch ( species )
			{
				case ThornsWildlifeSpeciesKind.Rabbit:
					if ( u < 0.55 )
						Push( "animal_hide", 1 );
					break;
				case ThornsWildlifeSpeciesKind.Deer:
				case ThornsWildlifeSpeciesKind.Elk:
				case ThornsWildlifeSpeciesKind.Moose:
				case ThornsWildlifeSpeciesKind.Bison:
					if ( u < 0.45 )
						Push( "bone_fragments", rng.Next( 1, 3 ) );
					else
						Push( "animal_hide", rng.Next( 1, 3 ) );
					break;
				case ThornsWildlifeSpeciesKind.Boar:
					if ( u < 0.5 )
						Push( "bone_fragments", rng.Next( 1, 3 ) );
					else
						Push( "animal_hide", rng.Next( 1, 3 ) );
					break;
				case ThornsWildlifeSpeciesKind.Wolf:
				case ThornsWildlifeSpeciesKind.Fox:
				case ThornsWildlifeSpeciesKind.Cougar:
				case ThornsWildlifeSpeciesKind.Panther:
					if ( u < 0.62 )
						Push( "bone_fragments", rng.Next( 1, 3 ) );
					else
						Push( "animal_hide", rng.Next( 1, 3 ) );
					break;
				case ThornsWildlifeSpeciesKind.Bear:
					if ( u < 0.55 )
						Push( "bone_fragments", rng.Next( 1, 3 ) );
					else
						Push( "animal_hide", rng.Next( 1, 3 ) );
					break;
				default:
					if ( u < 0.4 )
						Push( "animal_hide", 1 );
					break;
			}
		}

		if ( isBossWildlife && rng.NextDouble() < 0.40 )
			TryPushLegendaryRolledGun( rng, grid, ref ix );

		return grid;
	}
}
