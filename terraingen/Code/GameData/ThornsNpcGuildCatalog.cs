namespace Terraingen.GameData;

/// <summary>Seed NPC rival factions for rankings, guild tab intel, and world outposts.</summary>
public static class ThornsNpcGuildCatalog
{
	public sealed class NpcGuildTemplate
	{
		public string GuildId { get; init; } = "";
		public string GuildName { get; init; } = "";
		public string Motto { get; init; } = "";
		public string Announcement { get; init; } = "";
		public string AnnouncementAuthor { get; init; } = "";
		public int GuildLevel { get; init; } = 1;
		public float GuildXp { get; init; }
		public int HeadquartersSeedSalt { get; init; }
	}

	public const string IdPrefix = "npc_";
	public const string IronWolvesId = "npc_iron_wolves";
	public const string AshReaversId = "npc_ash_reavers";
	public const string CrimsonPackId = "npc_crimson_pack";

	public const int IronWolvesHqSeedSalt = unchecked( (int)0x4E50434851 );
	public const int AshReaversHqSeedSalt = unchecked( (int)0x4E50434852 );
	public const int CrimsonPackHqSeedSalt = unchecked( (int)0x4E50434853 );

	public static bool IsNpcGuildId( string guildId )
		=> !string.IsNullOrWhiteSpace( guildId )
		   && guildId.StartsWith( IdPrefix, StringComparison.OrdinalIgnoreCase );

	public static bool IsAuthorizedNpcGuildId( string guildId )
		=> TryGet( guildId ) is not null;

	public static NpcGuildTemplate TryGet( string guildId )
		=> All.FirstOrDefault( g => string.Equals( g.GuildId, guildId, StringComparison.OrdinalIgnoreCase ) );

	/// <summary>Rebuilt each access so hotload picks up catalog edits without a full restart.</summary>
	public static IReadOnlyList<NpcGuildTemplate> All => BuildCatalog();

	static List<NpcGuildTemplate> BuildCatalog() => new()
	{
		new()
		{
			GuildId = IronWolvesId,
			GuildName = "Iron Wolves",
			Motto = "We survive. We gather. We dominate.",
			Announcement = "Push the Dominion path hard. Establish outposts across the wasteland.",
			AnnouncementAuthor = "Ragnar",
			GuildLevel = 12,
			GuildXp = 8400f,
			HeadquartersSeedSalt = IronWolvesHqSeedSalt
		},
		new()
		{
			GuildId = AshReaversId,
			GuildName = "Ash Reavers",
			Motto = "Scrap is power. Burn what you cannot carry.",
			Announcement = "Strip the lowlands and fortify every ridge we claim.",
			AnnouncementAuthor = "Mako",
			GuildLevel = 10,
			GuildXp = 6200f,
			HeadquartersSeedSalt = AshReaversHqSeedSalt
		},
		new()
		{
			GuildId = CrimsonPackId,
			GuildName = "Crimson Pack",
			Motto = "Blood pays debts. Territory keeps the pack fed.",
			Announcement = "Hunt rivals and raise red banners on every coast.",
			AnnouncementAuthor = "Sera",
			GuildLevel = 11,
			GuildXp = 7100f,
			HeadquartersSeedSalt = CrimsonPackHqSeedSalt
		}
	};
}
