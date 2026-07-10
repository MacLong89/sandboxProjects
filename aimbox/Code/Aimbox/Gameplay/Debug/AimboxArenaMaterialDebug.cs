namespace Sandbox;

/// <summary>Runtime diagnostics for procedural arena material binding.</summary>
public static class AimboxArenaMaterialDebug
{
	public static bool Enabled { get; private set; } = true;
	public static int MaxMaterialLogs { get; private set; } = 96;
	public static int MaxRendererLogs { get; private set; } = 192;

	static readonly HashSet<string> SeenMaterialEvents = new( StringComparer.OrdinalIgnoreCase );
	static int _materialLogCount;
	static int _rendererLogCount;

	[ConCmd( "aimbox_material_debug" )]
	public static void SetEnabled( bool enabled = true )
	{
		Enabled = enabled;
		ResetCounters();
		Log.Info( $"[Aimbox MatDbg] Material diagnostics {(enabled ? "enabled" : "disabled")}." );
	}

	[ConCmd( "aimbox_material_probe" )]
	public static void ProbeConsole() => ProbeArena( "console" );

	[ConCmd( "aimbox_material_rebind" )]
	public static void RebindConsole() => RebindArena( "console" );

	public static void ResetCounters()
	{
		_materialLogCount = 0;
		_rendererLogCount = 0;
		SeenMaterialEvents.Clear();
	}

	public static void LogMaterialEvent( string stage, AimboxArenaSurface surface, Material material )
	{
		if ( !Enabled || _materialLogCount >= MaxMaterialLogs )
			return;

		var slug = surface.MaterialSlug() ?? "";
		var path = AimboxArenaMaterials.MaterialPath( surface );
		var materialDesc = DescribeMaterial( material );
		var fileDesc = DescribeMaterialFiles( path );
		var key = $"{stage}|{surface}|{slug}|{path}|{materialDesc}";
		if ( !SeenMaterialEvents.Add( key ) )
			return;

		_materialLogCount++;
		Log.Info(
			$"[Aimbox MatDbg] material {stage}: surface={surface} slug='{slug}' path='{path ?? "(none)"}' "
			+ $"files={fileDesc} material={materialDesc}" );
	}

	public static void LogDefaultMaterialEvent( string stage, Material material )
	{
		if ( !Enabled || _materialLogCount >= MaxMaterialLogs )
			return;

		var materialDesc = DescribeMaterial( material );
		var fileDesc = DescribeMaterialFiles( AimboxArenaMaterials.DefaultMaterialPath );
		var key = $"{stage}|default|{materialDesc}";
		if ( !SeenMaterialEvents.Add( key ) )
			return;

		_materialLogCount++;
		Log.Info(
			$"[Aimbox MatDbg] material {stage}: defaultPath='{AimboxArenaMaterials.DefaultMaterialPath}' "
			+ $"files={fileDesc} material={materialDesc}" );
	}

	public static void LogRendererState( string stage, GameObject go, ModelRenderer renderer )
	{
		if ( !Enabled || _rendererLogCount >= MaxRendererLogs )
			return;

		if ( !go.IsValid() || renderer is null || !renderer.IsValid() )
			return;

		_rendererLogCount++;
		var tag = go.Components.Get<AimboxArenaMaterialTag>();
		var surface = tag?.Surface.ToString() ?? "(untagged)";
		var slug = tag?.Slug ?? "";
		var path = tag?.MaterialPath ?? "";
		var size = tag is not null ? Format( tag.Size ) : "(unknown)";
		var model = renderer.Model;
		var modelName = DescribeModel( model );
		var baseMaterial = DescribeModelMaterial( model, 0 );
		var materialOverride = DescribeMaterial( renderer.MaterialOverride );
		var slots = renderer.Materials?.Count ?? 0;

		Log.Info(
			$"[Aimbox MatDbg] renderer {stage} #{_rendererLogCount}: go='{go.Name}' root='{go.Parent?.Name ?? "(none)"}' "
			+ $"surface={surface} slug='{slug}' path='{path}' size={size} "
			+ $"enabled={renderer.Enabled} gameLayer={renderer.RenderOptions.Game} "
			+ $"model={modelName} slots={slots} modelMat0={baseMaterial} override={materialOverride}" );
	}

	public static void ProbeArena( string reason, int maxBlocks = 36 )
	{
		if ( !Enabled )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			Log.Warning( $"[Aimbox MatDbg] probe ({reason}): no active scene." );
			return;
		}

		Log.Info( $"[Aimbox MatDbg] === arena material probe: {reason} ===" );

