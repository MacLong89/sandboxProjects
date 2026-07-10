namespace UnderPressure;

/// <summary>One line of spray-painted text stamped onto a cleanable grime mask.</summary>
public sealed class GraffitiLine
{
	public string Text { get; init; } = "";

	/// <summary>0..1 across the panel width (0 = left, 1 = right).</summary>
	public float X { get; init; } = 0.5f;

	/// <summary>0..1 up the panel height (0 = bottom, 1 = top).</summary>
	public float Y { get; init; } = 0.5f;

	public float Scale { get; init; } = 1f;
	public Color Color { get; init; } = Color.White;

	/// <summary>When true, <see cref="X"/> is the horizontal center of the line.</summary>
	public bool Centered { get; init; } = true;

	/// <summary>Stable key for save tracking when this graffiti is uncovered.</summary>
	public string DiscoveryId { get; init; }

	/// <summary>Leo's inner monologue when this graffiti is uncovered.</summary>
	public string Monologue { get; init; }

	/// <summary>Fraction of the text bounding box that must be cleaned before the popup fires.</summary>
	public float RevealThreshold { get; init; } = 0.55f;
}

/// <summary>Stamps bitmap lettering onto an underlay mask revealed as grime above is cleaned away.</summary>
public static class GraffitiRaster
{
	private const int GlyphW = 5;
	private const int GlyphH = 7;
	private const int GlyphSpacing = 1;

	public static void Apply( Color32[] pixels, int texW, int texH, IReadOnlyList<GraffitiLine> lines )
	{
		if ( pixels is null || lines is null || lines.Count == 0 )
			return;

		foreach ( var line in lines )
			StampTextLine( pixels, texW, texH, line );
	}

	/// <summary>Stamp one line of bitmap text (shared with <see cref="SecretRaster"/>).</summary>
	public static void StampTextLine( Color32[] pixels, int texW, int texH, GraffitiLine line ) =>
		StampLine( pixels, texW, texH, line );

	private static void StampLine( Color32[] pixels, int texW, int texH, GraffitiLine line )
	{
		if ( string.IsNullOrWhiteSpace( line.Text ) )
			return;

		var scale = Math.Max( 1, (int)MathF.Round( line.Scale ) );
		var charStep = (GlyphW + GlyphSpacing) * scale;
		var textW = line.Text.Length * charStep - GlyphSpacing * scale;
		var startX = line.Centered
			? (int)MathF.Round( line.X * texW - textW * 0.5f )
			: (int)MathF.Round( line.X * texW );
		var baselineY = (int)MathF.Round( line.Y * texH );

		for ( var i = 0; i < line.Text.Length; i++ )
		{
			var ch = line.Text[i];
			if ( ch == ' ' )
				continue;

			StampGlyph( pixels, texW, texH, ch, startX + i * charStep, baselineY, scale, line.Color );
		}
	}

	private static void StampGlyph( Color32[] pixels, int texW, int texH, char ch, int x, int y, int scale, Color color )
	{
		var rows = GlyphRows( ch );
		if ( rows is null )
			return;

		for ( var row = 0; row < GlyphH; row++ )
		{
			var bits = rows[row];
			// Bitmap row 0 is the top of the glyph; increasing texel Y maps to panel-up.
			var pixelRow = y + (GlyphH - 1 - row) * scale;
			for ( var col = 0; col < GlyphW; col++ )
			{
				if ( (bits & (1 << (GlyphW - 1 - col))) == 0 )
					continue;

				FillBlock( pixels, texW, texH, x + col * scale, pixelRow, scale, scale, color );
			}
		}
	}

	private static void FillBlock( Color32[] pixels, int texW, int texH, int x, int y, int w, int h, Color color )
	{
		var shade = 0.88f + Game.Random.Float( 0f, 0.12f );
		var c = new Color32(
			(byte)Math.Clamp( (int)(color.r * shade * 255f), 0, 255 ),
			(byte)Math.Clamp( (int)(color.g * shade * 255f), 0, 255 ),
			(byte)Math.Clamp( (int)(color.b * shade * 255f), 0, 255 ),
			255 );

		for ( var py = y; py < y + h; py++ )
		{
			if ( py < 0 || py >= texH )
				continue;

			for ( var px = x; px < x + w; px++ )
			{
				if ( px < 0 || px >= texW )
					continue;

				pixels[py * texW + px] = c;
			}
		}
	}

