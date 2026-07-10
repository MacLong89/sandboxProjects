namespace ThinkDrink.Studio;

/// <summary>Hollow studio: square floor plan with half-height ceiling.</summary>
public static class StudioDimensions
{
	public const float PlayerHeight = 72f;
	public const float RoomSize = PlayerHeight * 30f;
	public const float RoomHeight = RoomSize * 0.5f;
	public const float WallThickness = 2f;

	public static float Half => RoomSize * 0.5f;
	public static float HeightHalf => RoomHeight * 0.5f;
	public static float FloorTopZ => WallThickness;
	public static float PlayerSpawnZ => FloorTopZ;

	public static Vector3 RoomCenter => new( 0f, 0f, HeightHalf );
	public static Vector3 PlayAreaFocus => RoomCenter;

	// Game-show board: smaller than the back wall and pulled into the room.
	public const float BillboardUiWidth = 1920f;
	public const float BillboardUiHeight = 1080f;
	public const float BillboardRenderScale = 0.99f;
	public const float BillboardWallClearance = 72f;
	public const float BillboardBottomZ = 8f;

	public static float BillboardWorldWidth => BillboardUiWidth * BillboardRenderScale;
	public static float BillboardWorldHeight => BillboardUiHeight * BillboardRenderScale;

	public static float BillboardWorldY => -Half + BillboardWallClearance;
	public static float BillboardCenterZ => BillboardBottomZ + BillboardWorldHeight * 0.5f;

	public static Vector3 BillboardWorldPos => new( 0f, BillboardWorldY, BillboardCenterZ );

	// Yaw 90 faces the rendered WorldPanel toward the center spawn.
	public static Rotation BillboardRotation => Rotation.From( 0f, 90f, 0f );

	public static Vector2 BillboardPanelPx => new( BillboardUiWidth, BillboardUiHeight );

	public static Vector2 MainBoardPanelPx => BillboardPanelPx;
	public static float MainBoardRenderScale => BillboardRenderScale;
	public static Vector3 MainBoardMountPos => BillboardWorldPos;
	public static Rotation MainBoardRotation => BillboardRotation;

	public static Vector3 ScoreWallPos => new( -Half + 48f, -Half * 0.35f, HeightHalf );
	public static Rotation ScoreWallRotation => Rotation.From( 0f, 0f, 0f );
	public static Vector2 ScoreWallPanelPx => new( 480f, 640f );
	public const float ScoreWallRenderScale = 0.55f;
}
