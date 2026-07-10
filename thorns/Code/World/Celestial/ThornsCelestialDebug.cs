namespace Sandbox;

/// <summary>Console helpers for <see cref="ThornsCelestialSystem"/>.</summary>
public static class ThornsCelestialDebug
{
	[ConVar( "sky_debug" )]
	public static bool SkyDebug { get; set; }

	[ConVar( "freeze_time" )]
	public static bool FreezeTime { get; set; }

	[ConCmd( "set_time", ConVarFlags.Server )]
	public static void SetTime( float time01 )
	{
		if ( !ThornsCelestialSystem.TryGet( Game.ActiveScene, out var celestial ) )
		{
			Log.Warning( "[Thorns Celestial] set_time: no ThornsCelestialSystem in scene." );
			return;
		}

		celestial.SetTimeOfDay( time01 );
		Log.Info( $"[Thorns Celestial] Time set to {celestial.TimeOfDay01:F3}" );
	}

	[ConCmd( "sky_capture_palette", ConVarFlags.Server )]
	public static void CapturePalette()
	{
		if ( !ThornsCelestialSystem.TryGet( Game.ActiveScene, out var celestial ) )
		{
			Log.Warning( "[Thorns Celestial] sky_capture_palette: no ThornsCelestialSystem in scene." );
			return;
		}

		celestial.CaptureComputedSkyToLiveOverrides();
	}

	[ConCmd( "sky_print_state", ConVarFlags.Server )]
	public static void PrintState()
	{
		if ( !ThornsCelestialSystem.TryGet( Game.ActiveScene, out var celestial ) )
		{
			Log.Warning( "[Thorns Celestial] sky_print_state: no ThornsCelestialSystem in scene." );
			return;
		}

		var s = celestial.CurrentState;
		Log.Info(
			$"[Thorns Celestial] time={s.TimeOfDay01:F3} exp={s.SkyExposure:F2} glow={s.HorizonGlowStrength:F2} " +
			$"zenith={s.SkyZenith} horizon={s.SkyHorizon} fog={s.FogColor}" );
	}
}
