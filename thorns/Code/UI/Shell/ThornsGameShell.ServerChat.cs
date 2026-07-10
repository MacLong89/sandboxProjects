#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Network;
using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	const int ServerChatMaxVisibleLines = 3;
	const int ServerChatMaxMessageLen = 200;

	Panel _serverChatRoot;
	Panel _serverChatFeed;
	ThornsUiServerChatTextEntry _serverChatEntry;
	readonly Label[] _serverChatLineLabels = new Label[ServerChatMaxVisibleLines];
	readonly List<(string Text, bool System)> _serverChatLines = new();
	bool _serverChatPendingEntryClear;

	void BuildServerChatPanel( Panel hudTopLeft )
	{
		_serverChatRoot = ThornsUiPanelAdd.AddChildPanel( hudTopLeft, "thorns-shell-server-chat" );
		_serverChatRoot.Style.FlexDirection = FlexDirection.Column;
		_serverChatRoot.Style.PointerEvents = PointerEvents.All;

		_serverChatRoot.AddChild( new Label( "SERVER CHAT", "thorns-shell-server-chat-cap" ) ).Style.PointerEvents =
			PointerEvents.None;

		_serverChatFeed = ThornsUiPanelAdd.AddChildPanel( _serverChatRoot, "thorns-shell-server-chat-feed" );
		_serverChatFeed.Style.FlexDirection = FlexDirection.Column;
		_serverChatFeed.Style.PointerEvents = PointerEvents.None;

		for ( var i = 0; i < _serverChatLineLabels.Length; i++ )
		{
			var lbl = _serverChatFeed.AddChild( new Label( "", "thorns-shell-server-chat-line" ) );
			lbl.Style.PointerEvents = PointerEvents.None;
			lbl.Style.WhiteSpace = WhiteSpace.Normal;
			lbl.Style.Display = DisplayMode.None;
			_serverChatLineLabels[i] = lbl;
		}

		_serverChatEntry = _serverChatRoot.AddChild( new ThornsUiServerChatTextEntry( TrySubmitServerChatFromEntry ) );
		_serverChatEntry.AddClass( "thorns-shell-server-chat-entry" );
		_serverChatEntry.Placeholder = "Enter: focus field, then Enter: send";
		_serverChatEntry.Style.PointerEvents = PointerEvents.All;
	}

	/// <summary>Host-only: fan out a line to every player's shell (join/leave/system + relayed chat).</summary>
	public static void HostNotifyAllPlayersServerChatLine( Scene scene, string line, bool system = false )
	{
		if ( !Networking.IsHost || scene is null || !scene.IsValid() || string.IsNullOrWhiteSpace( line ) )
			return;

		var t = line.TrimEnd();
		foreach ( var shell in scene.GetAllComponents<ThornsGameShell>() )
		{
			if ( shell is null || !shell.IsValid() )
				continue;

			if ( !Networking.IsActive )
			{
				shell.ClientAppendServerChatLine( t, system );
				continue;
			}

			var local = Connection.Local;
			if ( local is not null && local.Id == shell.GameObject.Network.OwnerId )
				shell.ClientAppendServerChatLine( t, system );
			else
				shell.RpcOwnerAppendServerChatLine( t, system );
		}
	}

	[Rpc.Owner]
	void RpcOwnerAppendServerChatLine( string line, bool system )
	{
		ClientAppendServerChatLine( line, system );
	}

	[Rpc.Host]
	void RpcSubmitServerChatFromOwner( string rawMessage )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || Rpc.Caller.Id != GameObject.Network.OwnerId )
			return;

		var msg = SanitizeServerChatMessage( rawMessage );
		if ( string.IsNullOrEmpty( msg ) )
			return;

		var name = Rpc.Caller.DisplayName?.Trim();
		if ( string.IsNullOrEmpty( name ) )
			name = "Player";

		HostNotifyAllPlayersServerChatLine( Scene, $"{name}: {msg}", system: false );
	}

	static string SanitizeServerChatMessage( string raw )
	{
		if ( string.IsNullOrEmpty( raw ) )
			return "";

		var sb = new StringBuilder( raw.Length );
		foreach ( var ch in raw.Trim() )
		{
			if ( ch == '\r' || ch == '\n' || ch == '\t' )
				sb.Append( ' ' );
			else if ( ch < ' ' )
				continue;
			else
				sb.Append( ch );
		}

		var s = sb.ToString().Trim();
		if ( s.Length > ServerChatMaxMessageLen )
			s = s[..ServerChatMaxMessageLen];

		return s;
	}

	void ClientAppendServerChatLine( string line, bool system )
	{
		if ( !IsLocalOwned || string.IsNullOrWhiteSpace( line ) )
			return;

		if ( _serverChatFeed is null || !_serverChatFeed.IsValid )
			return;

		_serverChatLines.Add( (line.TrimEnd(), system) );
		while ( _serverChatLines.Count > ServerChatMaxVisibleLines )
			_serverChatLines.RemoveAt( 0 );

		RefreshServerChatFeed();
	}

	void RefreshServerChatFeed()
	{
		if ( _serverChatFeed is null || !_serverChatFeed.IsValid )
			return;

		for ( var i = 0; i < _serverChatLineLabels.Length; i++ )
		{
			var lbl = _serverChatLineLabels[i];
			if ( lbl is null || !lbl.IsValid )
				continue;

			if ( i >= _serverChatLines.Count )
			{
				lbl.Text = "";
				lbl.SetClass( "thorns-shell-server-chat-line--system", false );
				lbl.Style.Display = DisplayMode.None;
				continue;
			}

			var (text, system) = _serverChatLines[i];
			lbl.Text = system ? $"• {text}" : text;
			lbl.SetClass( "thorns-shell-server-chat-line--system", system );
			lbl.Style.Display = DisplayMode.Flex;
		}
	}

	void TickServerChatDeferredEntryClear()
	{
		if ( !_serverChatPendingEntryClear )
			return;

		_serverChatPendingEntryClear = false;
		if ( _serverChatEntry is null || !_serverChatEntry.IsValid )
			return;

		if ( InputFocus.Current == _serverChatEntry )
			return;

		_serverChatEntry.Text = "";
	}

	void ScheduleServerChatEntryClear() => _serverChatPendingEntryClear = true;

	void TickServerChatInput()
	{
		if ( !IsLocalOwned )
			return;

		if ( _serverChatEntry is null || !_serverChatEntry.IsValid )
			return;

		if ( BlocksGameplayShellOverlay )
		{
			if ( InputFocus.Current == _serverChatEntry )
				InputFocus.Clear();
			return;
		}

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && (hp.IsDeadState || !hp.IsAlive) )
		{
			if ( InputFocus.Current == _serverChatEntry )
				InputFocus.Clear();
			return;
		}

		// Enter while focused is handled by ThornsUiServerChatTextEntry (UI eats the key).
		if ( InputFocus.Current == _serverChatEntry )
			return;

		if ( !Input.Keyboard.Pressed( "Enter" ) )
			return;

		if ( MenuOpen )
			return;

		InputFocus.Set( _serverChatEntry );
	}

	bool ServerChatTryConsumeEscape()
	{
		if ( _serverChatEntry is null || !_serverChatEntry.IsValid )
			return false;

		if ( InputFocus.Current != _serverChatEntry )
			return false;

		InputFocus.Clear();
		return true;
	}

	void TrySubmitServerChatFromEntry()
	{
		if ( _serverChatEntry is null || !_serverChatEntry.IsValid )
			return;

		var t = SanitizeServerChatMessage( _serverChatEntry.Text ?? "" );
		InputFocus.Clear();
		ScheduleServerChatEntryClear();

		if ( string.IsNullOrEmpty( t ) )
			return;

		if ( Networking.IsActive )
			RpcSubmitServerChatFromOwner( t );
		else
			ClientAppendServerChatLine(
				$"{Connection.Local?.DisplayName?.Trim() ?? "You"}: {t}",
				system: false );
	}
}
