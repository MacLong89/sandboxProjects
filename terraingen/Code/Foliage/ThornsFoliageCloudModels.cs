namespace Terraingen.Foliage;

using Sandbox;
using Terraingen;

/// <summary>Tree model resolution — local .vmdl first, then s&amp;box cloud fallbacks when meshes are missing.</summary>
public static class ThornsFoliageCloudModels
{
	public const string PineCloudIdent = "facepunch.tp_tree_01";
	public const string OakCloudIdent = "facepunch.tree_oak_small_a";
	public const string AspenCloudIdent = "facepunch.tp_tree_01";

	static Model _pineCloudModel;
	static Model _oakCloudModel;
	static Model _aspenCloudModel;

	/// <summary>Cloud.Model requires compile-time string literals (SB2000).</summary>
	static Model PineCloudModel =>
		IsUsable( _pineCloudModel ) ? _pineCloudModel : _pineCloudModel = Cloud.Model( "facepunch.tp_tree_01" );

	static Model OakCloudModel =>
		IsUsable( _oakCloudModel ) ? _oakCloudModel : _oakCloudModel = Cloud.Model( "facepunch.tree_oak_small_a" );

	static Model AspenCloudModel =>
		IsUsable( _aspenCloudModel ) ? _aspenCloudModel : _aspenCloudModel = Cloud.Model( "facepunch.tp_tree_01" );

	public static FoliageModelLoadResult LoadModelSet( ThornsFoliageConfig config, ThornsFoliageDebugStats stats )
	{
		var pine = Resolve( config.PineModel, PineCloudModel, PineCloudIdent, "pine", stats );
		var aspen = Resolve( config.AspenModel, AspenCloudModel, AspenCloudIdent, "aspen", stats );
		var oak = Resolve( config.OakModel, OakCloudModel, OakCloudIdent, "oak", stats );

		if ( config.VerboseDebug )
		{
			LogModel( "pine", config.PineModel, pine );
			LogModel( "aspen", config.AspenModel, aspen );
			LogModel( "oak", config.OakModel, oak );
		}

		var set = new ThornsFoliagePlacer.FoliageModelSet( pine.Model, aspen.Model, oak.Model );
		Log.Info(
			$"[Thorns Foliage] Tree model sources — pine:{pine.Source} '{pine.SourcePath}' "
			+ $"aspen:{aspen.Source} '{aspen.SourcePath}' oak:{oak.Source} '{oak.SourcePath}'" );
		return new FoliageModelLoadResult( set, pine, aspen, oak );
	}

	static ResolvedTreeModel Resolve(
		string localPath,
		Model cloudModel,
		string cloudIdent,
		string label,
		ThornsFoliageDebugStats stats )
	{
		var local = ThornsFoliageModelCache.Load( localPath );
		if ( ThornsModelResourceLoad.IsUsable( local ) )
		{
			if ( HasRenderableMesh( local ) )
				return new ResolvedTreeModel( local, FoliageModelSource.Local, localPath );

			var bounds = local.Bounds.Size;
			var maxAxis = MathF.Max( bounds.x, MathF.Max( bounds.y, bounds.z ) );
			Log.Warning(
				$"[Thorns Foliage] Local {label} loaded but rejected as renderable "
				+ $"(bounds={bounds}, maxAxis={maxAxis:F3}, path='{localPath}')." );
		}
		else if ( !string.IsNullOrWhiteSpace( localPath ) )
		{
			Log.Warning(
				$"[Thorns Foliage] Local {label} model not loadable ('{localPath}') "
				+ $"mounted={ThornsModelResourceLoad.MountedVmdlExists( localPath )}." );
		}

		if ( IsUsable( cloudModel ) && HasRenderableMesh( cloudModel ) )
		{
			Log.Warning(
				$"[Thorns Foliage] Local {label} model unusable ('{localPath}') — using cloud fallback '{cloudIdent}'." );
			return new ResolvedTreeModel( cloudModel, FoliageModelSource.Cloud, cloudIdent );
		}

		stats.LastError = $"{label} model load failed";
		Log.Error(
			$"[Thorns Foliage] Failed to load {label} tree — local='{localPath}', cloud='{cloudIdent}'." );
		return new ResolvedTreeModel( default, FoliageModelSource.None, "" );
	}

	static bool IsUsable( Model model ) => ThornsModelResourceLoad.IsUsable( model );

	public static bool HasRenderableMesh( Model model )
	{
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
			return false;

		var size = model.Bounds.Size;
		var maxAxis = MathF.Max( size.x, MathF.Max( size.y, size.z ) );
		return maxAxis >= 0.25f;
	}

	public static float MeshHeightInches( Model model, ThornsFoliageConfig config = null )
	{
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
			return 1f;

		var bounds = model.RenderBounds.Size.LengthSquared > 1e-12f
			? model.RenderBounds
			: model.Bounds;
		var size = bounds.Size;
		var height = MathF.Max( MathF.Max( size.z, MathF.Max( size.x, size.y ) ), 0.01f );

		// foliage2 vmdl meshes are authored in meters (~1 unit tall), not inches.
		if ( height <= 32f && config is not null )
			height *= MathF.Max( config.InchesPerMeter, 1f );

		return MathF.Max( height, 1f );
	}

	public static float ComputeUniformScale(
		Model model,
		float targetHeightInches,
		ThornsFoliageConfig config,
		Random rng )
	{
		var meshHeight = MeshHeightInches( model, config );
		config.EnsureScaleLimits();
		var uniform = (targetHeightInches / meshHeight) * config.ScaleMultiplier * config.TreeSizeMultiplier;
		var minScale = MathF.Min( config.MinTreeRenderScale, config.MaxTreeRenderScale );
		var maxScale = MathF.Max( config.MinTreeRenderScale, config.MaxTreeRenderScale );
		uniform = Math.Clamp( uniform, minScale, maxScale );
		return uniform;
	}

	public static float EstimateWorldHeightInches( Model model, float uniformScale, float fallbackTargetInches, ThornsFoliageConfig config = null )
	{
		if ( HasRenderableMesh( model ) )
			return MeshHeightInches( model, config ) * uniformScale;

		return fallbackTargetInches * uniformScale;
	}

	static void LogModel( string label, string localPath, ResolvedTreeModel resolved )
	{
		var bounds = resolved.Model.IsValid ? resolved.Model.Bounds.Size : default;
		Log.Info(
			$"[Thorns Foliage] Model {label} source={resolved.Source} path='{resolved.SourcePath}' "
			+ $"valid={resolved.Model.IsValid} bounds={bounds}" );
	}
}

public enum FoliageModelSource
{
	None,
	Local,
	Cloud
}

public readonly struct ResolvedTreeModel
{
	public ResolvedTreeModel( Model model, FoliageModelSource source, string sourcePath )
	{
		Model = model;
		Source = source;
		SourcePath = sourcePath ?? "";
	}

	public Model Model { get; }
	public FoliageModelSource Source { get; }
	public string SourcePath { get; }
}

public readonly struct FoliageModelLoadResult
{
	public FoliageModelLoadResult(
		ThornsFoliagePlacer.FoliageModelSet set,
		ResolvedTreeModel pine,
		ResolvedTreeModel aspen,
		ResolvedTreeModel oak )
	{
		Set = set;
		Pine = pine;
		Aspen = aspen;
		Oak = oak;
	}

	public ThornsFoliagePlacer.FoliageModelSet Set { get; }
	public ResolvedTreeModel Pine { get; }
	public ResolvedTreeModel Aspen { get; }
	public ResolvedTreeModel Oak { get; }
}
