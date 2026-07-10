namespace Terraingen;

/// <summary>Validates that first-run / published clients can load shipped gameplay meshes (not dev error placeholders).</summary>
public static class ThornsPublishedAssetValidation
{
	public static readonly string[] CriticalModelPaths =
	[
		"models/clutter/grass_common_short.vmdl",
		"models/tools/bow.vmdl",
		"models/foliage2/pine_tree.vmdl",
		"models/foliage2/aspen_tree.vmdl",
		"models/foliage2/oak_tree.vmdl",
		"models/deer/deer.vmdl",
		"models/wolf/wolf.vmdl",
		"models/boulders/boulder1.vmdl",
		"models/placeables/chest.vmdl",
		"models/tools/stone_axe.vmdl"
	];

	public static bool TryValidateCriticalModels( out string[] missing )
	{
		var gaps = new List<string>();
		foreach ( var path in CriticalModelPaths )
		{
			if ( ThornsModelResourceLoad.TryLoadUsable( path, out _ ) )
				continue;

			gaps.Add( path );
		}

		missing = gaps.ToArray();
		return missing.Length == 0;
	}

	public static void LogBootValidation( string context )
	{
		ThornsMountedFiles.LogMountProbe( context );

		if ( !TryValidateCriticalModels( out var missing ) )
		{
			Log.Error(
				$"[Thorns Assets] {missing.Length} critical gameplay model(s) missing or error placeholders ({context}): " +
				$"{string.Join( ", ", missing )}. Republish after confirming terraingen.sbproj Resources includes models, materials, fbx sources, scenes, and shaders." );
			return;
		}

		Log.Info( $"[Thorns Assets] Critical gameplay models OK ({context})." );
	}
}
