namespace Terraingen.UI;

using Terraingen.TerrainGen;
using Terraingen.UI.Core;

/// <summary>One-time top-down map texture from the world heightfield (or terrain rays as fallback).</summary>
public static class ThornsMapTextureCache
{
	const int TextureBuildVersion = 4;

	static Texture _texture;
	static int _seed = int.MinValue;
	static int _buildVersion;
	static bool _fromSculptedField;

	public static Texture Texture => _texture;
	public static bool IsReady => _texture is not null && _texture.IsValid;

	public static void Invalidate()
	{
		_texture = null;
		_seed = int.MinValue;
		_buildVersion = 0;
		_fromSculptedField = false;
	}

	public static void Ensure( Terrain terrain, int seed, HeightmapField field = null, ThornsTerrainConfig config = null )
	{
		if ( _buildVersion != TextureBuildVersion )
		{
			_texture = null;
			_seed = int.MinValue;
			_fromSculptedField = false;
		}

		if ( field is null && _texture is not null && _texture.IsValid && _seed == seed && _buildVersion == TextureBuildVersion )
			return;

		if ( field is not null && _fromSculptedField && _texture is not null && _texture.IsValid && _seed == seed && _buildVersion == TextureBuildVersion )
			return;

		if ( field is not null && field.Width > 0 && field.Height > 0 && terrain.IsValid() )
		{
			if ( TryBuildFromHeightfield( terrain, field, config, seed ) )
				return;
		}

		if ( terrain.IsValid() )
			TryBuildFromTerrainRays( terrain, config, seed );
	}

	static bool TryBuildFromHeightfield( Terrain terrain, HeightmapField field, ThornsTerrainConfig config, int seed )
	{
		try
		{
			const int resolution = 512;
			var bmp = new Bitmap( resolution, resolution );
			var waterLevel = ThornsMapProjection.GetVisualWaterLevelNormalized( config );

			for ( var y = 0; y < resolution; y++ )
			{
				for ( var x = 0; x < resolution; x++ )
				{
					ThornsMapProjection.MapPixelToWorld( terrain, x, y, resolution, out var worldX, out var worldY );
					Color color;
					if ( ThornsMapProjection.TrySampleNormalizedHeight( field, terrain, worldX, worldY, out var h ) )
						color = HeightToColor( h, waterLevel );
					else
						color = ThornsMapProjection.MapOceanColor;

					bmp.SetPixel( x, y, color );
				}
			}

			ApplyTexture( bmp, seed, fromSculptedField: true );
			Log.Info( $"[Thorns Map] Map texture generated from heightfield ({resolution}x{resolution})." );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Map] Heightfield map texture generation failed." );
			return false;
		}
	}

	static void TryBuildFromTerrainRays( Terrain terrain, ThornsTerrainConfig config, int seed )
	{
		try
		{
			const int resolution = 512;
			var bmp = new Bitmap( resolution, resolution );
			var origin = terrain.GameObject.WorldPosition;
			var maxH = terrain.TerrainHeight;
			var waterLevel = ThornsMapProjection.GetVisualWaterLevelNormalized( config );

			for ( var y = 0; y < resolution; y++ )
			{
				for ( var x = 0; x < resolution; x++ )
				{
					ThornsMapProjection.MapPixelToWorld( terrain, x, y, resolution, out var wx, out var wy );
					var rayStart = new Vector3( wx, wy, origin.z + maxH * 2f );
					var color = new Color( 0.91f, 0.85f, 0.75f );

					if ( terrain.RayIntersects( new Ray( rayStart, Vector3.Down ), maxH * 4f, out var localHit ) )
					{
						var h01 = (localHit.z / maxH).Clamp( 0f, 1.25f );
						color = HeightToColor( h01, waterLevel );
					}

					bmp.SetPixel( x, y, color );
				}
			}

			ApplyTexture( bmp, seed, fromSculptedField: false );
			Log.Info( $"[Thorns Map] Map texture generated from terrain rays ({resolution}x{resolution})." );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Map] Terrain ray map texture generation failed." );
		}
	}

	static Color HeightToColor( float h01, float waterLevel )
	{
		if ( h01 <= waterLevel )
			return ThornsMapProjection.MapWaterColor;

		if ( h01 > 0.72f )
			return new Color( 0.68f, 0.70f, 0.72f );

		if ( h01 > 0.45f )
			return new Color( 0.42f, 0.54f, 0.38f );

		return new Color( 0.52f, 0.64f, 0.44f );
	}

	static void ApplyTexture( Bitmap bmp, int seed, bool fromSculptedField )
	{
		_texture = bmp.ToTexture();
		_seed = seed;
		_buildVersion = TextureBuildVersion;
		_fromSculptedField = fromSculptedField;
		UiRevisionBus.Publish( UiRevisionChannel.Map );
	}
}
