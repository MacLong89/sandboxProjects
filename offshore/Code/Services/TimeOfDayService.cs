namespace Offshore;

/// <summary>Day clock + smoothly blended sky/celestial state for the backdrop.</summary>
public sealed class TimeOfDayService
{
	public float MinuteOfDay { get; private set; }
	public int Day { get; private set; } = 1;
	/// <summary>Game minutes advanced per real second.</summary>
	public float TimeScale { get; set; } = 0.45f;
	public bool Paused;

	public DayPhase Phase => ResolvePhase( MinuteOfDay );
	public float Normalized => MinuteOfDay / (24f * 60f);

	/// <summary>Look-dev helper: jump/scrub the clock without going through Tick.</summary>
	public void SetMinuteOfDay( float minute )
	{
		while ( minute < 0f ) minute += 24f * 60f;
		while ( minute >= 24f * 60f ) minute -= 24f * 60f;
		MinuteOfDay = minute;
	}

	public void AddMinutes( float delta ) => SetMinuteOfDay( MinuteOfDay + delta );

	public void Load( SaveData save )
	{
		Day = save.Day;
		MinuteOfDay = save.MinuteOfDay;
	}

	public void SyncTo( SaveData save )
	{
		save.Day = Day;
		save.MinuteOfDay = MinuteOfDay;
	}

	public void Tick( float dt )
	{
		if ( Paused )
			return;
		MinuteOfDay += dt * TimeScale;
		while ( MinuteOfDay >= 24f * 60f )
		{
			MinuteOfDay -= 24f * 60f;
			Day++;
		}
	}

	public string ClockText
	{
		get
		{
			var hour24 = (int)(MinuteOfDay / 60f) % 24;
			var minute = (int)(MinuteOfDay % 60f);
			var suffix = hour24 >= 12 ? "PM" : "AM";
			var hour = ((hour24 + 11) % 12) + 1;
			return $"{hour}:{minute:00} {suffix}";
		}
	}

	public Color SkyTop => SampleBlended().top;
	public Color SkyHorizon => SampleBlended().horizon;
	public Color WaterTint => SampleBlended().water;
	public Color CloudTint => SampleBlended().cloud;
	public Color SunTint => SampleBlended().sun;

	/// <summary>-1 below horizon, 0 on horizon, 1 zenith.</summary>
	public float SunAltitude
	{
		get
		{
			var t = Normalized;
			// Rise ~5:30 (0.23), set ~19:30 (0.81)
			if ( t < 0.22f || t > 0.82f ) return -0.85f;
			var day = (t - 0.22f) / 0.60f;
			return MathF.Sin( day * MathF.PI );
		}
	}

	public float MoonAltitude
	{
		get
		{
			// Opposite arc to the sun
			var a = -SunAltitude;
			return Math.Clamp( a, -1f, 1f );
		}
	}

	public float StarVisibility
	{
		get
		{
			var sun = SunAltitude;
			if ( sun > 0.15f ) return 0f;
			if ( sun < -0.2f ) return 1f;
			return Math.Clamp( (-sun + 0.05f) / 0.35f, 0f, 1f );
		}
	}

	public float LampFactor
	{
		get
		{
			if ( Phase is DayPhase.Night or DayPhase.PreDawn ) return 1f;
			if ( Phase is DayPhase.Dusk ) return 0.85f;
			if ( Phase is DayPhase.Sunset ) return 0.55f;
			if ( Phase is DayPhase.GoldenHour ) return 0.25f;
			return 0f;
		}
	}

	/// <summary>How many clouds should feel present (0–1), before weather multiplies.</summary>
	public float AmbientCloudCover => Phase switch
	{
		DayPhase.Midday => 0.35f,
		DayPhase.Morning or DayPhase.Afternoon => 0.45f,
		DayPhase.GoldenHour or DayPhase.Sunset => 0.7f,
		DayPhase.Dusk or DayPhase.Sunrise => 0.55f,
		DayPhase.Night or DayPhase.PreDawn => 0.45f,
		_ => 0.4f
	};

	/// <summary>Primary / secondary sky plates + blend for crossfade.</summary>
	public void GetSkyPlates( out string primary, out string secondary, out float blend )
	{
		var h = MinuteOfDay / 60f;
		// Smooth windows between plates
		if ( h < 4.5f )
		{
			primary = "env/sky_night"; secondary = "env/sky_night"; blend = 0f;
		}
		else if ( h < 6.2f )
		{
			primary = "env/sky_night"; secondary = "env/sky_dawn";
			blend = Smooth01( (h - 4.5f) / 1.7f );
		}
		else if ( h < 8f )
		{
			primary = "env/sky_dawn"; secondary = "env/sky_day";
			blend = Smooth01( (h - 6.2f) / 1.8f );
		}
		else if ( h < 16.5f )
		{
			primary = "env/sky_day"; secondary = "env/sky_day"; blend = 0f;
		}
		else if ( h < 18.2f )
		{
			primary = "env/sky_day"; secondary = "env/sky_sunset";
			blend = Smooth01( (h - 16.5f) / 1.7f );
		}
		else if ( h < 20f )
		{
			primary = "env/sky_sunset"; secondary = "env/sky_night";
			blend = Smooth01( (h - 18.2f) / 1.8f );
		}
		else
		{
			primary = "env/sky_night"; secondary = "env/sky_night"; blend = 0f;
		}
	}

