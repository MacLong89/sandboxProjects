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
		return TerrainSculptPipeline.Sculpt( resized, config );
	}

	public static ushort[] ToTerrainHeightmap( HeightmapField field )
	{
		// Light pass reduces visible terracing from 8-bit source heightmaps.
		var smoothed = HeightmapBlur.BoxBlur( field, radius: 2 );
		var lowBand = 0.42f;

		var data = new ushort[field.Heights.Length];
		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var h = field.Heights[i];
			var smoothBlend = h < lowBand ? 0.62f : 0.38f;
			h = MathX.Lerp( h, smoothed[i], smoothBlend );
			data[i] = (ushort)Math.Clamp( (int)(h * 65535f), 0, 65535 );
		}

		return data;
	}

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
