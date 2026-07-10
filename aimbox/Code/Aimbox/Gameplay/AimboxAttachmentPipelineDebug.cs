namespace Sandbox;

/// <summary>Always-on attachment registration tracing — filter console for "Aimbox Attach".</summary>
public static class AimboxAttachmentPipelineDebug
{
	public static void Reg( string message ) => Log.Info( $"[Aimbox Attach] {message}" );

	public static string FormatList( IEnumerable<AimboxAttachmentId> attachments ) =>
		attachments is null ? "" : string.Join( ", ", attachments );
}
