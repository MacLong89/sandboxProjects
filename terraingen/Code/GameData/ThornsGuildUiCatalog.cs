namespace Terraingen.GameData;

/// <summary>Guild menu icon paths and copy.</summary>
public static class ThornsGuildUiCatalog
{
	public static string GuildEmblemPath => ThornsIconRegistry.GuildEmblem();
	public static string MemberAvatarPath => ThornsIconRegistry.GuildMember();
	public static string ActivityIconPath( string kind ) => ThornsIconRegistry.GuildActivity( kind );

	public static string ActivityIconPathDefault => ThornsIconRegistry.GuildActivityDefault();
}
