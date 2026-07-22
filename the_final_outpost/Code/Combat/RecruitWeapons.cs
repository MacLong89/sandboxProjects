using Sandbox.Citizen;

namespace FinalOutpost;

public enum RecruitWeaponType
{
	Pistol,
	Smg,
	AssaultRifle,
	Shotgun,
	Sniper
}

/// <summary>
/// Static definition for a recruit's weapon: combat stats, third-person visuals
/// (world model + citizen hold type + body tint) and economy costs. Modeled after
/// aimbox's stock weapon roster so the presentation matches.
/// </summary>
public sealed class RecruitWeaponDef
{
	public RecruitWeaponType Type { get; init; }
	public string Name { get; init; }
	public string ShortName { get; init; }
	public string Icon { get; init; }

	// --- Third-person presentation ---
	public string WorldModel { get; init; }

	/// <summary>Fire SFX for this weapon, correlated to terraingen's own weapon sounds.</summary>
	public string FireSound { get; init; } = Sfx.Shoot;
	public CitizenAnimationHelper.HoldTypes Hold { get; init; }
	public Color BodyTint { get; init; }
	public Color TracerColor { get; init; }
	public float WeaponScale { get; init; } = 1.5f;

	// --- Combat ---
	public float Damage { get; init; }
	/// <summary>Design-unit range. Physical reach is <see cref="Range"/>.</summary>
	public float RangeDesign { get; init; }
	public float FireInterval { get; init; }
	public int Pellets { get; init; } = 1;
	public float SpreadDegrees { get; init; }
	public float DamagePerTrain { get; init; }

	public float BaseDps => CombatEconomy.Dps( Damage, FireInterval, Pellets );
	public float TrainDpsGain => CombatEconomy.Dps( DamagePerTrain, FireInterval, Pellets );
	/// <summary>World-space engagement range.</summary>
	public float Range => RangeDesign * GameConstants.RangeScale;

	/// <summary>Scrap for the first recruit of this weapon — fixed table.</summary>
	public double BaseCost => Type switch
	{
		RecruitWeaponType.Pistol => 50,
		RecruitWeaponType.Smg => 80,
		RecruitWeaponType.AssaultRifle => 90,
		RecruitWeaponType.Shotgun => 85,
		RecruitWeaponType.Sniper => 110,
		_ => 50
	};

	/// <summary>Extra scrap for each recruit you already own with this weapon.</summary>
	public double CostBump => Type switch
	{
		RecruitWeaponType.Pistol => 10,
		RecruitWeaponType.Smg or RecruitWeaponType.AssaultRifle or RecruitWeaponType.Shotgun => 12,
		RecruitWeaponType.Sniper => 15,
		_ => CombatEconomy.DefaultRepeatBump
	};

	/// <summary>Base train upgrade cost (grows with train level separately).</summary>
	public double TrainBaseCost => Type switch
	{
		RecruitWeaponType.Pistol => 35,
		RecruitWeaponType.Smg or RecruitWeaponType.Shotgun => 45,
		RecruitWeaponType.AssaultRifle => 50,
		RecruitWeaponType.Sniper => 60,
		_ => 40
	};

	/// <summary>First-copy recruit price. Live buy price is <see cref="DefenderManager.RecruitCost"/>.</summary>
	public double RecruitCost => BaseCost;

	/// <summary>Total per-shot damage a fully-fired volley deals (all pellets), for HUD/DPS hints.</summary>
	public float VolleyDamage( int trainLevel ) => (Damage + trainLevel * DamagePerTrain) * Pellets;
	public float Dps( int trainLevel = 0 ) => FireInterval > 0f ? VolleyDamage( trainLevel ) / FireInterval : 0f;
	public int UnlockNight => NightUnlocks.RecruitUnlockNight( this );
}

public static class RecruitWeapons
{
	/// <summary>Display / shop ordering, roughly cheapest to most expensive.</summary>
	public static readonly RecruitWeaponType[] Order =
	{
		RecruitWeaponType.Pistol,
		RecruitWeaponType.Smg,
		RecruitWeaponType.AssaultRifle,
		RecruitWeaponType.Shotgun,
		RecruitWeaponType.Sniper
	};

