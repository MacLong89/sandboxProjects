namespace Sandbox;

/// <summary>
/// Single authority for Minecraft-style day/night: sun, moon, sky, clouds, ambient, and fog.
/// Only <see cref="TimeOfDay01"/> is replicated; clients derive visuals locally.
/// </summary>
[Title( "Thorns — Celestial System" )]
[Category( "Thorns/World" )]
[Icon( "wb_sunny" )]
public class ThornsCelestialSystem : Component
{
	public const string SunObjectName = "Sun";
	public const string SunTag = "light_directional";
	const string CelestialSkyMaterialPath = ThornsCelestialSkyCarrier.CoreSkyboxMaterialPath;

	[Property, Group( "Time" )] public bool EnableDayNightCycle { get; set; } = true;
	[Property, Group( "Time" ), Range( 0f, 1f ), Title( "Time of day (0=midnight)" )] public float TimeOfDay01 { get; set; } = 0.38f;
	[Property, Group( "Time" ), Title( "Day length (minutes)" )] public float DayLengthMinutes { get; set; } = 60f;
	[Property, Group( "Time" )] public ThornsCelestialSunRiseDirection SunriseDirection { get; set; } = ThornsCelestialSunRiseDirection.East;

	[Property, Group( "Sun" )] public bool SunEnabled { get; set; } = true;
	[Property, Group( "Sun" ), Range( 0f, 8f )] public float SunPeakIntensity { get; set; } = 2.15f;
	[Property, Group( "Sun disc" ), Range( 8f, 32f )] public float SunDiscAngularDiameter { get; set; } = 18f;
	[Property, Group( "Sun disc" )] public bool UseCameraSunSprite { get; set; } = true;
	[Property, Group( "Sun disc" ), Range( -0.3f, 0.2f )] public float SunSpriteMinAltitudeRad { get; set; } = -0.08f;

	/// <summary>Applied to inspector diameter for sky + HUD sprites (legacy scenes often had 34°).</summary>
	public const float SunDiscVisualScale = 0.58f;

	/// <summary>Angular size used for rendering — smaller than raw inspector value for a stylized disc.</summary>
	public float EffectiveSunDiscAngularDiameter =>
		Math.Clamp( SunDiscAngularDiameter * SunDiscVisualScale, 6f, 14f );

	[Property, Group( "Moon" ), Range( 8f, 28f )] public float MoonDiscAngularDiameter { get; set; } = 16f;

	[Property, Group( "Sky" ), Title( "Sky carrier (core; ignored when runtime texture on)" )]
	public string SkyMaterialPath { get; set; } = CelestialSkyMaterialPath;
	[Property, Group( "Sky" )] public GameObject SkyboxObject { get; set; }
	[Property, Group( "Sky" ), Title( "Sky render mode" )]
	public ThornsCelestialSkyRenderMode SkyRenderMode { get; set; } = ThornsCelestialSkyRenderMode.RuntimeUniforms;
	[Property, Group( "Sky" ), Title( "Use SkyBox2D component (recommended)" )]
	public bool PreferSkyBox2DComponent { get; set; } = true;
	[Property, Group( "Sky" ), Title( "Procedural sky via camera fill (disables SkyBox2D)" )]
	public bool UseRuntimeSkyTexture { get; set; } = true;
	[Property, Group( "Sky" ), Range( 0f, 1f )] public float CloudOpacity { get; set; } = 0.28f;

	[Property, Group( "Live tuning (play)" ), Title( "FORCE test colors (magenta/orange sky)" )]
	public bool LiveForceTestColors { get; set; }

