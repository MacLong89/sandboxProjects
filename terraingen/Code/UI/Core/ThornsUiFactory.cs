namespace Terraingen.UI;

using Sandbox.UI;

/// <summary>Panel/label helpers matching s&box UI construction patterns.</summary>
public static class ThornsUiFactory
{
	static T RequireChild<T>( Panel parent, T child, string context ) where T : Panel
	{
		if ( parent is null || !parent.IsValid )
			throw new InvalidOperationException( $"[Thorns UI] {context}: parent panel is invalid." );

		parent.AddChild( child );
		if ( child is null || !child.IsValid )
			throw new InvalidOperationException( $"[Thorns UI] {context}: AddChild returned invalid panel." );

		return child;
	}

	public static Label AddLabel( Panel parent, string text, string cssClass = null )
	{
		var label = RequireChild( parent, new Label( text ), "AddLabel" );
		if ( !string.IsNullOrEmpty( cssClass ) )
		{
			foreach ( var c in cssClass.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
				label.AddClass( c );
		}

		Core.ThornsGameplayUiStyles.ApplyReadableLabel( label );
		return label;
	}

	/// <summary>Label that does not steal mouse hits (use inside clickable rows).</summary>
	public static Label AddPassiveLabel( Panel parent, string text, string cssClass = null )
	{
		var label = AddLabel( parent, text, cssClass );
		label.Style.PointerEvents = PointerEvents.None;
		return label;
	}

	public static Panel AddPanel( Panel parent, string cssClass )
	{
		var panel = RequireChild( parent, new Panel(), "AddPanel" );
		Core.ThornsUiPanelDefaults.DisableDragScroll( panel );
		if ( !string.IsNullOrEmpty( cssClass ) )
		{
			foreach ( var c in cssClass.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
				panel.AddClass( c );
		}

		return panel;
	}

	/// <summary>Clickable panel (instance-method handler — hotload-safe).</summary>
	public static ThornsClickPanel AddClickable( Panel parent, string cssClass, Action onClick )
	{
		var panel = RequireChild( parent, new ThornsClickPanel(), "AddClickable" );
		if ( !string.IsNullOrEmpty( cssClass ) )
		{
			foreach ( var c in cssClass.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
				panel.AddClass( c );
		}

		panel.SetClick( onClick );
		return panel;
	}

	public static ThornsClickPanel AddClickable( Panel parent, string cssClass, string labelText, Action onClick )
	{
		var panel = AddClickable( parent, cssClass, onClick );
		if ( !string.IsNullOrEmpty( labelText ) )
			AddPassiveLabel( panel, labelText );

		return panel;
	}
}
