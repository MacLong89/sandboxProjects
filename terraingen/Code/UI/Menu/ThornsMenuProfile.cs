namespace Terraingen.UI.Menu;

using Terraingen.GameData;

public sealed class ThornsMenuProfileCacheDto
{
	public int PlayerLevel { get; set; } = 1;
	public string GuildName { get; set; } = "";
	public bool InGuild { get; set; }
}

/// <summary>Display name from s&box; progression from last local snapshot cache.</summary>
public static class ThornsMenuProfile
{
	public const string CacheRelativePath = "Terraingen/menu_profile_cache.json";

	public static string DisplayName
	{
		get
		{
			if ( Connection.Local is not null && !string.IsNullOrWhiteSpace( Connection.Local.DisplayName ) )
				return Connection.Local.DisplayName.Trim();

			return "Survivor";
		}
	}

	public static ThornsMenuProfileCacheDto Cache { get; private set; } = new();

	public static void LoadCache()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( CacheRelativePath ) )
			{
				Cache = new ThornsMenuProfileCacheDto();
				return;
			}

			Cache = FileSystem.Data.ReadJson<ThornsMenuProfileCacheDto>( CacheRelativePath ) ?? new ThornsMenuProfileCacheDto();
		}
		catch
		{
			Cache = new ThornsMenuProfileCacheDto();
		}
	}

	public static void SaveFromSnapshot( ThornsPlayerSnapshotBundle bundle )
	{
		if ( bundle is null )
			return;

		Cache.PlayerLevel = Math.Max( 1, bundle.Skills?.PlayerLevel ?? 1 );
		Cache.InGuild = bundle.Guild?.InGuild == true;
		Cache.GuildName = bundle.Guild?.InGuild == true ? bundle.Guild.GuildName?.Trim() ?? "" : "";

		try
		{
			FileSystem.Data.WriteJson( CacheRelativePath, Cache );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Failed to write profile cache." );
		}

		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}
}
