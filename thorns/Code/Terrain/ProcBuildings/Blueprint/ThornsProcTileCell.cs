namespace Sandbox;

/// <summary>Single blueprint grid cell before compile.</summary>
public struct ThornsProcTileCell
{
	public bool Floor;
	public bool Opening;
	public ThornsProcRampDirection Ramp;
	public int RoomId;

	public bool DoorNorth;
	public bool DoorSouth;
	public bool DoorEast;
	public bool DoorWest;

	public bool WindowNorth;
	public bool WindowSouth;
	public bool WindowEast;
	public bool WindowWest;

	public static ThornsProcTileCell Empty => default;

	public bool HasFloorLike => Floor || Ramp != ThornsProcRampDirection.None;
}
