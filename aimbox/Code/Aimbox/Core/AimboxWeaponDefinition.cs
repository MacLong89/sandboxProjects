namespace Sandbox;

public sealed class AimboxWeaponDefinition
{
	public AimboxWeaponId Id { get; init; }
	public string Name { get; init; }
	public int UnlockLevel { get; init; }
	public int MagazineSize { get; init; }
	public int ReserveAmmo { get; init; }
	public float Damage { get; init; }
	public float HeadshotMultiplier { get; init; } = 1.6f;
	public float Range { get; init; } = 6000f;
	public float FireDelay { get; init; } = 0.1f;
	public float ReloadSeconds { get; init; } = 1.8f;
	public float SpreadDegrees { get; init; } = 1f;
	public float PelletSpreadDegrees { get; init; }
	public float AdsSpreadMultiplier { get; init; } = 0.58f;
	public float FalloffStart { get; init; } = 1400f;
	public float FalloffEnd { get; init; } = 5200f;
	public int Pellets { get; init; } = 1;
	public bool IsMelee { get; init; }
	public bool IsBow { get; init; }
	public bool IsGrenade { get; init; }
	public string ViewModelPath { get; init; }
	public string WorldModelPath { get; init; }
	public AimboxAttachmentId[] AttachmentUnlocks { get; init; } = [];

	public float DamageAtDistance( float distance )
	{
		if ( IsMelee )
			return Damage;

		if ( distance <= FalloffStart )
			return Damage;

		var t = Math.Clamp( (distance - FalloffStart) / MathF.Max( 1f, FalloffEnd - FalloffStart ), 0f, 1f );
		return Damage.LerpTo( Damage * 0.55f, t );
	}
}

public static class AimboxWeapons
{
	public static readonly AimboxWeaponId[] SlotOrder =
	[
		AimboxWeaponId.M4A1,
		AimboxWeaponId.Mp5,
		AimboxWeaponId.Usp,
		AimboxWeaponId.M700,
		AimboxWeaponId.SpaghelliM4,
		AimboxWeaponId.M9Bayonet,
		AimboxWeaponId.Trenchknife,
		AimboxWeaponId.Crowbar,
		AimboxWeaponId.HeGrenade,
		AimboxWeaponId.FlashGrenade
	];

