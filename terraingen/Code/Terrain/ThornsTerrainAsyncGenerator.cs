namespace Terraingen.TerrainGen;

/// <summary>Spreads heightfield build across frames to reduce startup hitches.</summary>
public sealed class ThornsTerrainAsyncGenerator
{
	enum Phase
	{
		Idle,
		LoadSource,
		CropResize,
		Sculpt,
		Complete,
		Failed
	}

	Phase _phase = Phase.Idle;
	ThornsTerrainConfig _config;
	HeightmapField _source;
	HeightmapField _cropped;
	HeightmapField _resized;
	HeightmapField _result;
	string _error;

	public bool IsComplete => _phase is Phase.Complete or Phase.Failed;
	public bool Succeeded => _phase == Phase.Complete;
	public HeightmapField Result => _result;
	public string Error => _error;

	public void Begin( ThornsTerrainConfig config )
	{
		_config = config;
		_phase = Phase.LoadSource;
		_error = "";
		_result = null;
	}

	public void Tick()
	{
		if ( IsComplete )
			return;

		try
		{
			switch ( _phase )
			{
				case Phase.LoadSource:
					_source = HeightmapLoader.LoadFromContent( _config.HeightmapPath );
					_phase = Phase.CropResize;
					break;

				case Phase.CropResize:
				{
					var crop = RegionCropSelector.SelectBestCrop( _source, _config, _config.WorldSeed );
					_cropped = HeightmapResampler.ExtractRegion(
						_source,
						crop.OriginX,
						crop.OriginY,
						crop.CropWidth,
						crop.CropHeight,
						_config.FlipHeightmapY );
					var resolution = ThornsTerrainGenerator.RoundDownToPowerOfTwo( _config.TerrainResolution );
					_resized = HeightmapResampler.Resize( _cropped, resolution, resolution );
					_phase = Phase.Sculpt;
					break;
				}

				case Phase.Sculpt:
					_result = TerrainSculptPipeline.Sculpt( _resized, _config );
					_phase = Phase.Complete;
					break;
			}
		}
		catch ( Exception ex )
		{
			_error = ex.Message;
			_phase = Phase.Failed;
		}
	}
}
