namespace Terraingen.World.Environment;

[Title( "Thorns Time Of Day" )]
[Category( "Environment" )]
[Icon( "schedule" )]
public sealed class ThornsTimeOfDaySystem : Component
{
	public static ThornsTimeOfDaySystem Instance { get; private set; }

	/// <summary>Peak sunrise palette hour (see <see cref="ThornsEnvironmentColorTrack"/>).</summary>
	public const float FreshWorldSunriseHour = 6f;

	[Sync( SyncFlags.FromHost )]
	public float TimeOfDayHours { get; set; } = 8f;

	[Property, Group( "Time" ), Range( 1f, 7200f )]
	public float DayLengthSeconds { get; set; } = 1200f;

	[Property, Group( "Time" ), Range( 0f, 24f )]
	public float StartHour { get; set; } = 8f;

	[Property, Group( "Time" )]
	public bool PauseTime { get; set; }

	[Property, Group( "Time" )]
	public bool UseDebugTime { get; set; }

	[Property, Group( "Time" ), Range( 0f, 24f )]
	public float DebugTimeHours { get; set; } = 8f;

	[Property, Group( "Sun" )]
	public ThornsSunriseDirection SunriseDirection { get; set; } = ThornsSunriseDirection.East;

	[Property, Group( "Sun" ), Range( -180f, 180f )]
	public float SunYawOffset { get; set; }

	[Property, Group( "Sun" ), Range( -30f, 30f )]
	public float SunPitchOffset { get; set; }

	[Property, Group( "Palette" )] public ThornsEnvironmentColorTrack ZenithColors { get; set; } = ThornsEnvironmentColorTrack.ZenithDefaults;
	[Property, Group( "Palette" )] public ThornsEnvironmentColorTrack HorizonColors { get; set; } = ThornsEnvironmentColorTrack.HorizonDefaults;
	[Property, Group( "Palette" )] public ThornsEnvironmentColorTrack SunColors { get; set; } = ThornsEnvironmentColorTrack.SunDefaults;
	[Property, Group( "Palette" )] public ThornsEnvironmentColorTrack AmbientColors { get; set; } = ThornsEnvironmentColorTrack.AmbientDefaults;
	[Property, Group( "Palette" )] public ThornsEnvironmentColorTrack FogColors { get; set; } = ThornsEnvironmentColorTrack.FogDefaults;
	[Property, Group( "Palette" )] public ThornsEnvironmentColorTrack CloudColors { get; set; } = ThornsEnvironmentColorTrack.CloudDefaults;

	[Property, Group( "Lighting" ), Range( 0f, 8f )] public float MaxSunIntensity { get; set; } = 2.35f;
	[Property, Group( "Lighting" ), Range( 0f, 4f )] public float MaxAmbientIntensity { get; set; } = 1.15f;
	[Property, Group( "Lighting" ), Range( 0f, 2f )] public float NightAmbientIntensity { get; set; } = 0.34f;

	[Property, Group( "Sky" ), Range( 0f, 2f )] public float DaySkyExposure { get; set; } = 1.05f;
	[Property, Group( "Sky" ), Range( 0f, 2f )] public float TwilightSkyExposure { get; set; } = 0.9f;
	[Property, Group( "Sky" ), Range( 0f, 2f )] public float NightSkyExposure { get; set; } = 0.42f;
	[Property, Group( "Sky" ), Range( 0f, 2f )] public float MaxStarIntensity { get; set; } = 0.62f;

	[Property, Group( "Fog" ), Range( 0f, 0.25f )] public float DayFogDensity { get; set; } = 0.045f;
	[Property, Group( "Fog" ), Range( 0f, 0.25f )] public float TwilightFogDensity { get; set; } = 0.075f;
	[Property, Group( "Fog" ), Range( 0f, 0.25f )] public float NightFogDensity { get; set; } = 0.035f;
	[Property, Group( "Fog" ), Range( 100f, 100000f )] public float FogStartDistance { get; set; } = 9000f;
	[Property, Group( "Fog" ), Range( 1000f, 250000f )] public float FogEndDistance { get; set; } = 72000f;

	[Property, Group( "Clouds" ), Range( 0f, 1f )] public float DayCloudOpacity { get; set; } = 0.88f;
	[Property, Group( "Clouds" ), Range( 0f, 1f )] public float TwilightCloudOpacity { get; set; } = 0.72f;
	[Property, Group( "Clouds" ), Range( 0f, 1f )] public float NightCloudOpacity { get; set; } = 0.0f;
	[Property, Group( "Clouds" ), Range( 0f, 1f )] public float CloudDriftSpeed { get; set; } = 0.065f;