	/// <summary>5-bit rows, MSB = left column.</summary>
	private static int[] GlyphRows( char ch )
	{
		return char.ToUpperInvariant( ch ) switch
		{
			'A' => Rows( "01110", "10001", "10001", "11111", "10001", "10001", "10001" ),
			'B' => Rows( "11110", "10001", "10001", "11110", "10001", "10001", "11110" ),
			'C' => Rows( "01111", "10000", "10000", "10000", "10000", "10000", "01111" ),
			'D' => Rows( "11110", "10001", "10001", "10001", "10001", "10001", "11110" ),
			'E' => Rows( "11111", "10000", "10000", "11110", "10000", "10000", "11111" ),
			'F' => Rows( "11111", "10000", "10000", "11110", "10000", "10000", "10000" ),
			'G' => Rows( "01111", "10000", "10000", "10111", "10001", "10001", "01111" ),
			'H' => Rows( "10001", "10001", "10001", "11111", "10001", "10001", "10001" ),
			'I' => Rows( "11111", "00100", "00100", "00100", "00100", "00100", "11111" ),
			'J' => Rows( "00111", "00010", "00010", "00010", "00010", "10010", "01100" ),
			'K' => Rows( "10001", "10010", "10100", "11000", "10100", "10010", "10001" ),
			'L' => Rows( "10000", "10000", "10000", "10000", "10000", "10000", "11111" ),
			'M' => Rows( "10001", "11011", "10101", "10001", "10001", "10001", "10001" ),
			'N' => Rows( "10001", "11001", "10101", "10011", "10001", "10001", "10001" ),
			'O' => Rows( "01110", "10001", "10001", "10001", "10001", "10001", "01110" ),
			'P' => Rows( "11110", "10001", "10001", "11110", "10000", "10000", "10000" ),
			'Q' => Rows( "01110", "10001", "10001", "10001", "10101", "10010", "01101" ),
			'R' => Rows( "11110", "10001", "10001", "11110", "10100", "10010", "10001" ),
			'S' => Rows( "01111", "10000", "10000", "01110", "00001", "00001", "11110" ),
			'T' => Rows( "11111", "00100", "00100", "00100", "00100", "00100", "00100" ),
			'U' => Rows( "10001", "10001", "10001", "10001", "10001", "10001", "01110" ),
			'V' => Rows( "10001", "10001", "10001", "10001", "10001", "01010", "00100" ),
			'W' => Rows( "10001", "10001", "10001", "10001", "10101", "11011", "10001" ),
			'X' => Rows( "10001", "10001", "01010", "00100", "01010", "10001", "10001" ),
			'Y' => Rows( "10001", "10001", "01010", "00100", "00100", "00100", "00100" ),
			'Z' => Rows( "11111", "00001", "00010", "00100", "01000", "10000", "11111" ),
			'0' => Rows( "01110", "10001", "10011", "10101", "11001", "10001", "01110" ),
			'1' => Rows( "00100", "01100", "00100", "00100", "00100", "00100", "01110" ),
			'2' => Rows( "01110", "10001", "00001", "00110", "01000", "10000", "11111" ),
			'3' => Rows( "01110", "10001", "00001", "00110", "00001", "10001", "01110" ),
			'4' => Rows( "00010", "00110", "01010", "10010", "11111", "00010", "00010" ),
			'5' => Rows( "11111", "10000", "11110", "00001", "00001", "10001", "01110" ),
			'6' => Rows( "01110", "10000", "10000", "11110", "10001", "10001", "01110" ),
			'7' => Rows( "11111", "00001", "00010", "00100", "01000", "01000", "01000" ),
			'8' => Rows( "01110", "10001", "10001", "01110", "10001", "10001", "01110" ),
			'9' => Rows( "01110", "10001", "10001", "01111", "00001", "00001", "01110" ),
			'.' => Rows( "00000", "00000", "00000", "00000", "00000", "00100", "00100" ),
			'/' => Rows( "00001", "00010", "00010", "00100", "01000", "01000", "10000" ),
			':' => Rows( "00000", "00100", "00100", "00000", "00100", "00100", "00000" ),
			'-' => Rows( "00000", "00000", "00000", "11111", "00000", "00000", "00000" ),
			'!' => Rows( "00100", "00100", "00100", "00100", "00100", "00000", "00100" ),
			'=' => Rows( "00000", "00000", "11111", "00000", "11111", "00000", "00000" ),
			'?' => Rows( "01110", "10001", "00001", "00110", "00100", "00000", "00100" ),
			_ => null,
		};
	}

	private static int[] Rows( params string[] lines )
	{
		var rows = new int[lines.Length];
		for ( var i = 0; i < lines.Length; i++ )
		{
			var n = 0;
			foreach ( var c in lines[i] )
				n = (n << 1) | (c == '1' ? 1 : 0);
			rows[i] = n;
		}

		return rows;
	}
}
