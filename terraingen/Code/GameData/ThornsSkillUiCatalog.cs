namespace Terraingen.GameData;

/// <summary>Skills menu presentation (labels, icons, layout helpers).</summary>
public static class ThornsSkillUiCatalog
{
	public static string CategoryIconPath( ThornsSkillCategory category ) =>
		ThornsIconRegistry.SkillCategory( category );

	public static string CategoryTitle( ThornsSkillCategory category ) => category switch
	{
		ThornsSkillCategory.Persistence => "SURVIVAL",
		ThornsSkillCategory.Instinct => "COMBAT",
		ThornsSkillCategory.Industry => "BUILDING",
		_ => category.ToString().ToUpper()
	};

	public static string CategoryDescription( ThornsSkillCategory category ) => category switch
	{
		ThornsSkillCategory.Persistence => "Improve hunger, thirst, and environmental resilience.",
		ThornsSkillCategory.Instinct => "Improve combat awareness, stamina, and threat control.",
		ThornsSkillCategory.Industry => "Improve gathering, crafting, and equipment upkeep.",
		_ => "Spend skill points to specialize your survivor."
	};

	public static int CategorySpent( ThornsSkillsSnapshotDto snap, ThornsSkillCategory category )
	{
		if ( snap?.Ranks is null )
			return 0;

		return snap.Ranks
			.Where( r => ThornsDefinitionRegistry.GetSkill( r.SkillId )?.Category == category )
			.Sum( r => r.Rank );
	}

	public static int CategoryTotal( ThornsSkillCategory category ) =>
		ThornsDefinitionRegistry.AllSkills.Values.Count( s => s.Category == category );

	public static int CategoryMaxRanks( ThornsSkillCategory category ) =>
		ThornsDefinitionRegistry.AllSkills.Values
			.Where( s => s.Category == category )
			.Sum( s => s.MaxRank );

	public static string CategoryProgressText( ThornsSkillsSnapshotDto snap, ThornsSkillCategory category ) =>
		$"{CategorySpent( snap, category )}/{CategoryMaxRanks( category )}";

	public static string TierLabel( int tier ) => tier switch
	{
		1 => "TIER I",
		2 => "TIER II",
		3 => "TIER III",
		4 => "TIER IV",
		5 => "TIER V",
		_ => $"TIER {tier}"
	};

	public static bool IsLocked( ThornsSkillDefinition skill, ThornsSkillsSnapshotDto snap )
	{
		if ( skill is null || string.IsNullOrWhiteSpace( skill.PrerequisiteSkillId ) )
			return false;

		var pre = snap.Ranks.FirstOrDefault( r =>
			string.Equals( r.SkillId, skill.PrerequisiteSkillId, StringComparison.OrdinalIgnoreCase ) );
		return (pre?.Rank ?? 0) < 1;
	}

	public static int CategoryDisplayLevel( ThornsSkillsSnapshotDto snap, ThornsSkillCategory category ) =>
		Math.Max( 1, CategorySpent( snap, category ) );

	public static IEnumerable<string> CurrentBonusBullets( ThornsSkillDefinition skill, int rank )
	{
		if ( skill is null || rank <= 0 )
			yield break;

		if ( skill.RankBonuses is { Count: > 0 } && rank <= skill.RankBonuses.Count )
			yield return skill.RankBonuses[rank - 1 ];

		if ( !string.IsNullOrWhiteSpace( skill.Description ) )
			yield return skill.Description;
	}

	public static string CurrentBonus( ThornsSkillDefinition skill, int rank )
	{
		if ( skill is null || rank <= 0 )
			return "No bonus yet.";

		if ( skill.RankBonuses is { Count: > 0 } && rank <= skill.RankBonuses.Count )
			return skill.RankBonuses[rank - 1];

		return $"Rank {rank} active.";
	}

	public static string NextBonus( ThornsSkillDefinition skill, int rank )
	{
		if ( skill is null || rank >= skill.MaxRank )
			return "Max rank reached.";

		var next = rank + 1;
		if ( skill.RankBonuses is { Count: > 0 } && next <= skill.RankBonuses.Count )
			return skill.RankBonuses[next - 1];

		return $"Rank {next} bonus.";
	}

	public static string RequirementText( ThornsSkillDefinition skill, ThornsSkillsSnapshotDto snap, int cost )
	{
		if ( IsLocked( skill, snap ) )
		{
			var pre = ThornsDefinitionRegistry.GetSkill( skill.PrerequisiteSkillId );
			return $"Requires 1 rank in {pre?.DisplayName ?? skill.PrerequisiteSkillId}.";
		}

		if ( snap.AvailablePoints < cost )
			return $"Requires {cost} skill point(s). You have {snap.AvailablePoints}.";

		return $"{cost} skill point(s) to upgrade.";
	}
}
