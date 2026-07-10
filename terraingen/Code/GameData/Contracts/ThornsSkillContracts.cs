namespace Terraingen.GameData;

public enum ThornsSkillCategory : byte
{
	Persistence,
	Instinct,
	Industry
}

public sealed class ThornsSkillDefinition
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Description { get; set; } = "";
	public ThornsSkillCategory Category { get; set; }
	public int Tier { get; set; } = 1;
	public int MaxRank { get; set; } = 10;
	public int BasePointCost { get; set; } = 2;
	public string PrerequisiteSkillId { get; set; } = "";
	public string IconPath { get; set; } = "";
	public List<string> RankBonuses { get; set; } = new();
}

public sealed class ThornsSkillRankDto
{
	public string SkillId { get; set; } = "";
	public int Rank { get; set; }
}

public sealed class ThornsSkillsSnapshotDto
{
	public ThornsSkillCategory ActiveCategory { get; set; } = ThornsSkillCategory.Persistence;
	public string SelectedSkillId { get; set; } = "";
	public int PlayerLevel { get; set; } = 1;
	public int TotalXp { get; set; }
	public int AvailablePoints { get; set; }
	public int SpentPoints { get; set; }
	public List<ThornsSkillRankDto> Ranks { get; set; } = new();
}
