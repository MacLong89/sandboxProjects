using System;
using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiCapsuleButton : Panel
{
	readonly Label _label;
	readonly Action _onClick;
	string _variant;

	public ThornsUiCapsuleButton( string label, string variant, Action onClick )
	{
		_onClick = onClick;
		_variant = string.IsNullOrWhiteSpace( variant ) ? "primary" : variant.Trim();
		AddClass( "thorns-capsule-btn" );
		AddClass( $"thorns-capsule-btn--{_variant}" );
		Style.PointerEvents = PointerEvents.All;

		_label = AddChild( new Label( label, "thorns-capsule-btn-label" ) );
		_label.Style.PointerEvents = PointerEvents.None;
	}

	public override bool WantsMouseInput() => _onClick is not null;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton != MouseButtons.Left )
			return;
		_onClick?.Invoke();
	}

	public void SetLabel( string text ) => _label.Text = text ?? "";

	public void SetVariant( string variant )
	{
		var next = string.IsNullOrWhiteSpace( variant ) || variant == "primary" ? "" : variant.Trim();
		var prev = string.IsNullOrEmpty( _variant ) || _variant == "primary" ? "" : _variant;
		if ( string.Equals( prev, next, StringComparison.Ordinal ) )
			return;

		if ( !string.IsNullOrEmpty( prev ) )
			RemoveClass( $"thorns-capsule-btn--{prev}" );
		_variant = next;
		if ( !string.IsNullOrEmpty( next ) )
			AddClass( $"thorns-capsule-btn--{next}" );
	}
}
