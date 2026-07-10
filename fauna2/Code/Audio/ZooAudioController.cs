namespace Fauna2;

/// <summary>Client-local music, ambience, water loops, and gameplay SFX reactions.</summary>
public sealed class ZooAudioController : Component
{
	public static ZooAudioController Instance { get; private set; }

	private enum AudioMode { Menu, InGame }

	private AudioMode _mode;
	private bool _ready;
	private bool _eventsSubscribed;

	private SoundHandle _music;
	private SoundHandle _ambience;
	private readonly Dictionary<PlaceableComponent, SoundHandle> _waterLoops = new();

	private SoundFile _musicFile;
	private SoundFile _ambienceFile;
	private SoundFile _waterFile;

	private float _musicBase = 0.105f;
	private float _ambienceBase = 0.19f;
	private const float WaterLoopBase = 0.175f;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		ZooSoundEffects.Preload();

		_musicFile = ZooSoundEffects.GetSoundFile( "safari_music" );
		_ambienceFile = ZooSoundEffects.GetSoundFile( "ambience_sound" );
		_waterFile = ZooSoundEffects.GetSoundFile( "water_noise" );

		_ready = true;
		SyncMode( force: true );
		Fauna2Debug.Info( "Sound", $"Audio ready music={_musicFile is not null} ambience={_ambienceFile is not null}" );
	}

	protected override void OnDestroy()
	{
		UnsubscribeEvents();
		StopBed( ref _music, 0.5f );
		StopBed( ref _ambience, 0.5f );
		StopWaterLoops( 0.5f );
		if ( Instance == this ) Instance = null;
	}

	public void RefreshLoopVolumes()
	{
		var scale = GameSettings.VolumeMultiplier;

		if ( _mode == AudioMode.Menu )
		{
			SetHandleVolume( _music, _musicBase * scale );
			return;
		}

		if ( _mode == AudioMode.InGame )
		{
			SetHandleVolume( _ambience, _ambienceBase * scale );
			foreach ( var handle in _waterLoops.Values )
				SetHandleVolume( handle, WaterLoopBase * scale );
		}
	}

	protected override void OnUpdate()
	{
		if ( !_ready ) return;

		TrySubscribeEvents();
		SyncMode();

		if ( _mode == AudioMode.Menu )
		{
			TickLoop( ref _music, "safari_music", _musicFile, _musicBase );
			StopBed( ref _ambience, 0.75f );
			StopWaterLoops( 0.75f );
			RefreshLoopVolumes();
			return;
		}

		if ( _mode == AudioMode.InGame )
		{
			StopBed( ref _music, 0.75f );
			TickLoop( ref _ambience, "ambience_sound", _ambienceFile, _ambienceBase );
			TickWaterLoops();
			RefreshLoopVolumes();
		}
	}

	private static void SetHandleVolume( SoundHandle handle, float volume )
	{
		try
		{
			if ( !handle.IsValid ) return;
			handle.Volume = volume;
		}
		catch { }
	}

	private void SyncMode( bool force = false )
	{
		var started = GameManager.Instance?.GameStarted ?? false;
		var target = started ? AudioMode.InGame : AudioMode.Menu;
		if ( !force && target == _mode ) return;

		_mode = target;
		StopBed( ref _music, 0.35f );
		StopBed( ref _ambience, 0.35f );
		StopWaterLoops( 0.35f );
	}

	private void TickLoop( ref SoundHandle handle, string stem, SoundFile file, float volume )
	{
		if ( file is null ) return;
		if ( !ZooSoundEffects.ShouldRestartLoop( handle, file ) ) return;

		var fadeIn = 1.25f;
		try { if ( handle.IsValid ) fadeIn = 0f; } catch { fadeIn = 1.25f; }

		handle = ZooSoundEffects.StartLoop2D( stem, volume, fadeIn );
	}

	private void TickWaterLoops()
	{
		if ( _waterFile is null ) return;

		var seen = new HashSet<PlaceableComponent>();
		foreach ( var placeable in PlaceableRegistry.All )
		{
			if ( !placeable.IsValid() ) continue;

			var id = Defs.IdOf( placeable.Definition );
			if ( id is not ("pond" or "fountain") ) continue;

			seen.Add( placeable );
			var pos = placeable.GameObject.WorldPosition + Vector3.Up * 12f;

			if ( _waterLoops.TryGetValue( placeable, out var handle )
				&& ZooSoundEffects.ShouldRestartLoop( handle, _waterFile ) )
			{
				if ( handle.IsValid ) handle.Stop( 0.1f );
				_waterLoops[placeable] = ZooSoundEffects.StartLoop3D( "water_noise", pos, WaterLoopBase, 2200f );
				continue;
			}

			if ( _waterLoops.ContainsKey( placeable ) ) continue;

			_waterLoops[placeable] = ZooSoundEffects.StartLoop3D( "water_noise", pos, WaterLoopBase, 2200f );
		}

		foreach ( var pair in _waterLoops.ToList() )
		{
			if ( seen.Contains( pair.Key ) && pair.Key.IsValid() ) continue;
			if ( pair.Value.IsValid ) pair.Value.Stop( 0.35f );
			_waterLoops.Remove( pair.Key );
		}

		foreach ( var pair in _waterLoops )
		{
			if ( !pair.Key.IsValid() ) continue;
			try
			{
				if ( !pair.Value.IsValid ) continue;
				pair.Value.Position = pair.Key.GameObject.WorldPosition + Vector3.Up * 12f;
			}
			catch { }
		}
	}

	private static void StopBed( ref SoundHandle handle, float fade )
	{
		try
		{
			if ( !handle.IsValid ) return;
			handle.Stop( fade );
		}
		catch { }

		handle = default;
	}

	private void StopWaterLoops( float fade )
	{
		foreach ( var handle in _waterLoops.Values )
			StopHandle( handle, fade );
		_waterLoops.Clear();
	}

	private static void StopHandle( SoundHandle handle, float fade )
	{
		try
		{
			if ( !handle.IsValid ) return;
			handle.Stop( fade );
		}
		catch { }
	}

	private void TrySubscribeEvents()
	{
		if ( _eventsSubscribed ) return;
		if ( !Networking.IsActive ) return;

		_eventsSubscribed = true;
		GameEvents.AnimalBred += OnAnimalBred;
		GameEvents.SpeciesDiscovered += OnSpeciesDiscovered;
		GameEvents.VariantDiscovered += OnVariantDiscovered;
		GameEvents.LevelUp += OnLevelUp;
		GameEvents.PlotPurchased += OnPlotPurchased;
		GameEvents.EconomyGain += OnEconomyGain;
	}

	private void UnsubscribeEvents()
	{
		if ( !_eventsSubscribed ) return;

		GameEvents.AnimalBred -= OnAnimalBred;
		GameEvents.SpeciesDiscovered -= OnSpeciesDiscovered;
		GameEvents.VariantDiscovered -= OnVariantDiscovered;
		GameEvents.LevelUp -= OnLevelUp;
		GameEvents.PlotPurchased -= OnPlotPurchased;
		GameEvents.EconomyGain -= OnEconomyGain;
		_eventsSubscribed = false;
	}

	private void OnAnimalBred( AnimalComponent _ ) => ZooSoundNetwork.PlayForAll( "breedingdiscovery" );
	private void OnSpeciesDiscovered( string _ ) => ZooSoundNetwork.PlayForAll( "breedingdiscovery" );
	private void OnVariantDiscovered( string _ ) => ZooSoundNetwork.PlayForAll( "breedingdiscovery" );
	private void OnLevelUp( int _ ) => ZooSoundNetwork.PlayForAll( "milestoneprogressionlevelup" );
	private void OnPlotPurchased() => ZooSoundNetwork.PlayPlaceForAll();
	private void OnEconomyGain( int _ ) => ZooSoundNetwork.PlayForAll( "economy", ZooSoundEffects.EconomyVolume );
}
