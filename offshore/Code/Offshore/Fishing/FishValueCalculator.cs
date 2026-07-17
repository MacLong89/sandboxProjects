namespace Offshore;

/// <summary>Centralized fish valuation for catch records and sales.</summary>
public static class FishValueCalculator
{
	public static float Calculate(
		FishDefinition def,
		float size,
		float weight,
		FishConditionContext conditions = null,
		float interactionValueMul = 1f )
	{
		if ( def is null )
			return 0f;

		var sizeNorm = 0.5f;
		if ( def.MaxSize > def.MinSize )
			sizeNorm = Math.Clamp( (size - def.MinSize) / (def.MaxSize - def.MinSize), 0f, 1f );

		var weightNorm = 0.5f;
		if ( def.MaxWeight > def.MinWeight )
			weightNorm = Math.Clamp( (weight - def.MinWeight) / (def.MaxWeight - def.MinWeight), 0f, 1f );

		var rarityMult = def.Rarity switch
		{
			FishRarity.Common => 1f,
			FishRarity.Uncommon => 1.35f,
			FishRarity.Rare => 1.8f,
			FishRarity.Epic => 2.4f,
			FishRarity.Legendary => 3.5f,
			_ => 1f
		};

		var scale = 0.75f + 0.55f * sizeNorm + 0.35f * weightNorm;
		var offshoreMul = conditions is not null
			? OffshoreDistance.ValueMultiplier( def.Rarity, conditions.Offshore01 )
			: 1f;
		var interactMul = Math.Clamp( interactionValueMul, 0.85f, 2.5f );

		return MathF.Max( 1f, MathF.Round( def.BaseValue * rarityMult * scale * offshoreMul * interactMul ) );
	}
}
