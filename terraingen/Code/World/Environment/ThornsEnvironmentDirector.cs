namespace Terraingen.World.Environment;

using Terraingen.TerrainGen;
using Terraingen.Rendering;

[Title( "Thorns Environment Director" )]
[Category( "Environment" )]
[Icon( "tune" )]
public sealed class ThornsEnvironmentDirector : Component
{
	public const string EnvironmentTag = "thorns_environment";
	public static ThornsEnvironmentDirector Instance { get; private set; }

	[Property, Group( "Modules" )] public ThornsTimeOfDaySystem TimeSystem { get; set; }
	[Property, Group( "Modules" )] public ThornsCelestialController Celestial { get; set; }
	[Property, Group( "Modules" )] public ThornsLightingController Lighting { get; set; }
	[Property, Group( "Modules" )] public ThornsSkyController Sky { get; set; }
	[Property, Group( "Modules" )] public ThornsAtmosphereController Atmosphere { get; set; }
	[Property, Group( "Modules" )] public ThornsCloudController Clouds { get; set; }
	[Property, Group( "Modules" )] public ThornsPostProcessTuner PostProcess { get; set; }

	[Property, Group( "Setup" )] public bool AutoCreateSceneObjects { get; set; } = true;
	[Property, Group( "Setup" )] public bool DisableOtherDirectionalLights { get; set; } = true;
	[Property, Group( "Setup" ), Range( 1, 4 )] public int SunShadowCascadeCount { get; set; } = ThornsSunShadowPolicy.DefaultCascadeCount;
	[Property, Group( "Debug" )] public bool LogSetup { get; set; }

	bool _loggedFirstApply;
	bool _modulesBound;

	protected override void OnAwake()
	{
		Instance = this;
		GameObject.Tags.Add( EnvironmentTag );

		if ( AutoCreateSceneObjects )
			EnsureSceneObjects();

		BindModules();
		if ( DisableOtherDirectionalLights )
			DisableForeignDirectionalLights();

		if ( LogSetup )
			Log.Info( "[Thorns Environment] Fresh lighting-test environment initialized." );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( !_modulesBound )
			BindModules();
		if ( TimeSystem is null || !TimeSystem.IsValid() )
			return;

		TimeSystem.TickClock();
		ApplyCurrentState();

		if ( !_loggedFirstApply && Game.IsPlaying )
		{
			_loggedFirstApply = true;
			if ( LogSetup )
			{
				var state = TimeSystem.CurrentState;
				Log.Info( $"[Thorns Environment] First apply: time={state.Hours:F2} phase={state.Phase} sun={state.SunIntensity:F2} fog={state.FogDensity:F3}" );
			}
		}
	}

	public void ApplyCurrentState()
	{
		if ( TimeSystem is null || !TimeSystem.IsValid() )
			return;

		var state = TimeSystem.CurrentState;
		Celestial?.ApplyEnvironment( state );
		Lighting?.ApplyEnvironment( state );
		Sky?.ApplyEnvironment( state );
		Atmosphere?.ApplyEnvironment( state );
		Clouds?.ApplyEnvironment( state );
	}

