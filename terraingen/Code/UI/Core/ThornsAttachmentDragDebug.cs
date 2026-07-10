namespace Terraingen.UI.Core;

/// <summary>Verbose console logging for weapon attachment drag-drop (toggle in console).</summary>
public static class ThornsAttachmentDragDebug
{
	[ConVar( "thorns_attachment_drag_debug" )]
	public static bool Enabled { get; set; }

	[ConCmd( "thorns_attachment_drag_debug_toggle" )]
	public static void Toggle()
	{
		Enabled = !Enabled;
		Log.Info( $"[Thorns Attach Drag] debug={( Enabled ? "ON" : "OFF" )} — or set thorns_attachment_drag_debug 1" );
	}

	public static void Write( string message )
	{
		if ( !Enabled )
			return;

		Log.Info( $"[Thorns Attach Drag] {message}" );
	}

	public static void LogReject( string stage, string reason )
	{
		if ( !Enabled )
			return;

		Log.Warning( $"[Thorns Attach Drag] REJECT @ {stage}: {reason}" );
	}
}
