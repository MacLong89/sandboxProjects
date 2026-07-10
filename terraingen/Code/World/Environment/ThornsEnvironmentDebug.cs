namespace Terraingen.World.Environment;

public static class ThornsEnvironmentDebug
{
	[ConVar( "thorns_env_debug" )]
	public static bool ShowDebugHud { get; set; }

	[ConCmd( "thorns_time_set" )]
	public static void SetTime( float hours )
	{
		if ( !ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
		{
			Log.Warning( "[Thorns Environment] No ThornsTimeOfDaySystem in scene." );
			return;
		}

		time.SetTimeHours( hours, true );
		if ( ThornsEnvironmentDirector.TryGet( Game.ActiveScene, out var director ) )
			director.ApplyCurrentState();

		Log.Info( $"[Thorns Environment] Time set to {time.ResolvedHours:F2}h and debug time enabled." );
	}

	[ConCmd( "thorns_time_pause" )]
	public static void PauseTime( bool pause )
	{
		if ( !ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
			return;

		time.PauseTime = pause;
		Log.Info( $"[Thorns Environment] PauseTime={pause}" );
	}

	[ConCmd( "thorns_time_speed" )]
	public static void TimeSpeed( float speed )
	{
		ThornsTimeOfDaySystem.TimeSpeedMultiplier = Math.Max( 0.01f, speed );
		Log.Info( $"[Thorns Environment] Time speed x{ThornsTimeOfDaySystem.TimeSpeedMultiplier:F2}" );
	}

	[ConCmd( "thorns_time_debug" )]
	public static void DebugTime( float hours )
	{
		if ( !ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
			return;

		time.DebugTimeHours = ThornsEnvironmentMath.WrapHours( hours );
		time.UseDebugTime = true;
		Log.Info( $"[Thorns Environment] Debug time {time.DebugTimeHours:F2}h" );
	}

	[ConCmd( "thorns_time_resume" )]
	public static void ResumeTime()
	{
		if ( !ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
			return;

		time.UseDebugTime = false;
		time.PauseTime = false;
		Log.Info( "[Thorns Environment] Time resumed." );
	}

	[ConCmd( "thorns_env_status" )]
	public static void Status()
	{
		if ( !ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
		{
			Log.Warning( "[Thorns Environment] No ThornsTimeOfDaySystem in scene." );
			return;
		}

		var state = time.CurrentState;
		Log.Info(
			$"[Thorns Environment] time={state.Hours:F2} phase={state.Phase} " +
			$"sunDir={state.SunDirection} sunHeight={state.SunHeight:F2} sunI={state.SunIntensity:F2} " +
			$"ambient={state.AmbientIntensity:F2} fog={state.FogDensity:F3} " +
			$"zenith={state.ZenithColor} horizon={state.HorizonColor}" );
	}

	[ConCmd( "thorns_sky_rebind" )]
	public static void RebindSky()
	{
		var sky = FindSkyController();
		if ( sky is null || !sky.IsValid() )
		{
			Log.Warning( "[Thorns Environment] No ThornsSkyController in scene." );
			return;
		}

		sky.ForceReloadMaterial();
		if ( ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
		{
			if ( ThornsEnvironmentDirector.TryGet( Game.ActiveScene, out var director ) )
				director.ApplyCurrentState();

			sky.ApplyEnvironment( time.CurrentState );
		}

		Log.Info( "[Thorns Environment] Sky material rebound." );
	}

	[ConCmd( "thorns_sky_status" )]
	public static void SkyStatus()
	{
		var sky = FindSkyController();
		if ( sky is null || !sky.IsValid() )
		{
			Log.Warning( "[Thorns Environment] No ThornsSkyController in scene." );
			return;
		}

		if ( ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) )
			Log.Info( $"[Thorns Environment] Sky status: {sky.BuildStatusLine( time.CurrentState )}" );
		else
			Log.Info( $"[Thorns Environment] Sky status: {sky.BuildStatusLine()}" );

		var camera = Game.ActiveScene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c is not null && c.IsValid() );
		if ( camera is not null && camera.IsValid() )
			Log.Info( $"[Thorns Environment] Camera sky: clear={camera.ClearFlags} background={camera.BackgroundColor}" );

		if ( ThornsEnvironmentDirector.TryGet( Game.ActiveScene, out var director )
		     && director.Clouds is not null && director.Clouds.IsValid()
		     && director.Clouds.Billboards is not null && director.Clouds.Billboards.IsValid() )
		{
			Log.Info( $"[Thorns Environment] Clouds: {director.Clouds.Billboards.BuildDebugStatus()}" );
		}
	}

	static ThornsSkyController FindSkyController()
	{
		if ( ThornsEnvironmentDirector.TryGet( Game.ActiveScene, out var director ) && director.Sky is not null && director.Sky.IsValid() )
			return director.Sky;

		if ( Game.ActiveScene is null || !Game.ActiveScene.IsValid() )
			return null;

		foreach ( var sky in Game.ActiveScene.GetAllComponents<ThornsSkyController>() )
		{
			if ( sky is not null && sky.IsValid() && sky.Enabled )
				return sky;
		}

		return null;
	}

	public static string BuildHudLine( ThornsEnvironmentState state )
	{
		return $"Time: {state.Hours:F2} SunHeight: {state.SunHeight:F2} SunFactor: {state.SunFactor:F2} Phase: {state.Phase}";
	}
}