	public static readonly IReadOnlyDictionary<RecruitWeaponType, RecruitWeaponDef> All =
		new Dictionary<RecruitWeaponType, RecruitWeaponDef>
		{
			[RecruitWeaponType.Pistol] = new()
			{
				Type = RecruitWeaponType.Pistol,
				Name = "Pistol",
				ShortName = "USP",
				Icon = "sports_martial_arts",
				WorldModel = WeaponModels.PistolWorld,
				FireSound = Sfx.ShootPistol,
				Hold = CitizenAnimationHelper.HoldTypes.Pistol,
				BodyTint = new Color( 0.72f, 0.76f, 0.86f ),
				TracerColor = new Color( 1f, 0.95f, 0.7f ),
				WeaponScale = 1.15f,
				Damage = 7f,
				RangeDesign = 290f,
				FireInterval = 0.42f,
				Pellets = 1,
				DamagePerTrain = 2f
			},
			[RecruitWeaponType.Smg] = new()
			{
				Type = RecruitWeaponType.Smg,
				Name = "SMG",
				ShortName = "MP5",
				Icon = "bolt",
				WorldModel = WeaponModels.SmgWorld,
				FireSound = Sfx.ShootSmg,
				Hold = CitizenAnimationHelper.HoldTypes.Rifle,
				BodyTint = new Color( 0.55f, 0.85f, 0.9f ),
				TracerColor = new Color( 0.75f, 1f, 1f ),
				WeaponScale = 1.4f,
				Damage = 4.5f,
				RangeDesign = 300f,
				FireInterval = 0.12f,
				Pellets = 1,
				SpreadDegrees = 2.5f,
				DamagePerTrain = 1.4f
			},
			[RecruitWeaponType.AssaultRifle] = new()
			{
				Type = RecruitWeaponType.AssaultRifle,
				Name = "Assault Rifle",
				ShortName = "M4A1",
				Icon = "shield",
				WorldModel = WeaponModels.RifleWorld,
				FireSound = Sfx.Shoot,
				Hold = CitizenAnimationHelper.HoldTypes.Rifle,
				BodyTint = new Color( 0.6f, 0.75f, 0.95f ),
				TracerColor = new Color( 1f, 0.9f, 0.45f ),
				WeaponScale = 1.5f,
				Damage = 9f,
				RangeDesign = 370f,
				FireInterval = 0.26f,
				Pellets = 1,
				SpreadDegrees = 1.2f,
				DamagePerTrain = 2.6f
			},
			[RecruitWeaponType.Shotgun] = new()
			{
				Type = RecruitWeaponType.Shotgun,
				Name = "Shotgun",
				ShortName = "Spaghelli",
				Icon = "grain",
				WorldModel = WeaponModels.ShotgunWorld,
				FireSound = Sfx.ShootShotgun,
				Hold = CitizenAnimationHelper.HoldTypes.Shotgun,
				BodyTint = new Color( 0.9f, 0.7f, 0.45f ),
				TracerColor = new Color( 1f, 0.6f, 0.25f ),
				WeaponScale = 1.5f,
				Damage = 4f,
				RangeDesign = 210f,
				FireInterval = 0.85f,
				Pellets = 7,
				SpreadDegrees = 9f,
				DamagePerTrain = 1.1f
			},
			[RecruitWeaponType.Sniper] = new()
			{
				Type = RecruitWeaponType.Sniper,
				Name = "Sniper",
				ShortName = "M700",
				Icon = "center_focus_strong",
				WorldModel = WeaponModels.SniperWorld,
				FireSound = Sfx.ShootSniper,
				Hold = CitizenAnimationHelper.HoldTypes.Rifle,
				BodyTint = new Color( 0.5f, 0.6f, 0.5f ),
				TracerColor = new Color( 0.85f, 0.95f, 1f ),
				WeaponScale = 1.65f,
				Damage = 42f,
				RangeDesign = 560f,
				FireInterval = 1.35f,
				Pellets = 1,
				SpreadDegrees = 0.2f,
				DamagePerTrain = 12f
			}
		};

	public static RecruitWeaponDef Get( RecruitWeaponType type ) =>
		All.TryGetValue( type, out var def ) ? def : All[RecruitWeaponType.AssaultRifle];

	public static RecruitWeaponType Parse( string value )
	{
		if ( !string.IsNullOrWhiteSpace( value ) && Enum.TryParse<RecruitWeaponType>( value, out var t ) && All.ContainsKey( t ) )
			return t;

		return RecruitWeaponType.AssaultRifle;
	}
}
