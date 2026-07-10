namespace Terraingen.Combat.Attachments;

public static class ThornsAttachmentModifiers
{
	public static float RecoilKickMultiplier( IReadOnlyCollection<ThornsAttachmentId> attachments )
	{
		if ( attachments.Contains( ThornsAttachmentId.ForegripStraight ) )
			return 0.82f;

		if ( attachments.Contains( ThornsAttachmentId.ForegripAngled ) )
			return 0.88f;

		return 1f;
	}

	public static float AdsPresentationSpeedMultiplier( IReadOnlyCollection<ThornsAttachmentId> attachments ) => 1f;

	public static bool HasSuppressor( IReadOnlyCollection<ThornsAttachmentId> attachments ) =>
		attachments.Contains( ThornsAttachmentId.Suppressor );

	public static bool HasExtendedMag( IReadOnlyCollection<ThornsAttachmentId> attachments ) =>
		attachments.Contains( ThornsAttachmentId.ExtendedMag );

	public static float MagazineSizeMultiplier( IReadOnlyCollection<ThornsAttachmentId> attachments ) =>
		HasExtendedMag( attachments ) ? 1.5f : 1f;

	public static float NoiseLoudnessMultiplier( IReadOnlyCollection<ThornsAttachmentId> attachments ) =>
		HasSuppressor( attachments ) ? 0.25f : 1f;

	public static float SpreadMultiplier( IReadOnlyCollection<ThornsAttachmentId> attachments )
	{
		if ( attachments.Contains( ThornsAttachmentId.RangedSight ) )
			return 0.75f;

		if ( attachments.Any( ThornsAttachmentCatalog.IsRedDotStyle ) )
			return 0.86f;

		return 1f;
	}

	public static float BloomMultiplier( IReadOnlyCollection<ThornsAttachmentId> attachments ) =>
		SpreadMultiplier( attachments );
}
