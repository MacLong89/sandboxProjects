namespace Terraingen.UI.Core;

using System.Linq;
using Sandbox.UI;
using Terraingen.Player;
using Terraingen.UI;

/// <summary>Verbose client UI diagnostics — set <see cref="Enabled"/> false once HUD is stable.</summary>
public static class ThornsGameplayUiDiagnostics
{
	public static bool Enabled { get; set; } = false;

	/// <summary>Visible HUD banner is editor-only; live players should never see debug chrome.</summary>
	public static bool ShowVisibleBanner => Enabled && Game.IsEditor;

	static TimeUntil _heartbeatIn;
	static bool _loggedFirstHeartbeat;
	static bool _loggedTabPress;
	static bool _loggedMissingHost;
	static bool? _lastMenuOpenLogged;

	public static void Event( string message )
	{
		if ( !Enabled )
			return;

		Log.Info( $"[Thorns UI Diag] {message}" );
	}

	public static void Warn( string message ) =>
		Log.Warning( $"[Thorns UI Diag] {message}" );

	public static void Heartbeat(
		ThornsGameplayUiHost host,
		ThornsMenuHost menuHost,
		GameObject screenUiRoot )
	{
		if ( !Enabled || !Game.IsPlaying )
			return;

		if ( !_loggedFirstHeartbeat )
		{
			_loggedFirstHeartbeat = true;
			Event( "Diagnostics enabled (heartbeat every 3s while in world)." );
		}

		if ( _heartbeatIn > 0f )
			return;

		_heartbeatIn = 3f;

		var screenPanels = host?.Scene?.GetAllComponents<ScreenPanel>().Count() ?? 0;
		var menuHosts = host?.Scene?.GetAllComponents<ThornsMenuHost>().Count() ?? 0;

		var hostOk = menuHost is not null && menuHost.IsValid;
		var panelOk = hostOk && menuHost.Panel.IsValid;
		var built = hostOk && menuHost.IsUiBuilt;
		var childCount = panelOk ? menuHost.Panel.Children.Count() : 0;

		Event(
			$"heartbeat hostOk={hostOk} panelOk={panelOk} uiBuilt={built} menuOpen={ThornsMenuHost.IsOpen} " +
			$"snapshot={ThornsUiClientState.HasSnapshot} screenPanelsInScene={screenPanels} menuHosts={menuHosts} " +
			$"screenUiGo={screenUiRoot.IsValid()} panelChildren={childCount} " +
			$"localPlayer={ThornsPlayerGameplay.Local.IsValid()}" );

		if ( panelOk )
			Event( DescribePanel( menuHost.Panel, "menuRoot" ) );

		if ( hostOk && built )
			Event( menuHost.DescribeUiState() );

		if ( !hostOk && !_loggedMissingHost )
		{
			_loggedMissingHost = true;
			Warn( "ThornsMenuHost missing during heartbeat — input and HUD will not run." );
		}
	}

	public static void OnTabInput( ThornsMenuHost menuHost, bool uiBuilt )
	{
		if ( !Enabled )
			return;

		if ( !_loggedTabPress )
		{
			_loggedTabPress = true;
			Event( "First Tab/InventoryMenu press detected." );
		}

		Event(
			$"Tab pressed uiBuilt={uiBuilt} hostOk={menuHost is not null && menuHost.IsValid} openBefore={ThornsMenuHost.IsOpen} " +
			$"panelOk={menuHost is not null && menuHost.IsValid && menuHost.Panel.IsValid}" );
	}

	public static void OnSetOpen( bool open, bool uiBuilt, bool overlayOk )
	{
		if ( !Enabled )
			return;

		if ( _lastMenuOpenLogged == open )
			return;

		_lastMenuOpenLogged = open;
		Event( $"SetOpen({open}) uiBuilt={uiBuilt} overlayOk={overlayOk} resultOpen={ThornsMenuHost.IsOpen}" );
	}

	public static string DescribePanel( Panel panel, string name )
	{
		if ( panel is null || !panel.IsValid )
			return $"{name}: invalid";

		return
			$"{name}: visible={panel.IsVisible} display={panel.Style.Display} opacity={panel.Style.Opacity} " +
			$"w={panel.Style.Width} h={panel.Style.Height} children={panel.Children.Count()}";
	}
}
