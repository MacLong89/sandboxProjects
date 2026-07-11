namespace Terraingen.UI.Menu;

using Terraingen.Multiplayer;

/// <summary>Legacy entry points — delegate to <see cref="ThornsSessionEnterController"/>.</summary>
public static class ThornsMenuJoinHandoff
{
	public static void TryComplete() => ThornsSessionEnterController.TryCompleteEnter( "handoff" );

	public static void ForceCompleteWithDiagnostics( string reason ) =>
		ThornsSessionEnterController.ForceCompleteEnter( reason );
}
