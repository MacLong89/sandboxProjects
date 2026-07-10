namespace ThinkDrink.Studio;

/// <summary>Simple hollow cube room with a back-wall answer board.</summary>
public static class StudioSetBuilder
{
	public sealed class BuildResult
	{
		public GameObject Root { get; init; }
		public GameObject MainAnswerBoard { get; init; }
		public IReadOnlyList<StudioEnvironment.ContestantSpot> SpawnSpots { get; init; }
	}

	public static BuildResult Build( GameObject parent )
	{
		var root = StudioPrimitives.CreateGroup( parent, "Hollow Cube Studio" );
		BuildHollowCube( root );
		BuildLighting( root );

		var mainAnswerBoard = BuildQuestionBillboard( root );
		BuildScoreWall( root );
		BuildBuzzerStation( root );
		var spawns = BuildCenterSpawns( root );

		return new BuildResult
		{
			Root = root,
			MainAnswerBoard = mainAnswerBoard,
			SpawnSpots = spawns
		};
	}

	static GameObject BuildQuestionBillboard( GameObject root )
	{
		return StudioWorldPanels.CreateWorldPanel<ThinkDrink.UI.StudioQuestionBillboard>(
			root,
			"Question Billboard UI",
			GetQuestionBillboardPosition(),
			StudioDimensions.BillboardRotation,
			StudioDimensions.BillboardPanelPx * ThinkDrink.StudioBoardTuner.DefaultScreenScale,
			StudioDimensions.BillboardRenderScale );
	}

	static Vector3 GetQuestionBillboardPosition() =>
		new( StudioDimensions.BillboardWorldPos.x, ThinkDrink.StudioBoardTuner.DefaultScreenY, ThinkDrink.StudioBoardTuner.DefaultScreenCenterZ );

	static void BuildScoreWall( GameObject root )
	{
		StudioWorldPanels.CreateWorldPanel<ThinkDrink.UI.MatchScoreWall>(
			root,
			"Live Score Wall",
			StudioDimensions.ScoreWallPos,
			StudioDimensions.ScoreWallRotation,
			StudioDimensions.ScoreWallPanelPx,
			StudioDimensions.ScoreWallRenderScale );
	}

	static void BuildBuzzerStation( GameObject root )
	{
		var group = StudioPrimitives.CreateGroup( root, "Physical Buzzer Station" );
		const float devModelUnit = 50f;
		var tableSize = new Vector3( 0.36f, 0.24f, 0.22f );
		var buttonSize = new Vector3( 0.14f, 0.14f, 0.06f );
		var tableHeight = tableSize.z * devModelUnit;
		var buttonHeight = buttonSize.z * devModelUnit;
		var tablePos = new Vector3( 0f, -132f, StudioDimensions.FloorTopZ + tableHeight + 70f );

		var table = StudioPrimitives.CreateSolidBox( group, "White Buzzer Table",
			tablePos,
			tableSize,
			new Color( 0.92f, 0.92f, 0.88f ) );
		table.Tags.Add( "buzzer_table" );

		var button = StudioPrimitives.CreateVisualSphere( group, "Red Buzz Button",
			tablePos + new Vector3( 0f, 0f, tableHeight * 0.5f + buttonHeight * 0.5f + 1f ),
			buttonSize,
			new Color( 0.95f, 0.04f, 0.05f ) );
		button.Tags.Add( "buzzer" );
		button.Components.Create<StudioBuzzerButton>();

		var collider = button.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( 30f, 30f, 18f );
		collider.Static = true;
	}

	static void BuildHollowCube( GameObject root )
	{
		var shell = StudioPrimitives.CreateGroup( root, "Cube Shell" );
		var half = StudioDimensions.Half;
		var size = StudioDimensions.RoomSize;
		var height = StudioDimensions.RoomHeight;
		var heightHalf = StudioDimensions.HeightHalf;
		var t = StudioDimensions.WallThickness;
		var wallColor = StudioPalette.WallDark;
		var floorColor = new Color( 0.14f, 0.11f, 0.08f );
		var ceilingColor = new Color( 0.05f, 0.07f, 0.12f );

		StudioPrimitives.CreateSolidBox( shell, "Floor",
			new Vector3( 0f, 0f, t * 0.5f ),
			new Vector3( size + t * 2f, size + t * 2f, t ),
			floorColor );

		StudioPrimitives.CreateSolidBox( shell, "Ceiling",
			new Vector3( 0f, 0f, height - t * 0.5f ),
			new Vector3( size + t * 2f, size + t * 2f, t ),
			ceilingColor );

		StudioPrimitives.CreateSolidBox( shell, "Wall Left",
			new Vector3( -half - t * 0.5f, 0f, heightHalf ),
			new Vector3( t, size, height ),
			wallColor );
		StudioPrimitives.CreateSolidBox( shell, "Wall Right",
			new Vector3( half + t * 0.5f, 0f, heightHalf ),
			new Vector3( t, size, height ),
			wallColor );
		StudioPrimitives.CreateSolidBox( shell, "Wall Back",
			new Vector3( 0f, -half - t * 0.5f, heightHalf ),
			new Vector3( size, t, height ),
			wallColor );
		StudioPrimitives.CreateSolidBox( shell, "Wall Front",
			new Vector3( 0f, half + t * 0.5f, heightHalf ),
			new Vector3( size, t, height ),
			wallColor );
	}

	static List<StudioEnvironment.ContestantSpot> BuildCenterSpawns( GameObject root )
	{
		var spawns = new List<StudioEnvironment.ContestantSpot>();
		var group = StudioPrimitives.CreateGroup( root, "Spawns" );
		var floorZ = StudioDimensions.PlayerSpawnZ;
		var boardTarget = GetQuestionBillboardPosition();

		for ( var i = 0; i < 8; i++ )
		{
			Vector3 pos;
			if ( i == 0 )
			{
				pos = new Vector3( 0f, 0f, floorZ );
			}
			else
			{
				var angle = (i - 1) * 45f;
				var rad = angle * (MathF.PI / 180f );
				var offset = new Vector3( MathF.Cos( rad ) * 48f, MathF.Sin( rad ) * 48f, 0f );
				pos = new Vector3( offset.x, offset.y, floorZ );
			}

			var eye = pos + new Vector3( 0f, 0f, 64f );
			var rot = Rotation.LookAt( boardTarget - eye, Vector3.Up );

			spawns.Add( new StudioEnvironment.ContestantSpot( pos, rot ) );
		}

		return spawns;
	}

	static void BuildLighting( GameObject root )
	{
		var light = new GameObject( root, true, "Room Light" );
		light.WorldRotation = new Angles( 55, 25, 0 );
		var dl = light.AddComponent<DirectionalLight>();
		dl.LightColor = new Color( 0.9f, 0.88f, 1f );
		dl.Shadows = true;

		var fill = new GameObject( root, true, "Fill Light" );
		fill.WorldRotation = new Angles( 30, -140, 0 );
		var fl = fill.AddComponent<DirectionalLight>();
		fl.LightColor = new Color( 0.35f, 0.30f, 0.55f );
		fl.Shadows = false;

		var boardSpot = new GameObject( root, true, "Board Spotlight" );
		boardSpot.WorldPosition = GetQuestionBillboardPosition() + new Vector3( 0f, 120f, StudioDimensions.BillboardWorldHeight * 0.35f );
		var bl = boardSpot.AddComponent<PointLight>();
		bl.LightColor = StudioPalette.AccentGold.WithAlpha( 0.85f );
		bl.Radius = StudioDimensions.RoomSize * 0.45f;
		bl.Shadows = false;

	}
}
