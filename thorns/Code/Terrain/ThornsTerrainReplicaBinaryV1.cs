#nullable disable

using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Sandbox;

/// <summary>
/// Compact v1 binary terrain replica (join payload) — replaces monolithic JSON for <see cref="ThornsTerrainChunk"/>.
/// <b>Legacy:</b> if payload empty, fall back to <see cref="ThornsTerrainNetSpec.Deserialize"/> from JSON.
/// </summary>
public static class ThornsTerrainReplicaBinaryV1
{
	public const int FormatVersion = 8;

	const int MaxRoadCorridors = 512;
	const int MaxSettlementInfluences = 8;
	const int MaxSettlementBlocks = 48;

	const int MagicSize = 8;
	static readonly byte[] MagicBytes = { (byte)'T', (byte)'H', (byte)'R', (byte)'N', (byte)'S', (byte)'P', (byte)'0', (byte)'1' };

	const int MaxPads = 8192;
	const int MaxStringBytes = 1024;

	public static ulong Fnv1a64( ReadOnlySpan<byte> data )
	{
		const ulong offset = 14695981039346656037UL;
		const ulong prime = 1099511628211UL;
		var h = offset;
		for ( var i = 0; i < data.Length; i++ )
		{
			h ^= data[i];
			h *= prime;
		}

		return h;
	}

	public static string EncodeToBase64( ThornsTerrainNetSpec spec )
	{
		var bytes = Encode( spec );
		return Convert.ToBase64String( bytes );
	}

	public static byte[] Encode( ThornsTerrainNetSpec spec )
	{
		if ( spec is null )
			spec = new ThornsTerrainNetSpec();

		ThornsTerrainDecorScatter.EnsureDecorNetDefaults( spec );
		spec.ProcBuildingTerrainPads ??= new List<ThornsTerrainProcBuildingPad>();

		var est = 512 + spec.ProcBuildingTerrainPads.Count * 40 + ( spec.RoadCorridors?.Count ?? 0 ) * 24 + 512;
		var buf = new byte[Math.Min( 8_000_000, Math.Max( 4096, est ) )];
		var o = 0;

		void Need( int n )
		{
			if ( o + n > buf.Length )
				throw new InvalidOperationException( "ThornsTerrainReplicaBinaryV1: buffer too small" );
		}

		Need( MagicSize );
		MagicBytes.AsSpan().CopyTo( buf.AsSpan( o ) );
		o += MagicSize;

		Need( 4 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), FormatVersion );
		o += 4;

		uint flags = 0;
		if ( spec.EnableSmoothing )
			flags |= 1u << 0;
		if ( spec.CenterOnWorldOrigin )
			flags |= 1u << 1;
		if ( spec.EnableCoastalEdgeFalloff )
			flags |= 1u << 2;
		if ( spec.DecorGenerateFoliageFluff )
			flags |= 1u << 3;
		if ( spec.DecorEnableFoliageDistanceCulling )
			flags |= 1u << 4;
		if ( spec.EnableSeaLevelWaterSheet )
			flags |= 1u << 5;

		var g = spec.DecorGrass ?? ThornsTerrainDecorGrassNet.EngineDefaults();
		var m = spec.DecorMushroom ?? new ThornsTerrainDecorMushroomNet();
		if ( g.ScatterGrassFoliage )
			flags |= 1u << 6;
		if ( m.ScatterMushroomFoliage )
			flags |= 1u << 7;
		if ( spec.UseTerraingenWorld )
			flags |= 1u << 8;
		if ( spec.UseTerraingenFoliage )
			flags |= 1u << 9;

