namespace SceneLab;

/// <summary>
/// Look-dev neighborhood sized from <see cref="CityScale"/> (house-referenced).
/// </summary>
public static class WorkbenchScene
{
	public const string Title = "Three-Street Neighborhood";
	public const string Notes = "CityScale from houses: roads, lots, commercial all relative.";

	public static Vector3 SpawnPosition { get; private set; } = new( 0f, 140f, 16f );
	public static float SpawnYaw { get; private set; } = 0f;

	public static void Build( GameObject parent )
	{
		var roadSpec = new RoadCorridorPiece.Spec
		{
			Length = CityScale.StreetHalfLen * 2f,
			RoadWidth = CityScale.RoadWidth,
			SidewalkWidth = CityScale.SidewalkWidth,
			EmbankmentWidth = CityScale.EmbankmentWidth,
			BuildEmbankments = true,
			CenterLine = true,
		};

		var corridorHalf = RoadNetwork.RowHalf( roadSpec, includeBank: true );
		var yardBand = CityScale.YardBand;
		var streetSpacing = (corridorHalf + yardBand) * 2f;

		float[] streetYs = { -streetSpacing, 0f, streetSpacing };
		var halfLen = roadSpec.Length * 0.5f;

		GroundPadPiece.Build( parent, Vector3.Zero, new Vector3( halfLen * 2.4f, streetSpacing * 3.2f, 16f ) );

		var lotSpacing = CityScale.LotPitch;
		var crossSpacing = CityScale.CrossPeriod;
		var crossXs = new List<float>();
		for ( var x = -halfLen + crossSpacing * 0.5f; x < halfLen - CityScale.LotPitch * 0.25f; x += crossSpacing )
			crossXs.Add( x );

		var junctionHalf = roadSpec.RoadWidth * 0.5f + roadSpec.SidewalkWidth + Depth.Step * 4f;
		var lotClearance = junctionHalf + CityScale.HouseFront * 0.15f;

		var crossSpec = new RoadCorridorPiece.Spec
		{
			RoadWidth = roadSpec.RoadWidth,
			SidewalkWidth = roadSpec.SidewalkWidth,
			EmbankmentWidth = CityScale.EmbankmentWidth * 0.65f,
			RoadThickness = roadSpec.RoadThickness,
			SidewalkThickness = roadSpec.SidewalkThickness,
			CurbHeight = roadSpec.CurbHeight,
			EmbankmentThickness = roadSpec.EmbankmentThickness,
			CenterLine = true,
			BuildEmbankments = true,
			BaseLift = 0f,
		};

		var seed = 5000;
		var commercialOrigin = CityScale.LotPitch * 0.7f;
		var commercialMinX = commercialOrigin - CityScale.LotPitch * 0.2f;
		var commercialMaxX = commercialOrigin + CityScale.CommercialPitch * 4.5f;

		for ( var s = 0; s < streetYs.Length; s++ )
		{
			var streetY = streetYs[s];
			var street = new GameObject( parent, true, $"Street_{s}" );
			street.LocalPosition = new Vector3( 0f, streetY, 0f );

			RoadNetwork.BuildAxisSegments(
				street,
				yaw: 0f,
				template: roadSpec,
				axisMin: -halfLen,
				axisMax: halfLen,
				junctionCenters: crossXs,
				junctionHalf: junctionHalf );

			var skipCommercial = s == 1;
			var edgeMargin = CityScale.LotPitch * 0.55f;

			for ( var x = -halfLen + edgeMargin; x <= halfLen - edgeMargin; x += lotSpacing )
			{
				if ( Near( x, crossXs, lotClearance ) )
					continue;

				if ( !(skipCommercial && x >= commercialMinX && x <= commercialMaxX) )
					YardLotPiece.Build( street, x, sideSign: +1f, roadSpec, seed: seed++ );

				var xOpp = x + lotSpacing * 0.5f;
				if ( xOpp <= halfLen - edgeMargin && !Near( xOpp, crossXs, lotClearance ) )
					YardLotPiece.Build( street, xOpp, sideSign: -1f, roadSpec, seed: seed++ );
			}

			var bankPos = RoadCorridorPiece.EmbankmentCenterY( roadSpec, +1f );
			var bankNeg = RoadCorridorPiece.EmbankmentCenterY( roadSpec, -1f );
			var grassZ = RoadCorridorPiece.EmbankmentTopZ( roadSpec ) + Depth.Step;
			var treeStep = CityScale.LotPitch * 0.45f;
			for ( var x = -halfLen + CityScale.LotPitch * 0.25f; x < halfLen - CityScale.LotPitch * 0.25f; x += treeStep )
			{
				if ( Near( x, crossXs, lotClearance + CityScale.HouseFront * 0.08f ) )
					continue;
				TreePinePiece.Build( street, new Vector3( x, bankPos, grassZ ), height: CityScale.HouseStory * 0.9f + MathF.Abs( x % 60f ), yaw: x * 0.05f );
				TreePinePiece.Build( street, new Vector3( x + CityScale.HouseFront * 0.25f, bankNeg, grassZ ), height: CityScale.HouseStory * 0.95f + MathF.Abs( x % 45f ), yaw: -x * 0.04f );
			}
		}

		var yMin = streetYs[0] - corridorHalf - CityScale.HouseFront * 0.07f;
		var yMax = streetYs[^1] + corridorHalf + CityScale.HouseFront * 0.07f;

		foreach ( var cx in crossXs )
		{
			var crossRoot = new GameObject( parent, true, "Cross" );
			crossRoot.LocalPosition = new Vector3( cx, 0f, 0f );
			crossRoot.LocalRotation = Rotation.FromYaw( 90f );

			RoadNetwork.BuildAxisSegments(
				crossRoot,
				yaw: 0f,
				template: crossSpec,
				axisMin: yMin,
				axisMax: yMax,
				junctionCenters: streetYs,
				junctionHalf: junctionHalf );

			foreach ( var sy in streetYs )
				IntersectionPiece.Build( parent, new Vector3( cx, sy, 0f ), roadSpec, junctionHalf );
		}

		PlaceCommercialStrip( parent, streetYs[1], roadSpec, originX: commercialOrigin );

		var midWalkY = RoadCorridorPiece.SidewalkCenterY( roadSpec, +1f );
		SpawnPosition = new Vector3( commercialOrigin - CityScale.LotPitch * 0.2f, midWalkY, 16f );
		SpawnYaw = 0f;

		var walkTopZ = roadSpec.RoadThickness + Depth.Step * 2f;
		FireHydrantPiece.Build( parent, new Vector3( SpawnPosition.x + CityScale.LotPitch * 0.35f, midWalkY - roadSpec.SidewalkWidth * 0.25f, walkTopZ + Depth.Sit ), yaw: 90f );
	}

