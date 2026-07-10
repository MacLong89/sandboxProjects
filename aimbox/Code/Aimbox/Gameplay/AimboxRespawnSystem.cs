namespace Sandbox;

public sealed class AimboxRespawnSystem
{
	public float RespawnDelay { get; set; } = 3f;

	bool _spreadAssignmentActive;
	int _spreadAssignmentIndex;

	public void BeginSpreadAssignment()
	{
		_spreadAssignmentActive = true;
		_spreadAssignmentIndex = 0;
	}

	public void EndSpreadAssignment() => _spreadAssignmentActive = false;

	public Transform SelectSpawn( Scene scene, AimboxPlayerController player, IReadOnlyList<AimboxPlayerController> players ) =>
		SelectSpawn( scene, player, players.Select( x => x as IAimboxCombatActor ).ToList() );

	public Transform SelectSpawn( Scene scene, IAimboxCombatActor actor, IReadOnlyList<IAimboxCombatActor> actors )
	{
		var allCandidates = AimboxSpawnResolve.GetCandidates( scene );
		var candidates = AimboxSpawnResolve.FilterForActor( allCandidates, actor ).ToList();
		candidates = AimboxSpawnClearance.FilterClear( scene, actor.GameObject, candidates ).ToList();
		if ( candidates.Count == 0 )
		{
			var feetZ = AimboxGame.Instance?.GetSpawnFeetZ() ?? AimboxMapDesignRules.FloorWalkZ;
			Log.Warning( $"[Aimbox] No spawn candidates — using fallback at z={feetZ}." );
			var fallback = Vector3.Up * feetZ;
			return BuildSpawnTransform( scene, fallback );
		}

		var mode = AimboxGame.Instance?.Match.Mode ?? AimboxGameMode.FreeForAll;
		if ( mode == AimboxGameMode.Duel )
		{
			var duelSpawn = candidates[0];
			var resolved = ResolveSpawnPosition( scene, actor.GameObject, duelSpawn.Position, out var groundHit, out var groundZ );
			AimboxArenaDiagnostics.LogSpawnResolution(
				actor.CombatId,
				duelSpawn.Name,
				duelSpawn.Position,
				resolved,
				groundHit,
				groundZ );
			return BuildSpawnTransform( scene, resolved );
		}

		var enemies = actors.Where( x => x != actor && x.IsAlive && !x.IsTeammate( actor ) ).ToArray();
		var chosen = _spreadAssignmentActive
			? PickSpreadSpawn( candidates )
			: PickCombatSpawn( candidates, enemies );

		var spawnPos = ResolveSpawnPosition( scene, actor.GameObject, chosen.Position, out var hit, out var gz );
		AimboxArenaDiagnostics.LogSpawnResolution(
			actor.CombatId,
			chosen.Name,
			chosen.Position,
			spawnPos,
			hit,
			gz );
		return BuildSpawnTransform( scene, spawnPos );
	}

	static Transform BuildSpawnTransform( Scene scene, Vector3 position ) =>
		new( position, AimboxSpawnResolve.GetRotationFacingArenaCenter( scene, position ) );

	AimboxSpawnCandidate PickSpreadSpawn( List<AimboxSpawnCandidate> candidates )
	{
		var ordered = AimboxSpawnResolve.OrderForMaxSpread( candidates );
		var chosen = ordered[_spreadAssignmentIndex % ordered.Count];
		_spreadAssignmentIndex++;
		return chosen;
	}

	static Vector3 ResolveSpawnPosition( Scene scene, GameObject body, Vector3 position, out bool groundHit, out float groundZ )
	{
		groundHit = false;
		groundZ = position.z;

		var resolved = AimboxSpawnClearance.ResolveClearFeetPosition( scene, body, position );
		if ( AimboxCitizenMovementMotor.TryGetGroundHeight( scene, body, resolved, out groundZ ) )
		{
			groundHit = true;
			return resolved;
		}

		groundZ = resolved.z;
		return resolved;
	}

	static AimboxSpawnCandidate PickCombatSpawn( List<AimboxSpawnCandidate> candidates, IAimboxCombatActor[] enemies )
	{
		if ( candidates.Count == 0 )
			throw new InvalidOperationException( "No spawn candidates available." );

		if ( enemies.Length == 0 )
			return candidates[Game.Random.Int( 0, candidates.Count - 1 )];

		var shortlist = candidates
			.OrderByDescending( spawn => enemies.Min( enemy => spawn.Position.Distance( enemy.WorldPosition ) ) )
			.ThenByDescending( spawn => spawn.Weight )
			.Take( 3 )
			.ToList();
		return shortlist[Game.Random.Int( 0, shortlist.Count - 1 )];
	}
}
