namespace Sandbox;

/// <summary>
/// Resolves a sky material that does not depend on project-local shader compiles.
/// </summary>
static class ThornsCelestialSkyCarrier
{
	public const string CoreSkyboxMaterialPath = "materials/skybox/skybox_day_01.vmat";

	static readonly string[] EngineShaderPaths =
	{
		"shaders/sky2d.shader",
		"shaders/sky.shader",
	};

	static Material _cached;
	static Material _runtimeInstance;
	static string _runtimeInstanceShader = "";

	public static Material Resolve()
	{
		if ( _cached.IsValid() && !IsErrorShader( _cached ) )
			return _cached;

		// Prefer shipped core skybox — project-local sky2d FromShader often fails to compile.
		var core = Material.Load( CoreSkyboxMaterialPath );
		if ( core.IsValid() && !IsErrorShader( core ) )
		{
			_cached = core;
			return _cached;
		}

		foreach ( var shaderPath in EngineShaderPaths )
		{
			if ( TryFromShader( shaderPath, out var fromShader ) )
			{
				_cached = fromShader;
				return _cached;
			}
		}

		_cached = default;
		return _cached;
	}

	static bool TryFromShader( string shaderPath, out Material material )
	{
		material = default;
		if ( string.IsNullOrWhiteSpace( shaderPath ) )
			return false;

		try
		{
			var created = Material.FromShader( shaderPath );
			if ( created is null || !created.IsValid() || IsErrorShader( created ) )
				return false;

			material = created;
			return true;
		}
		catch
		{
			return false;
		}
	}

	static bool IsErrorShader( Material material )
	{
		var shader = material.ShaderName ?? "";
		return shader.Contains( "error.shader", StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Material copy with the baked panorama bound (SkyBox2D.SkyTexture is read-only).</summary>
	public static Material CreateRuntimeInstance( Texture skyTexture )
	{
		var carrier = Resolve();
		if ( !carrier.IsValid() || !skyTexture.IsValid() )
			return default;

		var shaderName = carrier.ShaderName ?? "";
		if ( !_runtimeInstance.IsValid() || _runtimeInstanceShader != shaderName )
		{
			var copy = carrier.CreateCopy();
			_runtimeInstance = copy.IsValid() ? copy : carrier;
			_runtimeInstanceShader = shaderName;
		}

		BindRuntimeTexture( _runtimeInstance, skyTexture );
		return _runtimeInstance;
	}

	public static void BindRuntimeTexture( Material material, Texture skyTexture )
	{
		if ( !material.IsValid() || !skyTexture.IsValid() )
			return;

		string[] textureParams =
		{
			"g_tSkyTexture",
			"SkyTexture",
			"TextureSky",
			"TextureHDR",
			"TextureColor",
			"Color",
			"g_tColor",
			"g_tPanorama",
			"Panorama",
			"g_tSky",
			"Sky",
			"Albedo",
			"TextureAlbedo",
			"g_tAlbedo",
		};

		foreach ( var name in textureParams )
		{
			material.Set( name, skyTexture );
			material.Attributes?.Set( name, skyTexture );
		}
	}
}
