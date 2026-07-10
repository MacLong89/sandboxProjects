namespace ThinkDrink;

public sealed class LeaderboardManager : Component
{
	public static LeaderboardManager Instance { get; private set; }

	private readonly LeaderboardService _service = new();

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			PersistenceManager.Instance?.LoadAll();
			Rebuild();
		}
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void Rebuild()
	{
		if ( !Networking.IsHost ) return;

		var profiles = PersistenceManager.Instance?.Profiles.Values ?? Enumerable.Empty<PlayerProfile>();
		var snap = _service.Build( profiles );
		PersistenceManager.Instance?.SaveLeaderboards( snap );

		var ranked = snap.Wins;
		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			var idx = ranked.FindIndex( e => e.SteamId == p.SteamKey );
			p.LeaderboardRank = idx >= 0 ? idx + 1 : 0;
		}
	}

	public int GetRank( string steamId )
	{
		var snap = PersistenceManager.Instance?.GetLeaderboards();
		if ( snap?.Wins is null ) return 0;
		var idx = snap.Wins.FindIndex( e => e.SteamId == steamId );
		return idx >= 0 ? idx + 1 : 0;
	}

	public LeaderboardSnapshot GetSnapshot() =>
		PersistenceManager.Instance?.GetLeaderboards() ?? new LeaderboardSnapshot();
}
