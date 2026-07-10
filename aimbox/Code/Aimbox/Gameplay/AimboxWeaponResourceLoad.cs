namespace Sandbox;

public static class AimboxWeaponResourceLoad
{
	public const string FallbackWeaponModelPath = "models/dev/box.vmdl";
	public const string FirstPersonArmsHumanPath = "models/first_person/v_first_person_arms_human.vmdl";

	public const string M4FirstPersonViewmodelPath = "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl";
	public const string M4WorldModelPath = "models/weapons/sbox_assault_m4a1/w_m4a1.vmdl";
	public const string Mp5FirstPersonViewmodelPath = "models/weapons/sbox_smg_mp5/v_mp5.vmdl";
	public const string Mp5WorldModelPath = "models/weapons/sbox_smg_mp5/w_mp5.vmdl";
	public const string UspFirstPersonViewmodelPath = "models/weapons/sbox_pistol_usp/v_usp.vmdl";
	public const string UspWorldModelPath = "models/weapons/sbox_pistol_usp/w_usp.vmdl";
	public const string ShotgunFirstPersonViewmodelPath = "models/weapons/sbox_shotgun_spaghellim4/v_spaghellim4.vmdl";
	public const string ShotgunWorldModelPath = "models/weapons/sbox_shotgun_spaghellim4/w_spaghellim4.vmdl";
	public const string SniperFirstPersonViewmodelPath = "models/weapons/sbox_sniper_m700/v_m700.vmdl";
	public const string SniperWorldModelPath = "models/weapons/sbox_sniper_m700/w_m700.vmdl";
	public const string BayonetM9FirstPersonViewmodelPath = "models/weapons/sbox_melee_m9bayonet/v_m9bayonet.vmdl";
	public const string BayonetM9WorldModelPath = "models/weapons/sbox_melee_m9bayonet/w_m9bayonet.vmdl";
	public const string TrenchknifeFirstPersonViewmodelPath = "models/weapons/sbox_melee_trenchknife/v_trenchknife.vmdl";
	public const string CrowbarFirstPersonViewmodelPath = "models/weapons/sbox_melee_crowbar/v_crowbar.vmdl";
	public const string CrowbarWorldModelPath = "models/weapons/sbox_melee_crowbar/w_crowbar.vmdl";
	public const string HeGrenadeFirstPersonViewmodelPath = "models/weapons/sbox_grenade_explosive/v_he_grenade.vmdl";
	public const string HeGrenadeWorldModelPath = "models/weapons/sbox_grenade_explosive/w_he_grenade.vmdl";
	public const string FlashGrenadeFirstPersonViewmodelPath = "models/weapons/sbox_grenade_flash/v_flash_grenade.vmdl";
	public const string FlashGrenadeWorldModelPath = "models/weapons/sbox_grenade_flash/w_flash_grenade.vmdl";
	public const string SmokeGrenadeFirstPersonViewmodelPath = "models/weapons/sbox_grenade_smoke/v_smoke_grenade.vmdl";
	public const string SmokeGrenadeWorldModelPath = "models/weapons/sbox_grenade_smoke/w_smoke_grenade.vmdl";
	public const string DecoyGrenadeFirstPersonViewmodelPath = "models/weapons/sbox_grenade_decoy/v_decoy_grenade.vmdl";
	public const string DecoyGrenadeWorldModelPath = "models/weapons/sbox_grenade_decoy/w_decoy_grenade.vmdl";
	public const string IncendiaryGrenadeFirstPersonViewmodelPath = "models/weapons/sbox_grenade_incendiary/v_incendiary_grenade.vmdl";
	public const string IncendiaryGrenadeWorldModelPath = "models/weapons/sbox_grenade_incendiary/w_incendiary_grenade.vmdl";

	public static Model LoadWeaponModelOrFallback( string vmdlPath, string contextForLog, out bool usedFallbackGeometry )
		=> LoadWeaponModelOrFallback( vmdlPath, contextForLog, out usedFallbackGeometry, out _ );

	public static Model LoadWeaponModelOrFallback(
		string vmdlPath,
		string contextForLog,
		out bool usedFallbackGeometry,
		out bool usedBowStockFpPlaceholder )
	{
		usedFallbackGeometry = false;
		usedBowStockFpPlaceholder = false;

		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
		{
			usedFallbackGeometry = true;
			return LoadFallback( contextForLog, "(empty path)" );
		}

		var direct = Model.Load( vmdlPath );
		if ( IsUsableModel( direct ) )
			return direct;

		Log.Warning( $"[Aimbox] {contextForLog}: model missing or error ('{vmdlPath}'), using fallback." );
		usedFallbackGeometry = true;
		return LoadFallback( contextForLog, vmdlPath );
	}

	public static bool TryLoadWeaponWorldModel( string vmdlPath, string contextForLog, out Model worldModel )
	{
		worldModel = default;
		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
			return false;

		var m = Model.Load( vmdlPath );
		if ( IsUsableModel( m ) )
		{
			worldModel = m;
			return true;
		}

		Log.Warning( $"[Aimbox] {contextForLog}: world model missing or error ('{vmdlPath}'), using fallback." );
		worldModel = LoadFallback( contextForLog, vmdlPath );
		return IsUsableModel( worldModel );
	}

	public static bool UsesStockFpAnimatorSequences( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		var p = modelPath.Trim().Replace( '\\', '/' );
		return string.Equals( p, M4FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, Mp5FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, UspFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, ShotgunFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, SniperFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, BayonetM9FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, TrenchknifeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, CrowbarFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, HeGrenadeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, FlashGrenadeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, SmokeGrenadeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, DecoyGrenadeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, IncendiaryGrenadeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase );
	}

	public static bool IsMeleeFpModel( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		var p = modelPath.Trim().Replace( '\\', '/' );
		return string.Equals( p, BayonetM9FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, TrenchknifeFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, CrowbarFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase );
	}

	static Model LoadFallback( string context, string failed )
	{
		var fallback = Model.Load( FallbackWeaponModelPath );
		if ( IsUsableModel( fallback ) )
			return fallback;

		Log.Error( $"[Aimbox] {context}: fallback model failed too after '{failed}'." );
		return fallback;
	}

	static bool IsUsableModel( Model model ) => model.IsValid() && !model.IsError;
}
