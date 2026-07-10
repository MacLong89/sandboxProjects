namespace FinalOutpost;

/// <summary>
/// Global placement grid over the whole world. Cells are <see cref="GameConstants.CellSize"/> wide
/// and signed (cell 0 sits just past the origin), so the grid tiles perfectly across the large
/// plots that surround the base. Which cells are actually buildable is decided by <see cref="BuildManager"/>
/// (plot ownership / clearing), not by this coordinate helper.
/// </summary>
public static class BuildGrid
{
	public static Vector3 CellToWorld( int cellX, int cellY )
	{
		var x = (cellX + 0.5f) * GameConstants.CellSize;
		var y = (cellY + 0.5f) * GameConstants.CellSize;
		var z = OutpostTerrain.SampleHeight( x, y );
		return new Vector3( x, y, z );
	}

	public static bool WorldToCell( Vector3 world, out int cellX, out int cellY )
	{
		cellX = (int)MathF.Floor( world.x / GameConstants.CellSize );
		cellY = (int)MathF.Floor( world.y / GameConstants.CellSize );
		return true;
	}

	public static bool WorldToCell( Vector2 world, out int cellX, out int cellY )
	{
		cellX = (int)MathF.Floor( world.x / GameConstants.CellSize );
		cellY = (int)MathF.Floor( world.y / GameConstants.CellSize );
		return true;
	}

	/// <summary>Command post occupies the four cells around the world origin.</summary>
	public static bool IsCoreCell( int cellX, int cellY ) =>
		(cellX == -1 || cellX == 0) && (cellY == -1 || cellY == 0);
}
