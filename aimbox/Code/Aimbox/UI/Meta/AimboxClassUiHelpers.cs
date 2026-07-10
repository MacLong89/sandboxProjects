namespace Sandbox;

public static class AimboxClassUiHelpers
{
	public static string WeaponShortCode( AimboxWeaponId id ) => id switch
	{
		AimboxWeaponId.M4A1 => "M4",
		AimboxWeaponId.Mp5 => "MP5",
		AimboxWeaponId.Usp => "USP",
		AimboxWeaponId.M700 => "M700",
		AimboxWeaponId.SpaghelliM4 => "SG",
		_ => id.ToString()[..Math.Min( 3, id.ToString().Length )].ToUpperInvariant()
	};

	public static string WeaponClassLabel( AimboxWeaponId id ) => id switch
	{
		AimboxWeaponId.M4A1 => "ASSAULT RIFLE",
		AimboxWeaponId.Mp5 => "SMG",
		AimboxWeaponId.M700 => "SNIPER",
		AimboxWeaponId.SpaghelliM4 => "SHOTGUN",
		AimboxWeaponId.Usp => "PISTOL",
		_ when AimboxMw2Catalog.IsPrimaryWeapon( id ) => "PRIMARY",
		_ when AimboxMw2Catalog.IsSecondaryWeapon( id ) => "SECONDARY",
		_ => "WEAPON"
	};

	public static string WeaponAccent( AimboxWeaponId id ) => id switch
	{
		AimboxWeaponId.M4A1 => "#4a6741",
		AimboxWeaponId.Mp5 => "#3d566e",
		AimboxWeaponId.Usp => "#5a4a38",
		AimboxWeaponId.M700 => "#4f4258",
		AimboxWeaponId.SpaghelliM4 => "#6e4a35",
		_ => "#253140"
	};

	public static string WeaponUiImage( AimboxWeaponId id ) => id switch
	{
		AimboxWeaponId.M4A1 => "ui/m4a1.png",
		AimboxWeaponId.Mp5 => "ui/mp5.png",
		AimboxWeaponId.Usp => "ui/usp.png",
		AimboxWeaponId.M700 => "ui/m700.png",
		AimboxWeaponId.SpaghelliM4 => "ui/shotgun.png",
		_ => null
	};

	public static bool HasWeaponUiImage( AimboxWeaponId id ) => WeaponUiImage( id ) is not null;

	public static string WeaponThumbStyle( AimboxWeaponId id )
	{
		var image = WeaponUiImage( id );
		if ( image is not null )
			return $"background-image: url('{image}'); background-size: contain; background-repeat: no-repeat; background-position: center; background-color: rgba(0, 0, 0, 0.35);";

		return $"background-color: {WeaponAccent( id )};";
	}

	public static string ModeShortCode( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => "TDM",
		AimboxGameMode.Duel => "1v1",
		AimboxGameMode.Survival => "SUR",
		AimboxGameMode.Range => "RNG",
		AimboxGameMode.AimLevel1 => "GRID",
		AimboxGameMode.AimLevel2 => "FLICK",
		AimboxGameMode.AimLevel3 => "TRACK",
		AimboxGameMode.AimLevel4 => "mGRID",
		AimboxGameMode.AimLevel5 => "mFLICK",
		AimboxGameMode.AimLevel6 => "mTRACK",
		_ => "FFA"
	};

	public static string ModeAccent( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => "#3d5a80",
		AimboxGameMode.Duel => "#704040",
		AimboxGameMode.Survival => "#5a7040",
		AimboxGameMode.Range => "#4a6070",
		AimboxGameMode.AimLevel1 => "#6a4a80",
		AimboxGameMode.AimLevel2 => "#5a4580",
		AimboxGameMode.AimLevel3 => "#4a4080",
		AimboxGameMode.AimLevel4 => "#704a70",
		AimboxGameMode.AimLevel5 => "#5a4a78",
		AimboxGameMode.AimLevel6 => "#4a5078",
		_ => "#6a5030"
	};

	public static string PerkShortCode( AimboxPerkId id ) => id switch
	{
		AimboxPerkId.Lightweight => "LW",
		AimboxPerkId.StoppingPower => "SP",
		AimboxPerkId.Scavenger => "SC",
		AimboxPerkId.SleightOfHand => "SH",
		AimboxPerkId.Marathon => "MR",
		AimboxPerkId.Ninja => "NJ",
		_ => "--"
	};

	public static string KillstreakShortCode( AimboxKillstreakId id ) => id switch
	{
		AimboxKillstreakId.Uav => "UAV",
		AimboxKillstreakId.CarePackage => "CP",
		AimboxKillstreakId.PredatorMissile => "PM",
		AimboxKillstreakId.CounterUav => "CU",
		AimboxKillstreakId.SentryGun => "SG",
		_ => "--"
	};
}
