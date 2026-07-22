namespace Fauna2;

/// <summary>
/// Robust host-side persistence. Captures the entire zoo (layout, animals
/// with genetics, progression, codex, social data) into a versioned JSON file
/// under FileSystem.Data. Autosaves on a timer and on shutdown, survives
/// rejoins/restarts, and migrates old versions forward on load.
/// </summary>
public sealed class SaveSystem : Component
{
	public static SaveSystem Instance { get; private set; }

	/// <summary>Which slot autosave writes to.</summary>
	public int ActiveSlotId { get; set; } = 1;

	public long LastSaveSizeBytes { get; private set; }
	public string LastSaveTime { get; private set; } = "never";
	public bool IsApplying => _isApplying;

	private TimeUntil _nextAutosave;
	private TimeUntil _pendingEventSave;
	private bool _eventSaveQueued;
	private bool _isApplying;
	private List<TerrainObstacleSave> _terrainObstacleCache = new();
	private int _terrainObstacleClearedCache;
	private SaveData _lastGoodSnapshot;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		_nextAutosave = GameConstants.AutosaveInterval;
		SubscribeEvents();
	}

	protected override void OnDestroy()
	{
		UnsubscribeEvents();

		// Objects may already be destroyed — try to preserve layout from disk if capture races shutdown.
		if ( SaveHost.CanPersist && CanAutosave() )
			Save( preserveWorldOnEmptyCapture: true, verifyWrite: true, shutdown: true );

		if ( Instance == this ) Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !CanAutosave() ) return;

		if ( _eventSaveQueued && !_pendingEventSave )
		{
			_eventSaveQueued = false;
			Save();
			_nextAutosave = GameConstants.AutosaveInterval;
			return;
		}

		if ( !_nextAutosave ) return;

		_nextAutosave = GameConstants.AutosaveInterval;
		Save();
	}

	private bool CanAutosave() =>
		GameManager.Instance is not null && GameManager.Instance.GameStarted;

	private void SubscribeEvents()
	{
		GameEvents.AnimalSpawned += OnZooChanged;
		GameEvents.AnimalRemoved += OnZooChanged;
		GameEvents.AnimalBred += OnZooChanged;
		GameEvents.SpeciesDiscovered += OnZooChangedId;
		GameEvents.VariantDiscovered += OnZooChangedId;
		GameEvents.LevelUp += OnZooChangedLevel;
		GameEvents.PlotPurchased += OnZooChangedVoid;
		GameEvents.HabitatPlaced += OnZooChangedVoid;
		GameEvents.ZooModified += OnZooChangedVoid;
		GameEvents.EconomyGain += OnEconomyGain;
	}

	private void UnsubscribeEvents()
	{
		GameEvents.AnimalSpawned -= OnZooChanged;
		GameEvents.AnimalRemoved -= OnZooChanged;
		GameEvents.AnimalBred -= OnZooChanged;
		GameEvents.SpeciesDiscovered -= OnZooChangedId;
		GameEvents.VariantDiscovered -= OnZooChangedId;
		GameEvents.LevelUp -= OnZooChangedLevel;
		GameEvents.PlotPurchased -= OnZooChangedVoid;
		GameEvents.HabitatPlaced -= OnZooChangedVoid;
		GameEvents.ZooModified -= OnZooChangedVoid;
		GameEvents.EconomyGain -= OnEconomyGain;
	}

	private void OnZooChanged( AnimalComponent _ ) => RequestSave();
	private void OnZooChangedId( string _ ) => RequestSave();
	private void OnZooChangedLevel( int _ ) => RequestSave();
	private void OnZooChangedVoid() => RequestSave();
	private void OnEconomyGain( int _ ) => RequestSave();

	/// <summary>Debounced save after meaningful gameplay changes.</summary>
	public void RequestSave()
	{
		if ( _isApplying || !SaveHost.CanPersist || !CanAutosave() ) return;

		_eventSaveQueued = true;
		_pendingEventSave = GameConstants.AutosaveEventDelay;
	}

	// ── Save ────────────────────────────────────────────────

	/// <param name="preserveWorldOnEmptyCapture">Merge the last disk layout when a transient capture is unexpectedly empty.</param>
	/// <param name="verifyWrite">Read the new file back and validate it before considering the save complete.</param>
	/// <param name="shutdown">Use shutdown-safe behavior and diagnostics.</param>
	/// <param name="allowEmptyBuiltOverwrite">
	/// True only for intentional wipes (new game over a slot). Hotload/teardown must
	/// never pass this — otherwise empty habitat/placeable lists overwrite good saves.
	/// </param>
	public void Save(
		bool preserveWorldOnEmptyCapture = false,
		bool verifyWrite = false,
		bool shutdown = false,
		bool allowEmptyBuiltOverwrite = false )
	{
		if ( _isApplying )
			return;

		if ( !SaveHost.CanPersist )
		{
			Log.Warning( "Fauna: save skipped — this machine is not the host." );
			return;
		}

		if ( GameManager.Instance is not null && GameManager.Instance.ActiveSaveSlot > 0 )
			ActiveSlotId = GameManager.Instance.ActiveSaveSlot;

		var clock = DebugStats.StartTimer();
		var path = GetActivePath();

		try
		{
			if ( !TryCaptureForSave( out var data ) )
			{
				Log.Warning( "Fauna: save skipped — nothing could be captured." );
				return;
			}

			if ( shutdown )
				data = MergeWithCachedSnapshot( data, _lastGoodSnapshot );

			// CONTINUE/SAVE FIX: Hotload tears down habitats/placeables before SaveSystem
			// while terrain obstacles + ZooState money are still alive. Capture then looks
			// "non-empty" (HasWorldContent true via TerrainObstacles) and wiped layouts.
			// Revert hint: set allowEmptyBuiltOverwrite on intentional slot resets only.
			if ( !allowEmptyBuiltOverwrite )
				ProtectBuiltWorldFromEmptyCapture( data, path );

			// New-game overwrites must not snapshot the previous zoo into .bak —
			// Continue recovery would otherwise resurrect it.
			WriteSaveFile(
				path,
				data,
				preserveWorldOnEmptyCapture,
				verifyWrite,
				writeBuiltWorldBackup: !allowEmptyBuiltOverwrite );

			RememberSnapshot( data );

			LastSaveSizeBytes = FileSystem.Data.FileSize( path );
			LastSaveTime = DateTime.Now.ToString( "HH:mm:ss" );
			Log.Info( $"Fauna: saved slot {ActiveSlotId} to '{path}' ({LastSaveSizeBytes / 1024f:0.0} KB, {data.Habitats.Count} habitats, {data.Placeables.Count} placeables, {data.Animals.Count} animals, {data.WildAnimals.Count} wild)" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: save failed for '{path}' — {e.Message}" );
		}

		DebugStats.StopTimer( "Saving", clock );
	}

	/// <summary>
	/// If live capture lost all player-built objects but a prior snapshot/disk save
	/// still has them, merge that layout back before writing.
	/// </summary>
	private void ProtectBuiltWorldFromEmptyCapture( SaveData data, string path )
	{
		if ( data is null || HasBuiltWorldContent( data ) )
			return;

		SaveData source = null;
		if ( HasBuiltWorldContent( _lastGoodSnapshot ) )
			source = _lastGoodSnapshot;
		else if ( TryReadSaveFile( path, out var disk ) && HasBuiltWorldContent( disk ) )
			source = disk;

		if ( source is null )
			return;

		Log.Warning(
			"Fauna: live capture lost habitats/placeables/animals while a prior save still has them — " +
			"keeping the previous built world (hotload/teardown race)." );
		MergeWorldLayoutFrom( data, source );
	}

	private bool TryCaptureForSave( out SaveData data )
	{
		data = null;

		try
		{
			if ( ZooState.Instance.IsValid() && PlotSystem.Instance.IsValid() )
			{
				data = Capture();
				return true;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: live capture failed — {e.Message}" );
		}

		if ( _lastGoodSnapshot is not null )
		{
			data = CloneSaveData( _lastGoodSnapshot );
			data.SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			Log.Warning( "Fauna: using last in-memory snapshot for save." );
			return true;
		}

		var path = GetActivePath();
		if ( TryReadSaveFile( path, out var disk ) )
		{
			data = disk;
			Log.Warning( "Fauna: using on-disk save as capture fallback." );
			return true;
		}

		return false;
	}

	private void RememberSnapshot( SaveData data )
	{
		if ( data is null ) return;

		// Never let a gutted hotload capture become "last good" — shutdown merge
		// and ProtectBuiltWorldFromEmptyCapture both depend on it.
		if ( _lastGoodSnapshot is not null
			&& HasBuiltWorldContent( _lastGoodSnapshot )
			&& !HasBuiltWorldContent( data ) )
		{
			Log.Warning( "Fauna: refused to replace last-good snapshot with an empty-built capture." );
			return;
		}

		// Capture() allocates a fresh graph each time — no need to JSON-clone ~1000+ terrain cells every autosave.
		_lastGoodSnapshot = data;
	}

	private static SaveData CloneSaveData( SaveData source ) =>
		Json.Deserialize<SaveData>( Json.Serialize( source ) );

	private static SaveData MergeWithCachedSnapshot( SaveData live, SaveData cached )
	{
		if ( cached is null )
			return live;

		// AUDIT FIX B4 + CONTINUE/SAVE FIX (2026-07): Shutdown merge used to restore
		// ANY empty category from the previous snapshot (sell-all animals undid).
		// Then emptiness used HasWorldContent which stayed TRUE when only terrain
		// obstacles remained — hotload wiped habitats/placeables while "preserving".
		//
		// New rule: merge when LIVE lost all player-built content while the cache
		// still has habitats/placeables/animals. Obstacle-only captures count as empty.
		var merged = CloneSaveData( live );

		var liveLostBuilt = !HasBuiltWorldContent( merged );
		var cacheHasBuilt = HasBuiltWorldContent( cached );
		if ( liveLostBuilt && cacheHasBuilt )
		{
			Log.Warning( "Fauna: shutdown live capture lost player-built world — merging layout from last good snapshot." );
			MergeWorldLayoutFrom( merged, cached );
		}

		if ( !merged.HasOwnerPlayerPosition && cached.HasOwnerPlayerPosition )
		{
			merged.HasOwnerPlayerPosition = true;
			merged.OwnerPlayerPosition = cached.OwnerPlayerPosition;
		}

		if ( IsEmptyInventory( merged.OwnerInventory ) && !IsEmptyInventory( cached.OwnerInventory ) )
			merged.OwnerInventory = cached.OwnerInventory;

		return merged;
	}

	private static bool IsEmptyInventory( PlayerInventorySave save )
	{
		save ??= new PlayerInventorySave();
		return save.CarriedCount <= 0
			&& string.IsNullOrEmpty( save.CarriedSpecies0 )
			&& string.IsNullOrEmpty( save.CarriedSpecies1 )
			&& save.BaitCount <= 0
			&& save.TranquilizerCount <= 0
			&& !save.HasNet;
	}

	private static void WriteSaveFile(
		string path,
		SaveData data,
		bool preserveWorldOnEmptyCapture,
		bool verifyWrite,
		bool writeBuiltWorldBackup = true )
	{
		// AUDIT FIX B4 + CONTINUE/SAVE FIX: Shutdown/preserve resurrects layout when
		// player-built content is gone (not merely when TerrainObstacles are empty).
		// Revert hint: use HasWorldContent again only if obstacle-only empties should
		// also trigger disk restore.
		if ( preserveWorldOnEmptyCapture
			&& !HasBuiltWorldContent( data )
			&& TryReadSaveFile( path, out var existing )
			&& HasBuiltWorldContent( existing ) )
		{
			Log.Warning( "Fauna: save capture lost habitats/placeables/animals at shutdown — keeping previous layout on disk." );
			MergeWorldLayoutFrom( data, existing );
		}

		EnsureDirectoryForFile( path );

		// Keep one previous good build on disk so a future wipe can be recovered.
		// Skip on intentional New Game overwrite — otherwise the old zoo is copied to .bak
		// right before the new file is written, and Continue can resurrect it.
		if ( writeBuiltWorldBackup )
			TryWriteBuiltWorldBackup( path, data );

		FileSystem.Data.WriteJson( path, data );

		if ( !FileSystem.Data.FileExists( path ) )
			throw new InvalidOperationException( "save file was not created" );

		if ( !verifyWrite )
			return;

		if ( !TryReadSaveFile( path, out var verify ) )
			throw new InvalidOperationException( "save file could not be read back" );

		if ( verify.SaveSlotId != data.SaveSlotId )
			throw new InvalidOperationException( "save verification failed (slot mismatch)" );
	}

	/// <summary>Player-built / placed content — NOT terrain obstacles or wilds.</summary>
	private static bool HasBuiltWorldContent( SaveData data ) =>
		data is not null && (
			(data.Habitats?.Count ?? 0) > 0 ||
			(data.Placeables?.Count ?? 0) > 0 ||
			(data.Animals?.Count ?? 0) > 0 );

	private static bool HasWorldContent( SaveData data ) =>
		HasBuiltWorldContent( data ) ||
		(data?.TerrainObstacles?.Count ?? 0) > 0 ||
		(data?.WildAnimals?.Count ?? 0) > 0;

	private static void MergeWorldLayoutFrom( SaveData target, SaveData source )
	{
		target.Habitats = source.Habitats ?? new();
		target.Placeables = source.Placeables ?? new();
		target.Animals = source.Animals ?? new();
		target.TerrainObstacles = source.TerrainObstacles ?? new();
		target.TerrainObstaclesCleared = source.TerrainObstaclesCleared;
		target.WildAnimals = source.WildAnimals ?? new();
		target.Plots = source.Plots?.Count > 0 ? source.Plots : target.Plots;
	}

	private static void EnsureDirectoryForFile( string path )
	{
		var dir = System.IO.Path.GetDirectoryName( path )?.Replace( '\\', '/' );
		if ( string.IsNullOrEmpty( dir ) ) return;

		if ( !FileSystem.Data.DirectoryExists( dir ) )
			FileSystem.Data.CreateDirectory( dir );
	}

	private static string GetBackupPath( string path ) => $"{path}.bak";

	private static void TryWriteBuiltWorldBackup( string path, SaveData outgoing )
	{
		if ( !HasBuiltWorldContent( outgoing ) )
			return;

		if ( !TryReadSaveFile( path, out var existing ) || !HasBuiltWorldContent( existing ) )
			return;

		try
		{
			var bak = GetBackupPath( path );
			EnsureDirectoryForFile( bak );
			FileSystem.Data.WriteJson( bak, existing );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: could not write save backup for '{path}' — {e.Message}" );
		}
	}

	private static void TryDeleteBackup( string path )
	{
		var bak = GetBackupPath( path );
		try
		{
			if ( FileSystem.Data.FileExists( bak ) )
				FileSystem.Data.DeleteFile( bak );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: could not delete save backup '{bak}' — {e.Message}" );
		}
	}

	/// <summary>
	/// If the slot looks gutted but a .bak still has habitats/placeables/animals, prefer the backup.
	/// Helps recover from older hotspot wipes that already hit disk.
	/// </summary>
	private static bool TryRecoverBuiltWorldFromBackup( string path, ref SaveData data )
	{
		if ( data is null || HasBuiltWorldContent( data ) )
			return false;

		var bak = GetBackupPath( path );
		if ( !TryReadSaveFile( bak, out var backup ) || !HasBuiltWorldContent( backup ) )
			return false;

		Log.Warning( $"Fauna: slot '{path}' has no player-built world but backup does — restoring layout from '{bak}'." );
		// Keep money/meta from the primary file (those usually survive teardown); restore layout only.
		MergeWorldLayoutFrom( data, backup );
		return true;
	}

	private static bool TryReadSaveFile( string path, out SaveData data )
	{
		data = null;

		if ( !FileSystem.Data.FileExists( path ) )
			return false;

		try
		{
			data = FileSystem.Data.ReadJson<SaveData>( path );
			return data is not null;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: failed to read save '{path}' — {e.Message}" );
			return false;
		}
	}

	public SaveData Capture()
	{
		var state = ZooState.Instance;
		if ( state is null || !state.IsValid() )
			throw new InvalidOperationException( "ZooState is not ready." );

		var plots = PlotSystem.Instance;
		if ( plots is null || !plots.IsValid() )
			throw new InvalidOperationException( "PlotSystem is not ready." );

		var guests = GuestSystem.Instance;
		var social = SocialSystem.Instance;
		var weather = WeatherSeasonSystem.Instance;
		var events = SanctuaryEventSystem.Instance;
		var daily = DailySanctuarySystem.Instance;
		var momentum = SanctuaryMomentumSystem.Instance;
		var staff = StaffSystem.Instance;
		var research = ResearchSystem.Instance;
		var franchise = FranchiseSystem.Instance;

		var data = new SaveData
		{
			SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

			ZooName = state.ZooName,
			Money = state.Money,
			Xp = state.Xp,
			Level = state.Level,
			Prestige = state.Prestige,
			TotalEarned = state.TotalEarned,
			TotalSpent = state.TotalSpent,
			TutorialAnimalClaimed = state.TutorialAnimalClaimed,
			TotalAnimalsBought = state.TotalAnimalsBought,
			TotalAnimalsBred = state.TotalAnimalsBred,
			TotalAnimalsCaught = state.TotalAnimalsCaught,
			OwnerInventory = CaptureOwnerInventory(),
			HasOwnerPlayerPosition = CaptureOwnerPlayerPosition( out var ownerPlayerPosition ),
			OwnerPlayerPosition = ownerPlayerPosition,

			StarterProfileId = state.StarterProfileId,
			StarterBiome = (int)state.StarterBiome,
			GuestAppealModifier = state.GuestAppealModifier,
			NativeBiomeHappinessBonus = state.NativeBiomeHappinessBonus,
			NativeGuestAppealBonus = state.NativeGuestAppealBonus,
			GuestCapBonus = state.GuestCapBonus,
			SaveSlotId = ActiveSlotId,

			ObjectiveIndex = ObjectiveSystem.Instance?.CurrentIndex ?? 0,
			GuestRatingsReviewed = ObjectiveSystem.Instance?.GuestRatingsReviewed ?? false,
			ChallengeIndex = 0,
			LoginStreak = DailyBonusSystem.Instance?.LoginStreak ?? 0,
			GuestMilestoneFlags = ZooMilestones.Instance?.GuestMilestoneFlags ?? 0,
			CodexTierFlags = ZooMilestones.Instance?.CodexTierFlags ?? 0,
			HabitatTierFlags = ZooMilestones.Instance?.HabitatTierFlags ?? 0,
			ProfitableNotified = ZooMilestones.Instance?.ProfitableNotified ?? false,
			EconomyTutorialShown = ZooMilestones.Instance?.EconomyTutorialShown ?? false,
			AchievementFlags = AchievementSystem.Instance?.UnlockedFlags ?? 0,
			BreedingHistory = BreedingSystem.Instance?.History.ToList() ?? new(),

			TerrainObstacles = CaptureTerrainObstacles( out var terrainObstaclesCleared ),
			TerrainObstaclesCleared = terrainObstaclesCleared,

			Plots = plots.OwnedPlots.ToList(),

			GuestCount = guests?.GuestCount ?? 0,
			PeakGuests = guests?.PeakGuests ?? 0,
			Cleanliness = guests?.Cleanliness ?? 100f,
			GuestSatisfaction = guests?.Satisfaction ?? 75f,

			Likes = social?.Likes.ToList() ?? new(),
			SocialFavorites = social?.Favorites.ToList() ?? new(),
			TotalVisitors = social?.TotalVisitors ?? 0,
			VisitBonusDays = social?.VisitBonusDays.ToDictionary( kv => kv.Key.ToString(), kv => kv.Value ) ?? new(),
			LastDailyBonusUnixDay = DailyBonusSystem.Instance?.LastBonusUnixDay ?? 0,
			WeeklyBestScore = social?.WeeklyBestScore ?? 0,
			WeeklyTheme = social?.WeeklyTheme ?? "",

			WeatherDay = weather?.Day ?? 1,
			Season = (int)(weather?.Season ?? FaunaSeason.Spring),
			Weather = (int)(weather?.Weather ?? FaunaWeather.Clear),
			ActiveEventTitle = events?.ActiveEventTitle ?? "",
			ActiveEventDetail = events?.ActiveEventDetail ?? "",
			ActiveEventIcon = events?.ActiveEventIcon ?? "event",
			RareSightingSpeciesId = events?.RareSightingSpeciesId ?? "",
			EventGuestAppealBonus = events?.GuestAppealBonus ?? 0f,
			EventIncomeMultiplier = events?.IncomeMultiplier ?? 1f,
			EventRareSpawnMultiplier = events?.RareSpawnMultiplier ?? 1f,
			EventBuildCostMultiplier = events?.BuildCostMultiplier ?? 1f,
			DailySeed = daily?.DailySeed ?? 0,
			DailyCompletedMask = daily?.CompletedMask ?? 0,
			DailyStartingCleared = daily?.StartingCleared ?? 0,
			DailyStartingGuests = daily?.StartingGuests ?? 0,
			DailyStartingEarned = daily?.StartingEarned ?? 0,
			DailyStartingBred = daily?.StartingBred ?? 0,
			DailyStartingCaught = daily?.StartingCaught ?? 0,
			DailyStartingPlaceables = daily?.StartingPlaceables ?? 0,
			MomentumCompletedMask = momentum?.CompletedMask ?? 0,
			MomentumPoints = momentum?.MomentumPoints ?? 0,
			MomentumEventGranted = momentum?.MomentumEventGranted ?? false,
			StaffKeepers = staff?.Keepers ?? 0,
			StaffCleaners = staff?.Cleaners ?? 0,
			StaffGuides = staff?.Guides ?? 0,
			StaffVets = staff?.Vets ?? 0,
			ResearchHabitatCare = research?.HabitatCare ?? 0,
			ResearchAnimalCare = research?.AnimalCare ?? 0,
			ResearchGuestComfort = research?.GuestComfort ?? 0,
			ResearchFieldTools = research?.FieldTools ?? 0,
			ResearchDecorationDesign = research?.DecorationDesign ?? 0,
			FranchiseRank = franchise?.FranchiseRank ?? 0,
			LegacyTokens = franchise?.LegacyTokens ?? 0,
			BranchExpansions = franchise?.BranchExpansions ?? 0,
		};

		var codex = CollectionSystem.Instance;
		if ( codex.IsValid() )
		{
			foreach ( var kv in codex.Species ) data.CodexSpecies[kv.Key] = kv.Value;
			foreach ( var kv in codex.Variants ) data.CodexVariants[kv.Key] = kv.Value;
		}

		foreach ( var habitat in FindPersistedHabitats() )
		{
			data.Habitats.Add( new HabitatSave
			{
				HabitatId = habitat.HabitatId,
				DefinitionId = habitat.DefinitionId,
				Position = new SaveVector3( habitat.GameObject.WorldPosition ),
			} );
		}

		foreach ( var placeable in FindPersistedPlaceables() )
		{
			data.Placeables.Add( new PlaceableSave
			{
				DefinitionId = placeable.DefinitionId,
				Position = new SaveVector3( placeable.GameObject.WorldPosition ),
				Yaw = placeable.GameObject.WorldRotation.Yaw(),
				UncollectedRevenue = placeable.Components.Get<RestaurantComponent>()?.Uncollected ?? 0f,
			} );
		}

		foreach ( var animal in FindPersistedAnimals() )
		{
			data.Animals.Add( new AnimalSave
			{
				AnimalId = animal.AnimalId,
				DefinitionId = animal.DefinitionId,
				VariantId = animal.VariantId,
				Name = animal.AnimalName,
				HabitatId = animal.HabitatId,
				Position = new SaveVector3( animal.GameObject.WorldPosition ),
				Hunger = animal.Hunger,
				Happiness = animal.Happiness,
				Health = animal.Health,
				AgeSeconds = animal.AgeSeconds,
				Genome = animal.Genome,
			} );
		}

		foreach ( var wild in CaptureWildAnimals() )
			data.WildAnimals.Add( wild );

		return data;
	}

	private static IEnumerable<WildAnimalSave> CaptureWildAnimals()
	{
		foreach ( var wild in WildAnimalRegistry.All )
		{
			if ( !wild.IsValid() || wild.Fled || string.IsNullOrEmpty( wild.SpeciesId ) )
				continue;

			yield return new WildAnimalSave
			{
				WildId = wild.WildId,
				SpeciesId = wild.SpeciesId,
				Position = new SaveVector3( wild.GameObject.WorldPosition ),
				PlotX = wild.PlotX,
				PlotY = wild.PlotY,
			};
		}
	}

	private PlayerInventorySave CaptureOwnerInventory()
	{
		if ( PlayerInventory.Local?.IsValid() == true )
			return PlayerInventorySave.From( PlayerInventory.Local );

		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || !player.IsZooOwner ) continue;
			var inv = player.Components.Get<PlayerInventory>();
			if ( inv?.IsValid() == true )
				return PlayerInventorySave.From( inv );
		}

		return _lastGoodSnapshot?.OwnerInventory ?? new PlayerInventorySave();
	}

	private List<TerrainObstacleSave> CaptureTerrainObstacles( out int cleared )
	{
		var terrain = TerrainObstacleSystem.Instance;
		var obstacles = terrain?.CaptureSave() ?? new();
		cleared = terrain?.TotalCleared ?? _terrainObstacleClearedCache;

		if ( obstacles.Count == 0 && _terrainObstacleCache.Count > 0 && (terrain is null || !terrain.IsValid()) )
		{
			obstacles = _terrainObstacleCache;
			cleared = _terrainObstacleClearedCache;
		}
		else
		{
			_terrainObstacleCache = obstacles.ToList();
			_terrainObstacleClearedCache = cleared;
		}

		return obstacles;
	}

	private static bool CaptureOwnerPlayerPosition( out SaveVector3 position )
	{
		position = new SaveVector3();

		var local = PlayerState.Local;
		if ( local?.IsValid() == true && local.IsZooOwner )
		{
			position = new SaveVector3( local.FeetPosition );
			return true;
		}

		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || !player.IsZooOwner ) continue;
			position = new SaveVector3( player.FeetPosition );
			return true;
		}

		return false;
	}

	private IEnumerable<HabitatComponent> FindPersistedHabitats()
	{
		foreach ( var habitat in HabitatRegistry.All )
		{
			if ( !habitat.IsValid() || string.IsNullOrEmpty( habitat.HabitatId ) ) continue;
			yield return habitat;
		}
	}

	private IEnumerable<PlaceableComponent> FindPersistedPlaceables()
	{
		foreach ( var placeable in PlaceableRegistry.All )
		{
			if ( !placeable.IsValid() || string.IsNullOrEmpty( placeable.DefinitionId ) ) continue;
			yield return placeable;
		}
	}

	private IEnumerable<AnimalComponent> FindPersistedAnimals()
	{
		foreach ( var animal in AnimalRegistry.All )
		{
			if ( !animal.IsValid() || string.IsNullOrEmpty( animal.AnimalId ) ) continue;
			yield return animal;
		}
	}

	// ── Load ────────────────────────────────────────────────

	// ── Slots ───────────────────────────────────────────────

	public static string GetSlotPath( int slotId ) => GameConstants.SaveSlotPath( slotId );

	public string GetActivePath() => GetSlotPath( ActiveSlotId );

	public static List<SaveSlotInfo> ListSlots() => BuildSlotList();

	/// <summary>Removes a save file from disk. Slot 0 is the legacy save.</summary>
	public static bool DeleteSlot( int slotId )
	{
		var path = slotId == 0 ? GameConstants.LegacySaveFile : GetSlotPath( slotId );
		var existed = FileSystem.Data.FileExists( path ) || FileSystem.Data.FileExists( GetBackupPath( path ) );
		if ( !existed )
			return false;

		try
		{
			TryDeleteSlotFiles( path );
			Log.Info( $"Fauna: deleted save slot {(slotId == 0 ? "legacy" : slotId.ToString())}." );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: failed to delete save — {e.Message}" );
			return false;
		}
	}

	/// <summary>Delete slot JSON and its .bak so overwrite / delete cannot leave a recoverable old zoo.</summary>
	private static void TryDeleteSlotFiles( string path )
	{
		TryDeleteFile( path );
		TryDeleteBackup( path );
	}

	private static void TryDeleteFile( string path )
	{
		try
		{
			if ( FileSystem.Data.FileExists( path ) )
				FileSystem.Data.DeleteFile( path );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: could not delete save '{path}' — {e.Message}" );
		}
	}

	private static List<SaveSlotInfo> BuildSlotList()
	{
		var slots = new List<SaveSlotInfo>();

		for ( var i = 1; i <= GameConstants.SaveSlotCount; i++ )
			slots.Add( ReadSlotInfo( i, isLegacy: false ) );

		if ( FileSystem.Data.FileExists( GameConstants.LegacySaveFile ) )
			slots.Add( ReadLegacySlotInfo() );

		return slots;
	}

	private static SaveSlotInfo ReadSlotInfo( int slotId, bool isLegacy )
	{
		var path = isLegacy ? GameConstants.LegacySaveFile : GetSlotPath( slotId );
		if ( !FileSystem.Data.FileExists( path ) )
		{
			return new SaveSlotInfo
			{
				SlotId = slotId,
				Exists = false,
				Label = $"Save Slot {slotId}",
				ZooName = "",
			};
		}

		try
		{
			if ( !TryReadSaveFile( path, out var data ) )
				return EmptySlot( slotId );

			return new SaveSlotInfo
			{
				SlotId = slotId,
				Exists = true,
				Label = $"Save Slot {slotId}",
				ZooName = string.IsNullOrEmpty( data.ZooName ) ? "Unnamed Zoo" : data.ZooName,
				Level = data.Level,
				Money = data.Money,
				StarterProfileId = data.StarterProfileId,
				SavedAtUnix = data.SavedAtUnix,
				LoginStreak = data.LoginStreak,
				IsLegacy = isLegacy,
			};
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: could not read save slot {slotId} — {e.Message}" );
			return EmptySlot( slotId );
		}
	}

	private static SaveSlotInfo ReadLegacySlotInfo()
	{
		var info = ReadSlotInfo( 0, isLegacy: true );
		return new SaveSlotInfo
		{
			SlotId = 0,
			Exists = info.Exists,
			Label = "Legacy Save",
			ZooName = info.ZooName,
			Level = info.Level,
			Money = info.Money,
			StarterProfileId = info.StarterProfileId,
			SavedAtUnix = info.SavedAtUnix,
			LoginStreak = info.LoginStreak,
			IsLegacy = true,
		};
	}

	private static SaveSlotInfo EmptySlot( int slotId ) => new()
	{
		SlotId = slotId,
		Exists = false,
		Label = slotId == 0 ? "Legacy Save" : $"Save Slot {slotId}",
	};

	public bool TryLoadSlot( int slotId )
	{
		ActiveSlotId = slotId;
		var path = slotId == 0 ? GameConstants.LegacySaveFile : GetSlotPath( slotId );
		return TryLoadPath( path );
	}

	public void StartNewGame( ZooStarterProfile profile, int slotId )
	{
		if ( !SaveHost.CanPersist ) return;

		ActiveSlotId = slotId;
		PlayerSpawnPoint.ClearRestoredPosition();

		// Drop prior-session memory + disk so Protect / .bak recovery cannot resurrect the old zoo.
		_lastGoodSnapshot = null;
		_eventSaveQueued = false;
		_terrainObstacleCache = new();
		_terrainObstacleClearedCache = 0;
		var path = GetSlotPath( slotId );
		TryDeleteSlotFiles( path );

		ClearWorld();
		ResetProgressionForNewGame();

		ZooState.Instance.ApplyStarterProfile( profile );
		PlotSystem.Instance.SetNewGameDefaults();
		if ( TerrainObstacleSystem.Instance.IsValid() )
		{
			TerrainObstacleSystem.Instance.TotalCleared = 0;
			TerrainObstacleSystem.Instance.GenerateWorld( profile.Biome, PlotSystem.Instance, slotId );
		}

		// No pre-built entrance / path / habitat — player builds the loop themselves.
		if ( WildernessSpawner.Instance.IsValid() )
			WildernessSpawner.Instance.GenerateWorld( profile.Biome );

		ResetOwnerInventory();
		WeatherSeasonSystem.Instance?.Restore( 1, FaunaSeason.Spring, FaunaWeather.Clear );
		SanctuaryEventSystem.Instance?.RollDailyEvent();
		DailySanctuarySystem.Instance?.RollDailyGoals();
		SanctuaryMomentumSystem.Instance?.Restore( 0, 0, false );
		StaffSystem.Instance?.Restore( 0, 0, 0, 0 );
		ResearchSystem.Instance?.Restore( 0, 0, 0, 0, 0 );
		FranchiseSystem.Instance?.Restore( 0, 0 );
		if ( SocialSystem.Instance.IsValid() )
		{
			SocialSystem.Instance.Likes.Clear();
			SocialSystem.Instance.Favorites.Clear();
			SocialSystem.Instance.TotalVisitors = 0;
			SocialSystem.Instance.WeeklyBestScore = 0;
			SocialSystem.Instance.WeeklyTheme = "Popularity Sprint";
			SocialSystem.Instance.VisitBonusDays.Clear();
		}

		if ( ObjectiveSystem.Instance.IsValid() )
		{
			ObjectiveSystem.Instance.CurrentIndex = 0;
			ObjectiveSystem.Instance.GuestRatingsReviewed = false;
		}

		var guests = GuestSystem.Instance;
		if ( guests.IsValid() )
		{
			guests.GuestCount = 0;
			guests.PeakGuests = 0;
			guests.Cleanliness = 100f;
			guests.Satisfaction = 75f;
		}

		// Belt-and-suspenders: reused ZooCore / starter rewards must never boot a "Level 3" new zoo.
		ForceNewGameLevel();

		// Intentional slot wipe — skip Protect + .bak snapshot of the previous zoo.
		Save( verifyWrite: true, allowEmptyBuiltOverwrite: true );
		TryDeleteBackup( path );
		Log.Info( $"Fauna: new zoo '{profile.DisplayName}' in slot {slotId}." );
	}

	/// <summary>Public so GameManager can re-assert Level 1 after post-start catch-up.</summary>
	public void ForceNewGameLevel() => EnsureNewGameLevel( ZooState.Instance );

	/// <summary>Clear codex / milestones / achievements left on a reused ZooCore from a prior session.</summary>
	private static void ResetProgressionForNewGame()
	{
		if ( ObjectiveSystem.Instance.IsValid() )
		{
			ObjectiveSystem.Instance.CurrentIndex = 0;
			ObjectiveSystem.Instance.GuestRatingsReviewed = false;
		}

		CollectionSystem.Instance?.Restore( new Dictionary<string, int>(), new Dictionary<string, bool>() );
		ZooMilestones.Instance?.Restore( 0, 0, 0, false, false );
		AchievementSystem.Instance?.Restore( 0 );

		var breeding = BreedingSystem.Instance;
		if ( breeding.IsValid() )
		{
			breeding.History.Clear();
			breeding.TotalBredCount = 0;
		}

		var daily = DailyBonusSystem.Instance;
		if ( daily.IsValid() )
		{
			daily.LastBonusUnixDay = 0;
			daily.LoginStreak = 0;
		}
	}

	private static void EnsureNewGameLevel( ZooState state )
	{
		if ( !state.IsValid() ) return;

		if ( state.Level != 1 || state.Xp != 0 || state.Prestige != 0 )
		{
			Log.Warning(
				$"Fauna: new game had Level={state.Level} Xp={state.Xp} Prestige={state.Prestige} — forcing Level 1." );
		}

		state.Level = 1;
		state.Xp = 0;
		state.Prestige = 0;
	}

	public bool TryLoad()
	{
		if ( FileSystem.Data.FileExists( GetActivePath() ) )
			return TryLoadPath( GetActivePath() );

		if ( FileSystem.Data.FileExists( GameConstants.LegacySaveFile ) )
			return TryLoadPath( GameConstants.LegacySaveFile );

		return false;
	}

	private bool TryLoadPath( string path )
	{
		if ( !SaveHost.CanPersist ) return false;
		if ( !FileSystem.Data.FileExists( path ) ) return false;

		try
		{
			if ( !TryReadSaveFile( path, out var data ) )
			{
				Log.Warning( $"Fauna: load failed — '{path}' could not be parsed." );
				return false;
			}

			Migrate( data );

			if ( TryRecoverBuiltWorldFromBackup( path, ref data ) )
			{
				// Persist the recovered layout so the next Continue doesn't depend on .bak.
				try
				{
					WriteSaveFile( path, data, preserveWorldOnEmptyCapture: false, verifyWrite: true );
				}
				catch ( Exception e )
				{
					Log.Warning( $"Fauna: recovered layout but could not rewrite '{path}' — {e.Message}" );
				}
			}

			Apply( data );

			if ( data.SaveSlotId > 0 )
				ActiveSlotId = data.SaveSlotId;

			LastSaveSizeBytes = FileSystem.Data.FileExists( path ) ? FileSystem.Data.FileSize( path ) : 0;
			Log.Info( $"Fauna: loaded '{data.ZooName}' from '{path}' (v{data.Version}, {data.Animals.Count} animals, ${data.Money:n0})" );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: load failed for '{path}' — {e.Message}" );
			return false;
		}
	}

	/// <summary>Upgrade older saves step by step to the current schema.</summary>
	private static void Migrate( SaveData data )
	{
		if ( data.Version < 2 )
		{
			// v1 stored engine Vector3 values directly; re-save will convert to SaveVector3.
			data.Version = 2;
		}

		if ( data.Version < 3 )
		{
			data.ChallengeIndex = 0;
			data.LoginStreak = 0;
			data.Version = 3;
		}

		if ( data.Version < 4 )
		{
			data.TerrainObstacles ??= new();
			data.Version = 4;
		}

		if ( data.Version < 5 )
		{
			data.OwnerInventory ??= new();
			data.Version = 5;
		}

		if ( data.Version < 6 )
		{
			data.HasOwnerPlayerPosition = false;
			data.Version = 6;
		}

		if ( data.Version < 7 )
		{
			data.WildAnimals ??= new();
			if ( data.GuestSatisfaction <= 0f )
				data.GuestSatisfaction = 75f;
			data.Version = 7;
		}

		if ( data.Version < 8 )
		{
			data.WeatherDay = Math.Max( 1, data.WeatherDay );
			data.EventIncomeMultiplier = data.EventIncomeMultiplier <= 0f ? 1f : data.EventIncomeMultiplier;
			data.EventRareSpawnMultiplier = data.EventRareSpawnMultiplier <= 0f ? 1f : data.EventRareSpawnMultiplier;
			data.EventBuildCostMultiplier = data.EventBuildCostMultiplier <= 0f ? 1f : data.EventBuildCostMultiplier;
			data.Version = 8;
		}

		if ( data.Version < 9 )
			data.Version = 9;

		if ( data.Version < 10 )
		{
			// Legacy: 7-step tutorial (0–6) then challenges. v10: 8-step tutorial (0–7) then 8+.
			const int tutorialGoalCountV10 = 8;
			if ( data.ChallengeIndex > 0 )
				data.ObjectiveIndex = tutorialGoalCountV10 + data.ChallengeIndex;
			else if ( data.ObjectiveIndex >= 7 )
				data.ObjectiveIndex = tutorialGoalCountV10;
			data.ChallengeIndex = 0;
			data.Version = 10;
		}

		if ( data.Version < 11 )
		{
			// Catch moved from index 8 to index 0; clear→guests shift +1. Breed+ stay put.
			if ( data.ObjectiveIndex == 8 )
				data.ObjectiveIndex = 0;
			else if ( data.ObjectiveIndex >= 0 && data.ObjectiveIndex < 8 )
				data.ObjectiveIndex += 1;
			data.Version = 11;
		}

		if ( data.Version < 12 )
		{
			// v12: find inserted at 0, clear removed. catch→place shift.
			// old: 0 catch, 1 clear, 2 entrance…5 place, 6+ amenities
			// new: 0 find, 1 catch, 2 entrance…5 place, 6+ amenities
			data.ObjectiveIndex = data.ObjectiveIndex switch
			{
				0 => data.TotalAnimalsCaught > 0 ? 1 : 0,
				1 => 2, // was clear → entrance
				_ => data.ObjectiveIndex,
			};
			data.Version = 12;
		}

		if ( data.Version < 13 )
		{
			// v13: shop + ratings tips inserted at 8–9; welcome-10+ shift +2.
			if ( data.ObjectiveIndex >= 8 )
				data.ObjectiveIndex += 2;
			data.Version = 13;
		}

		if ( data.Version < 14 )
		{
			// v14: clear-land tip inserted at 3; path→ratings shift +1.
			if ( data.ObjectiveIndex >= 3 )
				data.ObjectiveIndex += 1;
			data.Version = 14;
		}

		data.Version = SaveData.CurrentVersion;
	}

	public void Apply( SaveData data )
	{
		if ( !SaveHost.CanPersist ) return;

		_isApplying = true;
		try
		{
			ApplyInternal( data );
		}
		finally
		{
			_isApplying = false;
		}
	}

	private void ApplyInternal( SaveData data )
	{
		ClearWorld();

		var state = ZooState.Instance;
		state.ZooName = string.IsNullOrEmpty( data.ZooName ) ? state.ZooName : data.ZooName;
		state.Money = data.Money;
		state.Xp = data.Xp;
		state.Level = Math.Max( 1, data.Level );
		state.Prestige = data.Prestige;
		state.TotalEarned = data.TotalEarned;
		state.TotalSpent = data.TotalSpent;
		state.TutorialAnimalClaimed = data.TutorialAnimalClaimed;
		state.TotalAnimalsBought = data.TotalAnimalsBought;
		state.TotalAnimalsBred = data.TotalAnimalsBred;
		state.TotalAnimalsCaught = data.TotalAnimalsCaught;

		RestoreOwnerInventory( data.OwnerInventory );

		state.StarterProfileId = data.StarterProfileId ?? "";
		state.StarterBiome = (Biome)data.StarterBiome;
		state.GuestAppealModifier = data.GuestAppealModifier;
		state.NativeBiomeHappinessBonus = data.NativeBiomeHappinessBonus;
		state.NativeGuestAppealBonus = data.NativeGuestAppealBonus;
		state.GuestCapBonus = data.GuestCapBonus;

		if ( data.SaveSlotId > 0 )
			ActiveSlotId = data.SaveSlotId;

		if ( ObjectiveSystem.Instance.IsValid() )
		{
			ObjectiveSystem.Instance.CurrentIndex = data.ObjectiveIndex;
			ObjectiveSystem.Instance.GuestRatingsReviewed = data.GuestRatingsReviewed;
		}

		if ( DailyBonusSystem.Instance.IsValid() )
		{
			DailyBonusSystem.Instance.LastBonusUnixDay = data.LastDailyBonusUnixDay;
			DailyBonusSystem.Instance.LoginStreak = data.LoginStreak;
			DailyBonusSystem.Instance.TryGrantDailyBonus();
		}

		WeatherSeasonSystem.Instance?.Restore(
			data.WeatherDay <= 0 ? 1 : data.WeatherDay,
			(FaunaSeason)data.Season,
			(FaunaWeather)data.Weather );

		SanctuaryEventSystem.Instance?.Restore(
			data.ActiveEventTitle,
			data.ActiveEventDetail,
			data.ActiveEventIcon,
			data.RareSightingSpeciesId,
			data.EventGuestAppealBonus,
			data.EventIncomeMultiplier,
			data.EventRareSpawnMultiplier,
			data.EventBuildCostMultiplier );

		DailySanctuarySystem.Instance?.Restore(
			data.DailySeed,
			data.DailyCompletedMask,
			data.DailyStartingCleared,
			data.DailyStartingGuests,
			data.DailyStartingEarned,
			data.DailyStartingBred,
			data.DailyStartingCaught,
			data.DailyStartingPlaceables );

		SanctuaryMomentumSystem.Instance?.Restore(
			data.MomentumCompletedMask,
			data.MomentumPoints,
			data.MomentumEventGranted );

		StaffSystem.Instance?.Restore( data.StaffKeepers, data.StaffCleaners, data.StaffGuides, data.StaffVets );
		ResearchSystem.Instance?.Restore(
			data.ResearchHabitatCare,
			data.ResearchAnimalCare,
			data.ResearchGuestComfort,
			data.ResearchFieldTools,
			data.ResearchDecorationDesign );
		FranchiseSystem.Instance?.Restore( data.FranchiseRank, data.LegacyTokens, data.BranchExpansions );

		ZooMilestones.Instance?.Restore(
			data.GuestMilestoneFlags,
			data.CodexTierFlags,
			data.HabitatTierFlags,
			data.ProfitableNotified,
			data.EconomyTutorialShown );

		AchievementSystem.Instance?.Restore( data.AchievementFlags );
		AchievementSystem.Instance?.Check();

		PlotSystem.Instance.SetOwnedPlots( data.Plots.Count > 0 ? data.Plots : new List<string> { PlotSystem.Key( 0, 0 ) } );

		var terrain = TerrainObstacleSystem.Instance;
		if ( terrain.IsValid() )
		{
			terrain.TotalCleared = data.TerrainObstaclesCleared;

			if ( data.TerrainObstacles is { Count: > 0 } )
			{
				Log.Info( $"Fauna: restoring {data.TerrainObstacles.Count} terrain obstacles (cleared={data.TerrainObstaclesCleared})." );
				terrain.Restore( data.TerrainObstacles, state.StarterBiome );
			}
			else
			{
				Log.Warning( "Fauna: save has no terrain obstacles — generating layout for this slot." );
				terrain.GenerateWorld( state.StarterBiome, PlotSystem.Instance, data.SaveSlotId > 0 ? data.SaveSlotId : ActiveSlotId );
			}
		}

		CollectionSystem.Instance?.Restore( data.CodexSpecies, data.CodexVariants );

		var breeding = BreedingSystem.Instance;
		if ( breeding.IsValid() )
		{
			breeding.History.Clear();
			breeding.History.AddRange( data.BreedingHistory );
			breeding.TotalBredCount = data.TotalAnimalsBred;
		}

		var guests = GuestSystem.Instance;
		if ( guests.IsValid() )
		{
			guests.GuestCount = data.GuestCount;
			guests.PeakGuests = data.PeakGuests;
			guests.Cleanliness = data.Cleanliness;
			guests.Satisfaction = data.GuestSatisfaction > 0f ? data.GuestSatisfaction : 75f;
		}

		var social = SocialSystem.Instance;
		if ( social.IsValid() )
		{
			social.Likes.Clear();
			foreach ( var id in data.Likes ) social.Likes.Add( id );
			social.Favorites.Clear();
			foreach ( var id in data.SocialFavorites ) social.Favorites.Add( id );
			social.TotalVisitors = data.TotalVisitors;
			social.WeeklyBestScore = data.WeeklyBestScore;
			social.WeeklyTheme = string.IsNullOrEmpty( data.WeeklyTheme ) ? social.WeeklyTheme : data.WeeklyTheme;
			social.VisitBonusDays.Clear();
			foreach ( var kv in data.VisitBonusDays )
			{
				if ( long.TryParse( kv.Key, out var steamId ) )
					social.VisitBonusDays[steamId] = kv.Value;
			}
		}

		// ── Rebuild the world ───────────────────────────────
		Log.Info( $"Fauna: restoring {data.Habitats.Count} habitats, {data.Placeables.Count} placeables, {data.Animals.Count} animals" );

		var habitatsById = new Dictionary<string, HabitatComponent>();

		foreach ( var h in data.Habitats )
		{
			var def = Defs.Placeable( h.DefinitionId );
			if ( def is null )
			{
				Log.Warning( $"Fauna: skipped habitat '{h.HabitatId}' — unknown definition '{h.DefinitionId}'." );
				continue;
			}

			var habitat = BuildSystem.Instance.SpawnHabitat( def, h.Position.ToVector3(), h.HabitatId );
			if ( habitat is not null )
				habitatsById[habitat.HabitatId] = habitat;
		}

		foreach ( var p in data.Placeables )
		{
			var def = Defs.Placeable( p.DefinitionId );
			if ( def is null )
			{
				Log.Warning( $"Fauna: skipped placeable — unknown definition '{p.DefinitionId}'." );
				continue;
			}

			var placeable = BuildSystem.Instance.SpawnPlaceable( def, p.Position.ToVector3(), p.Yaw, p.UncollectedRevenue );
		}

		var restoredAnimals = 0;
		foreach ( var a in data.Animals )
		{
			var def = Defs.Animal( a.DefinitionId );
			if ( !habitatsById.TryGetValue( a.HabitatId, out var habitat ) || habitat is null )
			{
				Log.Warning( $"Fauna: skipped animal '{a.Name}' — habitat '{a.HabitatId}' missing." );
				continue;
			}

			if ( def is null )
			{
				Log.Warning( $"Fauna: skipped animal '{a.Name}' — unknown species '{a.DefinitionId}'." );
				continue;
			}

			var animal = AnimalSystem.Instance.Spawn(
				def, a.Genome ?? new AnimalGenome(), habitat, a.Position.ToVector3(),
				name: a.Name, animalId: a.AnimalId, ageSeconds: a.AgeSeconds );

			if ( animal is null ) continue;

			animal.VariantId = a.VariantId;
			animal.Hunger = a.Hunger;
			animal.Happiness = a.Happiness;
			animal.Health = a.Health;
			restoredAnimals++;
		}

		Log.Info( $"Fauna: world restored ({habitatsById.Count} habitats, {data.Placeables.Count} placeables, {restoredAnimals} animals)." );

		if ( WildernessSpawner.Instance.IsValid() )
		{
			if ( data.WildAnimals is { Count: > 0 } )
			{
				Log.Info( $"Fauna: restoring {data.WildAnimals.Count} wild animals." );
				WildernessSpawner.Instance.Restore( data.WildAnimals );
			}
			else
			{
				WildernessSpawner.Instance.GenerateWorld( state.StarterBiome );
			}
		}

		RestoreOwnerPlayerPosition( data );
		RememberSnapshot( data );

		ObjectiveSystem.Instance?.RewindIndexIfBehindWorld();
	}

	private static void RestoreOwnerPlayerPosition( SaveData data )
	{
		if ( !data.HasOwnerPlayerPosition )
		{
			PlayerSpawnPoint.ClearRestoredPosition();
			return;
		}

		PlayerSpawnPoint.SetRestoredPosition( data.OwnerPlayerPosition.ToVector3() );

		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || !player.IsZooOwner || player.IsProxy ) continue;
			player.Components.Get<ZooPlayerController>()?.TeleportToSpawnPoint();
			return;
		}
	}

	private static void RestoreOwnerInventory( PlayerInventorySave save )
	{
		save ??= new PlayerInventorySave();
		var applied = false;

		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || !player.IsZooOwner ) continue;
			var inv = player.Components.Get<PlayerInventory>();
			if ( inv is not null )
			{
				save.ApplyTo( inv );
				applied = true;
			}
		}

		if ( !applied && PlayerInventory.Local is not null )
			save.ApplyTo( PlayerInventory.Local );
	}

	private static void ResetOwnerInventory()
	{
		var reset = false;

		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || !player.IsZooOwner ) continue;
			var inv = player.Components.Get<PlayerInventory>();
			inv?.ResetToStarterKit();
			reset = true;
		}

		if ( !reset )
			PlayerInventory.Local?.ResetToStarterKit();
	}

	/// <summary>Destroys every networked zoo object (used before loading and by fauna_wipe).</summary>
	public void ClearWorld()
	{
		if ( !Networking.IsHost ) return;

		foreach ( var animal in AnimalRegistry.All.ToList() )
		{
			if ( animal.IsValid() ) animal.GameObject.Destroy();
		}

		foreach ( var placeable in PlaceableRegistry.All.ToList() )
		{
			if ( placeable.IsValid() ) placeable.GameObject.Destroy();
		}

		foreach ( var habitat in HabitatRegistry.All.ToList() )
		{
			if ( habitat.IsValid() ) habitat.GameObject.Destroy();
		}

		foreach ( var obstacle in TerrainObstacleRegistry.All.ToList() )
		{
			if ( obstacle.IsValid() ) obstacle.GameObject.Destroy();
		}

		foreach ( var wild in WildAnimalRegistry.All.ToList() )
		{
			if ( wild.IsValid() ) wild.GameObject.Destroy();
		}

		if ( WildernessSpawner.Instance.IsValid() )
			WildernessSpawner.Instance.Clear();

		AnimalRegistry.Clear();
		PlaceableRegistry.Clear();
		HabitatRegistry.Clear();
		TerrainObstacleRegistry.Clear();
		WildAnimalRegistry.Clear();

		GameEvents.RaiseZooReset();
	}
}
