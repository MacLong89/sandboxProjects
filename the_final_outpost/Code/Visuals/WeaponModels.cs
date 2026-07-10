namespace FinalOutpost;

/// <summary>
/// Weapon world-model paths borrowed from the stock <c>facepunch.sboxweapons</c> package
/// (same assets aimbox uses for its third-person weapons), with a safe box fallback so
/// nothing crashes before the package finishes mounting.
/// </summary>
public static class WeaponModels
{
	public const string PistolWorld = "models/weapons/sbox_pistol_usp/w_usp.vmdl";
	public const string SmgWorld = "models/weapons/sbox_smg_mp5/w_mp5.vmdl";
	public const string RifleWorld = "models/weapons/sbox_assault_m4a1/w_m4a1.vmdl";
	public const string ShotgunWorld = "models/weapons/sbox_shotgun_spaghellim4/w_spaghellim4.vmdl";
	public const string SniperWorld = "models/weapons/sbox_sniper_m700/w_m700.vmdl";

	public static Model Load( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return MeshPrimitives.Box;

		try
		{
			var model = Model.Load( path );
			if ( model is not null && !model.IsError )
				return model;
		}
		catch
		{
			// fall through to placeholder
		}

		return MeshPrimitives.Box;
	}
}
