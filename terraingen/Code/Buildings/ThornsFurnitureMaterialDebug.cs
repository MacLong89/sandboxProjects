namespace Terraingen.Buildings;

using Terraingen;
using Terraingen.Rendering;

/// <summary>Verbose furniture material diagnostics (enable from floorplan test or world gen).</summary>
public static class ThornsFurnitureMaterialDebug
{
	public static bool Enabled { get; set; }
	public static int MaxLogs { get; set; } = 48;

	static int _count;

	public static void Reset() => _count = 0;

	public static void Write( string message )
	{
		if ( !Enabled || _count >= MaxLogs )
			return;

		_count++;
		Log.Info( $"[Thorns FurnitureMat] {message}" );
	}

	public static void LogSpawn(
		string phase,
		string structureDefId,
		string modelPath,
		Model model,
		GameObject root,
		bool usedFallbackBox,
		ModelRenderer renderer = null )
	{
		if ( !Enabled )
			return;

		var modelOk = ThornsModelResourceLoad.IsUsable( model );
		var mounted = ThornsModelResourceLoad.MountedVmdlExists( modelPath );
		var vmat = ThornsModelMaterialUvScale.InferBaseColorVmatPath( modelPath );
		var png = VmatToBasecolorPng( vmat );

		var localScale = root.IsValid() ? root.LocalScale.ToString() : "—";
		var worldScale = root.IsValid() ? root.WorldScale.ToString() : "—";
		var overrideDesc = DescribeMaterial( renderer?.MaterialOverride ?? default );
		var modelName = model.IsValid ? model.Name : "(invalid)";

		Write(
			$"{phase} id={structureDefId} fallbackBox={usedFallbackBox} mountedVmdl={mounted} modelOk={modelOk} modelError={model.IsError} modelName={modelName} "
			+ $"localScale={localScale} worldScale={worldScale} override={overrideDesc}" );
		Write(
			$"{phase} assets id={structureDefId} vmdlFile={FileExists( modelPath )} vmat={vmat} vmatFile={FileExists( vmat )} "
			+ $"png={png} pngFile={FileExists( png )}" );
	}

	public static void LogMaterialStep(
		string step,
		string modelPath,
		Vector3 meshScale,
		Vector2 uvScale,
		bool needsUv,
		Material src,
		Material resultOverride )
	{
		if ( !Enabled )
			return;

		Write(
			$"{step} path={modelPath} meshScale={meshScale} uv={uvScale} needsUv={needsUv} "
			+ $"src={DescribeMaterial( src )} overrideAfter={DescribeMaterial( resultOverride )}" );
	}

	public static string DescribeMaterial( Material mat )
	{
		if ( !mat.IsValid() )
			return "none";

		try
		{
			return mat.ResourcePath ?? mat.ToString() ?? "valid";
		}
		catch
		{
			return "valid";
		}
	}

	static string VmatToBasecolorPng( string vmatPath )
	{
		if ( string.IsNullOrWhiteSpace( vmatPath ) )
			return "";

		return vmatPath.EndsWith( "_basecolor.vmat", StringComparison.OrdinalIgnoreCase )
			? vmatPath[..^"_basecolor.vmat".Length] + "_basecolor.png"
			: "";
	}

	static bool FileExists( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		var normalized = path.Trim().Replace( '\\', '/' );
		if ( FileSystem.Mounted.FileExists( normalized ) )
			return true;

		return !normalized.StartsWith( '/' ) && FileSystem.Mounted.FileExists( $"/{normalized}" );
	}
}
