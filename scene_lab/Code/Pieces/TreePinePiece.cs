namespace SceneLab;

/// <summary>
/// Low-poly pine. Origin at ground. Canopy kept narrow so yards can clear house footprints.
/// Use <see cref="ClearRadius"/> when placing near buildings.
/// </summary>
public static class TreePinePiece
{
	/// <summary>Approx horizontal clearance needed around trunk for canopy.</summary>
	public static float ClearRadius( float height ) => height * 0.22f + 24f;

	public static GameObject Build( GameObject parent, Vector3 worldPos, float height = 200f, float yaw = 0f )
	{
		var root = new GameObject( parent, true, PieceIds.TreePine );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var trunkH = height * 0.38f;
		var trunkW = MathF.Max( height * 0.05f, 7f );
		KitBox.Box( root, "Trunk",
			new Vector3( 0f, 0f, Depth.CenterLift( trunkH ) ),
			new Vector3( trunkW, trunkW, trunkH ),
			Palette.Trunk );

		// Tapered cones — max width ~0.38 of height (was ~0.68); less house clipping
		var tiers = new (float t, float hFrac)[]
		{
			(0.38f, 0.28f),
			(0.28f, 0.24f),
			(0.18f, 0.20f),
		};
		var z = trunkH * 0.75f;
		for ( var i = 0; i < tiers.Length; i++ )
		{
			var (t, hFrac) = tiers[i];
			var w = height * t;
			var h = height * hFrac;
			z += h * 0.45f;
			var col = i % 2 == 0 ? Palette.LeafA : Palette.LeafB;
			KitBox.Box( root, $"Canopy_{i}", new Vector3( 0f, 0f, z ), new Vector3( w, w, h ), col );
			z += h * 0.35f;
		}

		return root;
	}
}
