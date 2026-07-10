namespace Terraingen.TerrainGen;

/// <summary>
/// Deterministic world-space height sampling for future chunk streaming.
/// </summary>
public sealed class TerrainChunkSampler
{
	readonly HeightmapField _field;
	readonly float _worldSize;
	readonly float _maxHeight;

	public TerrainChunkSampler( HeightmapField field, float worldSizeInches, float maxHeightInches )
	{
		_field = field;
		_worldSize = worldSizeInches;
		_maxHeight = maxHeightInches;
	}

	public float SampleHeightInches( float worldX, float worldY )
	{
		var u = worldX / _worldSize;
		var v = worldY / _worldSize;
		return _field.SampleBilinear( u, v ) * _maxHeight;
	}

	public bool IsUnderSeaLevel( float worldX, float worldY, float seaLevelNormalized )
	{
		var u = worldX / _worldSize;
		var v = worldY / _worldSize;
		return _field.SampleBilinear( u, v ) <= seaLevelNormalized;
	}
}
