using System;
using System.Linq;

namespace Sandbox;

/// <summary>Client-only read helpers for HUD: aggregates replicated game state without gameplay authority.</summary>
public static class YaHudMatchSnapshot
{
	public static YaGameStateSystem TryGameState() => TryGameState( null );

	/// <summary>Resolves match state on clients where <see cref="YaGameStateSystem.Instance"/> is not yet assigned.</summary>
	public static YaGameStateSystem TryGameState( Scene scene )
	{
		if ( YaGameStateSystem.Instance is { IsValid: true } gs )
			return gs;

		if ( scene is not null && scene.IsValid() )
		{
			foreach ( var c in scene.GetAllComponents<YaGameStateSystem>() )
			{
				if ( c.IsValid() )
					return c;
			}
		}

		return null;
	}

	/// <summary>
	/// Live countdown: use the max of timer + game-state mirror so clients stay correct if one
	/// <see cref="Sync"/> property lags (purpose vs seconds).
	/// </summary>
	public static float GetPhaseCountdownForUi( Scene scene )
	{
		var flow = TryGameState( scene );
		var timer = TryTimer( scene );

		var fromFlow = flow != null && flow.IsValid() ? flow.SyncedPhaseSecondsRemaining : 0f;

		if ( timer.IsValid() && timer.ActivePurpose != YaTimerPurpose.None )
			return Math.Max( 0f, Math.Max( timer.SyncedRemaining, fromFlow ) );

		return Math.Max( 0f, fromFlow );
	}

	public static YaServerTimerSystem TryTimer( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return default;
		foreach ( var t in scene.GetAllComponents<YaServerTimerSystem>() )
		{
			if ( t.IsValid() )
				return t;
		}

		return default;
	}

	/// <summary>Counts living human <see cref="YaPlayerRole.NotAlone"/> players (replicated health).</summary>
	public static int CountHumansAlive( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var n = 0;
		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			if ( YaTeamSystem.GetRole( root ) != YaPlayerRole.NotAlone )
				continue;
			var h = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( h is { IsValid: true, IsAlive: true } && !h.IsDeadState )
				n++;
		}

		return n;
	}

	/// <summary>Living practice / AI hunters with <see cref="YaPlayerRole.NotAlone"/>.</summary>
	public static int CountAliveNotAloneBots( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var n = 0;
		foreach ( var brain in scene.GetAllComponents<YaBotBrain>() )
		{
			if ( !brain.IsValid() || brain.BotRole != YaPlayerRole.NotAlone )
				continue;
			var hp = brain.GameObject.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( hp is { IsValid: true, IsAlive: true } && !hp.IsDeadState )
				n++;
		}

		return n;
	}

	/// <summary>All living Not Alone roster (human hunters + Not Alone bots) for hunter-side HUD.</summary>
	public static int CountNotAloneTeamAlive( Scene scene ) =>
		CountHumansAlive( scene ) + CountAliveNotAloneBots( scene );

	/// <summary>Whether the Alone pawn is still alive (for NotAlone HUD). Unknown → treated as alive if root missing.</summary>
	public static bool IsAloneAlive( Scene scene, Guid aloneConnectionId )
	{
		if ( aloneConnectionId == default )
			return false;

		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			if ( root.Network.OwnerId != aloneConnectionId )
				continue;
			var h = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			return h is { IsValid: true, IsAlive: true } && !h.IsDeadState;
		}

		return false;
	}

	public static string GetConnectionDisplayName( Guid connectionId )
	{
		if ( connectionId == default )
			return "Player";
		var c = Connection.Find( connectionId );
		return c is { DisplayName: var dn } && !string.IsNullOrWhiteSpace( dn ) ? dn : "Player";
	}

	/// <summary>Human connection name, or <see cref="YaBotIdentity.DisplayName"/> for practice bots.</summary>
	public static string GetPawnDisplayName( GameObject root )
	{
		if ( root is null || !root.IsValid() )
			return "Unknown";

		var identity = root.Components.Get<YaBotIdentity>( FindMode.EnabledInSelf );
		if ( identity.IsValid() && !string.IsNullOrWhiteSpace( identity.DisplayName ) )
			return identity.DisplayName;

		if ( root.Network.OwnerId != default )
			return GetConnectionDisplayName( root.Network.OwnerId );

		if ( !string.IsNullOrWhiteSpace( root.Name ) )
		{
			const string prefix = "Bot - ";
			if ( root.Name.StartsWith( prefix, StringComparison.Ordinal ) )
				return root.Name[prefix.Length..];
		}

		return "Player";
	}
}
