namespace Sandbox;

/// <summary>
/// Carries menu intent into <see cref="ThornsGameManager"/> across <see cref="Game.ChangeScene"/>.
/// <see cref="ThornsWorldPersistence"/> reads/writes JSON on the host for restarts.
/// </summary>
public readonly struct ThornsHostLocalSaveLobbyOptions
{
	public ThornsHostLocalSaveLobbyOptions(
		bool requireJoinPassword,
		string joinPassword,
		string serverDisplayName,
		string persistenceRelativePath,
		ThornsHostWorldGenerationIntent worldGenIntent = default )
	{
		RequireJoinPassword = requireJoinPassword;
		JoinPassword = joinPassword ?? "";
		ServerDisplayName = string.IsNullOrWhiteSpace( serverDisplayName )
			? "Thorns"
			: serverDisplayName.Trim();
		PersistenceRelativePath = string.IsNullOrWhiteSpace( persistenceRelativePath )
			? ThornsWorldPersistence.DefaultRelativePath
			: persistenceRelativePath.Trim().Replace( '\\', '/' );
		WorldGenIntent = worldGenIntent;
	}

	/// <summary>When true, lobby lists as public but joiners must pass <see cref="ThornsLobbyPasswordGate"/> (metadata hash).</summary>
	public bool RequireJoinPassword { get; }

	public string JoinPassword { get; }

	/// <summary>Shown in the Steam lobby list and Thorns UI.</summary>
	public string ServerDisplayName { get; }

	/// <summary><see cref="ThornsWorldPersistence.RelativeSavePath"/> on the host only.</summary>
	public string PersistenceRelativePath { get; }

	/// <summary>Overrides procedural layout for the next load when not <see cref="ThornsHostWorldGenMode.None"/>.</summary>
	public ThornsHostWorldGenerationIntent WorldGenIntent { get; }
}

/// <summary>Menu → gameplay handoff for hosted local-save sessions.</summary>
public static class ThornsSessionBootstrap
{
	static bool _hostFromLocalSaveNext;
	static ThornsHostLocalSaveLobbyOptions _hostOptions;

	public static void RequestHostFromLocalSaveNextGameplayLoad( ThornsHostLocalSaveLobbyOptions options )
	{
		_hostFromLocalSaveNext = true;
		_hostOptions = options;
		ThornsHostWorldGenHandoff.ArmForNextGameplayTerrain( options.WorldGenIntent );
	}

	/// <summary>Consume one-shot host-from-save request (call from gameplay after scene load).</summary>
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
		ThornsHostWorldGenHandoff.Clear();
		ThornsMenuAudioHandoff.Cancel();
	}
}