	private static void PlaceCommercialStrip( GameObject parent, float streetY, RoadCorridorPiece.Spec road, float originX )
	{
		var pitch = CityScale.CommercialPitch;
		var bankOuter = road.RoadWidth * 0.5f + road.SidewalkWidth + road.EmbankmentWidth;
		var grassTop = RoadCorridorPiece.EmbankmentTopZ( road ) + Depth.YardAboveBank;
		var strip = new GameObject( parent, true, "CommercialStrip" );
		strip.LocalPosition = new Vector3( 0f, streetY, 0f );

		var kinds = new[]
		{
			BuildingKind.Skyscraper,
			BuildingKind.Office,
			BuildingKind.Apartment,
			BuildingKind.Factory,
			BuildingKind.Warehouse,
		};

		var stripLen = pitch * (kinds.Length - 0.15f);
		const float yardThick = 6f;
		KitBox.Box( strip, "CommercialYard",
			new Vector3( originX + stripLen * 0.5f, bankOuter + CityScale.YardBand * 0.5f, grassTop - yardThick * 0.5f ),
			new Vector3( stripLen + CityScale.LotPitch * 0.25f, CityScale.YardBand * 0.95f, yardThick ),
			Palette.Dirt );

		for ( var i = 0; i < kinds.Length; i++ )
		{
			var x = originX + i * pitch;
			var y = bankOuter + CityScale.HouseDepth * 0.65f;
			CommercialBuildingPiece.Build( strip, new Vector3( x, y, grassTop ), yaw: -90f, kinds[i] );
		}
	}

	private static bool Near( float x, IReadOnlyList<float> centers, float radius )
	{
		foreach ( var c in centers )
		{
			if ( MathF.Abs( x - c ) < radius )
				return true;
		}

		return false;
	}
}
