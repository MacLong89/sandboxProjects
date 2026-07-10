namespace Terraingen.Core;

/// <summary>Shared world-chunk grid math for foliage, minerals, and clutter.</summary>
public static class ThornsChunkGrid
{
	public static (int CountX, int CountY) GetCellCounts( float terrainSizeInches, float chunkSizeInches )
	{
		var countX = Math.Max( 1, (int)Math.Ceiling( terrainSizeInches / chunkSizeInches ) );
		var countY = Math.Max( 1, (int)Math.Ceiling( terrainSizeInches / chunkSizeInches ) );
		return (countX, countY);
	}

	public static List<Vector2Int> BuildFullGrid( float terrainSizeInches, float chunkSizeInches )
	{
		var (countX, countY) = GetCellCounts( terrainSizeInches, chunkSizeInches );
		var cells = new List<Vector2Int>( countX * countY );

		for ( var y = 0; y < countY; y++ )
		for ( var x = 0; x < countX; x++ )
			cells.Add( new Vector2Int( x, y ) );

		return cells;
	}

	public static Vector3 CellCenter( Vector3 terrainOrigin, float chunkSizeInches, Vector2Int cell ) =>
		new(
			terrainOrigin.x + (cell.x + 0.5f) * chunkSizeInches,
			terrainOrigin.y + (cell.y + 0.5f) * chunkSizeInches,
			0f );
}
