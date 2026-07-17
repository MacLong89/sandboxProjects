namespace SceneLab;

/// <summary>
/// Suburban lot on a street. Parent should be the street root (road at local origin along +X).
/// <paramref name="sideSign"/>: +1 = +Y of that street, -1 = -Y.
/// Plot pitch comes from <see cref="CityScale"/> (house-referenced).
/// </summary>
public static class YardLotPiece
{
	public const float LotPitch = CityScale.LotPitch;

	public static GameObject Build( GameObject parent, float lotX, float sideSign, RoadCorridorPiece.Spec road, Color? houseColor = null, Color? carColor = null, int seed = 0 )
	{
		var root = new GameObject( parent, true, PieceIds.YardLot );
		root.LocalPosition = new Vector3( lotX, 0f, 0f );

		var rng = new Random( seed );
		var sign = sideSign >= 0f ? 1f : -1f;
		var houseSpec = HouseSpec.Roll( rng );

		var streetEdgeY = sign * (road.RoadWidth * 0.5f);
		var bankOuterY = sign * (road.RoadWidth * 0.5f + road.SidewalkWidth + road.EmbankmentWidth);

		var yardWidth = MathF.Min( LotPitch - CityScale.LotSidePad * 0.25f, houseSpec.FootprintWidth + CityScale.LotSidePad );
		var yardDepth = CityScale.YardBand * 0.85f + houseSpec.FootprintDepth * 0.25f;
		var yardCenterY = bankOuterY + sign * (yardDepth * 0.5f);
		var bankTop = RoadCorridorPiece.EmbankmentTopZ( road );
		// grassTop = walkable / build surface. Pad hangs below so its top is flush.
		var grassTop = bankTop + Depth.YardAboveBank;
		var driveZ = RoadCorridorPiece.RoadTopZ( road ) + Depth.DrivewayAboveRoad;

		const float yardThick = 6f;
		KitBox.Box( root, "Yard",
			new Vector3( 0f, yardCenterY, grassTop - yardThick * 0.5f ),
			new Vector3( yardWidth, yardDepth, yardThick ),
			Palette.Grass );

		var houseYaw = sign > 0f ? -90f : 90f;
		var houseX = 0f;
		var houseY = bankOuterY + sign * (yardDepth - houseSpec.FootprintDepth * 0.5f - CityScale.HouseDepth * 0.11f );
		HouseSuburbanPiece.Build(
			root,
			new Vector3( houseX, houseY, grassTop ),
			houseYaw,
			spec: houseSpec,
			wallOverride: houseColor );

		var houseFrontY = houseY - sign * (houseSpec.FootprintDepth * 0.5f - 8f);
		var drivewayLen = MathF.Abs( houseFrontY - streetEdgeY );
		var driveYaw = sign > 0f ? 90f : -90f;
		DrivewayPiece.Build( root, new Vector3( 0f, streetEdgeY, driveZ ), driveYaw, drivewayLen, width: CityScale.DriveWidth );

		if ( rng.NextSingle() > 0.25f )
		{
			var carDist = drivewayLen * (0.4f + rng.NextSingle() * 0.15f);
			var carY = streetEdgeY + sign * carDist;
			var carYaw = sign > 0f ? -90f : 90f;
			CarSedanPiece.Build( root, new Vector3( 12f, carY, driveZ ), carYaw, body: carColor ?? PickCar( rng ) );
		}

		PlaceYardTrees( root, houseX, houseY, houseSpec, sign, yardWidth, yardDepth, bankOuterY, grassTop, rng );
		return root;
	}

	private static void PlaceYardTrees(
		GameObject root,
		float houseX,
		float houseY,
		HouseSpec house,
		float sign,
		float yardWidth,
		float yardDepth,
		float bankOuterY,
		float grassTop,
		Random rng )
	{
		var halfW = house.FootprintWidth * 0.5f + CityScale.HouseFront * 0.09f;
		var halfD = house.FootprintDepth * 0.5f + CityScale.HouseFront * 0.09f;
		var toStreet = -sign;

		var candidates = new List<Vector3>
		{
			new( houseX - halfW - CityScale.HouseFront * 0.07f, houseY + toStreet * (halfD + CityScale.HouseDepth * 0.2f), grassTop ),
			new( houseX + halfW + CityScale.HouseFront * 0.07f, houseY + toStreet * (halfD + CityScale.HouseDepth * 0.2f), grassTop ),
			new( houseX - yardWidth * 0.32f, houseY + toStreet * (halfD + CityScale.HouseDepth * 0.3f), grassTop ),
			new( houseX + yardWidth * 0.32f, houseY + toStreet * (halfD + CityScale.HouseDepth * 0.3f), grassTop ),
		};

		var placed = 0;
		var want = 1 + rng.Next( 0, 3 );
		foreach ( var c in candidates )
		{
			if ( placed >= want )
				break;

			var h = CityScale.HouseStory * (0.85f + rng.NextSingle() * 0.4f);
			var clear = TreePinePiece.ClearRadius( h );
			var yardMinY = MathF.Min( bankOuterY, bankOuterY + sign * yardDepth );
			var yardMaxY = MathF.Max( bankOuterY, bankOuterY + sign * yardDepth );
			if ( c.x < -yardWidth * 0.42f || c.x > yardWidth * 0.42f )
				continue;
			if ( c.y < yardMinY + 20f || c.y > yardMaxY - 20f )
				continue;
			if ( MathF.Abs( c.x - houseX ) < halfW + clear * 0.3f
				&& MathF.Abs( c.y - houseY ) < halfD + clear * 0.3f )
				continue;
			if ( MathF.Abs( c.x ) < CityScale.DriveWidth * 0.65f )
				continue;

			TreePinePiece.Build( root, c, height: h, yaw: rng.NextSingle() * 360f );
			placed++;
		}
	}

	private static Color PickCar( Random rng )
	{
		Color[] opts = { Palette.CarTan, Palette.CarBlue, Palette.CarRed, Palette.CarChrome };
		return opts[rng.Next( opts.Length )];
	}
}
