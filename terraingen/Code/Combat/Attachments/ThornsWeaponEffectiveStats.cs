namespace Terraingen.Combat.Attachments;

using Terraingen.GameData;

/// <summary>Effective weapon stats after attachment modifiers — single resolver for combat, UI, and AI.</summary>
public readonly struct ThornsWeaponEffectiveStats
{
	public int ClipSize { get; init; }
	public float BloomHalfAngleDegrees { get; init; }
	public float RecoilKickMultiplier { get; init; }
	public float NoiseLoudnessMultiplier { get; init; }
	public ThornsAdsSightMode AdsSightMode { get; init; }
	public IReadOnlyList<ThornsAttachmentId> Attachments { get; init; }

	public static ThornsWeaponEffectiveStats Resolve(
		ThornsWeaponDefinitions.WeaponDefinition def,
		string combatWeaponId,
		in ThornsItemStack stack )
	{
		var attachments = ThornsWeaponAttachmentState.GetAttachments( stack );
		return Resolve( def, combatWeaponId, attachments );
	}

	public static ThornsWeaponEffectiveStats Resolve(
		ThornsWeaponDefinitions.WeaponDefinition def,
		string combatWeaponId,
		IReadOnlyList<ThornsAttachmentId> attachments )
	{
		attachments ??= Array.Empty<ThornsAttachmentId>();
		var sanitized = ThornsAttachmentCatalog.SanitizeForWeapon( combatWeaponId, attachments );
		var attachmentSet = sanitized as IReadOnlyCollection<ThornsAttachmentId> ?? sanitized.ToList();

		var clip = def.ClipSize > 0
			? Math.Max( 1, (int)MathF.Round( def.ClipSize * ThornsAttachmentModifiers.MagazineSizeMultiplier( attachmentSet ) ) )
			: def.ClipSize;

		var bloom = def.BloomHalfAngleDegreesBase * ThornsAttachmentModifiers.BloomMultiplier( attachmentSet );
		var recoilMul = ThornsAttachmentModifiers.RecoilKickMultiplier( attachmentSet );
		var noiseMul = ThornsAttachmentModifiers.NoiseLoudnessMultiplier( attachmentSet );

		var sight = ThornsAttachmentCatalog.ResolveEquippedSight( sanitized );
		var adsMode = sight.HasValue
			? ThornsAttachmentCatalog.ResolveAdsMode( sight.Value ) ?? ThornsAdsSightMode.IronSight
			: ThornsAdsSightMode.IronSight;

		return new ThornsWeaponEffectiveStats
		{
			ClipSize = clip,
			BloomHalfAngleDegrees = bloom,
			RecoilKickMultiplier = recoilMul,
			NoiseLoudnessMultiplier = noiseMul,
			AdsSightMode = adsMode,
			Attachments = sanitized
		};
	}

	public static int ResolveClipSize( ThornsWeaponDefinitions.WeaponDefinition def, string combatWeaponId, in ThornsItemStack stack ) =>
		Resolve( def, combatWeaponId, stack ).ClipSize;
}
