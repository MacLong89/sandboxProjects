namespace SceneLab;

/// <summary>
/// Low-poly sedan matching docs/ref_sedan_goal.md.
/// +X forward, +Z up. Opaque stacked volumes only (no interpenetration).
/// </summary>
public static class CarSedanPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw, Color? body = null, PropSpecs.Sedan spec = null )
	{
		spec ??= PropSpecs.Sedan.Default;
		var root = new GameObject( parent, true, PieceIds.CarSedan );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var paint = body ?? Palette.CarTan;
		var paintDark = paint * 0.88f;

		var len = spec.Length;
		var wid = spec.Width;
		var floor = spec.Ride;
		var skirtH = spec.SkirtH;
		var bodyH = spec.BodyH;
		var cabinH = spec.CabinH;
		var wheelD = spec.WheelDiameter;
		var wheelW = wheelD * spec.WheelWidthFrac;
		var wheelBase = len * spec.WheelBaseFrac;

		var skirtTop = floor + skirtH;
		var bodyTop = skirtTop + Depth.Step + bodyH;
		var cabinBase = bodyTop + Depth.Step;

		// --- Lower skirt / rocker ---
		KitBox.Box( root, "Skirt",
			new Vector3( 0f, 0f, floor + skirtH * 0.5f ),
			new Vector3( len * 0.94f, wid * 0.92f, skirtH ),
			paintDark );

		// --- Main body (hood + belt + trunk as one volume) ---
		KitBox.Box( root, "Body",
			new Vector3( 0f, 0f, skirtTop + Depth.Step + bodyH * 0.5f ),
			new Vector3( len * 0.96f, wid, bodyH ),
			paint );

		// --- Greenhouse ---
		KitBox.Box( root, "Cabin",
			new Vector3( -len * 0.02f, 0f, cabinBase + cabinH * 0.5f ),
			new Vector3( len * 0.48f, wid * 0.88f, cabinH ),
			paint );

		KitBox.Box( root, "Roof",
			new Vector3( -len * 0.03f, 0f, cabinBase + cabinH + Depth.Step + 2.5f ),
			new Vector3( len * 0.40f, wid * 0.78f, 5f ),
			paintDark );

		// Dark glass — windshield, rear, split side panes + B-pillar
		KitBox.Box( root, "Windshield",
			new Vector3( len * 0.18f, 0f, cabinBase + cabinH * 0.48f ),
			new Vector3( 3f, wid * 0.78f, cabinH * 0.78f ),
			Palette.CarGlass );

		KitBox.Box( root, "RearGlass",
			new Vector3( -len * 0.22f, 0f, cabinBase + cabinH * 0.45f ),
			new Vector3( 3f, wid * 0.76f, cabinH * 0.7f ),
			Palette.CarGlass );

		foreach ( var sy in new[] { -1f, 1f } )
		{
			var faceY = sy * (wid * 0.44f + Depth.Step);
			KitBox.Box( root, "SideWinF",
				new Vector3( len * 0.06f, faceY, cabinBase + cabinH * 0.48f ),
				new Vector3( len * 0.14f, 3f, cabinH * 0.62f ),
				Palette.CarGlass );
			KitBox.Box( root, "SideWinR",
				new Vector3( -len * 0.10f, faceY, cabinBase + cabinH * 0.48f ),
				new Vector3( len * 0.14f, 3f, cabinH * 0.62f ),
				Palette.CarGlass );
			KitBox.Box( root, "BPillar",
				new Vector3( -len * 0.02f, faceY, cabinBase + cabinH * 0.48f ),
				new Vector3( 5f, 4f, cabinH * 0.7f ),
				paintDark );

			// Side mirrors (front door)
			KitBox.Box( root, "Mirror",
				new Vector3( len * 0.12f, sy * (wid * 0.5f + 5f), cabinBase + cabinH * 0.35f ),
				new Vector3( 8f, 10f, 6f ),
				paintDark );
		}

		// --- Front fascia ---
		KitBox.Box( root, "Nose",
			new Vector3( len * 0.485f, 0f, floor + (skirtH + bodyH) * 0.35f ),
			new Vector3( 6f, wid * 0.98f, skirtH + bodyH * 0.55f ),
			paint );

		KitBox.Box( root, "Grille",
			new Vector3( len * 0.495f, 0f, skirtTop + bodyH * 0.25f ),
			new Vector3( 3f, wid * 0.22f, 6f ),
			Palette.CarGrille );

		KitParts.HeadlightPair(
			root,
			noseX: len * 0.498f,
			bodyWidth: wid,
			lightZ: skirtTop + bodyH * 0.42f,
			lightW: 16f,
			lightH: 5f,
			lightD: 3f,
			spanFraction: spec.HeadlightSpanFrac );

		// --- Rear fascia ---
		KitBox.Box( root, "Tail",
			new Vector3( -len * 0.485f, 0f, floor + (skirtH + bodyH) * 0.35f ),
			new Vector3( 6f, wid * 0.98f, skirtH + bodyH * 0.55f ),
			paint );

		KitParts.TaillightPair(
			root,
			tailX: -len * 0.498f,
			bodyWidth: wid,
			lightZ: skirtTop + bodyH * 0.45f,
			lightW: 14f,
			lightH: 10f,
			lightD: 3f,
			spanFraction: spec.TaillightSpanFrac );

		KitBox.Box( root, "Plate",
			new Vector3( -len * 0.498f, 0f, skirtTop + bodyH * 0.15f ),
			new Vector3( 3f, 18f, 8f ),
			Palette.CarPlate );

		// --- Wheels + angular fender flares ---
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
		{
			var ax = sx * wheelBase;
			var ay = sy * (wid * 0.5f - wheelW * 0.35f - wid * spec.WheelInsetFrac);

			KitParts.Wheel( root, new Vector3( ax, ay, wheelD * 0.5f ), wheelD, wheelW );

			// Fender flare over each wheel (angular, pushed out)
			KitBox.Box( root, "Fender",
				new Vector3( ax, sy * (wid * 0.5f + 3f), floor + wheelD * 0.55f ),
				new Vector3( wheelD * 1.15f, 8f, wheelD * 0.85f ),
				paint );
		}

		return root;
	}
}
