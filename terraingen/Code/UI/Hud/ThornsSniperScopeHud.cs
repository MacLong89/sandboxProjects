namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Published each frame by <see cref="Sandbox.ThornsAdsSightController"/> for HUD consumers.</summary>
public static class ThornsSniperScopeHudState
{
	public static bool ShowClassicScope { get; set; }
	public static bool HideStandardCrosshair { get; set; }
	public static bool ShowScopeCrosshair { get; set; }
	public static bool HideGameplayHotbar { get; set; }
}

/// <summary>Classic sniper scope ring HUD — black mask with centered reticle while scoped.</summary>
public sealed class ThornsSniperScopeHud
{
	readonly Panel _root;
	readonly Panel _scopeCrosshair;
	readonly Panel _scopeDot;

	public ThornsSniperScopeHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "thorns-scope-overlay" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels( 0 );
		_root.Style.Top = Length.Pixels( 0 );
		_root.Style.Width = Length.Percent( 100 );
		_root.Style.Height = Length.Percent( 100 );
		_root.Style.Opacity = 0f;
		_root.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyPassive( _root, ThornsUiPriority.PassiveOverlay );

		var mask = ThornsUiFactory.AddPanel( _root, "thorns-scope-mask" );
		var ring = ThornsUiFactory.AddPanel( _root, "thorns-scope-ring" );

		_scopeCrosshair = ThornsUiFactory.AddPanel( _root, "thorns-scope-crosshair" );
		_scopeDot = ThornsUiFactory.AddPanel( _scopeCrosshair, "thorns-scope-crosshair-dot" );

		_ = mask;
		_ = ring;
		Refresh();
	}

	public void Refresh()
	{
		if ( _root is null || !_root.IsValid )
			return;

		var visible = ThornsSniperScopeHudState.ShowClassicScope;
		_root.Style.Opacity = visible ? 1f : 0f;
		_root.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;

		if ( _scopeCrosshair is not null && _scopeCrosshair.IsValid )
			_scopeCrosshair.SetClass( "hidden", !ThornsSniperScopeHudState.ShowScopeCrosshair );
	}
}
