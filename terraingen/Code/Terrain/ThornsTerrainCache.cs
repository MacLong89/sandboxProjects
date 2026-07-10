namespace Terraingen.TerrainGen;

/// <summary>Cached terrain reference — avoids repeated scene scans.</summary>
public static class ThornsTerrainCache
{
	static Terrain _terrain;
	static ThornsTerrainConfig _config;

	public static Terrain Current => _terrain is not null && _terrain.IsValid() ? _terrain : null;
	public static ThornsTerrainConfig Config => _config;

	public static void Register( Terrain terrain, ThornsTerrainConfig config = null )
	{
		if ( terrain is not null && terrain.IsValid() )
			_terrain = terrain;

		if ( config is not null )
			_config = config;
	}

	public static void Clear()
	{
		_terrain = null;
		_config = null;
	}

	public static Terrain Resolve( Scene scene )
	{
		if ( Current is not null )
			return Current;

		if ( !scene.IsValid() )
			return null;

		foreach ( var terrain in scene.GetAllComponents<Terrain>() )
		{
			if ( terrain.IsValid() )
			{
				Register( terrain );
				return terrain;
			}
		}

		return null;
	}
}
