namespace Sandbox;

public readonly record struct AimboxSpawnCandidate( Vector3 Position, Rotation Rotation, float Weight, AimboxTeam Team, string Name = "" );

public static class AimboxSpawnResolve
{
	public static IReadOnlyList<AimboxSpawnCandidate> GetCandidates( Scene scene )
	{
		var candidates = new List<AimboxSpawnCandidate>();

		foreach ( var spawn in scene.GetAllComponents<AimboxSpawnPoint>() )
		{
			candidates.Add( new AimboxSpawnCandidate(
				spawn.WorldPosition,
				spawn.WorldRotation,
				MathF.Max( 0.1f, spawn.Weight ),
				spawn.Team,
				spawn.GameObject.Name ) );
		}

		if ( !IsArenaMapActive() )
		{
			foreach ( var spawn in scene.GetAllComponents<SpawnPoint>() )
			{
				candidates.Add( new AimboxSpawnCandidate(
					spawn.WorldPosition,
					spawn.WorldRotation,
					1f,
					AimboxTeam.None ) );
			}
		}

		if ( candidates.Count > 0 )
			return candidates;

		if ( IsArenaMapActive() )
			return candidates;

		var map = scene.GetAllComponents<MapInstance>().FirstOrDefault( x => x.IsLoaded );
		if ( map is null )
			return candidates;

		var bounds = map.Bounds;
		if ( bounds.Size.Length <= 1f )
			return candidates;

		var center = bounds.Center;
		var half = bounds.Size * 0.25f;
		candidates.Add( new AimboxSpawnCandidate( new Vector3( center.x + half.x, center.y + half.y, center.z ), Rotation.FromYaw( 45 ), 1f, AimboxTeam.None ) );
		candidates.Add( new AimboxSpawnCandidate( new Vector3( center.x - half.x, center.y + half.y, center.z ), Rotation.FromYaw( 135 ), 1f, AimboxTeam.None ) );
		candidates.Add( new AimboxSpawnCandidate( new Vector3( center.x + half.x, center.y - half.y, center.z ), Rotation.FromYaw( 315 ), 1f, AimboxTeam.None ) );
		candidates.Add( new AimboxSpawnCandidate( new Vector3( center.x - half.x, center.y - half.y, center.z ), Rotation.FromYaw( 225 ), 1f, AimboxTeam.None ) );

		return candidates;
	}

	static bool IsArenaMapActive()
	{
		var game = AimboxGame.Instance;
		return game is not null && !game.GunBuilderScene && !game.ThirdPersonWeaponLabScene;
	}

	public static IReadOnlyList<AimboxSpawnCandidate> FilterForActor( IReadOnlyList<AimboxSpawnCandidate> candidates, IAimboxCombatActor actor )
	{
		var mode = AimboxGame.Instance?.Match.Mode ?? AimboxGameMode.FreeForAll;
		var filtered = FilterForMode( candidates, mode, actor );
		if ( actor.Team == AimboxTeam.None )
			return filtered;

		var teamSpawns = filtered.Where( spawn => spawn.Team == actor.Team || spawn.Team == AimboxTeam.None ).ToList();
		return teamSpawns.Count > 0 ? teamSpawns : filtered;
	}

