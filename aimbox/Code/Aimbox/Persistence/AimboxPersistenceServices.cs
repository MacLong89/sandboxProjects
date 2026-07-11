namespace Sandbox;

public sealed class AimboxPlayerDataService
{
	readonly IAimboxDatabase _database;
	readonly AimboxLoadoutPersistenceService _loadouts = new();

	public AimboxPlayerDataService( IAimboxDatabase database )
	{
		_database = database;
	}

	public AimboxPlayerData LoadPlayer( string accountId )
	{
		var data = _database.LoadPlayer( accountId );
		if ( data.ProgressionVersion < AimboxPlayerData.CurrentProgressionVersion )
		{
			if ( data.ProgressionVersion >= 4 )
			{
				Log.Info( $"[Aimbox] Migrating progression for {accountId} (version {data.ProgressionVersion} -> {AimboxPlayerData.CurrentProgressionVersion})." );
				MigrateProgression( data, data.ProgressionVersion );
				data.ProgressionVersion = AimboxPlayerData.CurrentProgressionVersion;
			}
			else
			{
				Log.Info( $"[Aimbox] Resetting progression for {accountId} to fresh start (version {data.ProgressionVersion} -> {AimboxPlayerData.CurrentProgressionVersion})." );
				_database.DeletePlayer( accountId );
				data = AimboxPlayerData.CreateFreshStart( accountId );
			}

			_database.SavePlayer( data );
		}

		data.Validate();
		AimboxUnlockService.ApplyDebugUnlockAll( data );
		if ( AimboxUnlockService.BypassProgressionLocks )
			Log.Info( "[Aimbox] Debug unlock-all progression active — all weapons and compatible attachments available." );
		else
			AimboxUnlockService.EnforceSavedProgressionLocks( data );

		new AimboxChallengeSystem().EnsureDailyReset( data );
		_loadouts.ValidateAllLoadouts( data );
		Log.Info( $"[Aimbox] Loaded {accountId}: rank {data.PlayerLevel}, xp {data.TotalXp}, version {data.ProgressionVersion}, primary {data.Loadouts.FirstOrDefault()?.PrimaryWeapon}." );
		return data;
	}

	static void MigrateProgression( AimboxPlayerData data, int fromVersion )
	{
		if ( fromVersion < 5 )
		{
			foreach ( var loadout in data.Loadouts )
			{
				if ( !AimboxMw2Catalog.IsPrimaryWeapon( loadout.PrimaryWeapon ) )
					loadout.PrimaryWeapon = AimboxMw2Catalog.DefaultPrimaryForRank( data.PlayerLevel );

				if ( !AimboxMw2Catalog.IsSecondaryWeapon( loadout.SecondaryWeapon ) )
					loadout.SecondaryWeapon = AimboxMw2Catalog.DefaultSecondaryForRank( data.PlayerLevel );
			}
		}

		if ( fromVersion < 6 )
		{
			foreach ( var loadout in data.Loadouts )
			{
				loadout.PrimaryWeapon = AimboxUnlockService.ResolveWeapon(
					data,
					AimboxMw2Catalog.IsPrimaryWeapon( loadout.PrimaryWeapon )
						? loadout.PrimaryWeapon
						: AimboxMw2Catalog.DefaultPrimaryForRank( data.PlayerLevel ),
					AimboxMw2Catalog.PrimaryWeapons );
			}
		}

		if ( fromVersion < 7 )
		{
			foreach ( var loadout in data.Loadouts )
			{
				if ( !AimboxMw2Catalog.IsPrimaryWeapon( loadout.PrimaryWeapon ) )
					loadout.PrimaryWeapon = AimboxWeaponId.Usp;

				loadout.PrimaryWeapon = AimboxUnlockService.ResolveWeapon(
					data,
					loadout.PrimaryWeapon,
					AimboxMw2Catalog.PrimaryWeapons );
			}
		}

		if ( fromVersion < 8 )
			MigrateAttachmentProgression( data );

		if ( fromVersion < 9 )
			MigrateAttachmentCompatibility( data );

		if ( fromVersion < 10 )
			MigrateAttachmentCompatibility( data );

		if ( fromVersion < 11 )
			MigrateAttachmentCompatibility( data );

		if ( fromVersion < 12 )
		{
			foreach ( var loadout in data.Loadouts )
			{
				if ( loadout.PrimaryWeapon == AimboxWeaponId.Bow )
					loadout.PrimaryWeapon = AimboxWeaponId.Usp;
			}

			data.Weapons.Remove( AimboxWeaponId.Bow );
		}

		if ( fromVersion < 13 )
			data.AimModeBestScores ??= new Dictionary<AimboxGameMode, int>();
	}

