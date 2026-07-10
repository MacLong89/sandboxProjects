namespace Sandbox;

public static class AimboxScoreboardUiHelpers
{
	public static string TeamLabel( AimboxTeam team ) => team switch
	{
		AimboxTeam.Red => "ALPHA",
		AimboxTeam.Blue => "BRAVO",
		_ => "NONE"
	};

	public static string TeamAccent( AimboxTeam team ) => team switch
	{
		AimboxTeam.Red => "#ff2a6d",
		AimboxTeam.Blue => "#2ad4c8",
		_ => "#888888"
	};

	public static string TeamToneClass( AimboxTeam team ) => team switch
	{
		AimboxTeam.Red => "tone-alpha",
		AimboxTeam.Blue => "tone-bravo",
		_ => "tone-neutral"
	};

	public static string RosterLabel( AimboxScoreboardTeamColumn column ) =>
		$"{TeamLabel( column.Team )} {column.Entries.Count}/{Math.Max( column.Entries.Count, AimboxArenaConfig.TdmRosterPerTeam )}";

	public static string FormatKd( AimboxScoreboardEntry entry ) => entry.KdRatio.ToString( "0.00" );

	public static string PlayerBadge( AimboxScoreboardEntry entry )
	{
		if ( string.IsNullOrWhiteSpace( entry.Name ) )
			return "??";

		var parts = entry.Name.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length >= 2 )
			return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();

		return entry.Name.Length >= 2 ? entry.Name[..2].ToUpperInvariant() : entry.Name.ToUpperInvariant();
	}

	public static AimboxScoreboardEntry? LocalEntry( AimboxScoreboardView view, AimboxPlayerController local )
	{
		if ( local is null )
			return null;

		if ( view.IsTeamLayout )
		{
			foreach ( var column in view.TeamColumns )
			{
				foreach ( var entry in column.Entries )
				{
					if ( entry.IsLocal )
						return entry;
				}
			}

			return null;
		}

		foreach ( var entry in view.Entries )
		{
			if ( entry.IsLocal )
				return entry;
		}

		return null;
	}
}
