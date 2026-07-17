namespace SceneLab;

/// <summary>
/// Five house archetypes. Local +X = front, +Y = along street.
/// Hollow walkable interiors; attached rooms abut (no volume overlap).
/// </summary>
public static class HouseSuburbanPiece
{
	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw, HouseSpec spec = null, Color? wallOverride = null )
	{
		spec ??= HouseSpec.Default;
		var root = new GameObject( parent, true, PieceIds.HouseSuburban );
		root.LocalPosition = worldPos.WithZ( MathF.Max( worldPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		if ( wallOverride.HasValue )
		{
			spec = HouseSpec.Of( spec.Archetype, spec.WingSign );
			spec.Wall = KitBox.Solid( wallOverride.Value );
		}

		return spec.Archetype switch
		{
			HouseArchetype.Cottage => BuildCottage( root, spec ),
			HouseArchetype.Ranch => BuildRanch( root, spec ),
			HouseArchetype.Colonial => BuildColonial( root, spec ),
			HouseArchetype.LBungalow => BuildLBungalow( root, spec ),
			_ => BuildCraftsman( root, spec ),
		};
	}

	private static GameObject BuildCottage( GameObject root, HouseSpec spec )
	{
		var wallC = KitBox.Solid( spec.Wall );
		var trimC = KitBox.Solid( spec.Trim );
		var wallBase = spec.FoundationH;
		var h = spec.WallH;
		var mainD = spec.Depth * 0.9f;
		var mainW = spec.Width * 0.92f;
		var mainX = 0f;
		var mainCy = 0f;
		var front = mainX + mainD * 0.5f;
		var wallTop = wallBase + h;

		HouseShell.Foundation( root, spec.Depth, spec.Width, spec.FoundationH );
		HouseShell.HollowRoom( root, "Main", mainX, mainCy, mainD, mainW, wallBase, h, wallC, spec.Floor,
			frontDoor: true,
			frontWindows: new[] { new HouseShell.FrontWindow( mainCy + mainW * 0.28f, wallBase + h * 0.52f, 48f, 48f ) },
			windowTrim: trimC );

		HouseShell.Porch( root, front, mainCy, 34f, mainW * 0.5f, wallBase, h, trimC, spec.Roof, deepRoof: false );
		HouseShell.OpenDoorTrim( root, front, mainCy, wallBase, HouseShell.DoorW, MathF.Min( HouseShell.DoorH, h * 0.78f ), trimC, spec.Accent );
		HouseShell.WindowOnSide( root, mainX, mainCy + mainW * 0.5f, wallBase + h * 0.5f, 40f, 40f, trimC );
		HouseShell.Gable( root, mainX, mainCy, mainW + 16f, mainD + 18f, wallTop, spec.Roof, riseFrac: 0.36f );

		if ( spec.HasChimney )
			HouseShell.Chimney( root, mainX - mainD * 0.15f, mainCy + mainW * 0.18f, wallTop, mainW * 0.18f, Palette.HouseBrick );

		HouseInteriorPiece.DressCottage( root, mainX, mainCy, wallBase, spec.Accent );
		return root;
	}

	private static GameObject BuildRanch( GameObject root, HouseSpec spec )
	{
		var wallC = KitBox.Solid( spec.Wall );
		var trimC = KitBox.Solid( spec.Trim );
		var wallBase = spec.FoundationH;
		var h = spec.WallH;
		var gSign = -spec.WingSign;
		var mainW = spec.Width - spec.GarageWidth;
		var mainD = MathF.Max( spec.Depth * 0.9f, spec.GarageDepth );
		var mainX = 0f;
		var mainCy = -gSign * (spec.GarageWidth * 0.5f);
		var garCy = HouseShell.AbutY( mainCy, mainW, spec.GarageWidth, gSign );
		var garH = h * 0.88f;
		// Shared front face — garage not floating ahead/behind.
		var front = mainX + mainD * 0.5f;
		var garD = spec.GarageDepth;
		var garX = front - garD * 0.5f;
		var passX = (mainX + garX) * 0.5f;
		var wallTop = wallBase + h;

		HouseShell.Foundation( root, spec.Depth, spec.Width, spec.FoundationH );
		HouseShell.HollowRoom( root, "Main", mainX, mainCy, mainD, mainW, wallBase, h, wallC, spec.Floor,
			frontDoor: true,
			openSideSign: gSign, openPassW: HouseShell.PassW, openPassX: passX,
			frontWindows: new[]
			{
				new HouseShell.FrontWindow( mainCy - 0.22f * mainW, wallBase + h * 0.52f, 52f, 48f, shutters: true ),
				new HouseShell.FrontWindow( mainCy + 0.22f * mainW, wallBase + h * 0.52f, 52f, 48f, shutters: true ),
			},
			windowTrim: trimC );

		HouseShell.HollowRoom( root, "Garage", garX, garCy, garD, spec.GarageWidth, wallBase, garH,
			KitBox.Solid( wallC, 0.94f ), Palette.HouseFoundation,
			openFront: true,
			omitSideSign: -gSign );

		HouseShell.OpenDoorTrim( root, front, mainCy, wallBase, HouseShell.DoorW, MathF.Min( HouseShell.DoorH, h * 0.78f ), trimC, spec.Accent );

		HouseShell.Stoop( root, front, mainCy, wallBase, 70f );
		HouseShell.GarageFrame( root, garX, garCy, garD, spec.GarageWidth, wallBase, garH, trimC );
		HouseShell.HipRoof( root, mainX, mainCy, mainW + 14f, mainD + 14f, wallTop, spec.Roof );
		HouseShell.HipRoof( root, garX, garCy, spec.GarageWidth + 10f, garD + 10f, wallBase + garH, spec.Roof );

		HouseInteriorPiece.DressRanch( root, mainX, mainCy, wallBase, spec.Accent );
		return root;
	}

	private static GameObject BuildColonial( GameObject root, HouseSpec spec )
	{
		var wallC = KitBox.Solid( spec.Wall );
		var trimC = KitBox.Solid( spec.Trim );
		var wallBase = spec.FoundationH;
		var h = spec.WallH;
		var gSign = -spec.WingSign;
		var mainW = spec.Width - spec.GarageWidth;
		var mainD = MathF.Max( spec.Depth * 0.9f, spec.GarageDepth );
		var mainX = 0f;
		var mainCy = -gSign * (spec.GarageWidth * 0.5f);
		var garCy = HouseShell.AbutY( mainCy, mainW, spec.GarageWidth, gSign );
		var front = mainX + mainD * 0.5f;
		var garD = spec.GarageDepth;
		var garX = front - garD * 0.5f;
		var garH = h * 0.85f;
		var passX = (mainX + garX) * 0.5f;
		var upperH = CityScale.HouseStory * 0.7f;
		var upperBase = wallBase + h;
		var upperCx = mainX;
		var upperD = mainD * 0.96f;
		var upperW = mainW * 0.96f;
		var landingZ = upperBase + HouseShell.FloorT;

		var stairY = mainCy;
		var stairStartX = mainX + mainD * 0.28f;
		var maxRun = mainD * 0.62f;
		var (wellX, wellY, wellD, wellW) = HouseShell.Stairs(
			root, stairStartX, stairY, wallBase, landingZ, steps: 12, maxRun: maxRun );

		HouseShell.Foundation( root, spec.Depth, spec.Width, spec.FoundationH );
		HouseShell.HollowRoom( root, "Main", mainX, mainCy, mainD, mainW, wallBase, h, wallC, spec.Floor,
			frontDoor: true,
			openSideSign: gSign, openPassW: HouseShell.PassW, openPassX: passX,
			stairWellX: wellX, stairWellY: wellY, stairWellD: wellD, stairWellW: wellW,
			wellInCeiling: true,
			frontWindows: new[]
			{
				new HouseShell.FrontWindow( mainCy - 0.28f * mainW, wallBase + h * 0.5f, 40f, 42f, shutters: true ),
				new HouseShell.FrontWindow( mainCy + 0.28f * mainW, wallBase + h * 0.5f, 40f, 42f, shutters: true ),
			},
			windowTrim: trimC );

		HouseShell.HollowRoom( root, "Garage", garX, garCy, garD, spec.GarageWidth, wallBase, garH,
			KitBox.Solid( wallC, 0.95f ), Palette.HouseFoundation,
			openFront: true,
			omitSideSign: -gSign );

		// Upper sits directly on first-floor plate — stair well in floor, solid roof above.
		HouseShell.HollowRoom( root, "Upper", upperCx, mainCy, upperD, upperW, upperBase, upperH,
			KitBox.Solid( wallC, 0.97f ), KitBox.Solid( spec.Floor, 0.92f ),
			stairWellX: wellX, stairWellY: wellY, stairWellD: wellD, stairWellW: wellW,
			wellInFloor: true, wellInCeiling: false,
			frontWindows: new[]
			{
				new HouseShell.FrontWindow( mainCy - 0.28f * mainW, upperBase + upperH * 0.48f, 40f, 42f, shutters: true ),
				new HouseShell.FrontWindow( mainCy + 0.28f * mainW, upperBase + upperH * 0.48f, 40f, 42f, shutters: true ),
			},
			windowTrim: trimC );

		HouseShell.Porch( root, front, mainCy, 36f, mainW * 0.55f, wallBase, h, trimC, spec.Roof, deepRoof: false );
		HouseShell.OpenDoorTrim( root, front, mainCy, wallBase, HouseShell.DoorW, MathF.Min( HouseShell.DoorH, h * 0.78f ), trimC, spec.Accent );

		HouseShell.GarageFrame( root, garX, garCy, garD, spec.GarageWidth, wallBase, garH, trimC );
		HouseShell.Gable( root, upperCx, mainCy, upperW + 18f, upperD + 18f, upperBase + upperH, spec.Roof, riseFrac: 0.28f );
		HouseShell.HipRoof( root, garX, garCy, spec.GarageWidth + 10f, garD + 10f, wallBase + garH, spec.Roof );

		HouseInteriorPiece.DressColonial( root, mainX, mainCy, wallBase, upperBase, landingZ, wellX, wellY, wellD, wellW, spec.Accent );
		return root;
	}

	private static GameObject BuildLBungalow( GameObject root, HouseSpec spec )
	{
		var wallC = KitBox.Solid( spec.Wall );
		var trimC = KitBox.Solid( spec.Trim );
		var wallBase = spec.FoundationH;
		var h = spec.WallH;
		var wSign = spec.WingSign;
		var mainW = spec.Width - spec.WingWidth;
		var mainD = spec.Depth * 0.65f;
		var mainX = -spec.Depth * 0.05f;
		var mainCy = -wSign * (spec.WingWidth * 0.5f);
		var wingCy = HouseShell.AbutY( mainCy, mainW, spec.WingWidth, wSign );
		// Wing shares rear / side; front of wing pulls forward but still overlaps main in X.
		var wingD = spec.WingDepth;
		var wingX = mainX + (mainD - wingD) * 0.5f + mainD * 0.15f;
		var passLo = mainX - mainD * 0.25f;
		var passHi = mainX + mainD * 0.25f;
		var passX = mainX + mainD * 0.05f;
		if ( passX < passLo )
			passX = passLo;
		else if ( passX > passHi )
			passX = passHi;
		var front = mainX + mainD * 0.5f;
		var wallTop = wallBase + h;

		HouseShell.Foundation( root, spec.Depth, spec.Width, spec.FoundationH );
		HouseShell.HollowRoom( root, "Main", mainX, mainCy, mainD, mainW, wallBase, h, wallC, spec.Floor,
			frontDoor: true,
			openSideSign: wSign, openPassW: HouseShell.PassW, openPassX: passX,
			frontWindows: new[]
			{
				new HouseShell.FrontWindow( mainCy - wSign * mainW * 0.18f, wallBase + h * 0.52f, 46f, 46f ),
			},
			windowTrim: trimC );

		HouseShell.HollowRoom( root, "Wing", wingX, wingCy, wingD, spec.WingWidth, wallBase, h,
			KitBox.Solid( wallC, 0.96f ), KitBox.Solid( spec.Floor, 0.95f ),
			omitSideSign: -wSign,
			frontWindows: new[]
			{
				new HouseShell.FrontWindow( wingCy, wallBase + h * 0.5f, spec.WingWidth * 0.42f, 48f ),
			},
			windowTrim: trimC );

		HouseShell.Porch( root, front, mainCy, 34f, mainW * 0.5f, wallBase, h, trimC, spec.Roof, deepRoof: false );
		HouseShell.OpenDoorTrim( root, front, mainCy, wallBase, HouseShell.DoorW, MathF.Min( HouseShell.DoorH, h * 0.78f ), trimC, spec.Accent );

		HouseShell.Gable( root, mainX, mainCy, mainW + 14f, mainD + 14f, wallTop, spec.Roof );
		HouseShell.Gable( root, wingX, wingCy, spec.WingWidth + 12f, wingD + 12f, wallTop, spec.Roof, riseFrac: 0.26f );

		if ( spec.HasChimney )
			HouseShell.Chimney( root, mainX - mainD * 0.1f, mainCy, wallTop, mainW * 0.15f, Palette.HouseBrick );

		HouseInteriorPiece.DressLBungalow( root, mainX, mainCy, wingX, wingCy, wallBase, spec.Accent );
		return root;
	}

	private static GameObject BuildCraftsman( GameObject root, HouseSpec spec )
	{
		var wallC = KitBox.Solid( spec.Wall );
		var trimC = KitBox.Solid( spec.Trim );
		var wallBase = spec.FoundationH;
		var h = spec.WallH;
		var wSign = spec.WingSign;
		var gSign = -wSign;
		var mainW = spec.Width - spec.WingWidth - spec.GarageWidth;
		var mainD = MathF.Max( spec.Depth * 0.8f, spec.GarageDepth );
		var mainX = 0f;
		var mainCy = -wSign * (spec.WingWidth - spec.GarageWidth) * 0.5f;
		var wingCy = HouseShell.AbutY( mainCy, mainW, spec.WingWidth, wSign );
		var garCy = HouseShell.AbutY( mainCy, mainW, spec.GarageWidth, gSign );
		var front = mainX + mainD * 0.5f;
		var garD = spec.GarageDepth;
		var garX = front - garD * 0.5f;
		var garH = h * 0.85f;
		var wingD = spec.WingDepth;
		var wingX = mainX - (mainD - wingD) * 0.2f;
		var wingH = h;
		var wingPassX = mainX;
		var garPassX = (mainX + garX) * 0.5f;
		var wallTop = wallBase + h;

		HouseShell.Foundation( root, spec.Depth, spec.Width, spec.FoundationH );
		HouseShell.HollowRoom( root, "Main", mainX, mainCy, mainD, mainW, wallBase, h, wallC, spec.Floor,
			frontDoor: true,
			openSideSign: wSign, openPassW: HouseShell.PassW, openPassX: wingPassX,
			openOtherSideSign: gSign, openOtherPassW: HouseShell.PassW, openOtherPassX: garPassX,
			frontWindows: new[]
			{
				new HouseShell.FrontWindow( mainCy - 0.2f * mainW, wallBase + h * 0.5f, 48f, 44f ),
				new HouseShell.FrontWindow( mainCy + 0.2f * mainW, wallBase + h * 0.5f, 48f, 44f ),
			},
			windowTrim: trimC );

		HouseShell.HollowRoom( root, "Wing", wingX, wingCy, wingD, spec.WingWidth, wallBase, wingH,
			KitBox.Solid( wallC, 0.96f ), KitBox.Solid( spec.Floor, 0.94f ),
			omitSideSign: -wSign );

		HouseShell.HollowRoom( root, "Garage", garX, garCy, garD, spec.GarageWidth, wallBase, garH,
			KitBox.Solid( wallC, 0.93f ), Palette.HouseFoundation,
			openFront: true,
			omitSideSign: -gSign );

		HouseShell.Porch( root, front, mainCy, 48f, mainW * 0.9f + 16f, wallBase, h, trimC, spec.Roof, deepRoof: true );
		HouseShell.OpenDoorTrim( root, front, mainCy, wallBase, HouseShell.DoorW, MathF.Min( HouseShell.DoorH, h * 0.78f ), trimC, spec.Accent );

		HouseShell.GarageFrame( root, garX, garCy, garD, spec.GarageWidth, wallBase, garH, trimC );
		HouseShell.Gable( root, mainX, mainCy, mainW + 16f, mainD + 16f, wallTop, spec.Roof, riseFrac: 0.32f );
		HouseShell.Gable( root, wingX, wingCy, spec.WingWidth + 12f, wingD + 12f, wallBase + wingH, spec.Roof, riseFrac: 0.24f );
		HouseShell.HipRoof( root, garX, garCy, spec.GarageWidth + 10f, garD + 10f, wallBase + garH, spec.Roof );

		if ( spec.HasChimney )
			HouseShell.Chimney( root, mainX - mainD * 0.15f, mainCy + mainW * 0.12f, wallTop, mainW * 0.16f, Palette.HouseBrick );

		HouseInteriorPiece.DressCraftsman( root, mainX, mainCy, wingX, wingCy, wallBase, spec.Accent );
		return root;
	}
}
