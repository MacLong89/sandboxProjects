namespace Terraingen.TerrainGen;

public interface ITerrainSculptPass
{
	string Name { get; }
	void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature );
}
