namespace FinalOutpost;

/// <summary>HUD art paths under Assets/ui. Missing entries fall back to Material Icons in Razor.</summary>
public static class UiIcons
{
	public const string Brand = "ui/brand_emblem.png";

	public static bool TryBuild( BuildableId id, out string path )
	{
		path = id switch
		{
			BuildableId.GunTower => "ui/build_gun_tower.png",
			BuildableId.CannonTower => "ui/build_cannon.png",
			BuildableId.LongRangeTower => "ui/build_long_range.png",
			BuildableId.WallPiece => "ui/build_wall.png",
			BuildableId.Barracks => "ui/build_barracks.png",
			BuildableId.Lab => "ui/build_lab.png",
			_ => null
		};
		return path is not null;
	}
}

public enum BuildDockTab
{
	Defenses,
	Production,
	Support
}
