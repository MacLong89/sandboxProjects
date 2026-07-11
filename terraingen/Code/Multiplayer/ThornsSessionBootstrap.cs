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
	static bool _hostingLocalSaveActive;
	static bool _joinRemoteLobbyNext;
	static bool _joiningRemoteLobbyActive;
	static ThornsHostLocalSaveLobbyOptions _hostOptions;

	/// <summary>True while loading/hosting a local save world (not a remote join client).</summary>
	public static bool IsHostingLocalSave => _hostingLocalSaveActive;

	public static void RequestHostFromLocalSaveNextGameplayLoad( ThornsHostLocalSaveLobbyOptions options )
	{
		_hostFromLocalSaveNext = true;
		_hostingLocalSaveActive = true;
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
		_hostingLocalSaveActive = false;
		_hostOptions = default;
	}

	public static void RequestJoinRemoteLobbyNextGameplayLoad()
	{
		CancelHostFromLocalSaveRequest();
		_joinRemoteLobbyNext = true;
		_joiningRemoteLobbyActive = true;
	}

	/// <summary>True from join request until cancel/complete — survives TakeJoinRemoteLobbyRequest.</summary>
	public static bool IsJoiningRemoteLobby => _joiningRemoteLobbyActive;

	public static bool TakeJoinRemoteLobbyRequest()
	{
		if ( !_joinRemoteLobbyNext )
			return false;

		_joinRemoteLobbyNext = false;
		return true;
	}

	public static void CancelJoinRemoteLobbyRequest()
	{
		_joinRemoteLobbyNext = false;
		_joiningRemoteLobbyActive = false;
	}

	public static void CompleteJoinRemoteLobby() => _joiningRemoteLobbyActive = false;
}
