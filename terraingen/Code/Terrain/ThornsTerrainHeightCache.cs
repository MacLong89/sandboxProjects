namespace Terraingen.TerrainGen;

using System.Buffers.Binary;

/// <summary>Persistent heightfield cache so clients can skip host-equivalent sculpt work.</summary>
public static class ThornsTerrainHeightCache
{
	const string CacheFolder = "thorns_world_cache";
	const uint BinaryMagic = 0x54474854; // "THGT"

	sealed class ThornsHeightCacheDto
	{
		public int Width { get; set; }
		public int Height { get; set; }
		public float[] Heights { get; set; }
	}

	public static string BuildKey( ThornsTerrainConfig config )
		=> $"{config.WorldSeed}_{config.WorldBuildVersion}_{config.TerrainResolution}";

	static string GetJsonPath( string key ) => $"{CacheFolder}/{key}.json";
	static string GetBinaryPath( string key ) => $"{CacheFolder}/{key}.thgt";

	public static bool TryLoad( ThornsTerrainConfig config, out HeightmapField field )
	{
		field = null;
		var key = BuildKey( config );
		if ( TryLoadBinary( GetBinaryPath( key ), out field ) )
		{
			Log.Info( $"[Thorns Terrain] Loaded height cache {field.Width}x{field.Height} from {GetBinaryPath( key )}" );
			return true;
		}

		var jsonPath = GetJsonPath( key );
		if ( !FileSystem.Data.FileExists( jsonPath ) )
			return false;

		try
		{
			var dto = FileSystem.Data.ReadJson<ThornsHeightCacheDto>( jsonPath );
			if ( dto?.Heights is null || dto.Width <= 0 || dto.Height <= 0 )
				return false;

			if ( dto.Heights.Length != dto.Width * dto.Height )
				return false;

			field = new HeightmapField( dto.Width, dto.Height, dto.Heights );
			Log.Info( $"[Thorns Terrain] Loaded height cache {dto.Width}x{dto.Height} from {jsonPath}" );
			Save( config, field );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Thorns Terrain] Height cache load failed: {ex.Message}" );
			return false;
		}
	}

	public static void Save( ThornsTerrainConfig config, HeightmapField field )
	{
		if ( field is null )
			return;

		var key = BuildKey( config );
		var binPath = GetBinaryPath( key );
		try
		{
			FileSystem.Data.WriteAllBytes( binPath, EncodeBinary( field ) );
			Log.Info( $"[Thorns Terrain] Saved height cache {field.Width}x{field.Height} to {binPath}" );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Thorns Terrain] Height cache binary save failed: {ex.Message}" );
		}
	}

	static byte[] EncodeBinary( HeightmapField field )
	{
		var count = field.Width * field.Height;
		var bytes = new byte[12 + count * 4];
		BinaryPrimitives.WriteUInt32LittleEndian( bytes.AsSpan( 0 ), BinaryMagic );
		BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4 ), field.Width );
		BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 8 ), field.Height );

		for ( var i = 0; i < count; i++ )
			BinaryPrimitives.WriteSingleLittleEndian( bytes.AsSpan( 12 + i * 4 ), field.Heights[i] );

		return bytes;
	}

	static bool TryLoadBinary( string path, out HeightmapField field )
	{
		field = null;
		if ( !FileSystem.Data.FileExists( path ) )
			return false;

		try
		{
			var bytes = FileSystem.Data.ReadAllBytes( path );
			ReadOnlySpan<byte> span = bytes;
			if ( span.Length < 12 )
				return false;

			if ( BinaryPrimitives.ReadUInt32LittleEndian( span ) != BinaryMagic )
				return false;

			var width = BinaryPrimitives.ReadInt32LittleEndian( span.Slice( 4 ) );
			var height = BinaryPrimitives.ReadInt32LittleEndian( span.Slice( 8 ) );
			if ( width <= 0 || height <= 0 )
				return false;

			var count = width * height;
			if ( span.Length < 12 + count * 4 )
				return false;

			var heights = new float[count];
			for ( var i = 0; i < count; i++ )
				heights[i] = BinaryPrimitives.ReadSingleLittleEndian( span.Slice( 12 + i * 4 ) );

			field = new HeightmapField( width, height, heights );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Thorns Terrain] Height cache binary load failed: {ex.Message}" );
			return false;
		}
	}
}