		var total = 0;
		var enabled = 0;
		var missingOverride = 0;
		var errorModels = 0;
		var bySurface = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );

		foreach ( var rootName in AimboxArenaWorld.ArenaRootNames )
		{
			var root = AimboxArenaWorld.FindArenaRoot( rootName );
			if ( !root.IsValid() )
				continue;

			foreach ( var child in root.Children )
			{
				if ( !child.IsValid() )
					continue;

				var renderer = child.Components.Get<ModelRenderer>();
				if ( renderer is null || !renderer.IsValid() )
					continue;

				total++;
				if ( renderer.Enabled )
					enabled++;
				if ( !renderer.MaterialOverride.IsValid() )
					missingOverride++;
				if ( renderer.Model.IsValid() && renderer.Model.IsError )
					errorModels++;

				var tag = child.Components.Get<AimboxArenaMaterialTag>();
				var surface = tag?.Surface.ToString() ?? "(untagged)";
				bySurface[surface] = bySurface.TryGetValue( surface, out var count ) ? count + 1 : 1;

				if ( total <= maxBlocks )
					LogRendererState( "probe", child, renderer );
			}
		}

		var surfaces = bySurface.Count == 0
			? "none"
			: string.Join( ", ", bySurface.OrderBy( x => x.Key ).Select( x => $"{x.Key}={x.Value}" ) );

		Log.Info(
			$"[Aimbox MatDbg] probe summary: blocks={total} enabled={enabled} missingOverride={missingOverride} "
			+ $"errorModels={errorModels} surfaces=[{surfaces}]" );
	}

	public static void RebindArena( string reason )
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			Log.Warning( $"[Aimbox MatDbg] rebind ({reason}): no active scene." );
			return;
		}

		AimboxArenaMaterials.ResetCache( $"rebind:{reason}" );

		var total = 0;
		var rebound = 0;
		foreach ( var rootName in AimboxArenaWorld.ArenaRootNames )
		{
			var root = AimboxArenaWorld.FindArenaRoot( rootName );
			if ( !root.IsValid() )
				continue;

			foreach ( var child in root.Children )
			{
				if ( !child.IsValid() )
					continue;

				var renderer = child.Components.Get<ModelRenderer>();
				var tag = child.Components.Get<AimboxArenaMaterialTag>();
				if ( renderer is null || !renderer.IsValid() || tag is null )
					continue;

				total++;
				var material = AimboxArenaMaterials.Get( tag.Surface );
				if ( material is null || !material.IsValid() || material.IsError )
				{
					Log.Warning( $"[Aimbox MatDbg] rebind skipped '{child.Name}': material invalid for {tag.Surface}." );
					continue;
				}

				renderer.MaterialOverride = material;
				rebound++;
				LogRendererState( "rebind", child, renderer );
			}
		}

		Log.Info( $"[Aimbox MatDbg] rebind summary ({reason}): scanned={total} rebound={rebound}." );
	}

	public static string DescribeMaterial( Material material )
	{
		if ( material is null || !material.IsValid() )
			return "null-or-invalid";

		var resource = SafeMaterialResource( material );
		var shader = "unknown-shader";
		try
		{
			if ( material.Shader is not null && material.Shader.IsValid() )
				shader = material.Shader.ResourceName ?? "valid-shader";
		}
		catch
		{
			shader = "shader-read-failed";
		}

		return $"valid={material.IsValid()} error={material.IsError} resource='{resource}' shader='{shader}'";
	}

	static string DescribeModel( Model model )
	{
		if ( model is null || !model.IsValid() )
			return "null-or-invalid";

		var name = model.ResourceName ?? model.Name ?? "(unnamed)";
		return $"valid={model.IsValid()} error={model.IsError} name='{name}' meshes={model.MeshCount}";
	}

	static string DescribeModelMaterial( Model model, int drawCall )
	{
		if ( model is null || !model.IsValid() )
			return "model-invalid";

		var material = model.Materials.ElementAtOrDefault( drawCall );
		return DescribeMaterial( material );
	}

	static string DescribeMaterialFiles( string materialPath )
	{
		if ( string.IsNullOrWhiteSpace( materialPath ) )
			return "none";

		var texturePath = materialPath.EndsWith( ".vmat", StringComparison.OrdinalIgnoreCase )
			? materialPath[..^".vmat".Length] + ".png"
			: "";

		return $"vmat={MountedFileExists( materialPath )} png={MountedFileExists( texturePath )}";
	}

	static bool MountedFileExists( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		var normalized = path.Trim().Replace( '\\', '/' );
		if ( FileSystem.Mounted.FileExists( normalized ) )
			return true;

		return !normalized.StartsWith( '/' ) && FileSystem.Mounted.FileExists( $"/{normalized}" );
	}

	static string SafeMaterialResource( Material material )
	{
		try
		{
			return material.ResourcePath ?? material.ResourceName ?? material.ToString() ?? "(unknown)";
		}
		catch
		{
			return "(resource-read-failed)";
		}
	}

	static string Format( Vector3 v ) => $"{v.x:0.##},{v.y:0.##},{v.z:0.##}";
}

public sealed class AimboxArenaMaterialTag : Component
{
	public AimboxArenaSurface Surface { get; set; }
	public string Slug { get; set; } = "";
	public string MaterialPath { get; set; } = "";
	public Vector3 Size { get; set; }
}
