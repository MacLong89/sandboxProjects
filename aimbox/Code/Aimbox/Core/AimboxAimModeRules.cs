namespace Sandbox;

public static class AimboxAimModeRules
{
	public static bool IsAimMode( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.AimLevel1 or AimboxGameMode.AimLevel2 or AimboxGameMode.AimLevel3
			or AimboxGameMode.AimLevel4 or AimboxGameMode.AimLevel5 or AimboxGameMode.AimLevel6 => true,
		_ => false
	};

	public static AimboxAimDrill ToDrill( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.AimLevel2 => AimboxAimDrill.Flick,
		AimboxGameMode.AimLevel3 => AimboxAimDrill.Bounce,
		AimboxGameMode.AimLevel4 => AimboxAimDrill.MicroTriple,
		AimboxGameMode.AimLevel5 => AimboxAimDrill.MicroFlick,
		AimboxGameMode.AimLevel6 => AimboxAimDrill.MicroBounce,
		_ => AimboxAimDrill.Triple
	};
}
