namespace Sandbox;



public static class AimboxAttachmentCatalog

{

	public static readonly AimboxAttachmentId[] StandardAttachments =

	[

		AimboxAttachmentId.HoloSight,

		AimboxAttachmentId.RangedSight,

		AimboxAttachmentId.RaisedRedDot,

		AimboxAttachmentId.ExtendedMag,

		AimboxAttachmentId.ForegripStraight,

		AimboxAttachmentId.ForegripAngled,

		AimboxAttachmentId.Flashlight,

		AimboxAttachmentId.Suppressor

	];



	public static readonly AimboxWeaponId[] AttachmentCapableWeapons =

	[

		AimboxWeaponId.M4A1,

		AimboxWeaponId.Mp5,

		AimboxWeaponId.Usp,

		AimboxWeaponId.M700,

		AimboxWeaponId.SpaghelliM4

	];



	public static readonly AimboxAttachmentId[] RedDotStyleAttachments =

	[

		AimboxAttachmentId.HoloSight,

		AimboxAttachmentId.RaisedRedDot

	];



	static readonly AimboxAttachmentId[] M4Compatible =
	[
		AimboxAttachmentId.HoloSight,
		AimboxAttachmentId.RaisedRedDot,
		AimboxAttachmentId.RangedSight,
		AimboxAttachmentId.ForegripAngled,
		AimboxAttachmentId.ExtendedMag,
		AimboxAttachmentId.Suppressor
	];

	static readonly AimboxAttachmentId[] M700Compatible =
	[
		AimboxAttachmentId.RangedSight,
		AimboxAttachmentId.Suppressor
	];

	public static AimboxWeaponId NormalizeWeapon( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.AssaultRifle => AimboxWeaponId.M4A1,
		AimboxWeaponId.Smg => AimboxWeaponId.Mp5,
		AimboxWeaponId.Pistol => AimboxWeaponId.Usp,
		AimboxWeaponId.SniperRifle => AimboxWeaponId.M700,
		AimboxWeaponId.Shotgun => AimboxWeaponId.SpaghelliM4,
		AimboxWeaponId.Knife => AimboxWeaponId.M9Bayonet,
		_ => weapon
	};

	public static IReadOnlyList<AimboxAttachmentId> GetCompatibleAttachments( AimboxWeaponId weapon ) =>
		NormalizeWeapon( weapon ) switch
		{
			AimboxWeaponId.M4A1 => M4Compatible,
			AimboxWeaponId.Mp5 => [AimboxAttachmentId.Suppressor],
			AimboxWeaponId.Usp => [AimboxAttachmentId.Suppressor],
			AimboxWeaponId.M700 => M700Compatible,
			AimboxWeaponId.SpaghelliM4 => [AimboxAttachmentId.Suppressor],
			_ => Array.Empty<AimboxAttachmentId>()
		};

	public static bool IsCompatible( AimboxWeaponId weapon, AimboxAttachmentId attachment ) =>
		GetCompatibleAttachments( weapon ).Contains( attachment );

	public static bool SupportsAttachments( AimboxWeaponId weapon ) =>
		GetCompatibleAttachments( weapon ).Count > 0;

	public static bool UsesIntegratedM700Scope( AimboxWeaponId weapon ) =>
		NormalizeWeapon( weapon ) == AimboxWeaponId.M700;



	public static string Label( AimboxAttachmentId id ) => id switch

	{

		AimboxAttachmentId.HoloSight => "Holo Sight",

		AimboxAttachmentId.RangedSight => "Ranged Sight",

		AimboxAttachmentId.RaisedRedDot => "Raised Red Dot",

		AimboxAttachmentId.ExtendedMag => "Extended Mag",

		AimboxAttachmentId.ForegripStraight => "Foregrip (Straight)",

		AimboxAttachmentId.ForegripAngled => "Foregrip (Angled)",

		AimboxAttachmentId.Flashlight => "Flashlight",

		AimboxAttachmentId.Suppressor => "Suppressor",

		_ => id.ToString()

	};



