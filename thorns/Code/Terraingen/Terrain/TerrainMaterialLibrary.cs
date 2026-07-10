namespace Terraingen.TerrainGen;

/// <summary>
/// Resolves compiled .tmat assets for runtime terrain generation.
/// </summary>
public static class TerrainMaterialLibrary
{
	static readonly string[][] GrassVariantPaths =
	[
		["terrain_materials/thorns_grass_new_0.tmat", "thorns_grass_new_0.tmat"],
		["terrain_materials/thorns_grass_new_1.tmat", "thorns_grass_new_1.tmat"],
		["terrain_materials/thorns_grass_new_2.tmat", "thorns_grass_new_2.tmat"],
		["terrain_materials/thorns_grass_new_3.tmat", "thorns_grass_new_3.tmat"],
	];

	static readonly string[] GrassFallbackPaths =
	[
		"terrain_materials/thorns_grass.tmat",
		"thorns_grass.tmat",
	];

	static readonly string[] DirtPaths =
	[
		"terrain_materials/thorns_dirt.tmat",
		"thorns_dirt.tmat",
	];

	static readonly string[] RockPaths =
	[
		"terrain_materials/thorns_rock.tmat",
		"thorns_rock.tmat",
	];

	static readonly string[] SnowPaths =
	[
		"terrain_materials/thorns_snow.tmat",
		"thorns_snow.tmat",
	];

	public static void PopulateMaterials( TerrainStorage storage, ThornsTerrainConfig config )
	{
		storage.Materials.Clear();

		var grassLoaded = 0;
		if ( config.MaterialGrass is not null && !storage.Materials.Contains( config.MaterialGrass ) )
		{
			storage.Materials.Add( config.MaterialGrass );
			grassLoaded = 1;
		}
		else
		{
			foreach ( var paths in GrassVariantPaths )
			{
				if ( TryResolve( paths, out var mat ) )
				{
					storage.Materials.Add( mat );
					grassLoaded++;
				}
			}

			if ( grassLoaded == 0 )
			{
				if ( TryResolve( GrassFallbackPaths, out var fallback ) )
				{
					storage.Materials.Add( fallback );
					grassLoaded = 1;
				}
			}
			else if ( grassLoaded < GrassVariantPaths.Length )
			{
				// Fill missing slots with the first resolved variant so indices stay contiguous.
				var first = storage.Materials[0];
				while ( grassLoaded < GrassVariantPaths.Length )
				{
					storage.Materials.Add( first );
					grassLoaded++;
				}
			}
		}

		TryAdd( storage, config.MaterialDirt, DirtPaths, "dirt" );
		TryAdd( storage, config.MaterialRock, RockPaths, "rock" );
		TryAdd( storage, config.MaterialSnow, SnowPaths, "snow" );

		if ( storage.Materials.Count == 0 )
			Log.Error( "[Thorns Terrain] No terrain materials loaded — terrain will render magenta. Assign .tmat assets on ThornsTerrainConfig or compile terrain_materials/*.tmat in the asset browser." );
		else
		{
			var layout = TerrainMaterialLayout.FromStorage( storage );
			Log.Info( $"[Thorns Terrain] Loaded {storage.Materials.Count} terrain material(s) ({layout.GrassVariantCount} grass variant(s))." );
		}
	}

	static void TryAdd( TerrainStorage storage, TerrainMaterial assigned, string[] paths, string label )
	{
		if ( assigned is not null && !storage.Materials.Contains( assigned ) )
		{
			storage.Materials.Add( assigned );
			return;
		}

		if ( TryResolve( paths, out var mat ) )
		{
			if ( !storage.Materials.Contains( mat ) )
				storage.Materials.Add( mat );
			if ( label is "rock" or "snow" )
				Log.Info( $"[Thorns Terrain] {label} terrain material resolved (terrain_materials/{label}.vtex)." );
			return;
		}

		Log.Warning( $"[Thorns Terrain] Could not resolve {label} terrain material — cliffs may render black. Recompile terrain_materials/thorns_rock.tmat in the asset browser." );
	}

	static bool TryResolve( string[] paths, out TerrainMaterial mat )
	{
		foreach ( var path in paths )
		{
			if ( ResourceLibrary.TryGet<TerrainMaterial>( path, out mat ) && mat is not null )
				return true;
		}

		mat = null;
		return false;
	}
}