	[Property, Group( "Live tuning (play)" ), Title( "Sky exposure scale" ), Range( 0.05f, 4f )] public float LiveSkyExposureScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Sky color intensity" ), Range( 0.1f, 6f )] public float LiveSkyColorIntensity { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Sky color tint" )] public Color LiveSkyColorTint { get; set; } = Color.White;
	[Property, Group( "Live tuning (play)" ), Title( "Sky saturation" ), Range( 0f, 3f )] public float LiveSkySaturation { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Horizon glow scale" ), Range( 0f, 10f )] public float LiveHorizonGlowScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Exposure day (curve)" ), Range( 0.05f, 3f )] public float LiveSkyExposureDay { get; set; } = 1.18f;
	[Property, Group( "Live tuning (play)" ), Title( "Exposure night (curve)" ), Range( 0.02f, 1f )] public float LiveSkyExposureNight { get; set; } = 0.15f;
	[Property, Group( "Live tuning (play)" ), Title( "Horizon glow (curve)" ), Range( 0f, 8f )] public float LiveHorizonGlowStrengthScale { get; set; } = 3.2f;
	[Property, Group( "Live tuning (play)" ), Title( "Day/night curve power" ), Range( 0.5f, 5f )] public float LiveDayCurvePower { get; set; } = 2.65f;

	[Property, Group( "Live tuning (play)" ), Title( "Override sky palette" )] public bool LiveUseSkyPaletteOverride { get; set; }
	[Property, Group( "Live tuning (play)" ), Title( "Override blend" ), Range( 0f, 1f )] public float LivePaletteOverrideBlend { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Override zenith" )] public Color LiveOverrideZenith { get; set; } = new Color( 0.07f, 0.28f, 0.78f );
	[Property, Group( "Live tuning (play)" ), Title( "Override mid" )] public Color LiveOverrideMid { get; set; } = new Color( 0.19f, 0.5f, 0.89f );
	[Property, Group( "Live tuning (play)" ), Title( "Override horizon" )] public Color LiveOverrideHorizon { get; set; } = new Color( 0.46f, 0.74f, 1f );

	[Property, Group( "Live tuning (play)" ), Title( "Sun intensity scale" ), Range( 0f, 4f )] public float LiveSunIntensityScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Sun light color tint" )] public Color LiveSunLightColorTint { get; set; } = Color.White;
	[Property, Group( "Live tuning (play)" ), Title( "Ambient intensity scale" ), Range( 0f, 4f )] public float LiveAmbientIntensityScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Ambient color tint" )] public Color LiveAmbientColorTint { get; set; } = Color.White;

	[Property, Group( "Live tuning (play)" ), Title( "Fog strength scale" ), Range( 0f, 3f )] public float LiveFogStrengthScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Fog color tint" )] public Color LiveFogColorTint { get; set; } = Color.White;
	[Property, Group( "Live tuning (play)" ), Title( "Cloud opacity scale" ), Range( 0f, 3f )] public float LiveCloudOpacityScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Star brightness scale" ), Range( 0f, 5f )] public float LiveStarBrightnessScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Moon light scale" ), Range( 0f, 4f )] public float LiveMoonLightScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Sun disc intensity scale" ), Range( 0f, 4f )] public float LiveSunDiscIntensityScale { get; set; } = 1f;
	[Property, Group( "Live tuning (play)" ), Title( "Sun disc color tint" )] public Color LiveSunDiscColorTint { get; set; } = Color.White;

	[Property, Group( "Live readout" ), Title( "Zenith (computed + tuned)" ), ReadOnly] public Color DebugSkyZenith { get; private set; }
	[Property, Group( "Live readout" ), Title( "Horizon (computed + tuned)" ), ReadOnly] public Color DebugSkyHorizon { get; private set; }
	[Property, Group( "Live readout" ), Title( "Exposure (computed + tuned)" ), ReadOnly] public float DebugSkyExposure { get; private set; }
	[Property, Group( "Live readout" ), Title( "Sky render path" ), ReadOnly] public string DebugSkyRenderPath { get; private set; } = "";
	[Property, Group( "Live readout" ), Title( "Active sky .vmat" ), ReadOnly] public string DebugActiveSkyMaterial { get; private set; } = "";
	[Property, Group( "Live readout" ), Title( "Sky tint applied" ), ReadOnly] public Color DebugSkyTintApplied { get; private set; }

	[Property, Group( "Fog" )] public Light.FogInfluence FogMode { get; set; } = Light.FogInfluence.Enabled;
	[Property, Group( "Fog" ), Range( 0f, 1f )] public float FogStrengthMultiplier { get; set; } = 1f;

	[Property, Group( "Shadows" )] public bool Shadows { get; set; } = true;
	[Property, Group( "Shadows" )] public float ShadowBias { get; set; } = 0.0005f;
	[Property, Group( "Shadows" ), Range( 0f, 1f )] public float ShadowHardness { get; set; } = 0.62f;
	[Property, Group( "Shadows" ), Range( 1, 4 )] public int ShadowCascadeCount { get; set; } = 4;

	[Property, Group( "Master" )] public bool DisableOtherDirectionalLights { get; set; } = true;
	[Property, Group( "Master" )] public bool LogDiagnostics { get; set; }

	[Sync( SyncFlags.FromHost )] float SyncedTimeOfDay01 { get; set; }

	DirectionalLight _light;
	SkyBox2D _sky;
	SceneSkyBox _sceneSky;
	Material _skyMaterialRuntime;
	string _skyMaterialPathLoaded = "";
	float _resolvedTime01;
	ThornsCelestialState _state;
	bool _duplicateSunsPruned;
	bool _foreignDirectionalLightsDisabled;
	bool _conflictingSkyProbesDisabled;
	bool _loggedLegacySkyBlocked;
	bool _loggedReady;
	bool _loggedRuntimeSkyTexture;
	bool _loggedSkyMaterialBinding;
	bool _skyBox2DRefreshPending;
	double _nextDiagLog;
	double _nextSkyVisualApply;
	float _skyVisualInterval = 1f / 12f;
	float _lastAppliedLightTime01 = -1f;

	public ThornsCelestialState CurrentState => _state;

	protected override void OnStart()
	{
		EnsureTags();
		ConsolidateSceneAuthority();
		ResolveTime();
		Apply( pruneLights: true );
		LogReadyOnce();
	}

	protected override void OnUpdate()
	{
		AdvanceTime();
		ResolveTime();

		if ( Game.IsPlaying )
		{
			if ( !_duplicateSunsPruned )
				ConsolidateSceneAuthority();

			var now = Time.Now;
			var timeDelta = Math.Abs( _resolvedTime01 - _lastAppliedLightTime01 );
			var skyDue = now >= _nextSkyVisualApply || timeDelta >= 0.0025f;
			if ( !skyDue )
			{
				MaybeLogDiagnostics();
				MaybeDrawDebug();
				return;
			}

			_nextSkyVisualApply = now + _skyVisualInterval;
			_lastAppliedLightTime01 = _resolvedTime01;
			Apply( pruneLights: false, skipSkyVisuals: false );
			MaybeLogDiagnostics();
			MaybeDrawDebug();
		}
		else
		{
			Apply( pruneLights: false );
		}
	}

	public void ApplyPerformanceQuality( ThornsPerformanceQuality quality )
	{
		var hz = ThornsPerformanceQualityPresets.Get( quality ).CelestialVisualHz;
		_skyVisualInterval = 1f / Math.Max( 4f, hz );
		_nextSkyVisualApply = 0;
	}

	protected override void OnValidate()
	{
		if ( GameObject.Scene is null || !GameObject.Scene.IsValid() )
			return;

		ResolveTime();
		Apply( pruneLights: false );
	}

	protected override void OnDestroy()
	{
		DestroySceneSkyRenderer();
	}

	void AdvanceTime()
	{
		if ( !EnableDayNightCycle || !Game.IsPlaying || ThornsCelestialDebug.FreezeTime )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		var seconds = Math.Max( 60f, DayLengthMinutes * 60f );
		TimeOfDay01 = (TimeOfDay01 + Time.Delta / seconds) % 1f;
	}

	void ResolveTime()
	{
		if ( !EnableDayNightCycle )
		{
			_resolvedTime01 = 0.5f;
			_state = ThornsCelestialState.Evaluate( _resolvedTime01, BuildTuning() );
			_state = ThornsCelestialLiveTuning.ApplyFromInspector( _state, this );
			UpdateDebugReadout();
			return;
		}

		if ( Networking.IsActive && !Networking.IsHost )
			_resolvedTime01 = SyncedTimeOfDay01;
		else
		{
			_resolvedTime01 = (TimeOfDay01 % 1f + 1f) % 1f;
			if ( Networking.IsHost || !Networking.IsActive )
				SyncedTimeOfDay01 = _resolvedTime01;
		}

		_state = ThornsCelestialState.Evaluate( _resolvedTime01, BuildTuning() );
		_state = ThornsCelestialLiveTuning.ApplyFromInspector( _state, this );
		UpdateDebugReadout();
	}

	void UpdateDebugReadout()
	{
		DebugSkyZenith = _state.SkyZenith;
		DebugSkyHorizon = _state.SkyHorizon;
		DebugSkyExposure = _state.SkyExposure;
	}

	ThornsCelestialTuning BuildTuning()
	{
		return new ThornsCelestialTuning
		{
			SunriseDirection = SunriseDirection,
			SunPeakIntensity = SunPeakIntensity,
			CloudOpacity = CloudOpacity,
			SkyExposureDay = LiveSkyExposureDay,
			SkyExposureNight = LiveSkyExposureNight,
			HorizonGlowStrengthScale = LiveHorizonGlowStrengthScale,
			DayCurvePower = LiveDayCurvePower,
			NightCurvePower = LiveDayCurvePower * 0.88f
		};
	}

	public void SetTimeOfDay( float time01 )
	{
		TimeOfDay01 = (time01 % 1f + 1f) % 1f;
		if ( Networking.IsHost || !Networking.IsActive )
			SyncedTimeOfDay01 = TimeOfDay01;
		ResolveTime();
		Apply( pruneLights: false );
	}

	/// <summary>Copies the current computed sky colors into live override fields (inspector + console <c>sky_capture_palette</c>).</summary>
	public void CaptureComputedSkyToLiveOverrides()
	{
		var s = ThornsCelestialState.Evaluate( _resolvedTime01, BuildTuning() );
		LiveOverrideZenith = s.SkyZenith;
		LiveOverrideMid = s.SkyMid;
		LiveOverrideHorizon = s.SkyHorizon;
		LiveUseSkyPaletteOverride = true;
		LivePaletteOverrideBlend = 1f;
		Apply( pruneLights: false );
		Log.Info( $"[Thorns Celestial] Captured live palette — zenith={LiveOverrideZenith} horizon={LiveOverrideHorizon}" );
	}

	public void Apply( bool pruneLights, bool skipSkyVisuals = false )
	{
		if ( !ResolveDirectionalLight() )
			return;

		if ( pruneLights )
			PruneDuplicateLights();

		GameObject.WorldRotation = _state.SunLightRotation;

		var lightOn = SunEnabled && _state.SunLightIntensity > 0.02f;
		_light.Enabled = lightOn;
		_light.LightColor = _state.SunLightColor * _state.SunLightIntensity;
		_light.SkyColor = _state.AmbientSkyColor * _state.AmbientIntensity + new Color( _state.MoonLightContribution );
		_light.Shadows = Shadows && _state.ShadowsEnabled;
		_light.ShadowBias = ShadowBias;
		_light.ShadowHardness = ShadowHardness;
		_light.ShadowCascadeCount = Math.Clamp( ShadowCascadeCount, 1, 4 );
		_light.FogMode = LiveForceTestColors ? Light.FogInfluence.Disabled : FogMode;
		_light.FogStrength = LiveForceTestColors ? 0f : _state.FogStrength * FogStrengthMultiplier;

		if ( DisableOtherDirectionalLights )
			DisableForeignDirectionalLights();

		if ( !skipSkyVisuals )
			ApplySkybox();
	}

	void ApplySkybox()
	{
		if ( UseRuntimeSkyTexture )
		{
			ApplySkyboxRuntimeTexture();
			return;
		}

		if ( !EnsureRuntimeSkyMaterial() )
			return;

		// Custom shader + runtime uniforms (requires compiling thorns_celestial_sky.shader).
		PushSkyUniforms( _skyMaterialRuntime );
		DebugSkyTintApplied = Color.White;
		DebugActiveSkyMaterial = _skyMaterialPathLoaded;

		if ( PreferSkyBox2DComponent && TryResolveSkybox( out _sky ) )
		{
			DestroySceneSkyRenderer();
			DebugSkyRenderPath = "SkyBox2D+RuntimeUniforms";

			_sky.SkyMaterial = _skyMaterialRuntime;
			_sky.Tint = Color.White;
			_sky.SkyIndirectLighting = false;

			if ( _skyBox2DRefreshPending )
			{
				_sky.Enabled = false;
				_sky.Enabled = true;
				_skyBox2DRefreshPending = false;
			}

			if ( !_sky.Enabled )
				_sky.Enabled = true;

			DisableSceneSkyBoxComponents( keepEnabled: _sky );
		}
		else if ( EnsureSceneSkyRenderer() )
		{
			DebugSkyRenderPath = "SceneSkyBox+RuntimeUniforms";

			_sceneSky.SkyMaterial = _skyMaterialRuntime;
			_sceneSky.SkyTint = Color.White;
			PushSkyUniforms( _sceneSky.Attributes );
			DisableSceneSkyBoxComponents();
		}

		DisableConflictingSkyProbes();
	}

	void ApplySkyboxRuntimeTexture()
	{
		// Bake keeps palette in sync for future dome path; camera fill is what players actually see.
		_ = ThornsCelestialSkyTexture.GetOrUpdate( _state, EffectiveSunDiscAngularDiameter, LiveForceTestColors );

		DestroySceneSkyRenderer();
		DisableSceneSkyBoxComponents();

		var fill = ComputeCameraSkyFillColor( _state );
		DebugSkyRenderPath = "CameraBackground";
		DebugActiveSkyMaterial = "(camera fill)";
		DebugSkyTintApplied = fill;

		if ( LogDiagnostics && !_loggedRuntimeSkyTexture )
		{
			_loggedRuntimeSkyTexture = true;
			Log.Info( $"[Thorns Celestial] Sky = camera background fill (SkyBox2D off). color={fill}" );
		}

		DisableConflictingSkyProbes();
	}

	static Color ComputeCameraSkyFillColor( in ThornsCelestialState state )
	{
		var color = Color.Lerp( state.SkyHorizon, state.SkyZenith, 0.35f );
		var peak = Math.Max( color.r, Math.Max( color.g, color.b ) );
		if ( peak < 0.001f )
			return new Color( 0.02f, 0.04f, 0.08f, 1f );

		var scale = Math.Clamp( state.SkyExposure, 0.3f, 1.25f );
		var r = color.r / peak * scale;
		var g = color.g / peak * scale;
		var b = color.b / peak * scale;
		var max = Math.Max( r, Math.Max( g, b ) );
		if ( max > 1f )
		{
			r /= max;
			g /= max;
			b /= max;
		}

		return new Color(
			Math.Clamp( r, 0f, 1f ),
			Math.Clamp( g, 0f, 1f ),
			Math.Clamp( b, 0f, 1f ),
			1f );
	}

	Color ComputeSkyDisplayTint()
	{
		// SceneSkyBox / SkyBox2D multiply the drawn sky by SkyTint — use this when shader uniforms lag.
		var zenith = _state.SkyZenith;
		var horizon = _state.SkyHorizon;
		var blend = Color.Lerp( horizon, zenith, 0.38f );
		var peak = Math.Max( blend.r, Math.Max( blend.g, blend.b ) );
		if ( peak < 0.001f )
			return Color.White;

		var scale = Math.Max( 0.35f, _state.SkyExposure );
		if ( LiveForceTestColors )
			scale = Math.Max( scale, 1.35f );

		var r = blend.r / peak * scale;
		var g = blend.g / peak * scale;
		var b = blend.b / peak * scale;
		var max = Math.Max( r, Math.Max( g, b ) );
		if ( max > 1f )
		{
			r /= max;
			g /= max;
			b /= max;
		}

		return new Color(
			Math.Clamp( r, 0f, 1f ),
			Math.Clamp( g, 0f, 1f ),
			Math.Clamp( b, 0f, 1f ),
			1f );
	}

	bool EnsureSceneSkyRenderer()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() || scene.SceneWorld is null )
			return false;

		if ( _sceneSky is not null && _sceneSky.IsValid() )
			return true;

		_sceneSky = new SceneSkyBox( scene.SceneWorld, _skyMaterialRuntime );
		if ( _sceneSky is null || !_sceneSky.IsValid() )
		{
			_sceneSky = default;
			return false;
		}

		return true;
	}

	void DestroySceneSkyRenderer()
	{
		if ( _sceneSky is not null && _sceneSky.IsValid() )
			_sceneSky.Delete();

		_sceneSky = default;
	}

	void DisableSceneSkyBoxComponents( SkyBox2D keepEnabled = default )
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var candidate in scene.GetAllComponents<SkyBox2D>() )
		{
			if ( !candidate.IsValid() )
				continue;

			var keep = keepEnabled.IsValid() && candidate == keepEnabled;
			candidate.Enabled = keep;
		}
	}

	bool EnsureRuntimeSkyMaterial()
	{
		var path = ResolveAuthoritativeSkyMaterialPath();
		if ( _skyMaterialRuntime.IsValid() && path == _skyMaterialPathLoaded && UsesThornsSkyShader( _skyMaterialRuntime ) )
			return true;

		DestroySceneSkyRenderer();

		var loaded = Material.Load( path );
		if ( !loaded.IsValid() )
		{
			loaded = Material.Load( CelestialSkyMaterialPath );
			path = CelestialSkyMaterialPath;
		}

		if ( !loaded.IsValid() )
			return false;

		if ( !UsesThornsSkyShader( loaded ) )
		{
			loaded = Material.Load( CelestialSkyMaterialPath );
			path = CelestialSkyMaterialPath;
			if ( !loaded.IsValid() )
				return false;
		}

		var instance = loaded.CreateCopy();
		_skyMaterialRuntime = instance.IsValid() ? instance : loaded;
		_skyMaterialPathLoaded = path;
		_skyBox2DRefreshPending = true;

		if ( LogDiagnostics && !_loggedSkyMaterialBinding )
		{
			_loggedSkyMaterialBinding = true;
			Log.Info( $"[Thorns Celestial] Sky bound path={path} shader={_skyMaterialRuntime.ShaderName}" );
		}

		if ( _skyMaterialRuntime.IsValid() && (_skyMaterialRuntime.ShaderName ?? "").Contains( "error.shader", StringComparison.OrdinalIgnoreCase ) )
			Log.Warning( $"[Thorns Celestial] Sky material compiled to error.shader — check Asset Browser compile log for {path}" );

		return _skyMaterialRuntime.IsValid();
	}

	string ResolveAuthoritativeSkyMaterialPath()
	{
		var requested = string.IsNullOrWhiteSpace( SkyMaterialPath ) ? CelestialSkyMaterialPath : SkyMaterialPath.Trim();
		if ( IsBlockedSkyMaterialPath( requested ) )
		{
			if ( LogDiagnostics && !_loggedLegacySkyBlocked )
			{
				_loggedLegacySkyBlocked = true;
				Log.Warning( $"[Thorns Celestial] Blocked sky material '{requested}' — using {CelestialSkyMaterialPath}." );
			}

			return CelestialSkyMaterialPath;
		}

		return requested;
	}

	static bool UsesThornsSkyShader( Material material )
	{
		if ( !material.IsValid() )
			return false;

		var shader = material.ShaderName ?? "";
		if ( shader.Contains( "error.shader", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return shader.Contains( "thorns_celestial_sky", StringComparison.OrdinalIgnoreCase )
			|| shader.Contains( "thorns_atmosphere_sky", StringComparison.OrdinalIgnoreCase );
	}

	static bool IsBlockedSkyMaterialPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return true;

		if ( path.Contains( "thorns_sky_", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return false;
	}

	void DisableConflictingSkyProbes()
	{
		if ( _conflictingSkyProbesDisabled )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var probe in scene.GetAllComponents<EnvmapProbe>() )
		{
			if ( !probe.IsValid() || !probe.Enabled )
				continue;

			probe.Enabled = false;
		}

		_conflictingSkyProbesDisabled = true;
	}

	void PushSkyUniforms( Material material )
	{
		if ( material.IsValid() )
			PushSkyUniforms( material.Attributes );

		if ( !material.IsValid() )
			return;

		var s = _state;
		var fogBlend = LiveForceTestColors ? 0f : s.FogStrength * FogStrengthMultiplier;
		material.Set( "SkyZenith", ToVector3( s.SkyZenith ) );
		material.Set( "g_vSkyZenith", ToVector3( s.SkyZenith ) );
		material.Set( "SkyMid", ToVector3( s.SkyMid ) );
		material.Set( "g_vSkyMid", ToVector3( s.SkyMid ) );
		material.Set( "SkyHorizon", ToVector3( s.SkyHorizon ) );
		material.Set( "g_vSkyHorizon", ToVector3( s.SkyHorizon ) );
		material.Set( "HorizonGlowColor", ToVector3( s.HorizonGlowColor ) );
		material.Set( "g_vHorizonGlowColor", ToVector3( s.HorizonGlowColor ) );
		material.Set( "HorizonGlowStrength", s.HorizonGlowStrength );
		material.Set( "g_flHorizonGlowStrength", s.HorizonGlowStrength );
		material.Set( "StarBrightness", s.StarBrightness );
		material.Set( "g_flStarBrightness", s.StarBrightness );
		material.Set( "StarRotation", s.StarRotation );
		material.Set( "g_flStarRotation", s.StarRotation );
		material.Set( "CloudOpacity", s.CloudOpacity );
		material.Set( "g_flCloudOpacity", s.CloudOpacity );
		material.Set( "CloudTint", ToVector3( s.CloudTint ) );
		material.Set( "g_vCloudTint", ToVector3( s.CloudTint ) );
		material.Set( "CloudDrift", s.CloudDrift );
		material.Set( "g_flCloudDrift", s.CloudDrift );
		material.Set( "SunDiscColor", ToVector3( s.SunDiscColor ) );
		material.Set( "g_vSunDiscColor", ToVector3( s.SunDiscColor ) );
		material.Set( "SunDiscIntensity", s.SunDiscIntensity );
		material.Set( "g_flSunDiscIntensity", s.SunDiscIntensity );
		material.Set( "SunDiscGlow", s.SunDiscGlow );
		material.Set( "g_flSunDiscGlow", s.SunDiscGlow );
		material.Set( "SunDiscAngularDiameter", EffectiveSunDiscAngularDiameter );
		material.Set( "g_flSunDiscAngularDiameter", EffectiveSunDiscAngularDiameter );
		material.Set( "FogColor", ToVector3( s.FogColor ) );
		material.Set( "g_vCelestialSkyFog", ToVector3( s.FogColor ) );
		material.Set( "FogBlend", fogBlend );
		material.Set( "g_flFogBlend", fogBlend );
		material.Set( "SkyExposure", s.SkyExposure );
		material.Set( "g_flSkyExposure", s.SkyExposure );
		material.Set( "HorizonBandPower", s.HorizonBandPower );
		material.Set( "g_flHorizonBandPower", s.HorizonBandPower );
	}

	void PushSkyUniforms( RenderAttributes attributes )
	{
		if ( attributes is null )
			return;

		var s = _state;
		var fogBlend = LiveForceTestColors ? 0f : s.FogStrength * FogStrengthMultiplier;
		SetSkyAttr( attributes, "SkyZenith", "g_vSkyZenith", ToVector3( s.SkyZenith ) );
		SetSkyAttr( attributes, "SkyMid", "g_vSkyMid", ToVector3( s.SkyMid ) );
		SetSkyAttr( attributes, "SkyHorizon", "g_vSkyHorizon", ToVector3( s.SkyHorizon ) );
		SetSkyAttr( attributes, "HorizonGlowColor", "g_vHorizonGlowColor", ToVector3( s.HorizonGlowColor ) );
		SetSkyAttr( attributes, "HorizonGlowStrength", "g_flHorizonGlowStrength", s.HorizonGlowStrength );
		SetSkyAttr( attributes, "StarBrightness", "g_flStarBrightness", s.StarBrightness );
		SetSkyAttr( attributes, "StarRotation", "g_flStarRotation", s.StarRotation );
		SetSkyAttr( attributes, "CloudOpacity", "g_flCloudOpacity", s.CloudOpacity );
		SetSkyAttr( attributes, "CloudTint", "g_vCloudTint", ToVector3( s.CloudTint ) );
		SetSkyAttr( attributes, "CloudDrift", "g_flCloudDrift", s.CloudDrift );
		SetSkyAttr( attributes, "SunDiscColor", "g_vSunDiscColor", ToVector3( s.SunDiscColor ) );
		SetSkyAttr( attributes, "SunDiscIntensity", "g_flSunDiscIntensity", s.SunDiscIntensity );
		SetSkyAttr( attributes, "SunDiscGlow", "g_flSunDiscGlow", s.SunDiscGlow );
		SetSkyAttr( attributes, "SunDiscAngularDiameter", "g_flSunDiscAngularDiameter", EffectiveSunDiscAngularDiameter );
		SetSkyAttr( attributes, "FogColor", "g_vCelestialSkyFog", ToVector3( s.FogColor ) );
		SetSkyAttr( attributes, "FogBlend", "g_flFogBlend", fogBlend );
		SetSkyAttr( attributes, "SkyExposure", "g_flSkyExposure", s.SkyExposure );
		SetSkyAttr( attributes, "HorizonBandPower", "g_flHorizonBandPower", s.HorizonBandPower );
	}

	static void SetSkyAttr( RenderAttributes attributes, string name, string shaderName, Vector3 value )
	{
		attributes.Set( name, value );
		attributes.Set( shaderName, value );
	}

	static void SetSkyAttr( RenderAttributes attributes, string name, string shaderName, float value )
	{
		attributes.Set( name, value );
		attributes.Set( shaderName, value );
	}

	static Vector3 ToVector3( Color color ) => new( color.r, color.g, color.b );

	void MaybeDrawDebug()
	{
		if ( !ThornsCelestialDebug.SkyDebug )
			return;

		var s = _state;
		Log.Info(
			$"[Thorns Celestial] time={s.TimeOfDay01:F3} sunAlt={s.SunAltitudeDegrees:F1}° " +
			$"sunI={s.SunLightIntensity:F2} ambI={s.AmbientIntensity:F2} exp={s.SkyExposure:F2} " +
			$"zenith=({s.SkyZenith.r:F2},{s.SkyZenith.g:F2},{s.SkyZenith.b:F2}) " +
			$"horizon=({s.SkyHorizon.r:F2},{s.SkyHorizon.g:F2},{s.SkyHorizon.b:F2}) glow={s.HorizonGlowStrength:F2} " +
			$"night={s.NightWeight:F2}" );
	}

	void MaybeLogDiagnostics()
	{
		if ( !LogDiagnostics )
			return;

		var now = Time.Now;
		if ( now < _nextDiagLog )
			return;

		_nextDiagLog = now + 20.0;
		MaybeDrawDebug();
	}

	void LogReadyOnce()
	{
		if ( _loggedReady )
			return;

		_loggedReady = true;
		var shader = _skyMaterialRuntime.IsValid() ? _skyMaterialRuntime.ShaderName : "(none)";
		var s = _state;
		var renderPath = $"{DebugSkyRenderPath} mode={SkyRenderMode}";
		Log.Info(
			$"[Thorns Celestial] Sole sky/time authority — time={_resolvedTime01:F3}, shader={shader}, vmat={_skyMaterialPathLoaded}, render={renderPath}, " +
			$"zenith=({s.SkyZenith.r:F2},{s.SkyZenith.g:F2},{s.SkyZenith.b:F2}) exposure={s.SkyExposure:F2} glow={s.HorizonGlowStrength:F2}" );
	}

	public bool TryGetSunSkyDirection( out Vector3 sunSkyDirection, out bool aboveHorizon )
	{
		sunSkyDirection = _state.SunDirection;
		aboveHorizon = _state.SunAltitudeRadians > 0.01f;
		return true;
	}

	public static bool TryGetTimeOfDay( Scene scene, out float time01, out bool isNight )
	{
		time01 = 0f;
		isNight = false;
		if ( !TryGet( scene, out var celestial ) || !celestial.EnableDayNightCycle )
			return false;

		celestial.ResolveTime();
		time01 = celestial._resolvedTime01;
		isNight = celestial._state.IsNightPhase;
		return true;
	}

	public static bool TryGetSkyFallbackColor( Scene scene, out Color color )
	{
		color = default;
		if ( !TryGet( scene, out var celestial ) )
			return false;

		celestial.ResolveTime();
		color = ComputeCameraSkyFillColor( celestial._state );

		if ( celestial.UseRuntimeSkyTexture )
			return true;

		return celestial._state.NightWeight > 0.85f;
	}

	public static ThornsCelestialSystem EnsureInScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return default;

		var authority = FindSceneAuthority( scene );
		if ( authority.IsValid() )
		{
			authority.ConsolidateSceneAuthority();
			authority.Apply( pruneLights: true );
			return authority;
		}

		if ( TryFindSunObject( scene, out var sunGo ) )
		{
			var created = sunGo.Components.Get<ThornsCelestialSystem>() ?? sunGo.Components.Create<ThornsCelestialSystem>();
			created.ConsolidateSceneAuthority();
			created.Apply( pruneLights: true );
			return created;
		}

		sunGo = scene.CreateObject();
		sunGo.Name = SunObjectName;
		sunGo.Tags.Add( SunTag );
		sunGo.Tags.Add( "light" );
		var component = sunGo.Components.Create<ThornsCelestialSystem>();
		_ = sunGo.Components.Create<DirectionalLight>();
		component.Apply( pruneLights: true );
		return component;
	}

	static ThornsCelestialSystem FindSceneAuthority( Scene scene )
	{
		ThornsCelestialSystem onSun = default;
		ThornsCelestialSystem fallback = default;

		foreach ( var c in scene.GetAllComponents<ThornsCelestialSystem>() )
		{
			if ( !c.IsValid() )
				continue;

			var go = c.GameObject;
			if ( go.IsValid() && (go.Name == SunObjectName || go.Tags.Has( SunTag )) )
			{
				onSun = c;
				break;
			}

			fallback ??= c;
		}

		return onSun.IsValid() ? onSun : fallback;
	}

	void ConsolidateSceneAuthority()
	{
		PruneDuplicatesOnSelf();
		PruneDuplicates();
	}

	public static bool TryGet( Scene scene, out ThornsCelestialSystem celestial )
	{
		celestial = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		if ( Instance is not null && Instance.IsValid && Instance.Scene == scene )
		{
			celestial = Instance;
			return true;
		}

		foreach ( var c in scene.GetAllComponents<ThornsCelestialSystem>() )
		{
			if ( c.IsValid )
			{
				celestial = c;
				return true;
			}
		}

		return false;
	}

	public static ThornsCelestialSystem Instance { get; private set; }

	protected override void OnEnabled()
	{
		if ( Instance is not null && Instance.IsValid && Instance != this )
		{
			Enabled = false;
			return;
		}

		Instance = this;
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	bool ResolveDirectionalLight()
	{
		if ( !_light.IsValid() )
			_light = Components.Get<DirectionalLight>( FindMode.EverythingInSelf );
		if ( !_light.IsValid() )
			_light = Components.Create<DirectionalLight>();
		return _light.IsValid();
	}

	void PruneDuplicateLights()
	{
		foreach ( var dl in Components.GetAll<DirectionalLight>( FindMode.EverythingInSelf ) )
		{
			if ( dl.IsValid() && dl != _light )
				dl.Destroy();
		}
	}

	void PruneDuplicatesOnSelf()
	{
		var onSelf = Components.GetAll<ThornsCelestialSystem>( FindMode.EnabledInSelf ).ToArray();
		if ( onSelf.Length <= 1 )
			return;

		for ( var i = 1; i < onSelf.Length; i++ )
		{
			if ( onSelf[i].IsValid() && onSelf[i] != this )
			{
				if ( Game.IsPlaying )
					onSelf[i].Destroy();
				else
					onSelf[i].Enabled = false;
			}
		}
	}

	void PruneDuplicates()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var other in scene.GetAllComponents<ThornsCelestialSystem>() )
		{
			if ( !other.IsValid() || other == this )
				continue;

			if ( Game.IsPlaying )
				other.Destroy();
			else
				other.Enabled = false;
		}

		_duplicateSunsPruned = true;
	}

	void DisableForeignDirectionalLights()
	{
		if ( _foreignDirectionalLightsDisabled )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var dl in scene.GetAllComponents<DirectionalLight>() )
		{
			if ( dl.IsValid() && dl != _light )
				dl.Enabled = false;
		}

		_foreignDirectionalLightsDisabled = true;
	}

	bool TryResolveSkybox( out SkyBox2D sky )
	{
		sky = default;
		if ( _sky.IsValid() )
		{
			sky = _sky;
			return true;
		}

		if ( SkyboxObject.IsValid() )
		{
			sky = SkyboxObject.Components.Get<SkyBox2D>( FindMode.EverythingInSelf );
			if ( sky.IsValid() )
				return true;
		}

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var candidate in scene.GetAllComponents<SkyBox2D>() )
		{
			if ( !candidate.IsValid() )
				continue;

			var go = candidate.GameObject;
			if ( go.IsValid() && (go.Name == "2D Skybox" || go.Tags.Has( "skybox" )) )
			{
				SkyboxObject = go;
				sky = candidate;
				return true;
			}
		}

		foreach ( var candidate in scene.GetAllComponents<SkyBox2D>() )
		{
			if ( candidate.IsValid() )
			{
				SkyboxObject = candidate.GameObject;
				sky = candidate;
				return true;
			}
		}

		return false;
	}

	void EnsureTags()
	{
		if ( !GameObject.Tags.Has( SunTag ) )
			GameObject.Tags.Add( SunTag );
		if ( !GameObject.Tags.Has( "light" ) )
			GameObject.Tags.Add( "light" );
	}

	static bool TryFindSunObject( Scene scene, out GameObject sunGo )
	{
		sunGo = default;
		foreach ( var dl in scene.GetAllComponents<DirectionalLight>() )
		{
			if ( !dl.IsValid() )
				continue;

			var go = dl.GameObject;
			if ( go.IsValid() && (go.Name == SunObjectName || go.Tags.Has( SunTag )) )
			{
				sunGo = go;
				return true;
			}
		}

		foreach ( var dl in scene.GetAllComponents<DirectionalLight>() )
		{
			if ( dl.IsValid() )
			{
				sunGo = dl.GameObject;
				return true;
			}
		}

		return false;
	}
}
