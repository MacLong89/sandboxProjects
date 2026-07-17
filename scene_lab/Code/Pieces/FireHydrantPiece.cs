namespace SceneLab;

/// <summary>Low-poly fire hydrant for sidewalk / curb dressing. Modular export piece.</summary>
public static class FireHydrantPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw = 0f, float scale = 1f )
	{
		var root = new GameObject( parent, true, PieceIds.FireHydrant );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var s = scale;
		var bodyH = 28f * s;
		var bodyW = 14f * s;

		KitBox.Box( root, "Base",
			new Vector3( 0f, 0f, Depth.CenterLift( 4f * s ) ),
			new Vector3( bodyW * 1.25f, bodyW * 1.25f, 4f * s ),
			Palette.HydrantRed );

		KitBox.Box( root, "Body",
			new Vector3( 0f, 0f, 4f * s + Depth.CenterLift( bodyH ) ),
			new Vector3( bodyW, bodyW, bodyH ),
			Palette.HydrantRed );

		KitBox.Box( root, "Cap",
			new Vector3( 0f, 0f, 4f * s + bodyH + Depth.Step + Depth.CenterLift( 6f * s ) ),
			new Vector3( bodyW * 0.85f, bodyW * 0.85f, 6f * s ),
			Palette.HydrantCap );

		KitBox.Box( root, "Nozzle",
			new Vector3( bodyW * 0.65f, 0f, 4f * s + bodyH * 0.55f ),
			new Vector3( 10f * s, 8f * s, 8f * s ),
			Palette.HydrantCap );

		return root;
	}
}
