#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>Floating drag preview that follows the cursor inside a parent overlay panel.</summary>
public sealed class DragIconPanel : Panel
{
	readonly Panel _coordinateParent;
	readonly bool _armorStyle;

	public DragIconPanel( Panel coordinateParent, bool armorStyle = false )
	{
		_coordinateParent = coordinateParent;
		_armorStyle = armorStyle;
		AddClass( armorStyle ? "armor-drag-ghost-v2" : "inv-drag-ghost-v2" );
		Style.Position = PositionMode.Absolute;
		Style.Width = 44;
		Style.Height = 44;
		Style.MarginLeft = -22;
		Style.MarginTop = -22;
		Style.Padding = 0;
		Style.PointerEvents = PointerEvents.None;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;

		if ( armorStyle )
		{
			Style.BackgroundColor = new Color( 0.18f, 0.22f, 0.28f, 0.96f );
			Style.BorderWidth = 2;
			Style.BorderColor = new Color( 0.45f, 0.65f, 0.95f, 1f );
		}
		else
		{
			Style.BackgroundColor = new Color( 22f / 255f, 26f / 255f, 34f / 255f, 0.96f );
			Style.BorderWidth = 2;
			Style.BorderColor = new Color( 82f / 255f, 201f / 255f, 217f / 255f, 1f );
		}
	}

	public void SetGlyph( string glyphText, string textClass )
	{
		var lbl = AddChild( new Label( glyphText, textClass ) );
		lbl.Style.FontSize = 20;
		lbl.Style.FontColor = Color.White;
		lbl.Style.PointerEvents = PointerEvents.None;
	}

	public void UpdateFollowMouse()
	{
		if ( _coordinateParent is null || !_coordinateParent.IsValid )
			return;

		var screenPos = _coordinateParent.PanelPositionToScreenPosition( _coordinateParent.MousePosition );
		var panelDelta = _coordinateParent.ScreenPositionToPanelDelta( screenPos );
		Style.Left = Length.Fraction( panelDelta.x );
		Style.Top = Length.Fraction( panelDelta.y );
	}
}
