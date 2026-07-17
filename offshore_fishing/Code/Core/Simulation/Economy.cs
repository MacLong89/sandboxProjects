namespace OffshoreFishing.Core;

public static class Economy
{
	public static int ComputeWorth( FishDef def, float sizeCm, float quality, Rarity rarity, GameContent content, GameState state )
	{
		var sizeT = InverseLerp( def.MinCm, def.MaxCm, sizeCm );
		var sizeMult = MathF.Pow( 0.75f + sizeT * 0.75f, content.Economy.SizeValueCurve );
		var rarityMult = content.Economy.RarityValueMult[(int)rarity];
		var qualityMult = content.Economy.QualityValueMultMin
			+ (content.Economy.QualityValueMultMax - content.Economy.QualityValueMultMin) * quality;

		var baitBonus = 1f;
		if ( content.TryGetItem( state.EquippedBaitId, out var bait ) )
			baitBonus += bait.ValueBonus;

		var worth = def.BaseValue * sizeMult * rarityMult * qualityMult * baitBonus;
		return Math.Max( 1, (int)MathF.Round( worth ) );
	}

	public static Rarity RollRarity( FishDef def, SeededRng rng, GameContent content, GameState state )
	{
		var bonus = 0f;
		if ( content.TryGetItem( state.EquippedBaitId, out var bait ) )
			bonus += bait.RarityBonus;
		if ( content.TryGetItem( state.EquippedHookId, out var hook ) )
			bonus += hook.RarityBonus * 0.5f;

		// Soft ceiling: start from def rarity, chance to bump.
		var rarity = def.Rarity;
		var bumpChance = 0.08f + bonus;
		while ( rarity < Rarity.Legendary && rng.Chance( bumpChance ) )
		{
			rarity++;
			bumpChance *= 0.45f;
		}

		return rarity;
	}

	public static float InverseLerp( float a, float b, float v )
	{
		if ( MathF.Abs( b - a ) < 0.0001f ) return 0.5f;
		return Math.Clamp( (v - a) / (b - a), 0f, 1f );
	}
}
