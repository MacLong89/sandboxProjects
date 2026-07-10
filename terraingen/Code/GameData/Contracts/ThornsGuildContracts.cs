namespace Terraingen.GameData;

using Terraingen.NpcGuild;

public sealed class ThornsGuildMemberDto
{
	public string AccountKey { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Rank { get; set; } = "Member";
	public bool IsOnline { get; set; }
}

public sealed class ThornsGuildActivityDto
{
	public string EntryId { get; set; } = "";
	public string Message { get; set; } = "";
	public string Kind { get; set; } = "";
	public string TimestampUtc { get; set; } = "";
}

public sealed class ThornsGuildSnapshotDto
{
	public bool InGuild { get; set; }
	public string GuildId { get; set; } = "";
	public string GuildName { get; set; } = "";
	public int GuildLevel { get; set; }
	public float GuildXp { get; set; }
	public float GuildXpToNext { get; set; } = 12000f;
	public string Motto { get; set; } = "";
	public bool IsNpcGuild { get; set; }
	public string BannerIconPath { get; set; } = "";
	public string Announcement { get; set; } = "";
	public ThornsGuildOverviewDto Overview { get; set; } = new();
	public ThornsGuildNoticeDto Notice { get; set; } = new();
	public ThornsGuildCommandSnapshotDto Command { get; set; } = new();
	public List<ThornsGuildMemberDto> Members { get; set; } = new();
	public List<ThornsGuildActivityDto> Activity { get; set; } = new();
	public List<ThornsNpcGuildRivalDto> RivalNpcGuilds { get; set; } = new();
	public ThornsNpcGuildRivalDto RivalNpcGuild { get; set; } = new();
}
