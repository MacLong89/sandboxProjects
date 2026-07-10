#nullable disable

using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Sandbox;

/// <summary>
/// Compact v1 POI replica — replaces monolithic <see cref="ThornsPoiAuthority.PoiDatasetJson"/> on the wire when <see cref="ThornsPoiAuthority.PoiDescriptorVersion"/> ≥ 1.
/// <b>Legacy:</b> empty payload + JSON array still supported for scene prefabs / tooling.
/// </summary>
public static class ThornsPoiReplicaBinaryV1
{
	public const int FormatVersion = 1;

	static readonly byte[] MagicBytes = { (byte)'T', (byte)'H', (byte)'R', (byte)'N', (byte)'P', (byte)'O', (byte)'I', (byte)'1' };

	const int MagicSize = 8;
	const int MaxRecords = 4096;
	const int MaxKeyBytes = 64;
	const int MaxLabelBytes = 256;

	public static ulong Fnv1a64( ReadOnlySpan<byte> data ) => ThornsTerrainReplicaBinaryV1.Fnv1a64( data );

	public static string EncodeRecordsToBase64( IReadOnlyList<ThornsPoiAuthority.PoiClientRecord> list )
	{
		var bytes = EncodeRecords( list );
		return Convert.ToBase64String( bytes );
	}

	public static byte[] EncodeRecords( IReadOnlyList<ThornsPoiAuthority.PoiClientRecord> list )
	{
		var n = list?.Count ?? 0;
		if ( n > MaxRecords )
			n = MaxRecords;

		var buf = new byte[4096 + n * 128];
		var o = 0;

		void Need( int k )
		{
			if ( o + k > buf.Length )
				throw new InvalidOperationException( "ThornsPoiReplicaBinaryV1: buffer too small" );
		}

		Need( MagicSize + 4 + 2 );
		MagicBytes.AsSpan().CopyTo( buf.AsSpan( o ) );
		o += MagicSize;
		BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( o ), FormatVersion );
		o += 4;
		BinaryPrimitives.WriteUInt16LittleEndian( buf.AsSpan( o ), (ushort)n );
		o += 2;

		for ( var i = 0; i < n; i++ )
		{
			var r = list[i];
			Guid gid;
			if ( !Guid.TryParse( r.Id, out gid ) )
				gid = Guid.NewGuid();

			var gb = gid.ToByteArray();
			Need( 16 );
			gb.AsSpan().CopyTo( buf.AsSpan( o ) );
			o += 16;

			var key = string.IsNullOrEmpty( r.Key ) ? "general" : r.Key.Trim();
			var kb = Encoding.UTF8.GetBytes( key );
			if ( kb.Length > MaxKeyBytes )
				Array.Resize( ref kb, MaxKeyBytes );
			Need( 1 + kb.Length );
			buf[o++] = (byte)kb.Length;
			kb.CopyTo( buf.AsSpan( o ) );
			o += kb.Length;

			var lab = string.IsNullOrEmpty( r.Label ) ? "POI" : r.Label.Trim();
			var lb = Encoding.UTF8.GetBytes( lab );
			if ( lb.Length > MaxLabelBytes )
				Array.Resize( ref lb, MaxLabelBytes );
			Need( 2 + lb.Length );
			BinaryPrimitives.WriteUInt16LittleEndian( buf.AsSpan( o ), (ushort)lb.Length );
			o += 2;
			lb.CopyTo( buf.AsSpan( o ) );
			o += lb.Length;

			Need( 4 + 4 + 4 + 4 );
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.X );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.Y );
			o += 4;
			BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( o ), r.Rgba );
			o += 4;
			BinaryPrimitives.WriteSingleLittleEndian( buf.AsSpan( o ), r.BlipDiameterPx <= 0.5f ? 9f : r.BlipDiameterPx );
			o += 4;
		}

		Array.Resize( ref buf, o );
		return buf;
	}

	public static bool TryDecodeFromBase64( string b64, out List<ThornsPoiAuthority.PoiClientRecord> list )
	{
		list = null;
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

		return TryDecode( bytes, out list );
	}

	public static bool TryDecode( ReadOnlySpan<byte> data, out List<ThornsPoiAuthority.PoiClientRecord> list )
	{
		list = null;
		try
		{
			if ( data.Length < MagicSize + 4 + 2 )
				return false;
			if ( !data.Slice( 0, MagicSize ).SequenceEqual( MagicBytes ) )
				return false;

			var o = MagicSize;
			var ver = ReadI32( data, ref o );
			if ( ver != FormatVersion )
				return false;

			var n = BinaryPrimitives.ReadUInt16LittleEndian( data.Slice( o, 2 ) );
			o += 2;
			if ( n > MaxRecords )
				return false;

			list = new List<ThornsPoiAuthority.PoiClientRecord>( n );
			for ( var i = 0; i < n; i++ )
			{
				var gid = ReadGuid( data, ref o );
				var kl = data[o++];
				if ( kl > MaxKeyBytes || o + kl > data.Length )
					return false;
				var key = Encoding.UTF8.GetString( data.Slice( o, kl ) );
				o += kl;

				if ( o + 2 > data.Length )
					return false;
				var ll = BinaryPrimitives.ReadUInt16LittleEndian( data.Slice( o, 2 ) );
				o += 2;
				if ( ll > MaxLabelBytes || o + ll > data.Length )
					return false;
				var label = Encoding.UTF8.GetString( data.Slice( o, ll ) );
				o += ll;

				var x = ReadF32( data, ref o );
				var y = ReadF32( data, ref o );
				var rgba = ReadU32( data, ref o );
				var blip = ReadF32( data, ref o );

				list.Add( new ThornsPoiAuthority.PoiClientRecord
				{
					Id = gid.ToString( "N" ),
					Key = key,
					Label = label,
					X = x,
					Y = y,
					Rgba = rgba,
					BlipDiameterPx = blip
				} );
			}

			if ( o != data.Length )
			{
				list = null;
				return false;
			}

			return true;
		}
		catch
		{
			list = null;
			return false;
		}
	}

	static Guid ReadGuid( ReadOnlySpan<byte> d, ref int o )
	{
		if ( o + 16 > d.Length )
			throw new EndOfStreamException();
		var tmp = new byte[16];
		d.Slice( o, 16 ).CopyTo( tmp );
		o += 16;
		return new Guid( tmp );
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
}
