namespace Terraingen.Combat.Attachments;

using System.Collections.Generic;

public static class ThornsAttachmentCatalog
{
	public const int MaxSlotsPerWeapon = 3;

	/// <summary>Attachments hidden from loot, inventory catalog, and equip until re-enabled.</summary>
	public static readonly HashSet<ThornsAttachmentId> DisabledInGame =
	[
		ThornsAttachmentId.ForegripStraight,
		ThornsAttachmentId.Flashlight
	];

	public static bool IsEnabledInGame( ThornsAttachmentId attachment ) =>
		!DisabledInGame.Contains( attachment );

	public static readonly ThornsAttachmentId[] StandardAttachments =
	[
		ThornsAttachmentId.HoloSight,
		ThornsAttachmentId.RangedSight,
		ThornsAttachmentId.RaisedRedDot,
		ThornsAttachmentId.ExtendedMag,
		ThornsAttachmentId.ForegripAngled,
		ThornsAttachmentId.Suppressor
	];

	public static readonly ThornsAttachmentId[] RedDotStyleAttachments =
	[
		ThornsAttachmentId.HoloSight,
		ThornsAttachmentId.RaisedRedDot
	];

	static readonly ThornsAttachmentId[] M4Compatible =
	[
		ThornsAttachmentId.HoloSight,
		ThornsAttachmentId.RaisedRedDot,
		ThornsAttachmentId.RangedSight,
		ThornsAttachmentId.ForegripAngled,
		ThornsAttachmentId.ExtendedMag,
		ThornsAttachmentId.Suppressor
	];

	static readonly ThornsAttachmentId[] M700Compatible =
	[
		ThornsAttachmentId.RangedSight,
		ThornsAttachmentId.Suppressor
	];

	public static string NormalizeCombatWeaponId( string combatWeaponId ) => combatWeaponId?.Trim().ToLowerInvariant() switch
	{
		"m4" or "assault_rifle" => "m4",
		"mp5" or "smg" => "mp5",
		"usp" or "pistol" => "usp",
		"sniper" or "m700" => "sniper",
		"shotgun" or "spaghellim4" => "shotgun",
		_ => combatWeaponId?.Trim().ToLowerInvariant() ?? ""
	};

	public static IReadOnlyList<ThornsAttachmentId> GetCompatibleAttachments( string combatWeaponId ) =>
		NormalizeCombatWeaponId( combatWeaponId ) switch
		{
			"m4" => M4Compatible,
			"mp5" => [ThornsAttachmentId.Suppressor],
			"usp" => [ThornsAttachmentId.Suppressor],
			"sniper" => M700Compatible,
			"shotgun" => [ThornsAttachmentId.Suppressor],
			_ => Array.Empty<ThornsAttachmentId>()
		};

	public static bool IsCompatible( string combatWeaponId, ThornsAttachmentId attachment ) =>
		IsEnabledInGame( attachment ) && GetCompatibleAttachments( combatWeaponId ).Contains( attachment );

	public static bool SupportsAttachments( string combatWeaponId ) =>
		GetCompatibleAttachments( combatWeaponId ).Count > 0;

	public static bool UsesIntegratedSniperScope( string combatWeaponId ) =>
		NormalizeCombatWeaponId( combatWeaponId ) == "sniper";

	public static bool IsSight( ThornsAttachmentId id ) =>
		id is ThornsAttachmentId.HoloSight or ThornsAttachmentId.RangedSight or ThornsAttachmentId.RaisedRedDot;

	public static bool IsRedDotStyle( ThornsAttachmentId id ) =>
		id is ThornsAttachmentId.HoloSight or ThornsAttachmentId.RaisedRedDot;

	public static bool IsForegrip( ThornsAttachmentId id ) =>
		id is ThornsAttachmentId.ForegripStraight or ThornsAttachmentId.ForegripAngled;

	public static ThornsAdsSightMode? ResolveAdsMode( ThornsAttachmentId id ) => id switch
	{
		ThornsAttachmentId.RangedSight => ThornsAdsSightMode.SniperScope,
		ThornsAttachmentId.HoloSight or ThornsAttachmentId.RaisedRedDot => ThornsAdsSightMode.RedDot,
		_ => null
	};

	public static ThornsAttachmentId? ResolveEquippedSight( IEnumerable<ThornsAttachmentId> attachments )
	{
		if ( attachments is null )
			return null;

		foreach ( var attachment in attachments )
		{
			if ( attachment == ThornsAttachmentId.RangedSight )
				return attachment;
		}

		foreach ( var attachment in attachments )
		{
			if ( attachment == ThornsAttachmentId.HoloSight )
				return attachment;
		}

		foreach ( var attachment in attachments )
		{
			if ( attachment == ThornsAttachmentId.RaisedRedDot )
				return attachment;
		}

		return null;
	}

	public static List<ThornsAttachmentId> EnforceExclusivity( IEnumerable<ThornsAttachmentId> attachments )
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

	public static List<ThornsAttachmentId> SanitizeForWeapon( string combatWeaponId, IEnumerable<ThornsAttachmentId> attachments )
	{
		combatWeaponId = NormalizeCombatWeaponId( combatWeaponId );
		if ( !SupportsAttachments( combatWeaponId ) )
			return [];

		var list = (attachments ?? [])
			.Where( a => IsCompatible( combatWeaponId, a ) )
			.ToList();

		return EnforceExclusivity( list );
	}

	public static List<ThornsAttachmentId> ParseAttachmentItemIds( IEnumerable<string> itemIds )
	{
		var result = new List<ThornsAttachmentId>();
		if ( itemIds is null )
			return result;

		foreach ( var itemId in itemIds )
		{
			if ( ThornsAttachmentItemIds.TryParseItemId( itemId, out var attachment ) && IsEnabledInGame( attachment ) )
				result.Add( attachment );
		}

		return result;
	}
}
