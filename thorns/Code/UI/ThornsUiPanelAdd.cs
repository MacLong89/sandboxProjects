#nullable disable

using System;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// UI helpers for adding styled panel children (replaces removed string-based AddChild overloads).
/// Static methods (not extensions) so the sandbox compiler always binds them.
/// </summary>
public static class ThornsUiPanelAdd
{
	public static void AddClasses( Panel p, string cssClasses )
	{
		if ( p is null || string.IsNullOrWhiteSpace( cssClasses ) )
			return;

		foreach ( var token in cssClasses.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			p.AddClass( token );
	}

	public static Panel AddChildPanel( Panel parent, string cssClasses )
	{
		var child = parent.AddChild( new Panel() );
		AddClasses( child, cssClasses );
		return child;
	}

	public static T AddChildPanel<T>( Panel parent, string cssClasses ) where T : Panel, new()
	{
		var child = parent.AddChild( new T() );
		AddClasses( child, cssClasses );
		return child;
	}

	/// <summary>Replaces legacy <c>new Button(text, onClick)</c> when the engine no longer accepts that constructor.</summary>
	public static Panel AddClickableLabel(
		Panel parent,
		string text,
		Action onClick,
		string labelClasses = "thorns-ui-press-lbl",
		string panelClasses = "thorns-ui-press" ) =>
		parent.AddChild( new ThornsUiClickPanel( panelClasses, labelClasses, text, onClick ) );
}
