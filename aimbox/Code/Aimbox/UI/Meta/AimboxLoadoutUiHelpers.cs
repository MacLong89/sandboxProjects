namespace Sandbox;

public enum AimboxLoadoutSlotKind
{
	Primary,
	Secondary,
	Lethal,
	Tactical
}

public enum AimboxLoadoutWeaponCategory
{
	AssaultRifles,
	Smgs,
	Snipers,
	Shotguns,
	Special,
	Secondaries
}

public static class AimboxLoadoutUiHelpers
{
	public static readonly IReadOnlyList<AimboxLoadoutWeaponCategory> PrimaryCategories =
	[
		AimboxLoadoutWeaponCategory.AssaultRifles,
		AimboxLoadoutWeaponCategory.Smgs,
		AimboxLoadoutWeaponCategory.Snipers,
		AimboxLoadoutWeaponCategory.Shotguns,
		AimboxLoadoutWeaponCategory.Special
	];

	public static readonly IReadOnlyList<(string Id, AimboxWeaponId WeaponId)> LethalGrenades =
	[
		( "Frag", AimboxWeaponId.HeGrenade ),
		( "Incendiary", AimboxWeaponId.IncendiaryGrenade )
	];

	public static readonly IReadOnlyList<(string Id, AimboxWeaponId WeaponId)> TacticalGrenades =
	[
		( "Flash", AimboxWeaponId.FlashGrenade ),
		( "Smoke", AimboxWeaponId.SmokeGrenade ),
		( "Decoy", AimboxWeaponId.DecoyGrenade )
	];

	public static readonly IReadOnlyDictionary<AimboxLoadoutWeaponCategory, string> CategoryLabels =
		new Dictionary<AimboxLoadoutWeaponCategory, string>
		{
			[AimboxLoadoutWeaponCategory.AssaultRifles] = "ASSAULT RIFLES",
			[AimboxLoadoutWeaponCategory.Smgs] = "SMGS",
			[AimboxLoadoutWeaponCategory.Snipers] = "SNIPERS",
			[AimboxLoadoutWeaponCategory.Shotguns] = "SHOTGUNS",
			[AimboxLoadoutWeaponCategory.Special] = "SPECIAL",
			[AimboxLoadoutWeaponCategory.Secondaries] = "SECONDARIES"
		};

	public static AimboxLoadoutWeaponCategory CategoryForWeapon( AimboxWeaponId id )
	{
		return id switch
		{
			AimboxWeaponId.M4A1 => AimboxLoadoutWeaponCategory.AssaultRifles,
			AimboxWeaponId.SpaghelliM4 => AimboxLoadoutWeaponCategory.Shotguns,
			AimboxWeaponId.Mp5 => AimboxLoadoutWeaponCategory.Smgs,
			AimboxWeaponId.M700 => AimboxLoadoutWeaponCategory.Snipers,
			_ when AimboxMw2Catalog.SecondaryWeapons.Contains( id ) => AimboxLoadoutWeaponCategory.Secondaries,
			_ => AimboxLoadoutWeaponCategory.AssaultRifles
		};
	}

	public static IReadOnlyList<AimboxWeaponId> WeaponsInCategory( AimboxLoadoutWeaponCategory category )
	{
		var weapons = new List<AimboxWeaponId>();
		switch ( category )
		{
			case AimboxLoadoutWeaponCategory.AssaultRifles:
				AddPrimaryWeaponIfPresent( weapons, AimboxWeaponId.M4A1 );
				break;
			case AimboxLoadoutWeaponCategory.Smgs:
				AddPrimaryWeaponIfPresent( weapons, AimboxWeaponId.Mp5 );
				break;
			case AimboxLoadoutWeaponCategory.Snipers:
				AddPrimaryWeaponIfPresent( weapons, AimboxWeaponId.M700 );
				break;
			case AimboxLoadoutWeaponCategory.Shotguns:
				AddPrimaryWeaponIfPresent( weapons, AimboxWeaponId.SpaghelliM4 );
				break;
			case AimboxLoadoutWeaponCategory.Special:
				break;
			case AimboxLoadoutWeaponCategory.Secondaries:
				foreach ( var weapon in AimboxMw2Catalog.SecondaryWeapons )
					weapons.Add( weapon );
				break;
		}

		return weapons;
	}

	public static string DisplayWeaponName( AimboxWeaponId id ) => AimboxWeapons.Get( id ).Name.ToUpperInvariant();

	public static string GrenadeLabel( string grenadeId ) =>
		string.IsNullOrWhiteSpace( grenadeId ) ? "NONE" : grenadeId.Trim().ToUpperInvariant();

	public static AimboxWeaponId GrenadeWeaponId( string grenadeId ) =>
		AimboxGrenadeCatalog.ResolveLoadoutGrenade( grenadeId, AimboxWeaponId.HeGrenade );

