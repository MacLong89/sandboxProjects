namespace Sandbox;

public sealed record AimboxAttachmentChallenge(
	AimboxWeaponId Weapon,
	AimboxAttachmentId Attachment,
	string Label,
	int RequiredMasteryLevel );

public sealed record AimboxPerkDefinition(
	AimboxPerkId Id,
	string Name,
	int UnlockLevel,
	string Description );

public sealed record AimboxKillstreakDefinition(
	AimboxKillstreakId Id,
	string Name,
	int KillThreshold,
	int UnlockLevel,
	string Description );

public static class AimboxMw2Catalog
{
	public const int MaxRank = 70;
	public const int LoadoutCount = 5;

	public static readonly AimboxWeaponId[] PrimaryWeapons =
	[
		AimboxWeaponId.M4A1,
		AimboxWeaponId.Mp5,
		AimboxWeaponId.M700,
		AimboxWeaponId.SpaghelliM4
	];

	public static readonly AimboxWeaponId[] SecondaryWeapons =
	[
		AimboxWeaponId.Usp
	];

	public static readonly IReadOnlyList<AimboxAttachmentChallenge> AttachmentChallenges =
		BuildAttachmentChallenges();

	public static IReadOnlyList<AimboxAttachmentChallenge> BuildAttachmentChallenges()
	{
		var challenges = new List<AimboxAttachmentChallenge>();
		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
		{
			var attachments = AimboxAttachmentCatalog.GetCompatibleAttachments( weapon );
			for ( var i = 0; i < attachments.Count; i++ )
			{
				var attachment = attachments[i];
				challenges.Add( new(
					weapon,
					attachment,
					AimboxAttachmentCatalog.Label( attachment ),
					Math.Clamp( 2 + i, 2, AimboxWeaponProgressionSystem.MaxMasteryLevel ) ) );
			}
		}

		return challenges;
	}

	public static readonly IReadOnlyList<AimboxPerkDefinition> Perks =
	[
		new( AimboxPerkId.Lightweight, "Lightweight", 2, "+7% movement speed" ),
		new( AimboxPerkId.StoppingPower, "Stopping Power", 3, "+25% bullet damage" ),
		new( AimboxPerkId.Scavenger, "Scavenger", 5, "Refill ammo on kill" ),
		new( AimboxPerkId.SleightOfHand, "Sleight of Hand", 7, "Faster reloads and weapon swaps" ),
		new( AimboxPerkId.Marathon, "Marathon", 10, "Unlimited sprint" ),
		new( AimboxPerkId.Ninja, "Ninja", 12, "75% quieter footsteps for nearby enemies" )
	];

	public static bool IsKillstreakImplemented( AimboxKillstreakId id ) => id switch
	{
		AimboxKillstreakId.Uav or AimboxKillstreakId.CarePackage or AimboxKillstreakId.PredatorMissile => true,
		_ => false
	};

	public static IEnumerable<AimboxKillstreakDefinition> ImplementedKillstreaks =>
		Killstreaks.Where( k => IsKillstreakImplemented( k.Id ) );

	public static readonly IReadOnlyList<AimboxKillstreakDefinition> Killstreaks =
	[
		new( AimboxKillstreakId.Uav, "UAV", 3, 3, "Reveal enemies for 30 seconds" ),
		new( AimboxKillstreakId.CarePackage, "Care Package", 4, 5, "Drop a random unlocked primary" ),
		new( AimboxKillstreakId.PredatorMissile, "Predator Missile", 5, 8, "Guided air strike" ),
		new( AimboxKillstreakId.CounterUav, "Counter-UAV", 4, 15, "Block enemy UAVs" ),
		new( AimboxKillstreakId.SentryGun, "Sentry Gun", 5, 20, "Deploy automated turret" )
	];

	public static AimboxPerkDefinition GetPerk( AimboxPerkId id ) =>
		Perks.FirstOrDefault( p => p.Id == id );

	public static AimboxKillstreakDefinition GetKillstreak( AimboxKillstreakId id ) =>
		Killstreaks.FirstOrDefault( k => k.Id == id );

	public static bool TryGetKillstreak( AimboxKillstreakId id, out AimboxKillstreakDefinition definition )
	{
		definition = GetKillstreak( id );
		return definition is not null && id != AimboxKillstreakId.None;
	}

