namespace NoFly;

/// <summary>
/// Staff desk check-in — walk to your post and press E for a clear "you're on station" confirm.
/// </summary>
public static class StaffStation
{
	public const float CheckInRadius = 100f;

	public static bool IsStaff( RoleType role ) =>
		role is RoleType.DocumentAgent or RoleType.ScannerAgent or RoleType.SecurityOfficer;

	public static string StationName( RoleType role ) => role switch
	{
		RoleType.DocumentAgent => "Documents desk",
		RoleType.ScannerAgent => "Bag Scan desk",
		RoleType.SecurityOfficer => "Security desk",
		_ => "station"
	};

	public static string ZoneForRole( RoleType role ) => role switch
	{
		RoleType.DocumentAgent => "station_docs",
		RoleType.ScannerAgent => "station_scanner",
		RoleType.SecurityOfficer => "station_security",
		_ => null
	};

	public static RoleType? RoleForZone( string zoneTag ) => zoneTag switch
	{
		"station_docs" => RoleType.DocumentAgent,
		"station_scanner" => RoleType.ScannerAgent,
		"station_security" => RoleType.SecurityOfficer,
		_ => null
	};

	public static bool NearOwnDesk( NoFlyPlayer player )
	{
		var game = NoFlyGame.Instance;
		if ( player is null || game is null || !IsStaff( player.Role ) ) return false;
		var desk = game.GetStaffDesk( player.Role );
		return Vector3.DistanceBetween( player.WorldPosition.WithZ( 0 ), desk.WithZ( 0 ) ) <= CheckInRadius;
	}

	public static bool TryCheckIn( NoFlyPlayer player, string zoneTag = null )
	{
		if ( !Networking.IsHost || player is null ) return false;
		if ( !IsStaff( player.Role ) )
		{
			player.ActivePrompt = "Staff stations only";
			return false;
		}

		if ( !string.IsNullOrEmpty( zoneTag ) )
		{
			var expected = RoleForZone( zoneTag );
			if ( expected is null || expected != player.Role )
			{
				player.ActivePrompt = $"Wrong desk — your post is the {StationName( player.Role )}";
				return false;
			}
		}

		if ( !NearOwnDesk( player ) )
		{
			player.ActivePrompt = $"Get closer to the {StationName( player.Role )}";
			return false;
		}

		if ( player.AtStation )
		{
			player.ActivePrompt = $"Already stationed at {StationName( player.Role )} ✓";
			return true;
		}

		player.AtStation = true;
		player.ActivePrompt = $"Stationed at {StationName( player.Role )} ✓ — you're in the right spot";
		player.AddScore( 5, "objectives" );
		return true;
	}
}
