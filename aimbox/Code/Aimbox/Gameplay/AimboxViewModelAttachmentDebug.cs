namespace Sandbox;

/// <summary>Verbose FP attachment mount logging — filter console for "VM Attach".</summary>
public static class AimboxViewModelAttachmentDebug
{
	public static bool Enabled { get; set; } = false;

	public static void Info( string message )
	{
		if ( !Enabled )
			return;

		Log.Info( $"[Aimbox VM Attach] {message}" );
	}

	public static void Warn( string message )
	{
		if ( !Enabled )
			return;

		Log.Warning( $"[Aimbox VM Attach] {message}" );
	}
}