	public static IEnumerable<AimboxAttachmentChallenge> GetChallengesForWeapon( AimboxWeaponId weapon ) =>
		AttachmentChallenges.Where( c => c.Weapon == weapon );

	public static AimboxWeaponId DefaultPrimaryForRank( int rank ) =>
		PrimaryWeapons.LastOrDefault( w => AimboxWeapons.Get( w ).UnlockLevel <= rank, AimboxWeaponId.M4A1 );

	public static AimboxWeaponId DefaultSecondaryForRank( int rank ) =>
		SecondaryWeapons.LastOrDefault( w => AimboxWeapons.Get( w ).UnlockLevel <= rank, AimboxWeaponId.Usp );

	public static bool IsPrimaryWeapon( AimboxWeaponId id ) => PrimaryWeapons.Contains( id );

	public static bool IsSecondaryWeapon( AimboxWeaponId id ) => SecondaryWeapons.Contains( id );
}

public static class AimboxUnlockService
{
	public static bool BypassProgressionLocks =>
		AimboxGame.Instance?.DevUnlockAllProgressionActive == true;

	public static void ApplyDebugUnlockAll( AimboxPlayerData data )
	{
		if ( data is null || !BypassProgressionLocks )
			return;

		data.PlayerLevel = Math.Max( data.PlayerLevel, AimboxMw2Catalog.MaxRank );

		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
		{
			var weaponData = data.GetWeapon( weapon );
			foreach ( var attachment in AimboxAttachmentCatalog.GetCompatibleAttachments( weapon ) )
				weaponData.UnlockedAttachments.Add( attachment );
		}
	}

	/// <summary>Strip debug/migration unlocks and re-sync attachment mastery gates from saved weapon levels.</summary>
	public static void EnforceSavedProgressionLocks( AimboxPlayerData data )
	{
		if ( data is null || BypassProgressionLocks )
			return;

		data.Validate();

		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
		{
			var weaponData = data.GetWeapon( weapon );
			var earned = new HashSet<AimboxAttachmentId>();
			foreach ( var challenge in AimboxMw2Catalog.GetChallengesForWeapon( weapon ) )
			{
				if ( weaponData.Level >= challenge.RequiredMasteryLevel )
					earned.Add( challenge.Attachment );
			}

			weaponData.UnlockedAttachments = earned;
			weaponData.EquippedAttachments = weaponData.EquippedAttachments
				.Where( a => earned.Contains( a ) )
				.ToHashSet();
		}
	}

	public static bool IsWeaponUnlocked( AimboxPlayerData data, AimboxWeaponId id )
	{
		if ( !AimboxWeapons.All.TryGetValue( id, out var def ) )
			return false;

		if ( BypassProgressionLocks )
			return true;

		return data.PlayerLevel >= def.UnlockLevel;
	}

	public static bool IsPrimaryUnlocked( AimboxPlayerData data, AimboxWeaponId id ) =>
		AimboxMw2Catalog.IsPrimaryWeapon( id ) && IsWeaponUnlocked( data, id );

	/// <summary>Whether the saved primary may be added to the in-match weapon inventory.</summary>
	public static bool ShouldEquipPrimaryInMatch( AimboxPlayerData data, AimboxWeaponId primary )
	{
		if ( !AimboxMw2Catalog.IsPrimaryWeapon( primary ) )
			return false;

		// First three matches: always grant a primary so new players are not pistol-only vs rifle bots.
		if ( AimboxXpSystem.IsFirstSession( data ) )
			return true;

		return IsWeaponUnlocked( data, primary );
	}

	/// <summary>Match inventory gate — bayonet + first-session starter primaries allowed when locked.</summary>
	public static bool CanEquipWeaponInMatch( AimboxPlayerData data, AimboxWeaponId id )
	{
		if ( id == AimboxWeaponId.M9Bayonet )
			return true;

		if ( IsWeaponUnlocked( data, id ) )
			return true;

		return AimboxXpSystem.IsFirstSession( data ) && AimboxMw2Catalog.IsPrimaryWeapon( id );
	}

