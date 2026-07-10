namespace Terraingen.GameData;

/// <summary>Maps legacy/icon-stem item ids to canonical catalog ids used by <see cref="ThornsDefinitionRegistry"/>.</summary>
public static class ThornsItemIdAliases
{
	static readonly Dictionary<string, string> LegacyToCanonical = new( StringComparer.OrdinalIgnoreCase )
	{
		["kevlar_helmet"] = "kevlar_head",
		["kevlar_vest"] = "kevlar_chest",
		["kevlar_pants"] = "kevlar_legs",
		["scrap_helmet"] = "scrap_head",
		["scrap_pants"] = "scrap_legs",
		["water_bottle"] = "water",
		["clean_water"] = "water",
		["apple"] = "food",
		["metal_ingot"] = "smelt_metal",
		["ingot"] = "smelt_metal",
		["stone_pick"] = "stone_pickaxe",
		["stone_axe"] = "stone_hatchet",
		["iron_pick"] = "iron_pickaxe",
		["iron_axe"] = "iron_hatchet",
		["metal_axe"] = "iron_hatchet",
		["metal_hatchet"] = "iron_hatchet",
		["metal_pickaxe"] = "iron_pickaxe"
	};

	static readonly Dictionary<string, string> LegacyRecipeToCanonical = new( StringComparer.OrdinalIgnoreCase )
	{
		["recipe_stone_pick"] = "recipe_stone_pickaxe",
		["recipe_stone_axe"] = "recipe_stone_hatchet",
		["recipe_iron_pick"] = "recipe_iron_pickaxe",
		["recipe_iron_axe"] = "recipe_iron_hatchet",
		["recipe_metal_axe"] = "recipe_iron_hatchet",
		["recipe_metal_hatchet"] = "recipe_iron_hatchet",
		["recipe_metal_pickaxe"] = "recipe_iron_pickaxe"
	};

	/// <summary>Extra PNG stems under <c>ui/icons/</c> when the filename differs from the catalog id.</summary>
	static readonly Dictionary<string, string[]> ItemIconExtraStems = new( StringComparer.OrdinalIgnoreCase )
	{
		["stone_hatchet"] = ["stone_axe", "hatchet"],
		["stone_pickaxe"] = ["stone_pick", "pickaxe"],
		["iron_hatchet"] = ["iron_axe", "metal_hatchet", "metal_axe", "hatchet", "axe"],
		["iron_pickaxe"] = ["iron_pick", "pickaxe", "metal_pickaxe", "pick"],
		["bed_kit"] = ["bed"],
		["campfire_kit"] = ["campfire"],
		["storage_chest_kit"] = ["storage_chest", "chest"],
		["workbench_kit"] = ["workbench"],
		["research_kit"] = ["research", "research_station"],
		["smelt_metal"] = ["metal_ingot", "ingot", "metal"],
		["metal_ore"] = ["ore"],
		["animal_hide"] = ["hide", "leather"],
		["leather_scrap"] = ["leather", "scrap"],
		["raw_meat"] = ["meat"],
		["scrap_chest"] = ["scrap", "chestplate", "chest"],
		["scrap_head"] = ["scrap_helmet", "helmet"],
		["scrap_legs"] = ["scrap_pants", "pants", "legs"],
		["kevlar_head"] = ["kevlar_helmet", "helmet"],
		["kevlar_chest"] = ["kevlar_vest", "vest", "chestplate"],
		["kevlar_legs"] = ["kevlar_pants", "pants", "legs"],
		["wood_foundation"] = ["foundation"],
		["wood_wall"] = ["wall"],
		["wood_doorframe"] = ["doorframe", "door"],
		["rifle_ammo"] = ["ammo", "rifle_ammo"],
		["smg_ammo"] = ["smg_ammo", "ammo"],
		["shotgun_ammo"] = ["shotgun_ammo", "shells", "ammo"],
		["sniper_ammo"] = ["sniper_ammo", "ammo"],
		["food"] = ["apple"],
		["water"] = ["water_bottle", "bottle"],
		["storage_chest"] = ["chest"],
		["attachment_holo"] = ["holo_sight", "holo"],
		["attachment_ranged"] = ["ranged_sight", "ranged"],
		["attachment_red_dot"] = ["raised_red_dot_sight", "red_dot", "reddot"],
		["attachment_extended_mag"] = ["extended_mag"],
		["attachment_foregrip_angled"] = ["angled_foregrip"],
		["attachment_suppressor"] = ["suppressor"]
	};

