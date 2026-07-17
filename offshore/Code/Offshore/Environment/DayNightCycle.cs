namespace Offshore;

/// <summary>
/// Continuous 24h clock driving the sky. Syncs discrete <see cref="TimeOfDay"/> for gameplay.
/// </summary>
public sealed class DayNightCycle : Component
{
	/// <summary>Real seconds for a full midnight→midnight loop. Lower = faster cycle for testing.</summary>
	[Property] public float DayLengthSeconds { get; set; } = 300f;

	/// <summary>0 = midnight, 0.25 ≈ dawn, 0.5 = noon, 0.75 ≈ dusk.</summary>
	public float Phase01 { get; private set; } = 0.32f;

	/// <summary>Hour of day in [0, 24).</summary>
	public float HourOfDay => Phase01 * 24f;

	public TimeOfDay Bucket => Phase01 switch
	{
		< 0.22f => TimeOfDay.Night,
		< 0.35f => TimeOfDay.Dawn,
		< 0.68f => TimeOfDay.Day,
		< 0.82f => TimeOfDay.Dusk,
		_ => TimeOfDay.Night
	};

	/// <summary>0 at deep night / post-sunset, 1 at bright noon.</summary>
	public float Daylight01
	{
		get
		{
			var h = HourOfDay;
			if ( h is >= 10f and <= 16f )
				return 1f;
			if ( h < 5f || h > 21f )
				return 0f;
			if ( h < 10f )
				return Smooth01( (h - 5f) / 5f );
			return Smooth01( (21f - h) / 5f );
		}
	}

	/// <summary>Peaks near sunrise/sunset for warm sky washes.</summary>
	public float Golden01
	{
		get
		{
			var h = HourOfDay;
			var dawn = MathF.Exp( -MathF.Pow( (h - 6.2f) / 1.1f, 2f ) );
			var dusk = MathF.Exp( -MathF.Pow( (h - 18.5f) / 1.2f, 2f ) );
			return Math.Clamp( MathF.Max( dawn, dusk ), 0f, 1f );
		}
	}

	/// <summary>Stars / moon visible amount.</summary>
	public float Night01 => Math.Clamp( 1f - Daylight01 * 1.15f, 0f, 1f );

	public void SetHour( float hour )
	{
		Phase01 = Fract( hour / 24f );
		PushBucket();
	}

	public void AddHours( float hours )
	{
		Phase01 = Fract( Phase01 + hours / 24f );
		PushBucket();
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		if ( !OffshoreGameController.BootComplete )
			return;

		var len = MathF.Max( 5f, DayLengthSeconds );
		Phase01 = Fract( Phase01 + Time.Delta / len );
		PushBucket();
	}

	private void PushBucket()
	{
		var game = OffshoreGameController.Instance;
		if ( game?.Progression is null )
			return;

		game.Progression.TimeOfDay = Bucket;
	}

	private static float Fract( float v ) => v - MathF.Floor( v );

	private static float Smooth01( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}
}