	void BindModules()
	{
		if ( _modulesBound
		     && TimeSystem is not null && TimeSystem.IsValid()
		     && Sky is not null && Sky.IsValid()
		     && Clouds is not null && Clouds.IsValid()
		     && Clouds.Billboards is not null && Clouds.Billboards.IsValid() )
			return;

		TimeSystem ??= Components.Get<ThornsTimeOfDaySystem>( FindMode.EverythingInSelf );
		Sky ??= Components.Get<ThornsSkyController>( FindMode.EverythingInSelf );
		Atmosphere ??= Components.Get<ThornsAtmosphereController>( FindMode.EverythingInSelfAndDescendants );
		Clouds ??= Components.Get<ThornsCloudController>( FindMode.EverythingInSelf );
		PostProcess ??= Components.Get<ThornsPostProcessTuner>( FindMode.EverythingInSelfAndDescendants );
		if ( PostProcess is null || !PostProcess.IsValid() )
			PostProcess = ThornsPostProcessTuner.EnsureOnEnvironment( GameObject );

		EnsureCloudBillboards();

		if ( Celestial is null || !Celestial.IsValid() || Lighting is null || !Lighting.IsValid() )
		{
			foreach ( var sun in Scene.GetAllComponents<DirectionalLight>() )
			{
				if ( sun is null || !sun.IsValid() || !IsEnvironmentObject( sun.GameObject ) )
					continue;

				Celestial ??= sun.Components.Get<ThornsCelestialController>( FindMode.EverythingInSelf );
				Lighting ??= sun.Components.Get<ThornsLightingController>( FindMode.EverythingInSelf );
				break;
			}
		}

		_modulesBound = TimeSystem is not null && TimeSystem.IsValid();
		ApplySunShadowSettings();
	}

	void ApplySunShadowSettings()
	{
		var sun = Celestial?.SunLight;
		if ( sun is null || !sun.IsValid() )
			return;

		sun.ShadowCascadeCount = Math.Clamp( SunShadowCascadeCount, 1, 4 );
		ThornsSunShadowPolicy.ApplyDirectionalLightSettings( sun, sun.ShadowCascadeCount );
	}

