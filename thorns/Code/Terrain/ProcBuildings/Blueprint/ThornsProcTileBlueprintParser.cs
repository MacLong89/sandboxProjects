using System.Linq;

namespace Sandbox;

/// <summary>Parses ASCII blueprint rows into <see cref="ThornsProcTileLayer"/> grids.</summary>
public static class ThornsProcTileBlueprintParser
{
	public static ThornsProcTileLayer ParseLayer( params string[] rows )
	{
		if ( rows is null || rows.Length == 0 )
			throw new ArgumentException( "Blueprint layer requires at least one row." );

		// Blueprints are authored north → south (top row first); grid y=0 is south / doors.
		rows = rows.Reverse().ToArray();

		var depth = rows.Length;
		var width = 0;
		var tokens = new List<string>[depth];
		for ( var y = 0; y < depth; y++ )
		{
			tokens[y] = TokenizeRow( rows[y] );
			width = Math.Max( width, tokens[y].Count );
		}

		if ( width < 1 )
			throw new ArgumentException( "Blueprint layer has no cells." );

		var cells = new ThornsProcTileCell[width * depth];
		for ( var y = 0; y < depth; y++ )
		for ( var x = 0; x < width; x++ )
		{
			var tok = x < tokens[y].Count ? tokens[y][x].Trim() : ".";
			cells[y * width + x] = ParseToken( tok );
		}

		return new ThornsProcTileLayer( width, depth, cells );
	}

	static List<string> TokenizeRow( string row )
	{
		var list = new List<string>( 32 );
		if ( string.IsNullOrWhiteSpace( row ) )
			return list;

		var i = 0;
		while ( i < row.Length )
		{
			if ( char.IsWhiteSpace( row[i] ) )
			{
				i++;
				continue;
			}

			if ( row[i] == '[' )
			{
				var end = row.IndexOf( ']', i + 1 );
				if ( end < 0 )
					throw new FormatException( $"Unclosed '[' in blueprint row: {row}" );

				list.Add( row.Substring( i + 1, end - i - 1 ) );
				i = end + 1;
				continue;
			}

			var start = i;
			while ( i < row.Length && !char.IsWhiteSpace( row[i] ) && row[i] != '[' )
				i++;

			list.Add( row.Substring( start, i - start ) );
		}

		return list;
	}

	static ThornsProcTileCell ParseToken( string token )
	{
		var cell = ThornsProcTileCell.Empty;
		token = token?.Trim() ?? "";
		if ( token.Length == 0 || token == "." )
			return cell;

		var parts = token.Split( ':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		foreach ( var part in parts )
			ApplyPart( ref cell, part );

		if ( cell.Ramp != ThornsProcRampDirection.None )
			cell.Floor = true;

		return cell;
	}

	static void ApplyPart( ref ThornsProcTileCell cell, string part )
	{
		if ( part.Length == 0 )
			return;

		if ( part.Equals( "F", StringComparison.OrdinalIgnoreCase ) )
		{
			cell.Floor = true;
			return;
		}

		if ( part.Equals( "O", StringComparison.OrdinalIgnoreCase ) )
		{
			cell.Opening = true;
			return;
		}

		if ( part.Length == 2 && char.IsDigit( part[1] ) && part[0] is 'F' or 'f' )
		{
			cell.Floor = true;
			cell.RoomId = part[1] - '0';
			return;
		}

		if ( TryParseRamp( part, out var ramp ) )
		{
			cell.Ramp = ramp;
			return;
		}

		if ( TryParseEdge( part, "Door", out var door ) )
		{
			ApplyDoor( ref cell, door );
			cell.Floor = true;
			return;
		}

		if ( TryParseEdge( part, "Window", out var win ) )
		{
			ApplyWindow( ref cell, win );
			cell.Floor = true;
			return;
		}

		throw new FormatException( $"Unknown blueprint token part '{part}'." );
	}

	static bool TryParseRamp( string part, out ThornsProcRampDirection ramp )
	{
		ramp = part.ToUpperInvariant() switch
		{
			"R_N" => ThornsProcRampDirection.North,
			"R_S" => ThornsProcRampDirection.South,
			"R_E" => ThornsProcRampDirection.East,
			"R_W" => ThornsProcRampDirection.West,
			_ => ThornsProcRampDirection.None
		};

		return ramp != ThornsProcRampDirection.None;
	}

	static bool TryParseEdge( string part, string prefix, out char edge )
	{
		edge = '\0';
		if ( !part.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var suffix = part.Substring( prefix.Length );
		if ( suffix.Length == 0 || suffix[0] != '_' )
			return false;

		edge = suffix[1];
		return edge is 'N' or 'S' or 'E' or 'W';
	}

	static void ApplyDoor( ref ThornsProcTileCell cell, char edge )
	{
		switch ( edge )
		{
			case 'N': cell.DoorNorth = true; break;
			case 'S': cell.DoorSouth = true; break;
			case 'E': cell.DoorEast = true; break;
			case 'W': cell.DoorWest = true; break;
		}
	}

	static void ApplyWindow( ref ThornsProcTileCell cell, char edge )
	{
		switch ( edge )
		{
			case 'N': cell.WindowNorth = true; break;
			case 'S': cell.WindowSouth = true; break;
			case 'E': cell.WindowEast = true; break;
			case 'W': cell.WindowWest = true; break;
		}
	}
}
