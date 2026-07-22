namespace Fauna2;

/// <summary>Loads and plays zoo audio from Assets/sounds/.</summary>
public static class ZooSoundEffects
{
	private static readonly Dictionary<string, SoundFile> _files = new();

	private static readonly Dictionary<string, string> AnimalPlacementSounds = new( StringComparer.OrdinalIgnoreCase )
	{
		["wolf"] = "wolf_sound",
		["deer"] = "deer_sound",
		["moose"] = "moose_sound",
		["fox"] = "panther_sound",
		["blackbear"] = "panther_sound",
		["black_bear"] = "panther_sound",
		["alligator"] = "panther_sound",
		["rabbit"] = "deer_sound",
		["squirrel"] = "deer_sound",
	};

	private const string DefaultAnimalSound = "deer_sound";

	public static void Preload()
	{
		foreach ( var stem in new[]
		{
			"safari_music", "ambience_sound", "button", "place", "build", "demolish", "placement_error",
			"breedingdiscovery", "economy", "milestoneprogressionlevelup", "water_noise",
			"wolf_sound", "deer_sound", "moose_sound", "panther_sound", "walk_grass",
			"tree_remove", "rock_remove",
		} )
		{
			GetSound( stem );
		}
	}

	public static SoundHandle Play2D( string stem, float volume = 0.5f, float fadeIn = 0f )
	{
		var file = GetSound( stem );
		if ( file is null ) return default;

		var handle = Sound.PlayFile( file, ScaleVolume( volume ), 1f, fadeIn );
		Configure2D( handle );
		return handle;
	}

	public static SoundHandle Play3D( string stem, Vector3 position, float volume = 0.5f, float maxDistance = 6000f )
	{
		var file = GetSound( stem );
		if ( file is null ) return default;

		var handle = Sound.PlayFile( file, ScaleVolume( volume ), 1f, 0f );
		Configure3D( handle, position, maxDistance );
		return handle;
	}

	public static void PlayAnimalPlaced( string definitionId, Vector3 position ) =>
		Play3D( AnimalPlacementStem( definitionId ), position, 0.475f );

	public static string AnimalPlacementStem( string definitionId )
	{
		var stem = Defs.ResourceStem( definitionId );
		if ( !AnimalPlacementSounds.TryGetValue( stem, out var soundStem ) )
			soundStem = DefaultAnimalSound;
		return soundStem;
	}

	public static void PlayUiClick() => Play2D( "button", 0.275f );

	public static void PlayWalkGrass( float volume = 0.2f ) => Play2D( "walk_grass", volume );

	public const float PlaceVolume = 0.35f;

	public static void PlayPlace() => Play2D( "place", PlaceVolume );

	public static void PlayObstacleClear( TerrainObstacleType type )
	{
		var stem = type == TerrainObstacleType.Tree ? "tree_remove" : "rock_remove";
		Play2D( stem, 0.425f );
	}

	public static void PlayBuild() => Play2D( "build", PlaceVolume );

	public static void PlayDemolish() => Play2D( "demolish", 0.375f );

	public static void PlayPlacementError() => Play2D( "placement_error", 0.325f );

	public const float EconomyVolume = 0.075f;

	public static void PlayEconomy() => Play2D( "economy", EconomyVolume );

	public static void PlayDiscovery() => Play2D( "breedingdiscovery", 0.425f );

	public static void PlayMilestone() => Play2D( "milestoneprogressionlevelup", 0.45f );

	public static SoundHandle StartLoop2D( string stem, float volume = 0.5f, float fadeIn = 1.5f ) =>
		Play2D( stem, volume, fadeIn );

	public static SoundHandle StartLoop3D( string stem, Vector3 position, float volume = 0.225f, float maxDistance = 2500f ) =>
		Play3D( stem, position, volume, maxDistance );

	public static bool ShouldRestartLoop( SoundHandle handle, SoundFile file )
	{
		if ( file is null ) return false;

		try
		{
			if ( !handle.IsValid ) return true;
			if ( handle.IsStopped ) return true;
			return !handle.IsPlaying;
		}
		catch
		{
			return true;
		}
	}

	public static SoundFile GetSoundFile( string stem ) => GetSound( stem );

	private static float ScaleVolume( float volume ) =>
		volume * GameSettings.VolumeMultiplier;

	private static SoundFile GetSound( string stem )
	{
		if ( _files.TryGetValue( stem, out var cached ) )
			return cached;

		SoundFile file = null;
		foreach ( var path in new[] { $"sounds/{stem}.vsnd", $"sounds/{stem}.mp3", stem } )
		{
			file = SoundFile.Load( path );
			if ( file is not null && file.IsValid )
				break;
		}

		if ( file is null || !file.IsValid )
		{
			Fauna2Debug.Warn( "Sound", $"Could not load '{stem}'" );
			return null;
		}

		_files[stem] = file;
		Fauna2Debug.Info( "Sound", $"Loaded '{stem}'" );
		return file;
	}

	private static void Configure2D( SoundHandle handle )
	{
		if ( !handle.IsValid ) return;

		handle.SpacialBlend = 0f;
		handle.DistanceAttenuation = false;
		handle.OcclusionEnabled = false;
	}

	private static void Configure3D( SoundHandle handle, Vector3 position, float maxDistance )
	{
		if ( !handle.IsValid ) return;

		handle.Position = position;
		handle.SpacialBlend = 1f;
		handle.DistanceAttenuation = true;
		handle.Distance = maxDistance;
		handle.OcclusionEnabled = false;
	}
}