	static void MigrateAttachmentCompatibility( AimboxPlayerData data )
	{
		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
		{
			var weaponData = data.GetWeapon( weapon );
			weaponData.UnlockedAttachments = weaponData.UnlockedAttachments
				.Where( a => AimboxAttachmentCatalog.IsCompatible( weapon, a ) )
				.ToHashSet();
			weaponData.EquippedAttachments = weaponData.EquippedAttachments
				.Where( a => AimboxAttachmentCatalog.IsCompatible( weapon, a ) )
				.ToHashSet();
		}

		foreach ( var loadout in data.Loadouts )
		{
			var sanitized = new Dictionary<AimboxWeaponId, List<AimboxAttachmentId>>();
			foreach ( var (weapon, attachments) in loadout.Attachments )
			{
				var next = AimboxUnlockService.SanitizeAttachments( data, weapon, attachments );
				if ( next.Count > 0 )
					sanitized[weapon] = next;
			}

			loadout.Attachments = sanitized;
		}
	}

	static void MigrateAttachmentProgression( AimboxPlayerData data )
	{
		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
		{
			var weaponData = data.GetWeapon( weapon );
			weaponData.UnlockedAttachments = AimboxAttachmentCatalog.MigrateLegacyAttachments( weaponData.UnlockedAttachments );
			weaponData.EquippedAttachments = AimboxAttachmentCatalog.MigrateLegacyAttachments( weaponData.EquippedAttachments );
		}

		foreach ( var loadout in data.Loadouts )
		{
			var migrated = new Dictionary<AimboxWeaponId, List<AimboxAttachmentId>>();
			foreach ( var (weapon, attachments) in loadout.Attachments )
			{
				var next = AimboxAttachmentCatalog.MigrateLegacyAttachments( attachments ).ToList();
				if ( next.Count > 0 )
					migrated[weapon] = next;
			}

			loadout.Attachments = migrated;
		}
	}

	public AimboxPlayerData ResetProgress( string accountId )
	{
		_database.DeletePlayer( accountId );
		var data = AimboxPlayerData.CreateFreshStart( accountId );
		AimboxUnlockService.EnforceSavedProgressionLocks( data );
		_database.SavePlayer( data );
		Log.Info( $"[Aimbox] Progress reset for {accountId}. Rank {data.PlayerLevel}, xp {data.TotalXp}, weapons {data.Weapons.Count}." );
		return data;
	}

	public void SavePlayer( AimboxPlayerData data ) => _database.SavePlayer( data );
}

public sealed class AimboxLoadoutPersistenceService
{
	public AimboxLoadoutData GetActiveLoadout( AimboxPlayerData data )
	{
		if ( data?.Loadouts is not { Count: > 0 } )
			return AimboxLoadoutData.Default();

		var index = Math.Clamp( data.ActiveLoadoutIndex, 0, data.Loadouts.Count - 1 );
		return data.Loadouts[index];
	}

	public void SaveLoadout( AimboxPlayerData data, AimboxLoadoutData loadout, int slot = -1 )
	{
		slot = slot < 0 ? data.ActiveLoadoutIndex : slot;
		while ( data.Loadouts.Count <= slot )
			data.Loadouts.Add( AimboxLoadoutData.Default( data.Loadouts.Count + 1 ) );

		data.Loadouts[slot] = ValidateLoadout( data, loadout );
	}

