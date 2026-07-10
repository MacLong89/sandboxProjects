namespace ThinkDrink.Persistence;

/// <summary>
/// Host-side profile and leaderboard persistence with JSON versioning.
/// Abstracted behind <see cref="IPersistenceStore"/> for future cloud migration.
/// </summary>
public sealed class PersistenceManager : Component
{
	public static PersistenceManager Instance { get; private set; }

	private IPersistenceStore _store = new LocalPersistenceStore();
	private ProfilesSaveFile _profiles = new();
	private LeaderboardSaveFile _leaderboards = new();
	private bool _dirty;
	private TimeUntil _nextAutosave;

	public IReadOnlyDictionary<string, PlayerProfile> Profiles => _profiles.Profiles;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart() => _nextAutosave = 60f;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Networking.IsHost || !_nextAutosave ) return;
		_nextAutosave = 60f;
		if ( _dirty ) SaveAll();
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost ) SaveAll();
		if ( Instance == this ) Instance = null;
	}

	public void SetStore( IPersistenceStore store )
	{
		if ( store is not null ) _store = store;
	}

	public void LoadAll()
	{
		if ( !Networking.IsHost ) return;

		try
		{
			if ( _store.Exists( GameConstants.ProfilesFile ) )
			{
				var json = _store.ReadText( GameConstants.ProfilesFile );
				_profiles = Json.Deserialize<ProfilesSaveFile>( json ) ?? new ProfilesSaveFile();
			}

			if ( _store.Exists( GameConstants.LeaderboardFile ) )
			{
				var json = _store.ReadText( GameConstants.LeaderboardFile );
				_leaderboards = Json.Deserialize<LeaderboardSaveFile>( json ) ?? new LeaderboardSaveFile();
			}

			Log.Info( $"Think & Drink: loaded {_profiles.Profiles.Count} profiles." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: persistence load failed — {e.Message}" );
			_profiles = new ProfilesSaveFile();
			_leaderboards = new LeaderboardSaveFile();
		}
	}

	public PlayerProfile GetOrCreateProfile( Connection connection )
	{
		var id = GetSteamKey( connection );
		if ( !_profiles.Profiles.TryGetValue( id, out var profile ) )
		{
			profile = new PlayerProfile
			{
				SteamId = id,
				DisplayName = connection?.DisplayName ?? "Player"
			};
			_profiles.Profiles[id] = profile;
			_dirty = true;
		}
		else if ( connection is not null )
		{
			profile.DisplayName = connection.DisplayName;
		}

		new ChallengeService().EnsureChallenges( profile, DateTime.UtcNow );
		return profile;
	}

	public PlayerProfile GetProfile( string steamId )
	{
		if ( string.IsNullOrEmpty( steamId ) ) return null;
		_profiles.Profiles.TryGetValue( steamId, out var profile );
		return profile;
	}

	public void SaveProfile( PlayerProfile profile )
	{
		if ( profile is null || !Networking.IsHost ) return;
		_profiles.Profiles[profile.SteamId] = profile;
		_dirty = true;
	}

	public void SaveLeaderboards( LeaderboardSnapshot snapshot )
	{
		if ( !Networking.IsHost || snapshot is null ) return;
		_leaderboards.Snapshot = snapshot;
		_dirty = true;
	}

	public LeaderboardSnapshot GetLeaderboards() => _leaderboards.Snapshot ?? new LeaderboardSnapshot();

	public void SaveAll()
	{
		if ( !Networking.IsHost || !_dirty ) return;

		try
		{
			_store.WriteText( GameConstants.ProfilesFile, Json.Serialize( _profiles ) );
			_store.WriteText( GameConstants.LeaderboardFile, Json.Serialize( _leaderboards ) );
			_dirty = false;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: save failed — {e.Message}" );
		}
	}

	public static string GetSteamKey( Connection connection )
	{
		if ( connection is not null && connection.SteamId.Value != 0 )
			return connection.SteamId.Value.ToString();
		return connection?.DisplayName ?? "local";
	}
}