	public static float TimeSpeedMultiplier { get; set; } = 1f;
	public float ResolvedHours { get; private set; } = 8f;
	public ThornsEnvironmentState CurrentState { get; private set; }
	public float DayPercent => ResolvedHours / 24f;
	public float SunFactor => CurrentState.SunFactor;
	public float NightFactor => CurrentState.NightFactor;

	bool _primed;
	float _clientSmoothHours = 8f;
	float _clientSyncedHours = 8f;
	bool _clientTimeReady;

	protected override void OnAwake()
	{
		Instance = this;
		if ( !_primed )
		{
			TimeOfDayHours = StartHour;
			ResolvedHours = StartHour;
			_primed = true;
		}
		Evaluate();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void TickClock()
	{
		if ( !PauseTime && !UseDebugTime && Game.IsPlaying && (!Networking.IsActive || Networking.IsHost) )
		{
			var dayLength = Math.Max( 1f, DayLengthSeconds );
			TimeOfDayHours = ThornsEnvironmentMath.WrapHours(
				TimeOfDayHours + Time.Delta / dayLength * 24f * Math.Max( 0.01f, TimeSpeedMultiplier ) );
		}

		if ( Networking.IsActive && !Networking.IsHost )
			ResolvedHours = AdvanceClientSmoothHours();
		else
			ResolvedHours = UseDebugTime ? DebugTimeHours : TimeOfDayHours;

		ResolvedHours = ThornsEnvironmentMath.WrapHours( ResolvedHours );
		if ( !Networking.IsActive || Networking.IsHost )
			TimeOfDayHours = ResolvedHours;

		Evaluate();
	}

	float AdvanceClientSmoothHours()
	{
		var synced = ThornsEnvironmentMath.WrapHours( TimeOfDayHours );
		if ( !_clientTimeReady )
		{
			_clientSyncedHours = synced;
			_clientSmoothHours = synced;
			_clientTimeReady = true;
			return synced;
		}

		if ( MathF.Abs( ThornsEnvironmentMath.ShortestHourDelta( _clientSyncedHours, synced ) ) > 0.0001f )
		{
			if ( MathF.Abs( ThornsEnvironmentMath.ShortestHourDelta( _clientSmoothHours, synced ) ) > 0.35f )
				_clientSmoothHours = synced;

			_clientSyncedHours = synced;
		}

		if ( !PauseTime && !UseDebugTime && Game.IsPlaying )
		{
			var dayLength = Math.Max( 1f, DayLengthSeconds );
			_clientSmoothHours = ThornsEnvironmentMath.WrapHours(
				_clientSmoothHours + Time.Delta / dayLength * 24f * Math.Max( 0.01f, TimeSpeedMultiplier ) );

			var error = ThornsEnvironmentMath.ShortestHourDelta( _clientSmoothHours, _clientSyncedHours );
			_clientSmoothHours = ThornsEnvironmentMath.WrapHours(
				_clientSmoothHours - error * Math.Clamp( Time.Delta * 1.5f, 0f, 1f ) );
		}
		else
		{
			_clientSmoothHours = synced;
		}

		return _clientSmoothHours;
	}

	public void SetTimeHours( float hours, bool pauseAfterSet )
	{
		if ( Networking.IsActive && !Networking.IsHost )
		{
			Log.Warning( "[Thorns Environment] Only the host can set time in multiplayer." );
			return;
		}

		TimeOfDayHours = ThornsEnvironmentMath.WrapHours( hours );
		DebugTimeHours = TimeOfDayHours;
		UseDebugTime = pauseAfterSet;
		ResolvedHours = TimeOfDayHours;
		Evaluate();
	}

	void Evaluate()
	{
		CurrentState = ThornsEnvironmentState.Evaluate( this );
	}

	public static bool TryGet( Scene scene, out ThornsTimeOfDaySystem system )
	{
		system = Instance;
		if ( system is not null && system.IsValid() )
			return true;

		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var candidate in scene.GetAllComponents<ThornsTimeOfDaySystem>() )
		{
			if ( candidate is not null && candidate.IsValid() && candidate.Enabled )
			{
				system = candidate;
				return true;
			}
		}

		system = null;
		return false;
	}
}
