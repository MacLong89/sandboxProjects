namespace SceneLab;

/// <summary>Large flat ground under the set so the walk controller has something to stand on.</summary>
public static class GroundPadPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, Vector3 size )
	{
		var root = new GameObject( parent, true, PieceIds.GroundPad );
		root.LocalPosition = worldPos;

		var h = MathF.Max( size.z, 8f );
		// Top of pad at local Z = 0 (ground plane for other pieces).
		KitBox.Box( root, "Pad",
			new Vector3( 0f, 0f, -h * 0.5f ),
			size.WithZ( h ),
			Palette.GroundFill );

		var col = root.Components.Create<BoxCollider>();
		col.Scale = size.WithZ( h );
		col.Center = new Vector3( 0f, 0f, -h * 0.5f );

		return root;
	}
}
