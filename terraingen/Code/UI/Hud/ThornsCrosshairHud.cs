namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;

/// <summary>Center reticle — simple dot on the gameplay HUD layer.</summary>
public sealed class ThornsCrosshairHud
{
	readonly Panel _root;
	readonly Panel _dot;

	public ThornsCrosshairHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "crosshair-hud" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels( 0 );
		_root.Style.Top = Length.Pixels( 0 );
		_root.Style.Width = Length.Percent( 100 );
		_root.Style.Height = Length.Percent( 100 );
		_root.Style.Display = DisplayMode.Flex;
		_root.Style.JustifyContent = Justify.Center;
		_root.Style.AlignItems = Align.Center;
		_root.Style.PointerEvents = PointerEvents.None;

		_dot = ThornsUiFactory.AddPanel( _root, "crosshair-dot" );

		ApplyScale();
		_dot.SetClass( "hit", false );
		RefreshHitFlash();
	}

	public void RefreshHitFlash()
	{
		if ( _dot is null || !_dot.IsValid )
			return;

		_dot.SetClass( "hit", ThornsHitmarkerState.IsHitFlashVisible );
	}

	public void ApplyScale()
	{
		if ( _dot is null || !_dot.IsValid )
			return;

		var size = 4f * ThornsCrosshairSettings.Scale;
		_dot.Style.Width = Length.Pixels( size );
		_dot.Style.Height = Length.Pixels( size );
	}

	public void SetVisible( bool visible )
	{
		if ( _root is null || !_root.IsValid )
			return;

		_root.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
	}
}
