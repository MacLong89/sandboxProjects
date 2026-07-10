namespace Sandbox;

/// <summary>Data-driven pools, cooldowns, and fade defaults for <see cref="ThornsAtmosphericMusic"/>.</summary>
public static class ThornsMusicCatalog
{
	public sealed class TrackEntry
	{
		public string SoundPath { get; init; }
		public float Weight { get; init; } = 1f;
		// Host silence window after this clip ends (real seconds).
		public float DurationSeconds { get; init; } = 180f;
	}

	public sealed class StateDefinition
	{
		public ThornsMusicState State { get; init; }
		public int Priority { get; init; }
		public float FadeInSeconds { get; init; } = 2.5f;
		public float FadeOutSeconds { get; init; } = 1.8f;
		public float Volume { get; init; } = 0.38f;
		public float MinSilenceAfterSeconds { get; init; } = 300f;
		public float MaxSilenceAfterSeconds { get; init; } = 900f;
		public float TriggerChancePerEvaluation { get; init; } = 0.07f;
		public float EvaluationIntervalSeconds { get; init; } = 2.2f;
		public TrackEntry[] Tracks { get; init; } = [];
	}

	static readonly StateDefinition[] States =
	[
		new()
		{
			State = ThornsMusicState.CalmExploration,
			Priority = 20,
			Volume = 0.4f,
			MinSilenceAfterSeconds = 300f,
			MaxSilenceAfterSeconds = 900f,
			TriggerChancePerEvaluation = 0.06f,
			Tracks =
			[
				new() { SoundPath = "sounds/thorns_music_blood_compass.sound", Weight = 1f, DurationSeconds = 185f },
				new() { SoundPath = "sounds/thorns_music_dusty_banjo.sound", Weight = 1.1f, DurationSeconds = 210f }
			]
		},
		new()
		{
			State = ThornsMusicState.NightExploration,
			Priority = 30,
			Volume = 0.34f,
			MinSilenceAfterSeconds = 360f,
			MaxSilenceAfterSeconds = 960f,
			TriggerChancePerEvaluation = 0.08f,
			Tracks =
			[
				new() { SoundPath = "sounds/thorns_music_dusty_banjo.sound", Weight = 1f, DurationSeconds = 210f },
				new() { SoundPath = "sounds/ambient_sound.sound", Weight = 0.85f, DurationSeconds = 200f }
			]
		},
		new()
		{
			State = ThornsMusicState.Rain,
			Priority = 35,
			Volume = 0.32f,
			MinSilenceAfterSeconds = 420f,
			MaxSilenceAfterSeconds = 900f,
			TriggerChancePerEvaluation = 0.09f,
			Tracks =
			[
				new() { SoundPath = "sounds/ambient_sound.sound", Weight = 1f, DurationSeconds = 200f }
			]
		},
		new()
		{
			State = ThornsMusicState.CampfireSafeZone,
			Priority = 40,
			Volume = 0.36f,
			MinSilenceAfterSeconds = 240f,
			MaxSilenceAfterSeconds = 720f,
			TriggerChancePerEvaluation = 0.22f,
			Tracks =
			[
				new() { SoundPath = "sounds/thorns_music_dusty_banjo.sound", Weight = 1f, DurationSeconds = 210f },
				new() { SoundPath = "sounds/thorns_music_blood_compass.sound", Weight = 0.7f, DurationSeconds = 185f }
			]
		},
		new()
		{
			State = ThornsMusicState.PostCombatReflection,
			Priority = 50,
			Volume = 0.3f,
			FadeInSeconds = 3.5f,
			MinSilenceAfterSeconds = 480f,
			MaxSilenceAfterSeconds = 1200f,
			TriggerChancePerEvaluation = 1f,
			Tracks =
			[
				new() { SoundPath = "sounds/ambient_sound.sound", Weight = 1.2f, DurationSeconds = 200f },
				new() { SoundPath = "sounds/thorns_music_blood_compass.sound", Weight = 0.65f, DurationSeconds = 185f }
			]
		},
		new()
		{
			State = ThornsMusicState.BloomCorruption,
			Priority = 45,
			Volume = 0.26f,
			FadeInSeconds = 4f,
			MinSilenceAfterSeconds = 360f,
			MaxSilenceAfterSeconds = 900f,
			TriggerChancePerEvaluation = 0.05f,
			Tracks =
			[
				new() { SoundPath = "sounds/paranoia_sound.sound", Weight = 1f, DurationSeconds = 95f },
				new() { SoundPath = "sounds/ambient_sound.sound", Weight = 0.5f, DurationSeconds = 200f }
			]
		},
		new()
		{
			State = ThornsMusicState.Storm,
			Priority = 38,
			Volume = 0.28f,
			MinSilenceAfterSeconds = 300f,
			MaxSilenceAfterSeconds = 840f,
			TriggerChancePerEvaluation = 0.08f,
			Tracks =
			[
				new() { SoundPath = "sounds/paranoia_sound.sound", Weight = 0.75f, DurationSeconds = 95f },
				new() { SoundPath = "sounds/ambient_sound.sound", Weight = 1f, DurationSeconds = 200f }
			]
		},
		new()
		{
			State = ThornsMusicState.TensionDanger,
			Priority = 55,
			Volume = 0.22f,
			MinSilenceAfterSeconds = 600f,
			MaxSilenceAfterSeconds = 1200f,
			TriggerChancePerEvaluation = 0.03f,
			Tracks =
			[
				new() { SoundPath = "sounds/paranoia_sound.sound", Weight = 1f, DurationSeconds = 95f }
			]
		}
	];

	public static IReadOnlyList<StateDefinition> AllStates => States;

	public static bool TryGetState( ThornsMusicState state, out StateDefinition def )
	{
		foreach ( var s in States )
		{
			if ( s.State == state )
			{
				def = s;
				return true;
			}
		}

		def = null;
		return false;
	}

	public static int GetStatePriority( ThornsMusicState state )
	{
		if ( !TryGetState( state, out var def ) )
			return 0;

		return def.Priority;
	}

	public static bool TryPickTrack( ThornsMusicState state, ref Random rng, out TrackEntry track, out int trackIndex )
	{
		track = null;
		trackIndex = -1;
		if ( !TryGetState( state, out var def ) || def.Tracks.Length == 0 )
			return false;

		var total = 0f;
		foreach ( var t in def.Tracks )
			total += MathF.Max( 0.01f, t.Weight );

		var roll = rng.NextSingle() * total;
		for ( var i = 0; i < def.Tracks.Length; i++ )
		{
			roll -= MathF.Max( 0.01f, def.Tracks[i].Weight );
			if ( roll > 0f )
				continue;

			track = def.Tracks[i];
			trackIndex = i;
			return true;
		}

		track = def.Tracks[^1];
		trackIndex = def.Tracks.Length - 1;
		return true;
	}

	public static bool SoundExists( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		return ResourceLibrary.Get<SoundFile>( path.Trim() ) is not null;
	}
}
