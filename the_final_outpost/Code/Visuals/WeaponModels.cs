namespace FinalOutpost;

/// <summary>
/// Recruit weapon world models from the mounted <c>facepunch.sboxweapons</c> package.
/// </summary>
public static class WeaponModels
{
	public const string PistolWorld = "models/weapons/sbox_pistol_usp/w_usp.vmdl";
	public const string SmgWorld = "models/weapons/sbox_smg_mp5/w_mp5.vmdl";
	public const string RifleWorld = "models/weapons/sbox_assault_m4a1/w_m4a1.vmdl";
	public const string ShotgunWorld = "models/weapons/sbox_shotgun_spaghellim4/w_spaghellim4.vmdl";
	public const string SniperWorld = "models/weapons/sbox_sniper_m700/w_m700.vmdl";

	public static readonly string[] RequiredWorldModels =
	{
		PistolWorld,
		SmgWorld,
		RifleWorld,
		ShotgunWorld,
		SniperWorld
	};

	public static Model Load( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return MeshPrimitives.Box;

		var cached = WeaponModelLoader.Instance?.Get( path );
		if ( cached is not null && !cached.IsError )
			return cached;

		// Avoid Model.Load here — unmounted paths spam ERROR_FILEOPEN in the console.
		return MeshPrimitives.Box;
	}
}
