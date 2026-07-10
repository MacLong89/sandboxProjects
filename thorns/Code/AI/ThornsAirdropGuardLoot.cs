using System;

namespace Sandbox;

/// <summary>Host drop when a human NPC bandit dies — random ammo type (uniform among registered calibers), healing, small chance of a uniformly picked rolled gun.</summary>
public static class ThornsAirdropGuardLoot
{
	static readonly string[] GuardLootAmmoCandidates =
	{
		"rifle_ammo",
		"smg_ammo",
		"shotgun_ammo",
		"sniper_ammo",
		"pistol_ammo"
	};

	static readonly string[] GuardLootGunCandidates =
	{
		"m4",
		"mp5",
		"shotgun",
		"sniper",
		"m9_bayonet"
	};

	/// <summary>Uniform among <paramref name="candidates"/> entries that exist in <see cref="ThornsItemRegistry"/>.</summary>
	static string PickRandomRegisteredItemId( Random rng, ReadOnlySpan<string> candidates )
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

	static int RollAmmoStackQuantity( Random rng, ThornsItemRegistry.ThornsItemDefinition ammoDef )
	{
		var cap = Math.Max( 1, ammoDef.MaxStack );
		// Roughly "a useful magazine worth" for any caliber — scales down for low cap (e.g. sniper boxes).
		var lo = Math.Clamp( cap / 8, 1, cap );
		var hi = Math.Clamp( cap / 3, lo + 1, cap );
		return rng.Next( lo, hi + 1 );
	}

	public static void HostTrySpawnLootCrateOnGuardKill( Scene scene, Vector3 deathWorldPosition, ThornsBanditBrain bandit )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( scene is null || !scene.IsValid() || bandit is null || !bandit.IsValid() )
			return;

		var rng = new Random(
			HashCode.Combine(
				bandit.GameObject.Name?.GetHashCode() ?? 0,
				(int)( deathWorldPosition.x * 7919f ),
				(int)( deathWorldPosition.y * 7937f ) ) );

		var grid = BuildGuardDropGrid( rng );
		if ( !ThornsWildlifeLoot.GridHasAny( grid ) )
			return;

		var pos = deathWorldPosition + Vector3.Up * 12f;
		ThornsLootCrate.SpawnHostWithGrid( scene, pos, ThornsLootCrateKind.HunterCache, grid );
	}

	static ThornsInventorySlot[] BuildGuardDropGrid( Random rng )
	{
		var grid = new ThornsInventorySlot[ThornsLootCrate.LootGridSlots];
		var ix = 0;

		void PushSlot( ThornsInventorySlot slot )
		{
			if ( ix >= grid.Length || slot.IsEmpty )
				return;
			grid[ix++] = slot;
		}

		var ammoId = PickRandomRegisteredItemId( rng, GuardLootAmmoCandidates );
		if ( !string.IsNullOrEmpty( ammoId ) && ThornsItemRegistry.TryGet( ammoId, out var ammoDef ) )
		{
			var q = RollAmmoStackQuantity( rng, ammoDef );
			PushSlot( new ThornsInventorySlot
			{
				ItemId = ammoId,
				Quantity = q,
				HasDurability = false,
				Durability = 0f,
				WeaponRollPayload = "",
				ArmorRollPayload = ""
			} );
		}

		if ( rng.NextDouble() < 0.58 )
		{
			if ( ThornsItemRegistry.TryGet( "bandage", out _ ) )
				PushSlot( new ThornsInventorySlot
				{
					ItemId = "bandage",
					Quantity = rng.Next( 1, 3 ),
					HasDurability = false,
					Durability = 0f,
					WeaponRollPayload = "",
					ArmorRollPayload = ""
				} );
		}
		else if ( ThornsItemRegistry.TryGet( "medkit_field", out _ ) )
		{
			PushSlot( new ThornsInventorySlot
			{
				ItemId = "medkit_field",
				Quantity = 1,
				HasDurability = false,
				Durability = 0f,
				WeaponRollPayload = "",
				ArmorRollPayload = ""
			} );
		}

		if ( rng.NextDouble() < 0.09 )
		{
			var gunId = PickRandomRegisteredItemId( rng, GuardLootGunCandidates );
			if ( !string.IsNullOrEmpty( gunId ) && ThornsItemRegistry.TryGet( gunId, out var witem ) )
			{
				var combatId = string.IsNullOrEmpty( witem.CombatWeaponDefinitionId ) ? witem.Id : witem.CombatWeaponDefinitionId;
				var wdef = ThornsWeaponDefinitions.Get( combatId );
				var rarity = ThornsLootRarity.Rare;
				var ( dmg, fr ) = ThornsGearRoll.RollWeaponMultipliers( rng, rarity );
				PushSlot( new ThornsInventorySlot
				{
					ItemId = witem.Id,
					Quantity = 1,
					HasDurability = true,
					Durability = wdef.MaxDurability,
					WeaponInstanceId = Guid.NewGuid().ToString( "D" ),
					WeaponLoadedAmmo = wdef.ClipSize,
					WeaponRollPayload = ThornsGearRoll.EncodeWeapon( rarity, dmg, fr ),
					ArmorRollPayload = ""
				} );
			}
		}

		return grid;
	}
}
