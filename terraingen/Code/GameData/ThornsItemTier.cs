namespace Terraingen.GameData;

/// <summary>Instance tier (1–4) for tools, weapons, and armor — independent of item type.</summary>
public static class ThornsItemTier
{
	public const int MinTier = 1;
	public const int MaxTier = 4;

	public static bool SupportsTiering( ThornsItemDefinition def ) =>
		def?.Category is ThornsItemCategory.Weapon or ThornsItemCategory.Tool or ThornsItemCategory.Armor;

	public static int ResolveTier( in ThornsItemStack stack, ThornsItemDefinition def = null )
	{
		if ( stack.ItemTier is >= MinTier and <= MaxTier )
			return stack.ItemTier;

		if ( def is null && !string.IsNullOrWhiteSpace( stack.ItemId ) )
			ThornsItemRegistry.TryGet( stack.ItemId, out def );

		return MinTier;
	}

	public static float ResolveStatRoll( in ThornsItemStack stack )
	{
		if ( stack.StatRoll <= 0.001f )
			return 0.5f;

		return Math.Clamp( stack.StatRoll, 0f, 1f );
	}

	public static float ResolveStatMultiplier( in ThornsItemStack stack, ThornsItemDefinition def = null )
	{
		var tier = ResolveTier( stack, def );
		var roll = ResolveStatRoll( stack );
		var (min, max) = StatRollBand( tier );
		return min + (max - min) * roll;
	}

	public static (float Min, float Max) StatRollBand( int tier ) => tier switch
	{
		2 => (0.95f, 1.08f),
		3 => (1.02f, 1.18f),
		4 => (1.10f, 1.28f),
		_ => (0.90f, 1.00f)
	};

	public static int RollLootTier( Random rng, bool premiumTable = false )
	{
		var roll = rng.NextSingle();
		if ( premiumTable )
		{
			if ( roll < 0.15f ) return 4;
			if ( roll < 0.50f ) return 3;
			if ( roll < 0.80f ) return 2;
			return 1;
		}

		if ( roll < 0.06f ) return 4;
		if ( roll < 0.26f ) return 3;
		if ( roll < 0.58f ) return 2;
		return 1;
	}

	public static float RollStatRollForTier( Random rng, int tier )
	{
		var bias = tier switch
		{
			4 => 0.72f,
			3 => 0.58f,
			2 => 0.45f,
			_ => 0.30f
		};
		return Math.Clamp( bias + rng.NextSingle() * (1f - bias), 0f, 1f );
	}

	public static void ApplyCraftDefaults( ref ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( !SupportsTiering( def ) )
			return;

		stack.ItemTier = MinTier;
		stack.StatRoll = 0f;
		ApplyTierScaledDurability( ref stack, def );
	}

	public static void ApplyLootRoll( ref ThornsItemStack stack, ThornsItemDefinition def, Random rng, bool premiumTable = false )
	{
		if ( !SupportsTiering( def ) )
			return;

		if ( stack.ItemTier is < MinTier or > MaxTier )
		{
			stack.ItemTier = RollLootTier( rng, premiumTable );
			stack.StatRoll = RollStatRollForTier( rng, stack.ItemTier );
		}
		else if ( stack.StatRoll <= 0.001f )
		{
			stack.StatRoll = RollStatRollForTier( rng, stack.ItemTier );
		}

		ApplyTierScaledDurability( ref stack, def );
	}

	public static bool CanUpgrade( in ThornsItemStack stack, ThornsItemDefinition def )
		=> SupportsTiering( def ) && ResolveTier( stack, def ) < MaxTier;

	public static bool CanRepair( in ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( !SupportsTiering( def ) || !stack.HasDurability )
			return false;

		var max = ResolveMaxDurability( stack, def );
		return max > 0.001f && stack.Durability < max * 0.995f;
	}

	public static bool IsWorkbenchServiceable( in ThornsItemStack stack, ThornsItemDefinition def )
		=> SupportsTiering( def ) && ( CanUpgrade( stack, def ) || CanRepair( stack, def ) );