	public static bool IsLethalGrenadeId( string grenadeId )
	{
		var trimmed = grenadeId?.Trim();
		foreach ( var grenade in LethalGrenades )
		{
			if ( grenade.Id.Equals( trimmed, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public static bool IsTacticalGrenadeId( string grenadeId )
	{
		var trimmed = grenadeId?.Trim();
		foreach ( var grenade in TacticalGrenades )
		{
			if ( grenade.Id.Equals( trimmed, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public static IEnumerable<(string Id, AimboxWeaponId WeaponId)> GrenadesForSlot( AimboxLoadoutSlotKind slot ) =>
		slot switch
		{
			AimboxLoadoutSlotKind.Lethal => LethalGrenades,
			AimboxLoadoutSlotKind.Tactical => TacticalGrenades,
			_ => []
		};

	public static string SlotPickerTitle( AimboxLoadoutSlotKind slot ) => slot switch
	{
		AimboxLoadoutSlotKind.Primary => "PRIMARY WEAPONS",
		AimboxLoadoutSlotKind.Secondary => "SECONDARY WEAPONS",
		AimboxLoadoutSlotKind.Lethal => "LETHAL GRENADES",
		AimboxLoadoutSlotKind.Tactical => "TACTICAL GRENADES",
		_ => "LOADOUT"
	};

	public static string LoadoutSlotLabel( int index, AimboxLoadoutData loadout ) =>
		$"CUSTOM LOADOUT {index + 1}";

	public static string PlayerDisplayName( AimboxPlayerController player )
	{
		var account = player?.AccountId ?? "offline";
		if ( account.Equals( "offline", StringComparison.OrdinalIgnoreCase ) )
			return "NOVA_07";

		return account.Length > 16 ? account[..16].ToUpperInvariant() : account.ToUpperInvariant();
	}

	public static string XpLabel( AimboxPlayerData data )
	{
		if ( data.PlayerLevel >= AimboxMw2Catalog.MaxRank )
			return $"{data.TotalXp:N0} XP · MAX";

		var floor = AimboxXpSystem.XpForLevel( data.PlayerLevel );
		var next = AimboxXpSystem.XpForLevel( data.PlayerLevel + 1 );
		return $"{data.TotalXp - floor:N0} / {next - floor:N0} XP";
	}

	public static string XpFillWidth( AimboxPlayerData data )
	{
		if ( data.PlayerLevel >= AimboxMw2Catalog.MaxRank )
			return "100%";

		return $"{(int)(AimboxXpSystem.LevelProgress( data ) * 100f)}%";
	}

	public static int WeaponMasteryLevel( AimboxPlayerData data, AimboxWeaponId weapon ) =>
		data?.GetWeapon( weapon ).Level ?? 1;

	public sealed record WeaponStat( string Label, int Value, float Fill );

	public static IReadOnlyList<WeaponStat> BuildWeaponStats( AimboxWeaponDefinition def )
	{
		var damage = Normalize( def.Damage, 15f, 65f );
		var fireRate = Normalize( 1f / MathF.Max( 0.05f, def.FireDelay ), 1f, 12f );
		var range = Normalize( def.Range, 2000f, 14000f );
		var accuracy = Normalize( 1f / MathF.Max( 0.01f, def.SpreadDegrees ), 2f, 30f );
		var mobility = Normalize( def.ReloadSeconds, 2.5f, 0.9f, invert: true );
		var control = Normalize( def.SpreadDegrees * def.FireDelay, 0.005f, 0.08f, invert: true );

		return
		[
			new( "DAMAGE", ScaleStat( damage ), damage ),
			new( "FIRE RATE", ScaleStat( fireRate ), fireRate ),
			new( "RANGE", ScaleStat( range ), range ),
			new( "ACCURACY", ScaleStat( accuracy ), accuracy ),
			new( "MOBILITY", ScaleStat( mobility ), mobility ),
			new( "CONTROL", ScaleStat( control ), control )
		];
	}

	static float Normalize( float value, float min, float max, bool invert = false )
	{
		var t = Math.Clamp( (value - min) / MathF.Max( 0.001f, max - min ), 0f, 1f );
		return invert ? 1f - t : t;
	}

	static int ScaleStat( float fill ) => Math.Clamp( (int)MathF.Round( 20f + fill * 80f ), 1, 100 );

	static void AddPrimaryWeaponIfPresent( List<AimboxWeaponId> weapons, AimboxWeaponId weapon )
	{
		foreach ( var candidate in AimboxMw2Catalog.PrimaryWeapons )
		{
			if ( candidate == weapon )
			{
				weapons.Add( candidate );
				return;
			}
		}
	}
}