	public static IReadOnlyList<AimboxSpawnCandidate> FilterForMode(
		IReadOnlyList<AimboxSpawnCandidate> candidates,
		AimboxGameMode mode,
		IAimboxCombatActor actor )
	{
	 IEnumerable<AimboxSpawnCandidate> Query() => mode switch
		{
			AimboxGameMode.FreeForAll => candidates.Where( c => c.Team == AimboxTeam.None && c.Name.StartsWith( "FFA", StringComparison.OrdinalIgnoreCase ) ),
			AimboxGameMode.TeamDeathmatch => candidates.Where( c =>
				(c.Team == actor.Team && c.Name.StartsWith( "TDM", StringComparison.OrdinalIgnoreCase ))
				|| (c.Team == AimboxTeam.None && c.Name.StartsWith( "TDM", StringComparison.OrdinalIgnoreCase )) ),
			AimboxGameMode.Duel => candidates.Where( c =>
				(actor.Team == AimboxTeam.Red && c.Name.StartsWith( "Duel Red", StringComparison.OrdinalIgnoreCase ))
				|| (actor.Team == AimboxTeam.Blue && c.Name.StartsWith( "Duel Blue", StringComparison.OrdinalIgnoreCase )) ),
			AimboxGameMode.Survival when actor.IsHumanPlayer => candidates.Where( c =>
				(c.Team == AimboxTeam.Red && c.Name.StartsWith( "TDM Red", StringComparison.OrdinalIgnoreCase ))
				|| c.Name.StartsWith( "Survival Player", StringComparison.OrdinalIgnoreCase ) ),
			AimboxGameMode.Survival => candidates.Where( c =>
				c.Team == AimboxTeam.Blue && c.Name.StartsWith( "TDM Blue", StringComparison.OrdinalIgnoreCase ) ),
			AimboxGameMode.Range => candidates.Where( c => c.Name.StartsWith( "Range Player", StringComparison.OrdinalIgnoreCase ) ),
			_ when AimboxAimModeRules.IsAimMode( mode ) => candidates.Where( c => c.Name.StartsWith( "AIM Player", StringComparison.OrdinalIgnoreCase ) ),
			_ => candidates
		};

		var list = Query().ToList();
		return list.Count > 0 ? list : candidates;
	}

	public static IReadOnlyList<AimboxSpawnCandidate> OrderForMaxSpread( IReadOnlyList<AimboxSpawnCandidate> candidates )
	{
		if ( candidates.Count <= 1 )
			return candidates;

		var ordered = new List<AimboxSpawnCandidate> { candidates[0] };
		var remaining = candidates.Skip( 1 ).ToList();
		while ( remaining.Count > 0 )
		{
			var anchor = ordered[^1].Position;
			var next = remaining.OrderByDescending( spawn => spawn.Position.Distance( anchor ) ).First();
			ordered.Add( next );
			remaining.Remove( next );
		}

		return ordered;
	}

	public static AimboxSpawnCandidate SelectOppositeCorner( IReadOnlyList<AimboxSpawnCandidate> candidates, IReadOnlyList<IAimboxCombatActor> enemies )
	{
		if ( candidates.Count == 0 )
			throw new InvalidOperationException( "No spawn candidates available." );

		if ( enemies.Count == 0 )
			return candidates[0];

		return candidates
			.OrderByDescending( spawn => enemies.Min( enemy => spawn.Position.Distance( enemy.WorldPosition ) ) )
			.ThenByDescending( spawn => spawn.Weight )
			.First();
	}

	public static Vector3 GetArenaCenter( Scene scene )
	{
		if ( AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default ) )
			return AimboxAimRoomLayout.ArenaCenter;

		if ( IsArenaMapActive() )
		{
			var feetZ = AimboxGame.Instance?.GetSpawnFeetZ() ?? AimboxMapDesignRules.FloorWalkZ;
			return new Vector3( 0f, 0f, feetZ );
		}

		var candidates = GetCandidates( scene );
		if ( candidates.Count > 0 )
		{
			var center = Vector3.Zero;
			foreach ( var spawn in candidates )
				center += spawn.Position;

			return center / candidates.Count;
		}

		var map = scene.GetAllComponents<MapInstance>().FirstOrDefault( x => x.IsLoaded );
		return map is not null ? map.Bounds.Center : Vector3.Zero;
	}

	/// <summary>Flat yaw rotation from <paramref name="position"/> toward the map center.</summary>
	public static Rotation GetRotationFacingArenaCenter( Scene scene, Vector3 position )
	{
		var toCenter = GetArenaCenter( scene ) - position;
		toCenter = toCenter.WithZ( 0f );
		if ( toCenter.LengthSquared < 1f )
			return Rotation.Identity;

		return Rotation.LookAt( toCenter.Normal );
	}
}