	/// <summary>Catalog attachment item id → legacy PNG stem under <c>ui/icons/</c>.</summary>
	public static readonly Dictionary<string, string> AttachmentItemIdToIconStem = new( StringComparer.OrdinalIgnoreCase )
	{
		["attachment_holo"] = "holo_sight",
		["attachment_ranged"] = "ranged_sight",
		["attachment_red_dot"] = "raised_red_dot_sight",
		["attachment_extended_mag"] = "extended_mag",
		["attachment_foregrip_angled"] = "angled_foregrip",
		["attachment_suppressor"] = "suppressor"
	};

	public static readonly string[] AttachmentIconOnlyStems =
	[
		"holo_sight",
		"ranged_sight",
		"raised_red_dot_sight",
		"extended_mag",
		"angled_foregrip",
		"suppressor"
	];

	public static string AttachmentIconStem( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "";

		var id = itemId.Trim();
		return AttachmentItemIdToIconStem.TryGetValue( id, out var stem ) ? stem : id;
	}

	public static string AttachmentLegacyIconPath( string itemId )
	{
		var stem = AttachmentIconStem( itemId );
		return string.IsNullOrWhiteSpace( stem ) ? "" : $"ui/icons/{stem}.png";
	}

	public static bool IsAttachmentIconOnlyStem( string discoveredId )
	{
		if ( string.IsNullOrWhiteSpace( discoveredId ) )
			return false;

		var normalizedId = NormalizeAttachmentDiscoveryStem( discoveredId );
		foreach ( var stem in AttachmentIconOnlyStems )
		{
			if ( string.Equals( discoveredId, stem, StringComparison.OrdinalIgnoreCase ) )
				return true;

			if ( string.Equals( normalizedId, NormalizeAttachmentDiscoveryStem( stem ), StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	static string NormalizeAttachmentDiscoveryStem( string stem ) =>
		new string( (stem ?? "").Where( char.IsLetterOrDigit ).ToArray() );

	public static string Canonicalize( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return itemId ?? "";

		var trimmed = itemId.Trim();
		return LegacyToCanonical.TryGetValue( trimmed, out var canonical ) ? canonical : trimmed;
	}

	public static string CanonicalizeRecipeId( string recipeId )
	{
		if ( string.IsNullOrWhiteSpace( recipeId ) )
			return recipeId ?? "";

		var trimmed = recipeId.Trim();
		return LegacyRecipeToCanonical.TryGetValue( trimmed, out var canonical ) ? canonical : trimmed;
	}

	/// <summary>PNG filename stems to try under <c>ui/icons/</c> (legacy filenames first, then canonical id).</summary>
	public static IEnumerable<string> IconLookupStems( string canonicalItemId )
	{
		if ( string.IsNullOrWhiteSpace( canonicalItemId ) )
			yield break;

		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var stem in CollectIconLookupStems( canonicalItemId ) )
		{
			if ( seen.Add( stem ) )
				yield return stem;
		}
	}

	public static IEnumerable<string> IconLookupKeys( string canonicalItemId )
	{
		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var stem in IconLookupStems( canonicalItemId ) )
		{
			foreach ( var key in IconKeyVariants( stem ) )
			{
				if ( seen.Add( key ) )
					yield return key;
			}
		}
	}

	public static string PreferredIconStem( string canonicalItemId )
	{
		foreach ( var stem in IconLookupStems( canonicalItemId ) )
			return stem;

		return canonicalItemId ?? "";
	}

	public static ThornsItemStack CanonicalizeStack( ThornsItemStack stack )
	{
		if ( stack.IsEmpty )
			return stack;

		var id = Canonicalize( stack.ItemId );
		if ( string.Equals( id, stack.ItemId, StringComparison.Ordinal ) )
			return stack;

		return new ThornsItemStack
		{
			ItemId = id,
			Count = stack.Count,
			HasDurability = stack.HasDurability,
			Durability = stack.Durability,
			WeaponInstanceId = stack.WeaponInstanceId,
			WeaponLoadedAmmo = stack.WeaponLoadedAmmo,
			AttachmentId0 = stack.AttachmentId0,
			AttachmentId1 = stack.AttachmentId1,
			AttachmentId2 = stack.AttachmentId2,
			ItemTier = stack.ItemTier,
			StatRoll = stack.StatRoll
		};
	}

	static IEnumerable<string> CollectIconLookupStems( string canonicalItemId )
	{
		foreach ( var pair in LegacyToCanonical )
		{
			if ( string.Equals( pair.Value, canonicalItemId, StringComparison.OrdinalIgnoreCase ) )
				yield return pair.Key;
		}

		yield return canonicalItemId;

		foreach ( var stem in ExpandStemVariants( canonicalItemId ) )
			yield return stem;

		if ( ItemIconExtraStems.TryGetValue( canonicalItemId, out var extras ) )
		{
			foreach ( var extra in extras )
				yield return extra;
		}
	}

	static IEnumerable<string> ExpandStemVariants( string stem )
	{
		if ( string.IsNullOrWhiteSpace( stem ) )
			yield break;

		if ( stem.EndsWith( "_kit", StringComparison.OrdinalIgnoreCase ) )
			yield return stem[..^4];

		if ( stem.EndsWith( "_scrap", StringComparison.OrdinalIgnoreCase ) )
			yield return stem[..^6];

		if ( stem.Contains( "_pick", StringComparison.OrdinalIgnoreCase )
		     && !stem.Contains( "pickaxe", StringComparison.OrdinalIgnoreCase ) )
			yield return stem.Replace( "_pick", "_pickaxe", StringComparison.OrdinalIgnoreCase );

		if ( stem.Contains( "hatchet", StringComparison.OrdinalIgnoreCase ) )
			yield return stem.Replace( "hatchet", "axe", StringComparison.OrdinalIgnoreCase );

		if ( stem.StartsWith( "iron_", StringComparison.OrdinalIgnoreCase ) )
			yield return $"metal_{stem[5..]}";

		if ( stem.StartsWith( "metal_", StringComparison.OrdinalIgnoreCase ) )
			yield return $"iron_{stem[6..]}";

		var parts = stem.Split( '_', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length > 1 )
		{
			yield return parts[^1];
			if ( parts.Length >= 2 )
				yield return string.Join( '_', parts.TakeLast( 2 ) );
		}
	}

	internal static IEnumerable<string> IconKeyVariants( string stem )
	{
		if ( string.IsNullOrWhiteSpace( stem ) )
			yield break;

		yield return NormalizeIconKey( stem );

		var compact = stem.Replace( "_", "", StringComparison.Ordinal );
		if ( !string.Equals( compact, stem, StringComparison.Ordinal ) )
			yield return NormalizeIconKey( compact );

		if ( stem.EndsWith( "_icon", StringComparison.OrdinalIgnoreCase ) )
			yield return NormalizeIconKey( stem[..^5] );
	}

	internal static string NormalizeIconKey( string stem )
	{
		var s = stem.Trim().ToLowerInvariant();
		foreach ( var affix in new[] { "_icon", "icon_", "_item", "item_" } )
		{
			if ( s.EndsWith( affix, StringComparison.Ordinal ) )
				s = s[..^affix.Length];
			if ( s.StartsWith( affix, StringComparison.Ordinal ) )
				s = s[affix.Length..];
		}

		return new string( s.Where( char.IsLetterOrDigit ).ToArray() );
	}
}