		Need( 4 * 3 + 4 * 6 + 4 * 2 + 4 + 4 * 5 + 4 + 4 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), spec.Seed );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), spec.HeightmapResolutionX );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), spec.HeightmapResolutionZ );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.WorldWidth );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.WorldDepth );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.NoiseScale );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.HeightMultiplier );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.TerrainNoisePersistence );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.TerrainNoiseLacunarity );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.TerrainHeightContrast );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), spec.TerrainNoiseOctaves );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), spec.SmoothingPasses );
		o += 4;
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( o ), flags );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.WaterLevelWorldZ );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.WaterSurfaceUvRepeat );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.CoastalInteriorLandFraction );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.CoastalDepthBelowSeaLevelZ );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), spec.DecorEdgeInsetFraction );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), spec.TerraingenWorldSeed );
		o += 4;

		o = WriteUtf8WithUInt16Len( buf, o, spec.MaterialPath ?? "" );
		o = WriteUtf8WithUInt16Len( buf, o, spec.WaterMaterialPath ?? "" );

		var padCount = spec.ProcBuildingTerrainPads.Count;
		if ( padCount > MaxPads )
			padCount = MaxPads;

		Need( 4 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), padCount );
		o += 4;

		for ( var i = 0; i < padCount; i++ )
		{
			var p = spec.ProcBuildingTerrainPads[i];
			Need( 4 * 15 + 1 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.CenterX );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.CenterY );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.HalfW );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.HalfD );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.YawRadians );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.TargetZ );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.Apron );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.PeakBlend );
			o += 4;
			buf[o++] = (byte)Math.Clamp( (int)p.Kind, 0, 255 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.FoundationEmbed );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.WallApron );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.FoundationHalfW );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.FoundationHalfD );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.DoorOutwardX );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.DoorOutwardY );
			o += 4;
			BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), p.BlockIndex );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), p.ApronStrengthMul );
			o += 4;
		}

		Need( 4 * 6 + 4 * 5 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), g.ScatterGrassPatchCount );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), g.ScatterGrassPerPatchMin );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), g.ScatterGrassPerPatchMax );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), g.ScatterGrassVariantCount );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), g.ScatterGrassDebugSampleCount );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), g.ScatterGrassPatchRadiusMin );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), g.ScatterGrassPatchRadiusMax );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), g.ScatterGrassUniformScaleMin );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), g.ScatterGrassUniformScaleMax );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), g.ScatterGrassGroundOffset );
		o += 4;
		o = WriteUtf8WithUInt16Len( buf, o, g.ScatterGrassModelPathPrefix ?? "" );

		Need( 4 * 5 + 4 * 5 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), m.ScatterMushroomClusterCount );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), m.ScatterMushroomsPerClusterMin );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), m.ScatterMushroomsPerClusterMax );
		o += 4;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), m.ScatterMushroomDebugSampleCount );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), m.ScatterMushroomClusterRadiusMin );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), m.ScatterMushroomClusterRadiusMax );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), m.ScatterMushroomUniformScaleMin );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), m.ScatterMushroomUniformScaleMax );
		o += 4;
		BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), m.ScatterMushroomGroundOffset );
		o += 4;
		o = WriteUtf8WithUInt16Len( buf, o, m.ScatterMushroomModelPath ?? "" );

		spec.RoadCorridors ??= new List<ThornsWorldRoadCorridor>();
		var roadCount = spec.RoadCorridors.Count;
		if ( roadCount > MaxRoadCorridors )
			roadCount = MaxRoadCorridors;

		Need( 4 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), roadCount );
		o += 4;

		for ( var i = 0; i < roadCount; i++ )
		{
			var r = spec.RoadCorridors[i];
			Need( 4 * 5 + 1 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.A.x );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.A.y );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.B.x );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.B.y );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.HalfWidth );
			o += 4;
			buf[o++] = (byte)Math.Clamp( (int)r.Kind, 0, 255 );
		}

		spec.SettlementTerrainInfluences ??= new List<ThornsSettlementTerrainInfluenceNet>();
		var infCount = spec.SettlementTerrainInfluences.Count;
		if ( infCount > MaxSettlementInfluences )
			infCount = MaxSettlementInfluences;

		Need( 4 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), infCount );
		o += 4;

		for ( var i = 0; i < infCount; i++ )
		{
			var inf = spec.SettlementTerrainInfluences[i];
			Need( 4 * 7 + 1 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.CenterX );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.CenterY );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.HubRadius );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.CoreRadius );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.TransitionRadius );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.OuterFeatherRadius );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), inf.TargetZ );
			o += 4;
			buf[o++] = (byte)Math.Clamp( (int)inf.Kind, 0, 255 );
		}

		spec.SettlementBlockTerrain ??= new List<ThornsSettlementBlockTerrainNet>();
		var blockCount = spec.SettlementBlockTerrain.Count;
		if ( blockCount > MaxSettlementBlocks )
			blockCount = MaxSettlementBlocks;

		Need( 4 );
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), blockCount );
		o += 4;

		for ( var i = 0; i < blockCount; i++ )
		{
			var blk = spec.SettlementBlockTerrain[i];
			Need( 4 * 8 + 2 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.CenterX );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.CenterY );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.HalfW );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.HalfD );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.YawRadians );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.TargetZ );
			o += 4;
			buf[o++] = (byte)Math.Clamp( (int)blk.Kind, 0, 255 );
			BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), blk.BlockIndex );
			o += 4;
			buf[o++] = (byte)Math.Clamp( blk.BuildingCount, 0, 255 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), blk.SurfaceStrength );
			o += 4;
		}

		Array.Resize( ref buf, o );
		return buf;
	}

	static int WriteUtf8WithUInt16Len( byte[] buf, int o, string s )
	{
		var t = s ?? "";
		var enc = Encoding.UTF8.GetBytes( t );
		if ( enc.Length > MaxStringBytes )
			Array.Resize( ref enc, MaxStringBytes );

		if ( o + 2 + enc.Length > buf.Length )
			throw new InvalidOperationException( "ThornsTerrainReplicaBinaryV1: string overflow" );

		BinaryPrimitives.WriteUInt16LittleEndian( buf.AsSpan( o ), (ushort)enc.Length );
		o += 2;
		enc.CopyTo( buf.AsSpan( o ) );
		return o + enc.Length;
	}

	public static bool TryDecodeFromBase64( string b64, out ThornsTerrainNetSpec spec )
	{
		spec = null;
		if ( string.IsNullOrWhiteSpace( b64 ) )
			return false;

		byte[] bytes;
		try
		{
			bytes = Convert.FromBase64String( b64.Trim() );
		}
		catch
		{
			return false;
		}

		return TryDecode( bytes, out spec );
	}

	public static bool TryDecode( ReadOnlySpan<byte> data, out ThornsTerrainNetSpec spec )
	{
		spec = null;
		try
		{
			if ( data.Length < MagicSize + 4 )
				return false;

			if ( !data.Slice( 0, MagicSize ).SequenceEqual( MagicBytes ) )
				return false;

			var o = MagicSize;
			var ver = ReadI32( data, ref o );
			if ( ver < 1 || ver > FormatVersion )
				return false;

			spec = new ThornsTerrainNetSpec();
			spec.Seed = ReadI32( data, ref o );
			spec.HeightmapResolutionX = ReadI32( data, ref o );
			spec.HeightmapResolutionZ = ReadI32( data, ref o );
			spec.WorldWidth = ReadF32( data, ref o );
			spec.WorldDepth = ReadF32( data, ref o );
			spec.NoiseScale = ReadF32( data, ref o );
			spec.HeightMultiplier = ReadF32( data, ref o );
			spec.TerrainNoisePersistence = ReadF32( data, ref o );
			spec.TerrainNoiseLacunarity = ReadF32( data, ref o );
			spec.TerrainHeightContrast = ReadF32( data, ref o );
			spec.TerrainNoiseOctaves = ReadI32( data, ref o );
			spec.SmoothingPasses = ReadI32( data, ref o );
			var flags = ReadU32( data, ref o );

			spec.EnableSmoothing = (flags & (1u << 0)) != 0;
			spec.CenterOnWorldOrigin = (flags & (1u << 1)) != 0;
			spec.EnableCoastalEdgeFalloff = (flags & (1u << 2)) != 0;
			spec.DecorGenerateFoliageFluff = (flags & (1u << 3)) != 0;
			spec.DecorEnableFoliageDistanceCulling = (flags & (1u << 4)) != 0;
			spec.EnableSeaLevelWaterSheet = (flags & (1u << 5)) != 0;

			spec.WaterLevelWorldZ = ReadF32( data, ref o );
			spec.WaterSurfaceUvRepeat = ReadF32( data, ref o );
			spec.CoastalInteriorLandFraction = ReadF32( data, ref o );
			spec.CoastalDepthBelowSeaLevelZ = ReadF32( data, ref o );
			spec.DecorEdgeInsetFraction = ReadF32( data, ref o );

			if ( ver >= 8 )
			{
				spec.TerraingenWorldSeed = ReadI32( data, ref o );
				if ( spec.TerraingenWorldSeed == 0 )
					spec.TerraingenWorldSeed = spec.Seed;
			}

			spec.MaterialPath = ReadUtf8UInt16( data, ref o );
			spec.WaterMaterialPath = ReadUtf8UInt16( data, ref o );

			var padCount = ReadI32( data, ref o );
			if ( padCount < 0 || padCount > MaxPads )
				return false;

			spec.ProcBuildingTerrainPads = new List<ThornsTerrainProcBuildingPad>( padCount );
			for ( var i = 0; i < padCount; i++ )
			{
				var pad = new ThornsTerrainProcBuildingPad
				{
					CenterX = ReadF32( data, ref o ),
					CenterY = ReadF32( data, ref o ),
					HalfW = ReadF32( data, ref o ),
					HalfD = ReadF32( data, ref o ),
					YawRadians = ReadF32( data, ref o ),
					TargetZ = ReadF32( data, ref o ),
					Apron = ReadF32( data, ref o )
				};

				if ( ver >= 2 )
				{
					pad.PeakBlend = ReadF32( data, ref o );
					if ( o >= data.Length )
						return false;

					pad.Kind = (ThornsSettlementTerrainPadKind)data[o++];
				}

				if ( ver >= 6 )
				{
					if ( o + 24 > data.Length )
						return false;

					pad.FoundationEmbed = ReadF32( data, ref o );
					pad.WallApron = ReadF32( data, ref o );
					pad.FoundationHalfW = ReadF32( data, ref o );
					pad.FoundationHalfD = ReadF32( data, ref o );
					pad.DoorOutwardX = ReadF32( data, ref o );
					pad.DoorOutwardY = ReadF32( data, ref o );
				}

				if ( ver >= 7 )
				{
					if ( o + 8 > data.Length )
						return false;

					pad.BlockIndex = ReadI32( data, ref o );
					pad.ApronStrengthMul = ReadF32( data, ref o );
				}
				else if ( pad.Kind == ThornsSettlementTerrainPadKind.LocalBuilding )
				{
					pad.FoundationHalfW = pad.HalfW * 0.82f;
					pad.FoundationHalfD = pad.HalfD * 0.82f;
					pad.FoundationEmbed = ThornsBuildingFoundationTerrain.DefaultEmbedTown;
					pad.WallApron = ThornsWorldGenTerrainPadFactory.TownWallApronWorld;
					pad.ApronStrengthMul = 1f;
				}

				spec.ProcBuildingTerrainPads.Add( pad );
			}

			var g = new ThornsTerrainDecorGrassNet();
			g.ScatterGrassFoliage = (flags & (1u << 6)) != 0;
			g.ScatterGrassPatchCount = ReadI32( data, ref o );
			g.ScatterGrassPerPatchMin = ReadI32( data, ref o );
			g.ScatterGrassPerPatchMax = ReadI32( data, ref o );
			g.ScatterGrassVariantCount = ReadI32( data, ref o );
			g.ScatterGrassDebugSampleCount = ReadI32( data, ref o );
			g.ScatterGrassPatchRadiusMin = ReadF32( data, ref o );
			g.ScatterGrassPatchRadiusMax = ReadF32( data, ref o );
			g.ScatterGrassUniformScaleMin = ReadF32( data, ref o );
			g.ScatterGrassUniformScaleMax = ReadF32( data, ref o );
			g.ScatterGrassGroundOffset = ReadF32( data, ref o );
			g.ScatterGrassModelPathPrefix = ReadUtf8UInt16( data, ref o );
			spec.DecorGrass = g;

			var mush = new ThornsTerrainDecorMushroomNet();
			mush.ScatterMushroomFoliage = (flags & (1u << 7)) != 0;
			spec.UseTerraingenWorld = (flags & (1u << 8)) != 0;
			spec.UseTerraingenFoliage = (flags & (1u << 9)) != 0;
			mush.ScatterMushroomClusterCount = ReadI32( data, ref o );
			mush.ScatterMushroomsPerClusterMin = ReadI32( data, ref o );
			mush.ScatterMushroomsPerClusterMax = ReadI32( data, ref o );
			mush.ScatterMushroomDebugSampleCount = ReadI32( data, ref o );
			mush.ScatterMushroomClusterRadiusMin = ReadF32( data, ref o );
			mush.ScatterMushroomClusterRadiusMax = ReadF32( data, ref o );
			mush.ScatterMushroomUniformScaleMin = ReadF32( data, ref o );
			mush.ScatterMushroomUniformScaleMax = ReadF32( data, ref o );
			mush.ScatterMushroomGroundOffset = ReadF32( data, ref o );
			mush.ScatterMushroomModelPath = ReadUtf8UInt16( data, ref o );
			spec.DecorMushroom = mush;

			if ( ver >= 3 && o + 4 <= data.Length )
			{
				var roadCount = ReadI32( data, ref o );
				if ( roadCount < 0 || roadCount > MaxRoadCorridors )
					return false;

				spec.RoadCorridors = new List<ThornsWorldRoadCorridor>( roadCount );
				for ( var i = 0; i < roadCount; i++ )
				{
					if ( o + 21 > data.Length )
						return false;

					spec.RoadCorridors.Add( new ThornsWorldRoadCorridor
					{
						A = new Vector2( ReadF32( data, ref o ), ReadF32( data, ref o ) ),
						B = new Vector2( ReadF32( data, ref o ), ReadF32( data, ref o ) ),
						HalfWidth = ReadF32( data, ref o ),
						Kind = (ThornsWorldRoadCorridorKind)data[o++]
					} );
				}
			}

			if ( ver >= 4 && o + 4 <= data.Length )
			{
				var infCount = ReadI32( data, ref o );
				if ( infCount < 0 || infCount > MaxSettlementInfluences )
					return false;

				spec.SettlementTerrainInfluences = new List<ThornsSettlementTerrainInfluenceNet>( infCount );
				for ( var i = 0; i < infCount; i++ )
				{
					if ( o + 29 > data.Length )
						return false;

					spec.SettlementTerrainInfluences.Add( new ThornsSettlementTerrainInfluenceNet
					{
						CenterX = ReadF32( data, ref o ),
						CenterY = ReadF32( data, ref o ),
						HubRadius = ReadF32( data, ref o ),
						CoreRadius = ReadF32( data, ref o ),
						TransitionRadius = ReadF32( data, ref o ),
						OuterFeatherRadius = ReadF32( data, ref o ),
						TargetZ = ReadF32( data, ref o ),
						Kind = (ThornsWorldSettlementKind)data[o++]
					} );
				}
			}

			if ( ver >= 5 && o + 4 <= data.Length )
			{
				var blockCount = ReadI32( data, ref o );
				if ( blockCount < 0 || blockCount > MaxSettlementBlocks )
					return false;

				spec.SettlementBlockTerrain = new List<ThornsSettlementBlockTerrainNet>( blockCount );
				for ( var i = 0; i < blockCount; i++ )
				{
					if ( o + 25 > data.Length )
						return false;

					var blk = new ThornsSettlementBlockTerrainNet
					{
						CenterX = ReadF32( data, ref o ),
						CenterY = ReadF32( data, ref o ),
						HalfW = ReadF32( data, ref o ),
						HalfD = ReadF32( data, ref o ),
						YawRadians = ReadF32( data, ref o ),
						TargetZ = ReadF32( data, ref o ),
						Kind = (ThornsWorldSettlementKind)data[o++]
					};

					if ( ver >= 7 )
					{
						if ( o + 9 > data.Length )
							return false;

						blk.BlockIndex = ReadI32( data, ref o );
						blk.BuildingCount = data[o++];
						blk.SurfaceStrength = ReadF32( data, ref o );
					}
					else
					{
						blk.SurfaceStrength = ThornsWorldSettlementBlockTerrain.SparseBlockSurfaceStrength;
					}

					spec.SettlementBlockTerrain.Add( blk );
				}
			}

			if ( o != data.Length )
			{
				spec = null;
				return false;
			}

			ThornsTerrainNetSpec.FixLegacyTerrainNoiseFields( spec );
			ThornsTerrainDecorScatter.EnsureDecorNetDefaults( spec );
			return true;
		}
		catch
		{
			spec = null;
			return false;
		}
	}

	static int ReadI32( ReadOnlySpan<byte> d, ref int o )
	{
		if ( o + 4 > d.Length )
			throw new EndOfStreamException();
		var v = BinaryPrimitives.ReadInt32LittleEndian( d.Slice( o, 4 ) );
		o += 4;
		return v;
	}

	static uint ReadU32( ReadOnlySpan<byte> d, ref int o )
	{
		if ( o + 4 > d.Length )
			throw new EndOfStreamException();
		var v = BinaryPrimitives.ReadUInt32LittleEndian( d.Slice( o, 4 ) );
		o += 4;
		return v;
	}

	static float ReadF32( ReadOnlySpan<byte> d, ref int o )
	{
		if ( o + 4 > d.Length )
			throw new EndOfStreamException();
		var v = BinaryPrimitives.ReadSingleLittleEndian( d.Slice( o, 4 ) );
		o += 4;
		return v;
	}

	static string ReadUtf8UInt16( ReadOnlySpan<byte> d, ref int o )
	{
		if ( o + 2 > d.Length )
			throw new EndOfStreamException();
		var len = BinaryPrimitives.ReadUInt16LittleEndian( d.Slice( o, 2 ) );
		o += 2;
		if ( len > MaxStringBytes || o + len > d.Length )
			throw new EndOfStreamException();
		var s = Encoding.UTF8.GetString( d.Slice( o, len ) );
		o += len;
		return s;
	}
}
