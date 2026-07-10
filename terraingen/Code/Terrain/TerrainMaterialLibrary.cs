namespace Terraingen.TerrainGen;

/// <summary>
/// Resolves compiled .tmat assets for runtime terrain generation.
/// </summary>
public static class TerrainMaterialLibrary
{
	static readonly string[] GrassPaths =
	{
		"terrain_materials/thorns_grass.tmat",
		"thorns_grass.tmat",
	};

	static readonly string[] DirtPaths =
	{
		"terrain_materials/thorns_dirt.tmat",
		"thorns_dirt.tmat",
	};

	static readonly string[] RockPaths =
	{
		"terrain_materials/thorns_rock.tmat",
		"thorns_rock.tmat",
	};

	static readonly string[] SnowPaths =
	{
		"terrain_materials/thorns_snow.tmat",
		"thorns_snow.tmat",
	};

	public static void PopulateMaterials( TerrainStorage storage, ThornsTerrainConfig config )
	{
		storage.Materials.Clear();

		TryAdd( storage, config.MaterialGrass, GrassPaths, "grass" );
		TryAdd( storage, config.MaterialDirt, DirtPaths, "dirt" );
		TryAdd( storage, config.MaterialRock, RockPaths, "rock" );
		TryAdd( storage, config.MaterialSnow, SnowPaths, "snow" );

		if ( storage.Materials.Count == 0 )
			Log.Error( "[Thorns Terrain] No terrain materials loaded — terrain will render magenta. Assign .tmat assets on ThornsTerrainConfig or compile terrain_materials/*.tmat in the asset browser." );
		else
			Log.Info( $"[Thorns Terrain] Loaded {storage.Materials.Count} terrain material(s)." );
	}

	static void TryAdd( TerrainStorage storage, TerrainMaterial assigned, string[] paths, string label )
	{
		if ( assigned is not null && !storage.Materials.Contains( assigned ) )
		{
			storage.Materials.Add( assigned );
			return;
		}

		foreach ( var path in paths )
		{
			if ( ResourceLibrary.TryGet<TerrainMaterial>( path, out var mat ) && mat is not null )
			{
				if ( !storage.Materials.Contains( mat ) )
					storage.Materials.Add( mat );
				return;
			}
		}

		Log.Warning( $"[Thorns Terrain] Could not resolve {label} terrain material." );
	}
}
