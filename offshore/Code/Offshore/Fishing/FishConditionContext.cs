namespace Offshore;

/// <summary>
/// Snapshot of conditions at bite time — bait, clock, weather, and how far the hook is from the dock.
/// </summary>
public sealed class FishConditionContext
{
	public string BaitId { get; set; } = "worm";
	public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Day;
	public WeatherType Weather { get; set; } = WeatherType.Clear;
	public float HookX { get; set; }
	public float HookDepth { get; set; }
	public string LocationId { get; set; } = "old_dock";

	/// <summary>0 = at the pier, 1 = deep offshore band.</summary>
	public float Offshore01 { get; set; }

	public static FishConditionContext From( OffshoreGameController game, Vector3 hookPos, float hookDepth )
	{
		var p = game?.Progression;
		var offshore = OffshoreDistance.Normalize01( hookPos.x );
		return new FishConditionContext
		{
			BaitId = p?.SelectedBaitId ?? "worm",
			TimeOfDay = p?.TimeOfDay ?? TimeOfDay.Day,
			Weather = p?.Weather ?? WeatherType.Clear,
			HookX = hookPos.x,
			HookDepth = hookDepth,
			LocationId = p?.CurrentLocationId ?? "old_dock",
			Offshore01 = offshore
		};
	}
}

/// <summary>Maps world X to an offshore 0–1 factor (further from the pier → higher).</summary>
public static class OffshoreDistance
{
	/// <summary>World X where “leaving the dock” begins.</summary>
	public const float NearX = 10f;

	/// <summary>World X where offshore bonuses are fully online.</summary>
	public const float FarX = 72f;

	public static float Normalize01( float worldX )
	{
		var t = (worldX - NearX) / MathF.Max( 1f, FarX - NearX );
		return Math.Clamp( t, 0f, 1f );
	}

	/// <summary>How strongly rarity / price should lean offshore for this rarity.</summary>
	public static float RaritySpawnMultiplier( FishRarity rarity, float offshore01 )
	{
		var t = Math.Clamp( offshore01, 0f, 1f );
		// Near dock: commons dominate. Far out: rares / epics / legends take over.
		return rarity switch
		{
			FishRarity.Common => MathX.Lerp( 1.25f, 0.22f, t ),
			FishRarity.Uncommon => MathX.Lerp( 1.0f, 1.15f, t ),
			FishRarity.Rare => MathX.Lerp( 0.45f, 2.4f, t ),
			FishRarity.Epic => MathX.Lerp( 0.18f, 3.1f, t ),
			FishRarity.Legendary => MathX.Lerp( 0.06f, 3.8f, t ),
			_ => 1f
		};
	}

	/// <summary>Sell-price markup from fishing further out (stacks with rarity).</summary>
	public static float ValueMultiplier( FishRarity rarity, float offshore01 )
	{
		var t = Math.Clamp( offshore01, 0f, 1f );
		var rarityBoost = rarity switch
		{
			FishRarity.Common => 0.12f,
			FishRarity.Uncommon => 0.28f,
			FishRarity.Rare => 0.5f,
			FishRarity.Epic => 0.75f,
			FishRarity.Legendary => 1.1f,
			_ => 0.2f
		};
		return 1f + t * rarityBoost;
	}
}
