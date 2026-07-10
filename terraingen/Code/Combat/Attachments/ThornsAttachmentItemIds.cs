namespace Terraingen.Combat.Attachments;

using Terraingen.GameData;

/// <summary>Maps catalog item ids to <see cref="ThornsAttachmentId"/>.</summary>
public static class ThornsAttachmentItemIds
{	public const string Holo = "attachment_holo";
	public const string Ranged = "attachment_ranged";
	public const string RedDot = "attachment_red_dot";
	public const string ExtendedMag = "attachment_extended_mag";
	public const string ForegripStraight = "attachment_foregrip_straight";
	public const string ForegripAngled = "attachment_foregrip_angled";
	public const string Flashlight = "attachment_flashlight";
	public const string Suppressor = "attachment_suppressor";

	public static readonly string[] All =
	[
		Holo,
		Ranged,
		RedDot,
		ExtendedMag,
		ForegripStraight,
		ForegripAngled,
		Flashlight,
		Suppressor
	];

	/// <summary>Attachment item ids that can spawn, be crafted, or appear in inventory.</summary>
	public static readonly string[] EnabledInGame =
	[
		Holo,
		Ranged,
		RedDot,
		ExtendedMag,
		ForegripAngled,
		Suppressor
	];

	public static bool TryParseItemId( string itemId, out ThornsAttachmentId attachment )
	{
		attachment = default;
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		switch ( itemId.Trim().ToLowerInvariant() )
		{
			case Holo:
				attachment = ThornsAttachmentId.HoloSight;
				break;
			case Ranged:
				attachment = ThornsAttachmentId.RangedSight;
				break;
			case RedDot:
				attachment = ThornsAttachmentId.RaisedRedDot;
				break;
			case ExtendedMag:
				attachment = ThornsAttachmentId.ExtendedMag;
				break;
			case ForegripStraight:
				attachment = ThornsAttachmentId.ForegripStraight;
				break;
			case ForegripAngled:
				attachment = ThornsAttachmentId.ForegripAngled;
				break;
			case Flashlight:
				attachment = ThornsAttachmentId.Flashlight;
				break;
			case Suppressor:
				attachment = ThornsAttachmentId.Suppressor;
				break;
			default:
				return false;
		}

		return ThornsAttachmentCatalog.IsEnabledInGame( attachment );
	}

	public static string ToItemId( ThornsAttachmentId attachment ) => attachment switch
	{
		ThornsAttachmentId.HoloSight => Holo,
		ThornsAttachmentId.RangedSight => Ranged,
		ThornsAttachmentId.RaisedRedDot => RedDot,
		ThornsAttachmentId.ExtendedMag => ExtendedMag,
		ThornsAttachmentId.ForegripStraight => ForegripStraight,
		ThornsAttachmentId.ForegripAngled => ForegripAngled,
		ThornsAttachmentId.Flashlight => Flashlight,
		ThornsAttachmentId.Suppressor => Suppressor,
		_ => ""
	};

	public static string Label( ThornsAttachmentId attachment ) => attachment switch
	{
		ThornsAttachmentId.HoloSight => "Holo Sight",
		ThornsAttachmentId.RangedSight => "Ranged Sight",
		ThornsAttachmentId.RaisedRedDot => "Raised Red Dot",
		ThornsAttachmentId.ExtendedMag => "Extended Mag",
		ThornsAttachmentId.ForegripStraight => "Foregrip (Straight)",
		ThornsAttachmentId.ForegripAngled => "Foregrip (Angled)",
		ThornsAttachmentId.Flashlight => "Flashlight",
		ThornsAttachmentId.Suppressor => "Suppressor",
		_ => attachment.ToString()
	};

	/// <summary>PNG filename stem under <c>ui/icons/</c> (without <c>.png</c>).</summary>
	public static string IconStem( string itemId ) => ThornsItemIdAliases.AttachmentIconStem( itemId );

	/// <summary>Icon filenames that map to catalog attachment ids — not standalone items.</summary>
	public static readonly string[] IconOnlyStems = ThornsItemIdAliases.AttachmentIconOnlyStems;

	public static bool IsIconOnlyDiscoveryStem( string discoveredId ) =>
		ThornsItemIdAliases.IsAttachmentIconOnlyStem( discoveredId );
}