	/// <summary>0 = day clouds, 1 = sunset-tinted cloud sprites.</summary>
	public float SunsetCloudMix
	{
		get
		{
			var h = MinuteOfDay / 60f;
			if ( h < 16f || h > 20.5f ) return 0f;
			if ( h < 17.5f ) return Smooth01( (h - 16f) / 1.5f );
			if ( h < 19f ) return 1f;
			return 1f - Smooth01( (h - 19f) / 1.5f );
		}
	}

	public static DayPhase ResolvePhase( float minute )
	{
		var h = minute / 60f;
		if ( h < 5f ) return DayPhase.Night;
		if ( h < 6f ) return DayPhase.PreDawn;
		if ( h < 7.5f ) return DayPhase.Sunrise;
		if ( h < 11f ) return DayPhase.Morning;
		if ( h < 14f ) return DayPhase.Midday;
		if ( h < 16.5f ) return DayPhase.Afternoon;
		if ( h < 18f ) return DayPhase.GoldenHour;
		if ( h < 19.5f ) return DayPhase.Sunset;
		if ( h < 21f ) return DayPhase.Dusk;
		return DayPhase.Night;
	}

	(Color top, Color horizon, Color water, Color cloud, Color sun) SampleBlended()
	{
		var h = MinuteOfDay / 60f;
		var keys = new (float hour, Color top, Color horizon, Color water, Color cloud, Color sun)[]
		{
			// Cloud RGB stays near-white so mid-grey PNG fills never vanish into night sky.
			(0f,   C(0.04f,0.05f,0.12f), C(0.08f,0.10f,0.22f), C(0.03f,0.07f,0.16f), C(0.92f,0.94f,1.0f), C(1f,0.85f,0.55f)),
			(5f,   C(0.10f,0.12f,0.26f), C(0.32f,0.22f,0.35f), C(0.06f,0.14f,0.26f), C(0.94f,0.92f,1.0f), C(1f,0.70f,0.45f)),
			(6.5f, C(0.40f,0.50f,0.82f), C(1.0f,0.55f,0.38f), C(0.18f,0.38f,0.55f), C(1.0f,0.92f,0.88f), C(1f,0.75f,0.40f)),
			(9f,   C(0.42f,0.70f,0.95f), C(0.78f,0.90f,1.0f), C(0.14f,0.48f,0.68f), C(1f,1f,1f), C(1f,0.95f,0.70f)),
			(13f,  C(0.32f,0.62f,0.95f), C(0.70f,0.86f,1.0f), C(0.10f,0.50f,0.72f), C(1f,1f,1f), C(1f,0.98f,0.85f)),
			(16.5f,C(0.45f,0.55f,0.85f), C(0.95f,0.70f,0.45f), C(0.12f,0.42f,0.62f), C(1.0f,0.94f,0.88f), C(1f,0.80f,0.40f)),
			(18f,  C(0.55f,0.32f,0.58f), C(1.0f,0.42f,0.22f), C(0.12f,0.28f,0.48f), C(1.0f,0.88f,0.80f), C(1f,0.55f,0.25f)),
			(19.5f,C(0.22f,0.16f,0.34f), C(0.60f,0.28f,0.32f), C(0.06f,0.14f,0.28f), C(0.95f,0.88f,0.92f), C(1f,0.45f,0.25f)),
			(22f,  C(0.05f,0.07f,0.16f), C(0.12f,0.14f,0.28f), C(0.03f,0.08f,0.18f), C(0.92f,0.94f,1.0f), C(1f,0.85f,0.55f)),
			(24f,  C(0.04f,0.05f,0.12f), C(0.08f,0.10f,0.22f), C(0.03f,0.07f,0.16f), C(0.92f,0.94f,1.0f), C(1f,0.85f,0.55f)),
		};

		for ( var i = 0; i < keys.Length - 1; i++ )
		{
			if ( h >= keys[i].hour && h <= keys[i + 1].hour )
			{
				var span = keys[i + 1].hour - keys[i].hour;
				var t = span <= 0.001f ? 0f : Smooth01( (h - keys[i].hour) / span );
				return (
					Color.Lerp( keys[i].top, keys[i + 1].top, t ),
					Color.Lerp( keys[i].horizon, keys[i + 1].horizon, t ),
					Color.Lerp( keys[i].water, keys[i + 1].water, t ),
					Color.Lerp( keys[i].cloud, keys[i + 1].cloud, t ),
					Color.Lerp( keys[i].sun, keys[i + 1].sun, t )
				);
			}
		}
		return (keys[0].top, keys[0].horizon, keys[0].water, keys[0].cloud, keys[0].sun);
	}

	static float Smooth01( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	static Color C( float r, float g, float b ) => new( r, g, b );
}
