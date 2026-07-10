namespace Sandbox;

/// <summary>One storey of a tile blueprint (row 0 = south / min Y).</summary>
public sealed class ThornsProcTileLayer
{
	public int Width { get; }
	public int Depth { get; }
	public ThornsProcTileCell[] Cells { get; }

	public ThornsProcTileLayer( int width, int depth, ThornsProcTileCell[] cells )
	{
		Width = width;
		Depth = depth;
		Cells = cells ?? throw new ArgumentNullException( nameof( cells ) );
		if ( cells.Length != width * depth )
			throw new ArgumentException( "Cell count must equal width * depth." );
	}

	public ref ThornsProcTileCell Cell( int x, int y ) => ref Cells[y * Width + x];
}
