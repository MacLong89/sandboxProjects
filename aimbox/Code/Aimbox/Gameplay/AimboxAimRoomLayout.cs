namespace Sandbox;

/// <summary>Fixed layout for the isolated AIM trainer booth (not a tactical map).</summary>
public static class AimboxAimRoomLayout
{
	public const string RootName = "Aimbox AIM Room";
	public const float RoomWidthMeters = 5.5f;
	public const float RoomDepthMeters = 7.5f;

	public static readonly AimboxMapLayout Layout =
		AimboxMapDesignRules.CreateLayout( RoomWidthMeters, RoomDepthMeters );

	public static float FeetZ => AimboxMapDesignRules.FloorWalkZ;

	public static Vector3 ArenaCenter => new( 0f, 0f, FeetZ );

	public static Vector3 PlayerSpawn => new( 0f, -Layout.ArenaHalfLength * 0.68f, FeetZ );

	public static Vector3 TargetCenter => new( 0f, Layout.ArenaHalfLength * 0.52f, FeetZ );

	public static Rotation PlayerFacing => Rotation.LookAt( (TargetCenter - PlayerSpawn).WithZ( 0f ).Normal );

	public static float BackWallY => Layout.ArenaHalfLength - Layout.WallThickness * 0.5f;

	/// <summary>Inner Y plane in front of the north wall where aim circles sit.</summary>
	public static float TargetPlaneY => BackWallY - 18f;

	public static Vector3 RandomBackWallPosition()
	{
		var halfWidth = Layout.ArenaHalfWidth * 0.72f;
		var minZ = FeetZ + 48f;
		var maxZ = FeetZ + Layout.WallHeight * 0.68f;
		return new Vector3(
			Game.Random.Float( -halfWidth, halfWidth ),
			TargetPlaneY,
			Game.Random.Float( minZ, maxZ ) );
	}

	public static Rotation FacePlayerFrom( Vector3 wallPosition ) =>
		Rotation.LookAt( (PlayerSpawn - wallPosition).WithZ( 0f ).Normal );
}
