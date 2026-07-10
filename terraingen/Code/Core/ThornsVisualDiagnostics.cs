namespace Terraingen.Core;

using Terraingen.Foliage;
using Terraingen.World.Environment;

/// <summary>Console probes for foliage model resolution and cloud materials.</summary>
public static class ThornsVisualDiagnostics
{
	[ConCmd( "thorns_foliage_model_diag" )]
	public static void FoliageModelDiag()
	{
		var config = new ThornsFoliageConfig();
		var stats = new ThornsFoliageDebugStats();
		Log.Info( "=== thorns_foliage_model_diag ===" );
		LogModelProbe( "pine", config.PineModel );
		LogModelProbe( "aspen", config.AspenModel );
		LogModelProbe( "oak", config.OakModel );

		var loaded = ThornsFoliageCloudModels.LoadModelSet( config, stats );
		Log.Info(
			$"LoadModelSet: pine={loaded.Pine.Source} '{loaded.Pine.SourcePath}' "
			+ $"aspen={loaded.Aspen.Source} '{loaded.Aspen.SourcePath}' "
			+ $"oak={loaded.Oak.Source} '{loaded.Oak.SourcePath}' valid={loaded.Set.IsValid}" );
		if ( !string.IsNullOrWhiteSpace( stats.LastError ) )
			Log.Warning( $"LoadModelSet error: {stats.LastError}" );
	}

	[ConCmd( "thorns_cloud_rebuild" )]
	public static void CloudRebuild()
	{
		var count = 0;
		foreach ( var layer in Game.ActiveScene?.GetAllComponents<ThornsCloudBillboardLayer>() ?? Array.Empty<ThornsCloudBillboardLayer>() )
		{
			if ( layer is null || !layer.IsValid() )
				continue;

			layer.ForceRebuild();
			count++;
		}

		Log.Info( $"thorns_cloud_rebuild: rebuilt {count} cloud layer(s)." );
	}

	[ConCmd( "thorns_cloud_material_diag" )]
	public static void CloudMaterialDiag()
	{
		Log.Info( "=== thorns_cloud_material_diag ===" );
		Log.Info(
			$"render context: playing={Game.IsPlaying} dedicated={Application.IsDedicatedServer} "
			+ $"headless={Application.IsHeadless} net={Networking.IsActive} host={Networking.IsHost} client={Networking.IsClient}" );

		if ( ThornsTextureResourceLoad.TryLoadCloudTexture( out _, out var texDetail ) )
			Log.Info( $"legacy cloud sprite texture: {texDetail}" );
		else
			Log.Warning( $"legacy cloud sprite texture unavailable; solid mesh clouds do not require it: {texDetail}" );

		const string materialPath = "materials/skybox/thorns_cloud_textured.vmat";
		var source = Material.Load( materialPath );
		if ( source is null || !source.IsValid() )
			Log.Error( $"Material.Load failed: '{materialPath}'" );
		else
		{
			var shaderName = source.Shader is not null && source.Shader.IsValid()
				? source.Shader.ResourceName
				: "(invalid shader)";
			Log.Info( $"textured cloud vmat '{materialPath}' shader='{shaderName}' valid={source.IsValid()}" );
		}

		var count = 0;
		foreach ( var layer in Game.ActiveScene?.GetAllComponents<ThornsCloudBillboardLayer>() ?? Array.Empty<ThornsCloudBillboardLayer>() )
		{
			if ( layer is null || !layer.IsValid() )
				continue;

			count++;
			Log.Info(
				$"layer #{count} go='{layer.GameObject.Name}' vmat='{layer.MaterialPath}' "
				+ $"status={layer.BuildDebugStatus()}" );
		}

		if ( count == 0 )
			Log.Warning( "No ThornsCloudBillboardLayer found in active scene." );
	}

	static void LogModelProbe( string label, string path )
	{
		var mounted = ThornsModelResourceLoad.MountedVmdlExists( path );
		var loadOk = ThornsModelResourceLoad.TryLoadUsable( path, out var model );
		var bounds = loadOk ? model.Bounds.Size : default;
		var maxAxis = MathF.Max( bounds.x, MathF.Max( bounds.y, bounds.z ) );
		var renderable = loadOk && ThornsFoliageCloudModels.HasRenderableMesh( model );
		Log.Info(
			$"{label}: path='{path}' mounted={mounted} loadOk={loadOk} "
			+ $"valid={( loadOk && model.IsValid )} error={( loadOk && model.IsError )} "
			+ $"bounds={bounds} maxAxis={maxAxis:F3} renderable={renderable}" );
	}
}
