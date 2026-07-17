namespace SceneLab;

/// <summary>
/// Asphalt driveway. <paramref name="worldPos"/>.z is the driving-surface height.
/// Local +X runs toward the house.
/// </summary>
public static class DrivewayPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw, float length, float width = 72f )
	{
		var root = new GameObject( parent, true, PieceIds.Driveway );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		const float thick = 5f;
		// Top of pad ≈ local Z 0 (same as root surface), body hangs below — no fight with higher yard
		KitBox.Box( root, "Pad",
			new Vector3( length * 0.5f, 0f, -thick * 0.5f + Depth.Sit ),
			new Vector3( length, width, thick ),
			Palette.Asphalt );

		return root;
	}
}
