using System;
using Dynasty.UI.Management;
using Sandbox.UI;

namespace Dynasty.UI.Components;

public static class UiModalHelpers
{
	public static string ZIndexStyle( UiWindowType window )
		=> $"z-index: {DynastyUiManager.Instance.GetWindowZIndex( window )};";

	public static void StopPropagation( PanelEvent e )
		=> e.StopPropagation();

	public static void BackdropClick( UiWindowType window, Action onClose )
	{
		var def = UiWindowRegistry.Get( window );
		if ( def?.DismissOnBackdrop == true )
			onClose?.Invoke();
	}
}
