namespace SceneLab;

/// <summary>Low-poly side chair. Origin at ground under seat center. Local +X = seat forward.</summary>
public static class ChairPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw = 0f, float scale = 1f )
	{
		var root = new GameObject( parent, true, PieceIds.Chair );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var s = scale;
		var seatW = 42f * s;
		var seatD = 40f * s;
		var seatH = 4f * s;
		var seatZ = 40f * s;
		var legT = 4f * s;
		var backH = 40f * s;

		KitBox.Box( root, "Seat",
			new Vector3( 0f, 0f, seatZ + Depth.CenterLift( seatH ) ),
			new Vector3( seatD, seatW, seatH ),
			Palette.Wood );

		KitBox.Box( root, "Cushion",
			new Vector3( 0f, 0f, seatZ + seatH + Depth.Step + Depth.CenterLift( 3f * s ) ),
			new Vector3( seatD * 0.92f, seatW * 0.92f, 3f * s ),
			Palette.Cushion );

		// Four legs
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "Leg",
				new Vector3( sx * (seatD * 0.4f), sy * (seatW * 0.4f), Depth.CenterLift( seatZ ) ),
				new Vector3( legT, legT, seatZ ),
				Palette.WoodDark );
		}

		// Backrest posts + panel (face-nudged behind seat)
		var backY = -seatW * 0.5f - Depth.Step;
		foreach ( var sx in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "BackPost",
				new Vector3( sx * (seatD * 0.35f), backY, seatZ + Depth.CenterLift( backH ) ),
				new Vector3( legT, legT, backH ),
				Palette.WoodDark );
		}

		KitBox.Box( root, "Back",
			new Vector3( 0f, backY - Depth.Step, seatZ + backH * 0.45f ),
			new Vector3( seatD * 0.75f, 3f * s, backH * 0.7f ),
			Palette.Wood );

		return root;
	}
}