	public void ValidateAllLoadouts( AimboxPlayerData data )
	{
		for ( var i = 0; i < data.Loadouts.Count; i++ )
			data.Loadouts[i] = ValidateLoadout( data, data.Loadouts[i] );
	}

	public AimboxLoadoutData ValidateLoadout( AimboxPlayerData data, AimboxLoadoutData loadout )
	{
		if ( !AimboxMw2Catalog.IsPrimaryWeapon( loadout.PrimaryWeapon ) )
			loadout.PrimaryWeapon = AimboxMw2Catalog.DefaultPrimaryForRank( data.PlayerLevel );

		loadout.PrimaryWeapon = AimboxUnlockService.ResolveWeapon(
			data,
			loadout.PrimaryWeapon,
			AimboxMw2Catalog.PrimaryWeapons );

		loadout.SecondaryWeapon = AimboxUnlockService.ResolveWeapon(
			data,
			loadout.SecondaryWeapon,
			AimboxMw2Catalog.SecondaryWeapons );

		loadout.Perk1 = AimboxUnlockService.ResolvePerk( data, loadout.Perk1, AimboxPerkId.None );
		loadout.Perk2 = AimboxUnlockService.ResolvePerk( data, loadout.Perk2, AimboxPerkId.None );
		loadout.Perk3 = AimboxUnlockService.ResolvePerk( data, loadout.Perk3, AimboxPerkId.None );

		loadout.Killstreak1 = AimboxUnlockService.ResolveKillstreak( data, loadout.Killstreak1, AimboxKillstreakId.None );
		loadout.Killstreak2 = AimboxUnlockService.ResolveKillstreak( data, loadout.Killstreak2, AimboxKillstreakId.None );
		loadout.Killstreak3 = AimboxUnlockService.ResolveKillstreak( data, loadout.Killstreak3, AimboxKillstreakId.None );

		var sanitized = new Dictionary<AimboxWeaponId, List<AimboxAttachmentId>>();
		foreach ( var (weapon, attachments) in loadout.Attachments )
		{
			var valid = AimboxUnlockService.SanitizeAttachments( data, weapon, attachments );
			if ( valid.Count > 0 )
				sanitized[weapon] = valid;
		}

		loadout.Attachments = sanitized;
		return loadout;
	}
}

public sealed class AimboxRankedPersistenceService
{
	public RankedData GetRankedData( AimboxPlayerData data ) => data.Ranked;

	public void SaveRankedData( AimboxPlayerData data, RankedData ranked )
	{
		data.Ranked = ranked;
	}
}

public sealed class AimboxChallengePersistenceService
{
	public int GetProgress( AimboxPlayerData data, string challengeId ) => data.ChallengeProgress.GetValueOrDefault( challengeId );

	public bool IsCompleted( AimboxPlayerData data, string challengeId ) => data.CompletedChallenges.Contains( challengeId );
}

public sealed class AimboxStatsPersistenceService
{
	public void RecordShot( AimboxPlayerData data, bool hit )
	{
		data.ShotsFired++;
		if ( hit )
			data.ShotsHit++;
	}

	public void RecordMatch( AimboxPlayerData data, bool won )
	{
		data.MatchesPlayed++;
		if ( won )
			data.Wins++;
		else
			data.Losses++;
	}
}

public sealed class AimboxMatchHistoryEntry
{
	public DateTime FinishedAtUtc { get; set; } = DateTime.UtcNow;
	public AimboxGameMode Mode { get; set; }
	public string AccountId { get; set; }
	public bool Won { get; set; }
	public int Kills { get; set; }
	public int Deaths { get; set; }
	public int XpEarned { get; set; }
}

public sealed class AimboxMatchHistoryService
{
	readonly List<AimboxMatchHistoryEntry> _recent = [];

	public IReadOnlyList<AimboxMatchHistoryEntry> Recent => _recent;

	public void Add( AimboxMatchHistoryEntry entry )
	{
		_recent.Add( entry );
		if ( _recent.Count > 100 )
			_recent.RemoveAt( 0 );
	}
}
