namespace Terraingen.Player;

using Terraingen.GameData;

/// <summary>Max caps and drain/restore modifiers from survival skill ranks.</summary>
public static class ThornsPlayerSurvivalStats
{
	public const float BaseCap = 100f;

	const float MaxBonusPerRank = 5f;
	const float EfficiencyPerRank = 0.05f;
	/// <summary>Global tuning — hunger/thirst bars drain at half the original rate, then scaled by <see cref="SurvivalDepletionMultiplier"/>.</summary>
	const float BaseHungerThirstDrainScale = 0.5f;
	/// <summary>Survival drain tuning (0.8 = 20% slower hunger, thirst, and starvation health loss).</summary>
	public const float SurvivalDepletionMultiplier = 0.8f;
	const float HungerThirstDrainMultiplier = BaseHungerThirstDrainScale * SurvivalDepletionMultiplier;
	public const float StarvationDamagePerSecond = 4f * SurvivalDepletionMultiplier;

	/// <summary>Both hunger and thirst must exceed this fraction of max to passively heal.</summary>
	public const float WellFedHealthRegenThreshold = 0.70f;

	public static bool IsWellFedAndHydrated( float food, float maxFood, float water, float maxWater )
	{
		if ( maxFood <= 0.01f || maxWater <= 0.01f )
			return false;

		return food / maxFood > WellFedHealthRegenThreshold
		       && water / maxWater > WellFedHealthRegenThreshold;
	}

	public static float HealthRegenPerSecond( ThornsSkillsSnapshotDto skills ) =>
		1.25f + Rank( skills, "weathered" ) * 0.15f;

	public static int Rank( ThornsSkillsSnapshotDto skills, string skillId )
	{
		if ( skills?.Ranks is null )
			return 0;

		return skills.Ranks.FirstOrDefault( r =>
			string.Equals( r.SkillId, skillId, StringComparison.OrdinalIgnoreCase ) )?.Rank ?? 0;
	}

	public static float MaxHealth( ThornsSkillsSnapshotDto skills ) =>
		BaseCap + Rank( skills, "weathered" ) * MaxBonusPerRank;

	public static float MaxFood( ThornsSkillsSnapshotDto skills ) =>
		BaseCap + Rank( skills, "iron_gut" ) * MaxBonusPerRank;

	public static float MaxWater( ThornsSkillsSnapshotDto skills ) =>
		BaseCap + Rank( skills, "hydration" ) * MaxBonusPerRank;

	public static float MaxStamina( ThornsSkillsSnapshotDto skills ) =>
		BaseCap + Rank( skills, "endurance" ) * 8f;

	public static float FoodDrainPerSecond( ThornsSkillsSnapshotDto skills )
	{
		var mult = 1f - Rank( skills, "iron_gut" ) * EfficiencyPerRank;
		return 0.32f * HungerThirstDrainMultiplier * Math.Max( 0.25f, mult );
	}

	public static float WaterDrainPerSecond( ThornsSkillsSnapshotDto skills )
	{
		var mult = 1f - Rank( skills, "hydration" ) * EfficiencyPerRank;
		return 0.42f * HungerThirstDrainMultiplier * Math.Max( 0.25f, mult );
	}

	public static float RestoreFood( ThornsSkillsSnapshotDto skills, float baseAmount )
	{
		var mult = 1f + Rank( skills, "iron_gut" ) * EfficiencyPerRank;
		return baseAmount * mult;
	}

	public static float RestoreWater( ThornsSkillsSnapshotDto skills, float baseAmount )
	{
		var mult = 1f + Rank( skills, "hydration" ) * EfficiencyPerRank;
		return baseAmount * mult;
	}

	public static float StaminaDrainPerSecond( ThornsSkillsSnapshotDto skills )
	{
		var mult = 1f - Rank( skills, "endurance" ) * EfficiencyPerRank;
		return 18f * Math.Max( 0.35f, mult );
	}

	public static float StaminaRegenPerSecond( ThornsSkillsSnapshotDto skills )
	{
		var mult = 1f + Rank( skills, "endurance" ) * 0.01f;
		return 14f * mult;
	}
}
