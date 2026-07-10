using System;

namespace Sandbox;

/// <summary>Comma-packed parallel goal progress (host writes, all clients read).</summary>
public static class ThornsMilestoneProgressCodec
{
	public const string Prefix = "v1:";

	public static string Serialize( int[] progress )
	{
		if ( progress is null || progress.Length == 0 )
			return Prefix;

		return Prefix + string.Join( ",", progress );
	}

	public static int[] ParsePacked( string packed, int expectedLength )
	{
		var arr = new int[Math.Max( 0, expectedLength )];
		if ( expectedLength <= 0 )
			return arr;

		if ( string.IsNullOrWhiteSpace( packed ) || !packed.StartsWith( Prefix, StringComparison.Ordinal ) )
			return arr;

		var body = packed[Prefix.Length..];
		if ( string.IsNullOrWhiteSpace( body ) )
			return arr;

		var parts = body.Split( ',' );
		for ( var i = 0; i < Math.Min( parts.Length, expectedLength ); i++ )
		{
			if ( int.TryParse( parts[i].Trim(), out var v ) )
				arr[i] = Math.Max( 0, v );
		}

		return arr;
	}
}