	public static readonly IReadOnlyDictionary<AimboxWeaponId, AimboxWeaponDefinition> All = new Dictionary<AimboxWeaponId, AimboxWeaponDefinition>
	{
		[AimboxWeaponId.M4A1] = new()
		{
			Id = AimboxWeaponId.M4A1,
			Name = "M4A1",
			UnlockLevel = 4,
			MagazineSize = 30,
			ReserveAmmo = 120,
			Damage = 24,
			FireDelay = 0.095f,
			ReloadSeconds = 1.75f,
			SpreadDegrees = 0.18f,
			ViewModelPath = AimboxWeaponResourceLoad.M4FirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.M4WorldModelPath,
			AttachmentUnlocks = AimboxAttachmentCatalog.GetCompatibleAttachments( AimboxWeaponId.M4A1 ).ToArray()
		},
		[AimboxWeaponId.Mp5] = new()
		{
			Id = AimboxWeaponId.Mp5,
			Name = "MP5",
			UnlockLevel = 2,
			MagazineSize = 34,
			ReserveAmmo = 136,
			Damage = 18,
			FireDelay = 0.065f,
			ReloadSeconds = 1.55f,
			SpreadDegrees = 0.24f,
			FalloffStart = 900,
			FalloffEnd = 3600,
			ViewModelPath = AimboxWeaponResourceLoad.Mp5FirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.Mp5WorldModelPath,
			AttachmentUnlocks = AimboxAttachmentCatalog.GetCompatibleAttachments( AimboxWeaponId.Mp5 ).ToArray()
		},
		[AimboxWeaponId.Usp] = new()
		{
			Id = AimboxWeaponId.Usp,
			Name = "USP",
			UnlockLevel = 0,
			MagazineSize = 12,
			ReserveAmmo = 48,
			Damage = 27,
			FireDelay = 0.22f,
			ReloadSeconds = 1.35f,
			SpreadDegrees = 0.22f,
			ViewModelPath = AimboxWeaponResourceLoad.UspFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.UspWorldModelPath,
			AttachmentUnlocks = AimboxAttachmentCatalog.GetCompatibleAttachments( AimboxWeaponId.Usp ).ToArray()
		},
		[AimboxWeaponId.M700] = new()
		{
			Id = AimboxWeaponId.M700,
			Name = "M700",
			UnlockLevel = 7,
			MagazineSize = 5,
			ReserveAmmo = 25,
			Damage = 82,
			HeadshotMultiplier = 2.2f,
			FireDelay = 1.05f,
			ReloadSeconds = 2.65f,
			SpreadDegrees = 0.08f,
			Range = 10000,
			FalloffStart = 4000,
			FalloffEnd = 9000,
			ViewModelPath = AimboxWeaponResourceLoad.SniperFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.SniperWorldModelPath,
			AttachmentUnlocks = AimboxAttachmentCatalog.GetCompatibleAttachments( AimboxWeaponId.M700 ).ToArray()
		},
		[AimboxWeaponId.SpaghelliM4] = new()
		{
			Id = AimboxWeaponId.SpaghelliM4,
			Name = "Spaghelli M4",
			UnlockLevel = 4,
			MagazineSize = 8,
			ReserveAmmo = 32,
			Damage = 13,
			HeadshotMultiplier = 1.25f,
			FireDelay = 0.75f,
			ReloadSeconds = 2.4f,
			SpreadDegrees = 5.6f,
			PelletSpreadDegrees = 3.5f,
			Range = 2600,
			FalloffStart = 450,
			FalloffEnd = 1800,
			Pellets = 8,
			ViewModelPath = AimboxWeaponResourceLoad.ShotgunFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.ShotgunWorldModelPath,
			AttachmentUnlocks = AimboxAttachmentCatalog.GetCompatibleAttachments( AimboxWeaponId.SpaghelliM4 ).ToArray()
		},
		[AimboxWeaponId.M9Bayonet] = new()
		{
			Id = AimboxWeaponId.M9Bayonet,
			Name = "M9 Bayonet",
			UnlockLevel = 1,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 55,
			FireDelay = 0.65f,
			Range = 95,
			ViewModelPath = AimboxWeaponResourceLoad.BayonetM9FirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.BayonetM9WorldModelPath,
			IsMelee = true
		},
		[AimboxWeaponId.Trenchknife] = new()
		{
			Id = AimboxWeaponId.Trenchknife,
			Name = "Trenchknife",
			UnlockLevel = 1,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 48,
			FireDelay = 0.55f,
			Range = 90,
			ViewModelPath = AimboxWeaponResourceLoad.TrenchknifeFirstPersonViewmodelPath,
			WorldModelPath = "",
			IsMelee = true
		},
		[AimboxWeaponId.Crowbar] = new()
		{
			Id = AimboxWeaponId.Crowbar,
			Name = "Crowbar",
			UnlockLevel = 1,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 42,
			FireDelay = 0.7f,
			Range = 100,
			ViewModelPath = AimboxWeaponResourceLoad.CrowbarFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.CrowbarWorldModelPath,
			IsMelee = true
		},
		[AimboxWeaponId.HeGrenade] = new()
		{
			Id = AimboxWeaponId.HeGrenade,
			Name = "HE Grenade",
			UnlockLevel = 0,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 580,
			FireDelay = 0.85f,
			Range = 100,
			ViewModelPath = AimboxWeaponResourceLoad.HeGrenadeFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.HeGrenadeWorldModelPath,
			IsGrenade = true
		},
		[AimboxWeaponId.FlashGrenade] = new()
		{
			Id = AimboxWeaponId.FlashGrenade,
			Name = "Flash Grenade",
			UnlockLevel = 0,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 0,
			FireDelay = 0.85f,
			Range = 100,
			ViewModelPath = AimboxWeaponResourceLoad.FlashGrenadeFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.FlashGrenadeWorldModelPath,
			IsGrenade = true
		},
		[AimboxWeaponId.SmokeGrenade] = new()
		{
			Id = AimboxWeaponId.SmokeGrenade,
			Name = "Smoke Grenade",
			UnlockLevel = 1,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 0,
			FireDelay = 0.85f,
			Range = 100,
			ViewModelPath = AimboxWeaponResourceLoad.SmokeGrenadeFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.SmokeGrenadeWorldModelPath,
			IsGrenade = true
		},
		[AimboxWeaponId.DecoyGrenade] = new()
		{
			Id = AimboxWeaponId.DecoyGrenade,
			Name = "Decoy Grenade",
			UnlockLevel = 1,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 0,
			FireDelay = 0.85f,
			Range = 100,
			ViewModelPath = AimboxWeaponResourceLoad.DecoyGrenadeFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.DecoyGrenadeWorldModelPath,
			IsGrenade = true
		},
		[AimboxWeaponId.IncendiaryGrenade] = new()
		{
			Id = AimboxWeaponId.IncendiaryGrenade,
			Name = "Incendiary Grenade",
			UnlockLevel = 1,
			MagazineSize = 1,
			ReserveAmmo = 0,
			Damage = 104,
			FireDelay = 0.85f,
			Range = 100,
			ViewModelPath = AimboxWeaponResourceLoad.IncendiaryGrenadeFirstPersonViewmodelPath,
			WorldModelPath = AimboxWeaponResourceLoad.IncendiaryGrenadeWorldModelPath,
			IsGrenade = true
		}
	};

	public static AimboxWeaponDefinition Get( AimboxWeaponId id ) => All[id];
}
