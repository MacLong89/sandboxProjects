namespace SceneLab;

/// <summary>Low-poly dumpster. Origin ground center. Ratios from <see cref="PropSpecs.Dumpster"/>.</summary>
public static class DumpsterPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw = 0f, float scale = 1f, Color? body = null, PropSpecs.Dumpster spec = null )
	{
		spec ??= PropSpecs.Dumpster.Default;
		var root = new GameObject( parent, true, PieceIds.Dumpster );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var s = scale;
		var paint = body ?? Palette.MetalGreen;
		var w = spec.Width * s;
		var d = spec.Depth * s;
		var h = spec.Height * s;
		var overhang = spec.LidOverhang * s;
		var casterD = MathF.Max( w, d ) * spec.CasterDiameterFrac;

		KitBox.Box( root, "Bin",
			new Vector3( 0f, 0f, Depth.CenterLift( h ) ),
			new Vector3( w, d, h ),
			paint );

		KitBox.Box( root, "Lid",
			new Vector3( 0f, -overhang, h + Depth.Step + Depth.CenterLift( 10f * s ) ),
			new Vector3( w + overhang, d + overhang, 10f * s ),
			Palette.MetalDark );

		foreach ( var sx in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "Pocket",
				new Vector3( sx * (w * 0.5f + 2f * s), 0f, h * 0.55f ),
				new Vector3( 8f * s, d * 0.55f, 22f * s ),
				Palette.MetalDark );
		}

		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitParts.Wheel( root,
				new Vector3( sx * w * 0.35f, sy * d * 0.32f, casterD * 0.5f ),
				casterD,
				casterD * 0.35f );
		}

		return root;
	}
}
