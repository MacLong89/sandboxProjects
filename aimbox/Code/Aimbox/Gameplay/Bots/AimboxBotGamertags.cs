namespace Sandbox;

/// <summary>Fake player-style names for AI bots.</summary>
public static class AimboxBotGamertags
{
	static readonly string[] Names =
	[
		"xXShadowSnipeXx",
		"NoScopeNate",
		"TacWolf47",
		"GhostReconMike",
		"HeadshotHero",
		"SilentStrike",
		"NightOwl92",
		"QuickDrawQuinn",
		"SteelRain",
		"ZeroMercy",
		"CampKingCarl",
		"RunAndGunRay",
		"FragHouseFrank",
		"OneTapTom",
		"DarkMatterDan",
		"BulletBill",
		"ScopeCreep",
		"HardpointHank",
		"RushHourRick",
		"ClutchCity",
		"SprayAndPraySam",
		"IronSightsIan",
		"DeadEyeDerek",
		"VaultBoyVince",
		"SmokeScreenSteve",
		"FlashBangFred",
		"CornerPeekPete",
		"LongshotLarry",
		"CloseQuartersCQ",
		"TriggerHappyTy",
		"ReloadRandy",
		"MagDumpManny",
		"PrefirePaul",
		"JumpshotJay",
		"DropShotDrew",
		"SlideCancelSeth",
		"WallbangWalt",
		"SpawnTrapSpencer",
		"FlankMasterFlex",
		"MidControlMax",
		"ObjPlayerOscar",
		"StreakChaserChad",
		"UAVUpUlrich",
		"PredatorPat",
		"CarePackageCole",
		"SentrySid",
		"LastStandLuke",
		"FinalKillFinn"
	];

	public static string ForSlot( int slot )
	{
		if ( Names.Length <= 0 )
			return $"Player{Math.Max( 1, slot )}";

		var index = Math.Clamp( slot - 1, 0, Names.Length - 1 );
		var name = Names[index];

		if ( slot <= Names.Length )
			return name;

		return $"{name}{slot - Names.Length}";
	}
}
