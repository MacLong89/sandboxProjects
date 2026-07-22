namespace NoFly;

public static class NoFlyDebug
{
	[ConCmd( "nofly_force_role" )]
	public static void ForceRole( string roleName )
	{
		if ( !Networking.IsHost ) return;
		if ( NoFlyGame.Instance?.Settings.DebugToolsEnabled != true ) return;
		if ( !Enum.TryParse<RoleType>( roleName, true, out var role ) ) return;
		var local = NoFlyGame.LocalPlayer;
		if ( local is null ) return;
		local.Role = role;
		local.Team = RoleCatalog.Get( role ).Team;
		local.ApplyAppearance();
		Log.Info( $"Forced role {role}" );
	}

	[ConCmd( "nofly_skip_phase" )]
	public static void SkipPhase()
	{
		if ( !Networking.IsHost ) return;
		if ( NoFlyGame.Instance?.Settings.DebugToolsEnabled != true ) return;
		var g = NoFlyGame.Instance;
		g.EnterState( g.State, 0.05f );
	}

	[ConCmd( "nofly_spawn_npc" )]
	public static void SpawnNpc()
	{
		if ( !Networking.IsHost ) return;
		var g = NoFlyGame.Instance;
		if ( g?.Airport is null ) return;
		var go = new GameObject( true, "NPC_debug" );
		go.WorldPosition = g.Airport.GetSpawn( "entrance" );
		go.Components.Create<NpcPassenger>();
		go.NetworkSpawn();
	}

	[ConCmd( "nofly_trigger_chase" )]
	public static void TriggerChase()
	{
		if ( !Networking.IsHost ) return;
		var g = NoFlyGame.Instance;
		var smug = g?.FindPlayer( g.SmugglerId );
		if ( smug is not null ) g.StartChase( smug, "debug" );
	}

	[ConCmd( "nofly_end_round" )]
	public static void EndRound( string side = "tsa" )
	{
		if ( !Networking.IsHost ) return;
		NoFlyGame.Instance?.EndRound( side.ToLower() == "smuggler" ? WinSide.Smuggler : WinSide.Tsa );
	}

	[ConCmd( "nofly_show_roles" )]
	public static void ShowRoles()
	{
		foreach ( var p in Game.ActiveScene.GetAllComponents<NoFlyPlayer>() )
			Log.Info( $"{p.DisplayName}: {p.Role} ({p.Team}) bot={p.IsBot}" );
	}

	[ConCmd( "nofly_start_solo" )]
	public static void StartSolo( string role = "Smuggler" )
	{
		if ( !Enum.TryParse<RoleType>( role, true, out var r ) ) r = RoleType.Smuggler;
		NoFlyGame.Instance?.RpcRequestSinglePlayer( r );
	}
}
