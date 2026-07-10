namespace Terraingen.TerrainGen;

/// <summary>
/// End-to-end heightmap import, crop selection, stylization, and terrain storage build.
/// </summary>
public static class ThornsTerrainGenerator
{
	public static HeightmapField GenerateHeightField( ThornsTerrainConfig config )
	{
		var source = HeightmapLoader.LoadFromContent( config.HeightmapPath );
		var crop = RegionCropSelector.SelectBestCrop( source, config, config.WorldSeed );

		Log.Info( $"[Thorns Terrain] Crop {crop.CropWidth}x{crop.CropHeight} at ({crop.OriginX},{crop.OriginY}) score={crop.Metrics.Score:F2}" );

		var cropped = HeightmapResampler.ExtractRegion( source, crop.OriginX, crop.OriginY, crop.CropWidth, crop.CropHeight, config.FlipHeightmapY );
		var resolution = RoundDownToPowerOfTwo( config.TerrainResolution );
		var resized = HeightmapResampler.Resize( cropped, resolution, resolution );
		PrefilterSourceQuantization( resized );
		return TerrainSculptPipeline.Sculpt( resized, config );
	}

	/// <summary>Remove isolated 8-bit outliers before sculpt — not a blanket blur.</summary>
	static void PrefilterSourceQuantization( HeightmapField field ) =>
		HeightmapSheerSmooth.RemoveIsolatedSpikesNormalized( field, maxPasses: 2 );

	public static int RoundDownToPowerOfTwo( int value )
	{
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return Math.Max( 512, value - (value >> 1) );
	}
}