	public static bool IsSight( AimboxAttachmentId id ) =>

		id is AimboxAttachmentId.HoloSight or AimboxAttachmentId.RangedSight or AimboxAttachmentId.RaisedRedDot;



	public static bool IsRedDotStyle( AimboxAttachmentId id ) =>

		id is AimboxAttachmentId.HoloSight or AimboxAttachmentId.RaisedRedDot;



	public static bool IsForegrip( AimboxAttachmentId id ) =>

		id is AimboxAttachmentId.ForegripStraight or AimboxAttachmentId.ForegripAngled;



	public static AimboxAdsSightMode? ResolveAdsMode( AimboxAttachmentId id ) => id switch

	{

		AimboxAttachmentId.RangedSight => AimboxAdsSightMode.SniperScope,

		AimboxAttachmentId.HoloSight or AimboxAttachmentId.RaisedRedDot => AimboxAdsSightMode.RedDot,

		_ => null

	};



	public static AimboxAttachmentId? ResolveEquippedSight( IEnumerable<AimboxAttachmentId> attachments )

	{

		if ( attachments is null )

			return null;



		foreach ( var attachment in attachments )

		{

			if ( attachment == AimboxAttachmentId.RangedSight )

				return attachment;

		}



		foreach ( var attachment in attachments )

		{

			if ( attachment == AimboxAttachmentId.HoloSight )

				return attachment;

		}



		foreach ( var attachment in attachments )

		{

			if ( attachment == AimboxAttachmentId.RaisedRedDot )

				return attachment;

		}



		return null;

	}



	public static List<AimboxAttachmentId> EnforceExclusivity( IEnumerable<AimboxAttachmentId> attachments )

	{

		var list = attachments?.Distinct().ToList() ?? [];

		if ( list.Count <= 1 )

			return list;



		var sights = list.Where( IsSight ).ToList();

		if ( sights.Count > 1 )

		{

			var keep = ResolveEquippedSight( sights ) ?? sights[0];

			foreach ( var sight in sights )

			{

				if ( sight != keep )

					list.Remove( sight );

			}

		}



		var grips = list.Where( IsForegrip ).ToList();

		if ( grips.Count > 1 )

		{

			var keep = grips[0];

			foreach ( var grip in grips )

			{

				if ( grip != keep )

					list.Remove( grip );

			}

		}



		return list;

	}



	public static List<AimboxAttachmentId> SanitizeForWeapon(

		AimboxWeaponId weapon,

		IEnumerable<AimboxAttachmentId> attachments )

	{

		weapon = NormalizeWeapon( weapon );

		if ( !SupportsAttachments( weapon ) )
			return [];

		var list = (attachments ?? [])
			.Where( a => IsCompatible( weapon, a ) )
			.ToList();



		return EnforceExclusivity( list );

	}



	public static void EnsureStandardAttachmentsUnlocked( AimboxWeaponData weaponData )
	{
		_ = weaponData;
	}



	public static AimboxAttachmentId? MigrateLegacyAttachment( int legacyValue ) => legacyValue switch

	{

		0 => AimboxAttachmentId.RaisedRedDot,

		1 => AimboxAttachmentId.ExtendedMag,

		2 => AimboxAttachmentId.Suppressor,

		3 => AimboxAttachmentId.ForegripStraight,

		_ => null

	};



	public static HashSet<AimboxAttachmentId> MigrateLegacyAttachments( IEnumerable<AimboxAttachmentId> attachments )

	{

		var migrated = new HashSet<AimboxAttachmentId>();

		foreach ( var attachment in attachments ?? [] )

		{

			if ( StandardAttachments.Contains( attachment ) )

			{

				migrated.Add( attachment );

				continue;

			}



			var legacy = MigrateLegacyAttachment( (int)attachment );

			if ( legacy.HasValue )

				migrated.Add( legacy.Value );

		}



		return migrated;

	}

}

