namespace NoFly;

public static class UiLabels
{
	public static string Field( DocumentFieldType field ) => field switch
	{
		DocumentFieldType.Photo => "Photo",
		DocumentFieldType.Name => "Name",
		DocumentFieldType.Date => "Expiry",
		DocumentFieldType.PassportNumber => "Pass No.",
		DocumentFieldType.CountrySymbol => "Symbol",
		DocumentFieldType.SecuritySeal => "Seal",
		DocumentFieldType.Destination => "Destination",
		DocumentFieldType.BackgroundPattern => "Pattern",
		_ => field.ToString()
	};

	public static string Phase( RoundState state ) => state switch
	{
		RoundState.WaitingForPlayers => "Lobby",
		RoundState.LobbyCountdown => "Starting",
		RoundState.AssigningRoles => "Assigning",
		RoundState.RoleReveal => "Role Reveal",
		RoundState.Preparation => "Before Security",
		RoundState.AirportOpen => "Security Open",
		RoundState.Boarding => "Boarding",
		RoundState.Chase => "Chase",
		RoundState.RoundEnd => "Round End",
		RoundState.Results => "Results",
		RoundState.Resetting => "Resetting",
		_ => state.ToString()
	};

	public static string Team( TeamType team ) => team switch
	{
		TeamType.Passenger => "PASSENGER",
		TeamType.Tsa => "TSA",
		_ => "UNKNOWN"
	};
}
