namespace Sandbox;

public static class AimboxAttachmentModifiers
{
	public static float RecoilKickMultiplier( IReadOnlyCollection<AimboxAttachmentId> attachments )
	{
		if ( attachments.Contains( AimboxAttachmentId.ForegripStraight ) )
			return 0.82f;

		if ( attachments.Contains( AimboxAttachmentId.ForegripAngled ) )
			return 0.88f;

		return 1f;
	}

	public static float AdsPresentationSpeedMultiplier( IReadOnlyCollection<AimboxAttachmentId> attachments ) => 1f;

	public static bool HasSuppressor( IReadOnlyCollection<AimboxAttachmentId> attachments ) =>
		attachments.Contains( AimboxAttachmentId.Suppressor );

	public static bool HasExtendedMag( IReadOnlyCollection<AimboxAttachmentId> attachments ) =>
		attachments.Contains( AimboxAttachmentId.ExtendedMag );

	/// <summary>1.5 with extended mag (+50% capacity).</summary>
	public static float MagazineSizeMultiplier( IReadOnlyCollection<AimboxAttachmentId> attachments ) =>
		HasExtendedMag( attachments ) ? 1.5f : 1f;

	/// <summary>0.25 with suppressor (−75% audible gunfire).</summary>
	public static float NoiseLoudnessMultiplier( IReadOnlyCollection<AimboxAttachmentId> attachments ) =>
		HasSuppressor( attachments ) ? 0.25f : 1f;

	public static float SpreadMultiplier( IReadOnlyCollection<AimboxAttachmentId> attachments )
	{
		if ( attachments.Contains( AimboxAttachmentId.RangedSight ) )
			return 0.75f;

		if ( attachments.Any( AimboxAttachmentCatalog.IsRedDotStyle ) )
			return 0.86f;

		return 1f;
	}
}
