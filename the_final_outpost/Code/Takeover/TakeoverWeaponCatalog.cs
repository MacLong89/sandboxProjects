namespace FinalOutpost;

/// <summary>
/// Aimbox-style feel (mag, reload, spread, fire delay, sounds) with outpost recruit damage / range.
/// </summary>
public sealed class TakeoverWeaponDef
{
	public RecruitWeaponType RecruitType { get; init; }
	public string Name { get; init; }
	public int MagazineSize { get; init; }
	public int ReserveAmmo { get; init; }
	public float Damage { get; init; }
	public float HeadshotMultiplier { get; init; } = 1.6f;
	public float Range { get; init; }
	public float FireDelay { get; init; }
	public float ReloadSeconds { get; init; }
	public float SpreadDegrees { get; init; }
	public float PelletSpreadDegrees { get; init; }
	public float AdsSpreadMultiplier { get; init; } = 0.58f;
	public int Pellets { get; init; } = 1;
	public bool UsesShellReload { get; init; }
	public string ViewModelPath { get; init; }
	public string WorldModelPath { get; init; }
	public string FireSound { get; init; }
	public string ReloadSound { get; init; }
	public Color TracerColor { get; init; } = new( 1f, 0.94f, 0.58f );
}

public static class TakeoverWeaponCatalog
{
	public const string ArmsPath = "models/first_person/v_first_person_arms_human.vmdl";
	/// <summary>Ship-safe fallback — never models/dev (editor-only).</summary>
	public static Model FallbackModel => MeshPrimitives.Box;

	public const string UspView = "models/weapons/sbox_pistol_usp/v_usp.vmdl";
	public const string Mp5View = "models/weapons/sbox_smg_mp5/v_mp5.vmdl";
	public const string M4View = "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl";
	public const string ShotgunView = "models/weapons/sbox_shotgun_spaghellim4/v_spaghellim4.vmdl";
	public const string SniperView = "models/weapons/sbox_sniper_m700/v_m700.vmdl";

	/// <summary>Aimbox look scale (see AimboxAdsSightTuning.DefaultLookScale).</summary>
	public const float DefaultLookScale = 0.04f;
	public const float SniperLookMultiplier = 0.35f;
	/// <summary>Aimbox red-dot / iron ADS look damp (slightly slower than hip).</summary>
	public const float IronSightLookMultiplier = 0.82f;

	/// <summary>Build a takeover weapon using aimbox mag/reload/spread/fire-delay + recruit damage/range.</summary>
	public static TakeoverWeaponDef BuildForRecruit( RecruitWeaponType type, float perShotDamage, float range )
	{
		var recruit = RecruitWeapons.Get( type );
		return type switch
		{
			RecruitWeaponType.Pistol => new TakeoverWeaponDef
			{
				RecruitType = type,
				Name = recruit.Name,
				MagazineSize = 12,
				ReserveAmmo = 48,
				Damage = perShotDamage,
				HeadshotMultiplier = 1.6f,
				Range = MathF.Max( range, 6000f ),
				FireDelay = 0.22f,
				ReloadSeconds = 1.35f,
				SpreadDegrees = 0.22f,
				Pellets = 1,
				ViewModelPath = UspView,
				WorldModelPath = WeaponModels.PistolWorld,
				FireSound = TakeoverSfx.FoPistol,
				ReloadSound = TakeoverSfx.M4Reload,
				TracerColor = new Color( 1f, 0.94f, 0.58f )
			},
			RecruitWeaponType.Smg => new TakeoverWeaponDef
			{
				RecruitType = type,
				Name = recruit.Name,
				MagazineSize = 34,
				ReserveAmmo = 136,
				Damage = perShotDamage,
				HeadshotMultiplier = 1.6f,
				Range = MathF.Max( range, 6000f ),
				FireDelay = 0.065f,
				ReloadSeconds = 1.55f,
				SpreadDegrees = 0.24f,
				Pellets = 1,
				ViewModelPath = Mp5View,
				WorldModelPath = WeaponModels.SmgWorld,
				FireSound = TakeoverSfx.FoSmg,
				ReloadSound = TakeoverSfx.M4Reload,
				TracerColor = new Color( 1f, 0.94f, 0.58f )
			},
			RecruitWeaponType.Shotgun => new TakeoverWeaponDef
			{
				RecruitType = type,
				Name = recruit.Name,
				MagazineSize = 8,
				ReserveAmmo = 32,
				Damage = perShotDamage,
				HeadshotMultiplier = 1.6f,
				Range = MathF.Max( range, 2600f ),
				FireDelay = 0.75f,
				ReloadSeconds = 2.4f,
				SpreadDegrees = 5.6f,
				PelletSpreadDegrees = 3.5f,
				Pellets = Math.Max( 8, recruit.Pellets ),
				UsesShellReload = true,
				ViewModelPath = ShotgunView,
				WorldModelPath = WeaponModels.ShotgunWorld,
				FireSound = TakeoverSfx.FoShotgun,
				ReloadSound = TakeoverSfx.ShotgunReload,
				TracerColor = new Color( 1f, 0.94f, 0.58f )
			},
			RecruitWeaponType.Sniper => new TakeoverWeaponDef
			{
				RecruitType = type,
				Name = recruit.Name,
				MagazineSize = 5,
				ReserveAmmo = 25,
				Damage = perShotDamage,
				HeadshotMultiplier = 2.2f,
				Range = MathF.Max( range, 10000f ),
				FireDelay = 1.05f,
				ReloadSeconds = 2.65f,
				SpreadDegrees = 0.08f,
				AdsSpreadMultiplier = 0.58f,
				Pellets = 1,
				ViewModelPath = SniperView,
				WorldModelPath = WeaponModels.SniperWorld,
				FireSound = TakeoverSfx.FoSniper,
				ReloadSound = TakeoverSfx.M4Reload,
				TracerColor = new Color( 1f, 0.94f, 0.58f )
			},
			_ => new TakeoverWeaponDef
			{
				RecruitType = RecruitWeaponType.AssaultRifle,
				Name = recruit.Name,
				MagazineSize = 30,
				ReserveAmmo = 120,
				Damage = perShotDamage,
				HeadshotMultiplier = 1.6f,
				Range = MathF.Max( range, 6000f ),
				FireDelay = 0.095f,
				ReloadSeconds = 1.75f,
				SpreadDegrees = 0.18f,
				Pellets = 1,
				ViewModelPath = M4View,
				WorldModelPath = WeaponModels.RifleWorld,
				FireSound = TakeoverSfx.FoRifle,
				ReloadSound = TakeoverSfx.M4Reload,
				TracerColor = new Color( 1f, 0.94f, 0.58f )
			}
		};
	}

	public static Model LoadModel( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return FallbackModel;

		// Prefer the mounted-package cache so published clients match the editor.
		var cached = WeaponModelLoader.Instance?.Get( path );
		if ( cached is not null && !cached.IsError )
			return cached;

		var m = AssetSafe.Model( path );
		if ( m is not null )
			return m;

		return FallbackModel;
	}

	public static bool UsesStockFpAnimator( string path ) =>
		path is UspView or Mp5View or M4View or ShotgunView or SniperView;
}