	void RemoveOldAutoSunDisc()
	{
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.IsValid() && string.Equals( go.Name, "Thorns Sun Disc", StringComparison.OrdinalIgnoreCase ) )
				go.Destroy();
		}
	}

	public void EnsureSceneObjects()
	{
		RemoveOldAutoSunDisc();
		TimeSystem ??= Components.Get<ThornsTimeOfDaySystem>( FindMode.EverythingInSelf ) ?? Components.Create<ThornsTimeOfDaySystem>();
		Sky ??= Components.Get<ThornsSkyController>( FindMode.EverythingInSelf ) ?? Components.Create<ThornsSkyController>();
		Clouds ??= Components.Get<ThornsCloudController>( FindMode.EverythingInSelf ) ?? Components.Create<ThornsCloudController>();

		var sunGo = FindOrCreateObject( "Thorns Sun" );
		var sunLight = sunGo.Components.Get<DirectionalLight>( FindMode.EverythingInSelf ) ?? sunGo.Components.Create<DirectionalLight>();
		sunLight.Enabled = true;
		sunLight.Shadows = true;
		sunLight.ShadowCascadeCount = Math.Clamp( SunShadowCascadeCount, 1, 4 );
		ThornsSunShadowPolicy.ApplyDirectionalLightSettings( sunLight, sunLight.ShadowCascadeCount );
		Celestial = sunGo.Components.Get<ThornsCelestialController>( FindMode.EverythingInSelf ) ?? sunGo.Components.Create<ThornsCelestialController>();
		Lighting = sunGo.Components.Get<ThornsLightingController>( FindMode.EverythingInSelf ) ?? sunGo.Components.Create<ThornsLightingController>();
		Celestial.SunLight = sunLight;
		Lighting.SunLight = sunLight;

		var skyGo = FindOrCreateObject( "Thorns Sky" );
		var skyBox = skyGo.Components.Get<SkyBox2D>( FindMode.EverythingInSelf ) ?? skyGo.Components.Create<SkyBox2D>();
		skyBox.Enabled = true;
		Sky.SkyBox = skyBox;

		var fogGo = FindOrCreateObject( "Thorns Atmosphere" );
		var fog = fogGo.Components.Get<GradientFog>( FindMode.EverythingInSelf ) ?? fogGo.Components.Create<GradientFog>();
		Atmosphere = fogGo.Components.Get<ThornsAtmosphereController>( FindMode.EverythingInSelf ) ?? fogGo.Components.Create<ThornsAtmosphereController>();
		Atmosphere.GradientFog = fog;
		Clouds.Sky = Sky;

		var billboards = skyGo.Components.Get<ThornsCloudBillboardLayer>( FindMode.EverythingInSelf )
		               ?? skyGo.Components.Create<ThornsCloudBillboardLayer>();
		billboards.CloudController = Clouds;
		Clouds.Billboards = billboards;

		PostProcess ??= ThornsPostProcessTuner.EnsureOnEnvironment( GameObject );
	}

	void EnsureCloudBillboards()
	{
		if ( Clouds is null || !Clouds.IsValid() )
			return;

		if ( Clouds.Billboards is not null && Clouds.Billboards.IsValid() )
			return;

		GameObject skyGo = null;
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.IsValid() && string.Equals( go.Name, "Thorns Sky", StringComparison.OrdinalIgnoreCase ) )
			{
				skyGo = go;
				break;
			}
		}

		skyGo ??= GameObject;

		var billboards = skyGo.Components.Get<ThornsCloudBillboardLayer>( FindMode.EverythingInSelf )
		               ?? skyGo.Components.Create<ThornsCloudBillboardLayer>();
		billboards.CloudController = Clouds;
		Clouds.Billboards = billboards;
	}

	GameObject FindOrCreateObject( string name )
	{
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.IsValid() && string.Equals( go.Name, name, StringComparison.OrdinalIgnoreCase ) )
			{
				go.Tags.Add( EnvironmentTag );
				return go;
			}
		}

		var created = Scene.CreateObject();
		created.Name = name;
		created.Tags.Add( EnvironmentTag );
		return created;
	}

	void DisableForeignDirectionalLights()
	{
		foreach ( var light in Scene.GetAllComponents<DirectionalLight>() )
		{
			if ( light is null || !light.IsValid() || IsEnvironmentObject( light.GameObject ) )
				continue;

			light.Enabled = false;
		}
	}

	static bool IsEnvironmentObject( GameObject go )
	{
		for ( var node = go; node.IsValid(); node = node.Parent )
		{
			if ( node.Tags.Has( EnvironmentTag ) )
				return true;
		}

		return false;
	}

	public static bool TryGet( Scene scene, out ThornsEnvironmentDirector director )
	{
		director = Instance;
		if ( director is not null && director.IsValid() )
			return true;

		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var candidate in scene.GetAllComponents<ThornsEnvironmentDirector>() )
		{
			if ( candidate is not null && candidate.IsValid() && candidate.Enabled )
			{
				director = candidate;
				return true;
			}
		}

		director = null;
		return false;
	}

	public static ThornsEnvironmentDirector EnsureInScene( Scene scene )
	{
		DisableLegacyStaticSkyboxes( scene );

		if ( TryGet( scene, out var existing ) )
			return existing;

		var root = scene.CreateObject();
		root.Name = "Thorns Environment";
		root.Tags.Add( EnvironmentTag );
		var director = root.Components.Create<ThornsEnvironmentDirector>();
		director.AutoCreateSceneObjects = true;
		return director;
	}

	/// <summary>Boot the procedural sky stack and retire painterly scene skyboxes.</summary>
	public static void EnsureGameplayEnvironment( Scene scene )
	{
		if ( scene is null || !scene.IsValid() || ThornsLightingTestSceneBootstrap.IsActive )
			return;

		EnsureInScene( scene );
	}

	static void DisableLegacyStaticSkyboxes( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var sky in scene.GetAllComponents<SkyBox2D>() )
		{
			if ( sky is null || !sky.IsValid() || IsEnvironmentObject( sky.GameObject ) )
				continue;

			var materialPath = sky.SkyMaterial?.ResourcePath ?? "";
			var legacySky = materialPath.Contains( "thorns_skybox.vmat", StringComparison.OrdinalIgnoreCase )
			                || string.Equals( sky.GameObject.Name, "Sky", StringComparison.OrdinalIgnoreCase );
			if ( !legacySky )
				continue;

			sky.Enabled = false;
		}
	}
}
