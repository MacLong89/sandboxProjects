namespace FinalOutpost;

/// <summary>
/// Recruit weapon world models. Prefer local/editor-mounted paths; fall back to
/// <see cref="WeaponModelLoader"/> cloud fetch (five small packages, not sboxweapons).
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
