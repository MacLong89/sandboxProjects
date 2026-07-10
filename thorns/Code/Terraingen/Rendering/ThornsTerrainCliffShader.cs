namespace Terraingen.Rendering;

using Sandbox;

/// <summary>
/// Finalizes <see cref="Terrain"/> material binding after <see cref="Terrain.UpdateMaterialsBuffer"/>.
/// </summary>
public static class ThornsTerrainCliffShader
{
	public const string BaseTerrainShaderPath = "shaders/terrain.shader";
	public const string ThornsTerrainShaderPath = "shaders/thorns_terrain.shader";

	/// <summary>Clears overrides and returns the label logged by terrain setup.</summary>
	public static string Apply( Terrain terrain, bool useThornsShader = true, bool useEngineShaderOverride = false )
	{
		if ( terrain is null || !terrain.IsValid() )
			return "none";

		if ( useEngineShaderOverride )
			return ApplyFromShaderPath( terrain, BaseTerrainShaderPath );

		if ( useThornsShader )
			return ApplyFromShaderPath( terrain, ThornsTerrainShaderPath );

		terrain.MaterialOverride = default;
		return "engine-default";
	}

	static string ApplyFromShaderPath( Terrain terrain, string shaderPath )
	{
		var material = TryCreateFromShader( shaderPath );
		if ( material is null || !material.IsValid() )
		{
			Log.Warning(
				$"[Thorns Terrain] Terrain splat shader missing — using engine-default. Tried '{shaderPath}'." );
			terrain.MaterialOverride = default;
			return "engine-default";
		}

		var copy = material.CreateCopy();
		var resolved = copy.IsValid() ? copy : material;
		DisableMaterialFog( resolved );
		terrain.MaterialOverride = resolved;
		return shaderPath;
	}

	static void DisableMaterialFog( Material material )
	{
		if ( material is null || !material.IsValid() )
			return;

		material.Set( "g_bFogEnabled", 0 );
		if ( material.Attributes is not null )
			material.Attributes.Set( "g_bFogEnabled", 0 );
	}

	static Material TryCreateFromShader( string shaderPath )
	{
		if ( string.IsNullOrWhiteSpace( shaderPath ) )
			return null;

		try
		{
			var mat = Material.FromShader( shaderPath );
			return mat is not null && mat.IsValid() ? mat : null;
		}
		catch
		{
			return null;
		}
	}
}
