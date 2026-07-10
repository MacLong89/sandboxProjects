namespace ThinkDrink;

public sealed class AchievementManager : Component
{
	public static AchievementManager Instance { get; private set; }

	private readonly AchievementService _service = new();
	private AchievementCatalog _catalog;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		_catalog = new AchievementCatalogLoader().Load();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public IReadOnlyList<AchievementDefinition> GetAllDefinitions() =>
		_catalog?.Achievements ?? new List<AchievementDefinition>();

	public void ProcessMatchResults( MatchResult result )
	{
		if ( !Networking.IsHost || result is null ) return;

		foreach ( var pr in result.Players )
		{
			var profile = PersistenceManager.Instance?.GetProfile( pr.SteamId );
			if ( profile is null ) continue;

			var unlocked = _service.Evaluate( profile, result, _catalog );
			foreach ( var def in unlocked )
			{
				if ( !profile.UnlockedAchievements.Contains( def.Id ) )
					profile.UnlockedAchievements.Add( def.Id );

				XpService.ApplyXp( profile, def.XpReward );
				result.NewAchievements.Add( def.Id );
				GameEvents.RaiseAchievementUnlocked( def );
				GameEvents.RaiseAudio( AudioEventId.AchievementUnlock );
			}

			PersistenceManager.Instance.SaveProfile( profile );
			ThinkDrinkPlayer.FindBySteamKey( pr.SteamId )?.RefreshLifetimeStats();
		}
	}

	public bool IsUnlocked( PlayerProfile profile, string achievementId ) =>
		profile?.UnlockedAchievements?.Contains( achievementId ) == true;
}
