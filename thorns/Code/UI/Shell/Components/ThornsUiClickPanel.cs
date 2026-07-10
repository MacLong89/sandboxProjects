#nullable disable

using System;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Left-click panel without <c>AddEventListener</c> lambdas (avoids s&amp;box lambda substitution errors on mouse-up).</summary>
public sealed class ThornsUiClickPanel : Panel
{
	Action _onClick;

	public ThornsUiClickPanel( string panelClasses, string labelClasses, string text, Action onClick )
	{
		_onClick = onClick;
		ThornsUiPanelAdd.AddClasses( this, panelClasses );
		Style.PointerEvents = PointerEvents.All;
		var lbl = AddChild( new Label( text, labelClasses ) );
		lbl.Style.PointerEvents = PointerEvents.None;
	}

	public override bool WantsMouseInput() => _onClick is not null;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton != MouseButtons.Left )
			return;
		_onClick?.Invoke();
	}
}
