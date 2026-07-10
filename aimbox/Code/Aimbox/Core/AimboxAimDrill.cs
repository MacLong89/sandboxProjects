namespace Sandbox;

public enum AimboxAimDrill
{
	Triple,
	Flick,
	Bounce,
	MicroTriple,
	MicroFlick,
	MicroBounce
}

public static class AimboxAimDrillLabels
{
	public static string Short( AimboxAimDrill drill ) => drill switch
	{
		AimboxAimDrill.Triple => "GRID",
		AimboxAimDrill.Flick => "FLICK",
		AimboxAimDrill.Bounce => "TRACK",
		AimboxAimDrill.MicroTriple => "mGRID",
		AimboxAimDrill.MicroFlick => "mFLICK",
		_ => "mTRACK"
	};

	public static string Long( AimboxAimDrill drill ) => drill switch
	{
		AimboxAimDrill.Triple => "Grid",
		AimboxAimDrill.Flick => "Flick",
		AimboxAimDrill.Bounce => "Track",
		AimboxAimDrill.MicroTriple => "mGrid",
		AimboxAimDrill.MicroFlick => "mFlick",
		_ => "mTrack"
	};

	public static string Description( AimboxAimDrill drill ) => drill switch
	{
		AimboxAimDrill.Triple => "Three spheres on the back wall — shoot one, it respawns instantly.",
		AimboxAimDrill.Flick => "One sphere on the back wall — shoot it, it respawns instantly.",
		AimboxAimDrill.Bounce => "A bouncing sphere on the back wall — five hits to clear, then a new one spawns.",
		AimboxAimDrill.MicroTriple => "Three smaller spheres — same grid drill at reduced target size.",
		AimboxAimDrill.MicroFlick => "One smaller sphere — same flick drill at reduced target size.",
		_ => "A smaller bouncing sphere — same track drill at reduced target size."
	};

	public static AimboxAimDrill FromLevelIndex( int index ) => index switch
	{
		0 => AimboxAimDrill.Triple,
		1 => AimboxAimDrill.Flick,
		2 => AimboxAimDrill.Bounce,
		3 => AimboxAimDrill.MicroTriple,
		4 => AimboxAimDrill.MicroFlick,
		_ => AimboxAimDrill.MicroBounce
	};
}
