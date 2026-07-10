namespace Sandbox;

/// <summary>Local preferences for the host-server modal (last server name, etc.).</summary>
public sealed class ThornsHostMenuPrefsDto
{
	public string LastHostedServerName { get; set; } = "";
}

public static class ThornsHostMenuPreferences
{
	public const string RelativePath = "Thorns/host_menu_prefs.json";

	public static string LoadLastHostedServerName()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( RelativePath ) )
				return "My Thorns Server";

			var dto = FileSystem.Data.ReadJson<ThornsHostMenuPrefsDto>( RelativePath );
			if ( dto is null || string.IsNullOrWhiteSpace( dto.LastHostedServerName ) )
				return "My Thorns Server";

			return dto.LastHostedServerName.Trim();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Host menu: could not read prefs; using default server name." );
			return "My Thorns Server";
		}
	}

	public static void SaveLastHostedServerName( string name )
	{
		try
		{
			var trimmed = name?.Trim() ?? "";
			FileSystem.Data.WriteJson( RelativePath,
				new ThornsHostMenuPrefsDto { LastHostedServerName = trimmed } );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Host menu: failed to write prefs." );
		}
	}
}