	public static bool IsPerkUnlocked( AimboxPlayerData data, AimboxPerkId id )
	{
		if ( id == AimboxPerkId.None )
			return false;

		if ( BypassProgressionLocks )
			return true;

		var def = AimboxMw2Catalog.Perks.FirstOrDefault( p => p.Id == id );
		return def is not null && data.PlayerLevel >= def.UnlockLevel;
	}

	public static bool IsKillstreakUnlocked( AimboxPlayerData data, AimboxKillstreakId id )
	{
		if ( id == AimboxKillstreakId.None )
			return false;

		if ( !AimboxMw2Catalog.IsKillstreakImplemented( id ) )
			return false;

		if ( BypassProgressionLocks )
			return true;

		var def = AimboxMw2Catalog.Killstreaks.FirstOrDefault( k => k.Id == id );
		return def is not null && data.PlayerLevel >= def.UnlockLevel;
	}

	public static bool IsAttachmentUnlocked( AimboxWeaponData weaponData, AimboxAttachmentId attachment ) =>
		IsAttachmentUnlocked( weaponData.Weapon, weaponData, attachment );

	public static bool IsAttachmentUnlocked(
		AimboxWeaponId weapon,
		AimboxWeaponData weaponData,
		AimboxAttachmentId attachment )
	{
		if ( !AimboxAttachmentCatalog.IsCompatible( weapon, attachment ) )
			return false;

		if ( BypassProgressionLocks )
			return true;

		return weaponData.UnlockedAttachments.Contains( attachment );
	}

	public static bool AreAttachmentsValid( AimboxWeaponId weapon, AimboxWeaponData weaponData, IEnumerable<AimboxAttachmentId> equipped ) =>
		equipped.All( a => IsAttachmentUnlocked( weapon, weaponData, a ) );

	public static AimboxWeaponId ResolveWeapon( AimboxPlayerData data, AimboxWeaponId requested, IEnumerable<AimboxWeaponId> pool )
	{
		var options = pool as IReadOnlyList<AimboxWeaponId> ?? pool.ToArray();
		if ( options.Count == 0 )
			return requested;

		if ( options.Contains( requested ) && IsWeaponUnlocked( data, requested ) )
			return requested;

		var unlocked = options.Where( w => IsWeaponUnlocked( data, w ) ).ToList();
		if ( unlocked.Count > 0 )
			return unlocked[^1];

		var fallback = options.Any( AimboxMw2Catalog.IsSecondaryWeapon )
			? AimboxMw2Catalog.DefaultSecondaryForRank( data.PlayerLevel )
			: AimboxMw2Catalog.DefaultPrimaryForRank( data.PlayerLevel );

		return options.Contains( fallback ) ? fallback : options[0];
	}

	public static List<AimboxAttachmentId> SanitizeAttachments( AimboxPlayerData data, AimboxWeaponId weapon, IEnumerable<AimboxAttachmentId> attachments )
	{
		if ( !AimboxAttachmentCatalog.SupportsAttachments( weapon ) )
			return [];

		var weaponData = data.GetWeapon( weapon );
		var sanitized = attachments
			.Where( a => AimboxAttachmentCatalog.IsCompatible( weapon, a ) )
			.Where( a => IsAttachmentUnlocked( weapon, weaponData, a ) )
			.ToList();

		return AimboxAttachmentCatalog.EnforceExclusivity( sanitized );
	}

	public static AimboxPerkId ResolvePerk( AimboxPlayerData data, AimboxPerkId requested, AimboxPerkId fallback )
	{
		if ( requested != AimboxPerkId.None && IsPerkUnlocked( data, requested ) )
			return requested;

		if ( fallback != AimboxPerkId.None && IsPerkUnlocked( data, fallback ) )
			return fallback;

		return AimboxPerkId.None;
	}

	public static AimboxKillstreakId ResolveKillstreak( AimboxPlayerData data, AimboxKillstreakId requested, AimboxKillstreakId fallback )
	{
		if ( requested != AimboxKillstreakId.None
		     && AimboxMw2Catalog.IsKillstreakImplemented( requested )
		     && IsKillstreakUnlocked( data, requested ) )
			return requested;

		if ( fallback != AimboxKillstreakId.None
		     && AimboxMw2Catalog.IsKillstreakImplemented( fallback )
		     && IsKillstreakUnlocked( data, fallback ) )
			return fallback;

		return AimboxKillstreakId.None;
	}
}
