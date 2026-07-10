namespace Terraingen.NpcGuild;

/// <summary>Read-only rival NPC guild intel shown on the player guild tab.</summary>
public sealed class ThornsNpcGuildRivalDto
{
	public bool HasRival { get; set; }
	public string GuildId { get; set; } = "";
	public string GuildName { get; set; } = "";
	public string Motto { get; set; } = "";
	public bool IsEliminated { get; set; }
	public bool HasDominionVictory { get; set; }
	public int OutpostCount { get; set; }
	public int OutpostTarget { get; set; } = 10;
	public float DominionPercent { get; set; }
	public string StatusLine { get; set; } = "";
}
