namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Full-screen translucent red pulse when the local player is hurt.</summary>
public sealed class ThornsDamageFlashHud
{
	readonly Panel _overlay;

	public ThornsDamageFlashHud( Panel parent )
	{
		var root = ThornsUiFactory.AddPanel( parent, "damage-flash-hud" );
		root.Style.Position = PositionMode.Absolute;
		root.Style.Left = Length.Pixels( 0 );
		root.Style.Top = Length.Pixels( 0 );
		root.Style.Width = Length.Percent( 100 );
		root.Style.Height = Length.Percent( 100 );
		root.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyPassive( root, ThornsUiPriority.PassiveOverlay );

		_overlay = ThornsUiFactory.AddPanel( root, "damage-flash-overlay" );
		_overlay.Style.Width = Length.Percent( 100 );
		_overlay.Style.Height = Length.Percent( 100 );

		Refresh();
	}

	public void Refresh()
	{
		if ( _overlay is null || !_overlay.IsValid )
			return;

		var opacity = ThornsDamageFlashState.OverlayOpacity;
		_overlay.Style.Opacity = opacity;
		_overlay.Style.Display = opacity > 0.001f ? DisplayMode.Flex : DisplayMode.None;
	}
}
