namespace Terraingen.Multiplayer;

/// <summary>Menu to gameplay handoff for hosted server-specific saves.</summary>
public readonly struct ThornsHostLocalSaveLobbyOptions
{
	public ThornsHostLocalSaveLobbyOptions(
		bool requireJoinPassword,
		string joinPassword,
		string serverDisplayName,
		string persistenceRelativePath )
	{
		RequireJoinPassword = requireJoinPassword;
		JoinPassword = joinPassword ?? "";
		ServerDisplayName = string.IsNullOrWhiteSpace( serverDisplayName )
			? "Thorns Terrain"
			: serverDisplayName.Trim();
		PersistenceRelativePath = string.IsNullOrWhiteSpace( persistenceRelativePath )
			? ThornsWorldPersistence.DefaultRelativePath
			: persistenceRelativePath.Trim().Replace( '\\', '/' );
	}

	public bool RequireJoinPassword { get; }
	public string JoinPassword { get; }
	public string ServerDisplayName { get; }
	public string PersistenceRelativePath { get; }
}

public static class ThornsSessionBootstrap
{
	static bool _hostFromLocalSaveNext;
	static bool _joinRemoteLobbyNext;
	static ThornsHostLocalSaveLobbyOptions _hostOptions;

	public static void RequestHostFromLocalSaveNextGameplayLoad( ThornsHostLocalSaveLobbyOptions options )
	{
		_hostFromLocalSaveNext = true;
		_hostOptions = options;
	}

	public static bool TakeRequestedHostFromLocalSave( out ThornsHostLocalSaveLobbyOptions options )
	{
		options = default;
		if ( !_hostFromLocalSaveNext )
			return false;

		_hostFromLocalSaveNext = false;
		options = _hostOptions;
		_hostOptions = default;
		return true;
	}

	public static void CancelHostFromLocalSaveRequest()
	{
		_hostFromLocalSaveNext = false;
		_hostOptions = default;
	}

	public static void RequestJoinRemoteLobbyNextGameplayLoad()
	{
		CancelHostFromLocalSaveRequest();
		_joinRemoteLobbyNext = true;
	}

	public static bool TakeJoinRemoteLobbyRequest()
	{
		if ( !_joinRemoteLobbyNext )
			return false;

		_joinRemoteLobbyNext = false;
		return true;
	}

	public static void CancelJoinRemoteLobbyRequest() => _joinRemoteLobbyNext = false;
}