	public static float ResolveMaxDurability( in ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( def is null || !SupportsTiering( def ) )
			return 0f;

		var mult = ResolveStatMultiplier( stack, def );

		if ( def.Category == ThornsItemCategory.Tool && def.ToolMaxDurability > 0.001f )
			return def.ToolMaxDurability * mult;

		if ( def.Category != ThornsItemCategory.Weapon )
			return 0f;

		var combatId = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId )
			? def.Id
			: def.CombatWeaponDefinitionId;
		var wdef = Terraingen.Combat.ThornsWeaponDefinitions.Get( combatId );
		return wdef.MaxDurability > 0.001f ? wdef.MaxDurability * mult : 0f;
	}

	public static IReadOnlyList<ThornsRecipeIngredient> GetRepairCost( in ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( !CanRepair( stack, def ) )
			return Array.Empty<ThornsRecipeIngredient>();

		var max = ResolveMaxDurability( stack, def );
		var missing = Math.Clamp( 1f - stack.Durability / max, 0.02f, 1f );
		var tier = ResolveTier( stack, def );
		var metal = Math.Max( 1, (int)MathF.Ceiling( missing * (2f + tier) ) );
		var cloth = Math.Max( 1, (int)MathF.Ceiling( missing * (1f + tier * 0.5f) ) );

		var costs = new List<ThornsRecipeIngredient>
		{
			new() { ItemId = "smelt_metal", Count = metal },
			new() { ItemId = "cloth", Count = cloth }
		};

		if ( tier >= 3 )
			costs.Add( new ThornsRecipeIngredient { ItemId = "leather_scrap", Count = Math.Max( 1, tier - 2 ) } );

		return costs;
	}

	public static void ApplyRepair( ref ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( !CanRepair( stack, def ) )
			return;

		ApplyTierScaledDurability( ref stack, def, refill: true );
	}

	public static int NextTier( in ThornsItemStack stack, ThornsItemDefinition def )
		=> Math.Min( ResolveTier( stack, def ) + 1, MaxTier );

	public static IReadOnlyList<ThornsRecipeIngredient> GetUpgradeCost( int fromTier )
	{
		return fromTier switch
		{
			1 =>
			[
				new ThornsRecipeIngredient { ItemId = "smelt_metal", Count = 5 },
				new ThornsRecipeIngredient { ItemId = "cloth", Count = 3 }
			],
			2 =>
			[
				new ThornsRecipeIngredient { ItemId = "smelt_metal", Count = 10 },
				new ThornsRecipeIngredient { ItemId = "cloth", Count = 6 },
				new ThornsRecipeIngredient { ItemId = "leather_scrap", Count = 2 }
			],
			3 =>
			[
				new ThornsRecipeIngredient { ItemId = "smelt_metal", Count = 15 },
				new ThornsRecipeIngredient { ItemId = "cloth", Count = 10 },
				new ThornsRecipeIngredient { ItemId = "leather_scrap", Count = 4 },
				new ThornsRecipeIngredient { ItemId = "stone", Count = 8 }
			],
			_ => Array.Empty<ThornsRecipeIngredient>()
		};
	}

	public static void ApplyUpgrade( ref ThornsItemStack stack, ThornsItemDefinition def, Random rng = null )
	{
		if ( !CanUpgrade( stack, def ) )
			return;

		stack.ItemTier = NextTier( stack, def );
		rng ??= Random.Shared;
		stack.StatRoll = RollStatRollForTier( rng, stack.ItemTier );
		ApplyTierScaledDurability( ref stack, def, refill: true );
	}

	public static bool StacksMatchForMerge( in ThornsItemStack a, in ThornsItemStack b, ThornsItemDefinition def )
	{
		if ( !SupportsTiering( def ) )
			return true;

		return ResolveTier( a, def ) == ResolveTier( b, def )
		       && Math.Abs( ResolveStatRoll( a ) - ResolveStatRoll( b ) ) < 0.001f;
	}

	public static float ResolveArmorProtection( in ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( def?.Category != ThornsItemCategory.Armor )
			return 0f;

		var baseProtection = def.ArmorProtection > 0f ? def.ArmorProtection : 0.05f;
		return baseProtection * ResolveStatMultiplier( stack, def );
	}

	public static void ApplyTierScaledDurability( ref ThornsItemStack stack, ThornsItemDefinition def, bool refill = true )
	{
		if ( def is null || !SupportsTiering( def ) )
			return;

		var mult = ResolveStatMultiplier( stack, def );

		if ( def.Category == ThornsItemCategory.Tool && def.ToolMaxDurability > 0.001f )
		{
			stack.HasDurability = true;
			var max = def.ToolMaxDurability * mult;
			if ( refill || stack.Durability <= 0.001f )
				stack.Durability = max;
			else
				stack.Durability = Math.Min( stack.Durability, max );
			return;
		}

		if ( def.Category != ThornsItemCategory.Weapon )
			return;

		var combatId = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId )
			? def.Id
			: def.CombatWeaponDefinitionId;
		var wdef = Terraingen.Combat.ThornsWeaponDefinitions.Get( combatId );
		if ( wdef.MaxDurability <= 0.001f )
			return;

		stack.HasDurability = true;
		var weaponMax = wdef.MaxDurability * mult;
		if ( refill || stack.Durability <= 0.001f )
			stack.Durability = weaponMax;
		else
			stack.Durability = Math.Min( stack.Durability, weaponMax );
	}
}
