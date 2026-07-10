namespace Terraingen.TerrainGen;

/// <summary>
/// Indices into <see cref="TerrainStorage.Materials"/> after <see cref="TerrainMaterialLibrary.PopulateMaterials"/>.
/// </summary>
public readonly struct TerrainMaterialLayout
{
	public int GrassVariantCount { get; init; }
	public int DirtIndex => GrassVariantCount;
	public int RockIndex => GrassVariantCount + 1;
	public int SnowIndex => GrassVariantCount + 2;
	public int TotalCount { get; init; }

	public bool HasDirt => TotalCount > DirtIndex;
	public bool HasRock => TotalCount > RockIndex;
	public bool HasSnow => TotalCount > SnowIndex;

	public static TerrainMaterialLayout FromStorage( TerrainStorage storage )
	{
		var n = storage?.Materials?.Count ?? 0;
		if ( n >= 7 )
			return new TerrainMaterialLayout { GrassVariantCount = 4, TotalCount = n };

		if ( n >= 4 )
			return new TerrainMaterialLayout { GrassVariantCount = 1, TotalCount = n };

		return new TerrainMaterialLayout { GrassVariantCount = Math.Max( 1, n ), TotalCount = n };
	}

	public byte PickGrassVariant( int cellIndex, int worldSeed )
	{
		if ( GrassVariantCount <= 1 )
			return 0;

		unchecked
		{
			var h = (uint)(cellIndex ^ (worldSeed * 0x9E3779B9));
			h ^= h >> 16;
			h *= 0x7feb352d;
			h ^= h >> 15;
			h *= 0x846ca68b;
			h ^= h >> 16;
			return (byte)(h % (uint)GrassVariantCount);
		}
	}
}
