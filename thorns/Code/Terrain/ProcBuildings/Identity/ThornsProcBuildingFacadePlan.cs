namespace Sandbox;

/// <summary>Per-edge facade for spawn: doors, windows, or omitted pieces (ruins).</summary>
public sealed class ThornsProcBuildingFacadePlan
{
	readonly HashSet<(int story, int side, int x, int y)> _doors = new();
	readonly HashSet<(int story, int side, int x, int y)> _forceWindow = new();
	readonly HashSet<(int story, int side, int x, int y)> _omitPiece = new();

	public float WindowChance { get; set; } = 0.2f;
	public bool PreferFrontWindows { get; set; }

	public void AddDoor( int story, int side, int gridX, int gridY ) =>
		_doors.Add( (story, side, gridX, gridY) );

	public void AddForceWindow( int story, int side, int gridX, int gridY ) =>
		_forceWindow.Add( (story, side, gridX, gridY) );

	public void OmitPiece( int story, int side, int gridX, int gridY ) =>
		_omitPiece.Add( (story, side, gridX, gridY) );

	public bool IsDoor( int story, int side, int x, int y ) =>
		_doors.Contains( (story, side, x, y) );

	public bool ShouldOmit( int story, int side, int x, int y ) =>
		_omitPiece.Contains( (story, side, x, y) );

	public string PickWallDef( Random rnd, int story, int side, int x, int y, bool isPrimaryGroundDoor )
	{
		if ( ShouldOmit( story, side, x, y ) )
			return null;

		if ( isPrimaryGroundDoor || IsDoor( story, side, x, y ) )
			return "wood_doorframe";

		if ( _forceWindow.Contains( (story, side, x, y) ) )
			return "wood_window";

		return rnd.NextDouble() < WindowChance ? "wood_window" : "wood_wall";
	}
}
