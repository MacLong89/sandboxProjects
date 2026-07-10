namespace Terraingen.Buildings;

/// <summary>Ramp spawn transforms aligned with thorns <c>wood_ramp</c> (100×100 footprint, 100 rise).</summary>
public static class ThornsProcBuildingRampGeometry
{
	/// <summary>Plan run and rise of the sloped deck (matches <see cref="ThornsBuildingRampMesh"/>).</summary>
	public static float RampRunWorld => ThornsBuildingModule.Cell;

	public static float RampRiseWorld => ThornsBuildingModule.WallHeight;

	public static float RampSpanYWorld => ThornsBuildingModule.Cell * 0.94f;

	public static float YawFromRampDirection( ThornsProcRampDirection dir ) => dir switch
	{
		ThornsProcRampDirection.North => 0f,
		ThornsProcRampDirection.South => 180f,
		ThornsProcRampDirection.East => 90f,
		ThornsProcRampDirection.West => 270f,
		_ => 90f
	};

	/// <summary>Proc spawn yaw — thorns adds 90° so mesh +X rise maps to ascent direction.</summary>
	public static float GetRampSpawnYawDegrees( ThornsProcRampSpec ramp ) =>
		YawFromRampDirection( ramp.Direction ) + 90f;

	public static Vector3 GetRampSpawnLocalPosition(
		ThornsProcRampSpec ramp,
		int widthCells,
		int depthCells,
		int stories )
	{
		if ( !TryGetCellCenterLocal( ramp.X, ramp.Y, widthCells, depthCells, out var center ) )
			return default;

		center.z = ThornsBuildingModule.ProcPerimeterWallCenterLocalZ( ramp.Story, stories );
		return center;
	}

	public static Rotation GetRampSpawnRotation( ThornsProcRampSpec ramp ) =>
		Rotation.FromYaw( GetRampSpawnYawDegrees( ramp ) );

	public static bool TryGetCellCenterLocal(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out Vector3 localCenter ) =>
		ThornsProcBuildingInterior.TryGridCellCenterLocal( gridX, gridY, widthCells, depthCells, out localCenter );

	/// <summary>Local Z for a ramp seated on a foundation top (thorns <c>RampSeatPivotZWorld</c>).</summary>
	public static float RampSeatPivotLocalZFromFoundationCenter() =>
		ThornsBuildingModule.FloorThickness * 0.5f + ThornsBuildingModule.WallHeight * 0.22f + 20f;
}
