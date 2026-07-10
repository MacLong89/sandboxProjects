#nullable disable

using System;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Single-line server chat field: Enter submits via callback (gameplay <c>Input</c> does not see Enter while focused).
/// </summary>
public sealed class ThornsUiServerChatTextEntry : TextEntry
{
	readonly Action _onSubmit;

	public ThornsUiServerChatTextEntry( Action onSubmit )
	{
		_onSubmit = onSubmit;
		Multiline = false;
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( IsEnterSubmit( e ) )
		{
			e.StopPropagation = true;
			_onSubmit?.Invoke();
			return;
		}

		base.OnButtonEvent( e );
	}

	static bool IsEnterSubmit( ButtonEvent e )
	{
		// Submit on key-up so the native QPlainTextEdit finishes Enter handling (avoids appendHtml-on-null).
		if ( e is null || e.Pressed )
			return false;

		// VK_RETURN
		if ( e.VirtualKey == 13 )
			return true;

		var b = e.Button ?? "";
		return b.Equals( "enter", StringComparison.OrdinalIgnoreCase )
		       || b.Equals( "return", StringComparison.OrdinalIgnoreCase );
	}
}
