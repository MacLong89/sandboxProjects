namespace Terraingen.Multiplayer;

public sealed class ThornsHostMenuPrefsDto
{
	public string LastHostedServerName { get; set; } = "";
}

public static class ThornsHostMenuPreferences
{
	public const string RelativePath = "Terraingen/host_menu_prefs.json";
	const string DefaultServerName = "My Thorns Terrain Server";

	public static string LoadLastHostedServerName()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( RelativePath ) )
				return DefaultServerName;

			var dto = FileSystem.Data.ReadJson<ThornsHostMenuPrefsDto>( RelativePath );
			if ( dto is null || string.IsNullOrWhiteSpace( dto.LastHostedServerName ) )
				return DefaultServerName;

			return dto.LastHostedServerName.Trim();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Terrain] Host menu: could not read prefs." );
			return DefaultServerName;
		}
	}

	public static void SaveLastHostedServerName( string name )
	{
		try
		{
			FileSystem.Data.WriteJson( RelativePath, new ThornsHostMenuPrefsDto
			{
				LastHostedServerName = name?.Trim() ?? ""
			} );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Terrain] Host menu: failed to write prefs." );
		}
	}
}
