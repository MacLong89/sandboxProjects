namespace Terraingen.Animals;

using Terraingen.AI;
using Terraingen.Core;
using Terraingen.TerrainGen;

/// <summary>Minimal overlap resolution — animals must not share hitbox space.</summary>
static class ThornsAnimalSeparation
{
	public const float SeparationPadding = 2f;
	const int SpawnSearchRings = 10;

	public static float EstimateBodyRadius( Model model, float uniformScale )
		=> ThornsAnimalHitbox.GetPlanarRadius( model, uniformScale );

	public static bool TryResolveClearSpawn(
		Scene scene,
		ThornsAnimalSpeciesData species,
		Vector3 snappedTerrainPos,
		out Vector3 clearPos )
	{
		clearPos = snappedTerrainPos;
		if ( scene is null || !ThornsMultiplayer.IsHostOrOffline )
			return true;

		var spawnRadius = EstimateBodyRadius( Model.Load( species?.ModelPath ), ThornsAnimalManager.VisualScale );
		var terrain = ThornsTerrainCache.Resolve( scene );
		for ( var ring = 0; ring < SpawnSearchRings; ring++ )
		{
			var candidate = ring == 0
				? snappedTerrainPos
				: snappedTerrainPos + Vector3.Random.WithZ( 0f ).Normal * (16f + ring * 24f);

			if ( terrain.IsValid() && !ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, candidate, out candidate ) )
				continue;

			if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( candidate, spawnRadius ) )
				continue;

			if ( !OverlapsAnyAnimal( candidate, spawnRadius, null, out _ ) )
			{
				clearPos = candidate;
				return true;
			}
		}

		return false;
	}

	/// <summary>Single-pass overlap fix at a desired position (motor sidestep only).</summary>
	public static Vector3 ResolveOverlapOnly( ThornsAnimalBrain self, Vector3 desired )
	{
		if ( self is null || !self.IsValid() || self.IsDead || self.IsMounted )
			return desired;

		return PushOutOfOverlaps( self, desired, self.GetBodyRadius() );
	}

	static readonly ThornsSpatialGrid<ThornsAnimalBrain> SeparationGrid = new( 128f );
	static readonly List<ThornsAnimalBrain> SeparationNeighbors = new( 16 );
	static readonly Dictionary<ThornsAnimalBrain, int> SeparationIndices = new( 128 );

	public static void RunSeparationPass( IReadOnlyList<ThornsAnimalBrain> animals, Terrain terrain )
	{
		if ( animals is null || animals.Count < 2 )
			return;

		if ( animals.Count >= 4 )
		{
			RunSeparationPassSpatial( animals, terrain );
			return;
		}

		for ( var i = 0; i < animals.Count; i++ )
		{
			var a = animals[i];
			if ( !IsLive( a ) )
				continue;

			for ( var j = i + 1; j < animals.Count; j++ )
			{
				var b = animals[j];
				if ( !IsLive( b ) )
					continue;

				SeparatePair( a, b, terrain );
			}
		}
	}

	static void RunSeparationPassSpatial( IReadOnlyList<ThornsAnimalBrain> animals, Terrain terrain )
	{
		SeparationGrid.Clear();
		SeparationIndices.Clear();
		for ( var i = 0; i < animals.Count; i++ )
		{
			var brain = animals[i];
			if ( IsLive( brain ) )
				SeparationIndices[brain] = i;
		}

		for ( var i = 0; i < animals.Count; i++ )
		{
			var brain = animals[i];
			if ( !IsLive( brain ) )
				continue;

			SeparationGrid.Insert( brain, brain.GameObject.WorldPosition );
		}

		const float queryRadius = 160f;
		for ( var i = 0; i < animals.Count; i++ )
		{
			var a = animals[i];
			if ( !IsLive( a ) )
				continue;

			SeparationGrid.QueryRadius( a.GameObject.WorldPosition, queryRadius, SeparationNeighbors, planar: true );
			for ( var n = 0; n < SeparationNeighbors.Count; n++ )
			{
				var b = SeparationNeighbors[n];
				if ( !IsLive( b ) || a == b || !SeparationIndices.TryGetValue( b, out var bIndex ) || bIndex <= i )
					continue;

				SeparatePair( a, b, terrain );
			}
		}
	}

	static void SeparatePair( ThornsAnimalBrain a, ThornsAnimalBrain b, Terrain terrain )
	{
		if ( ShouldBypassSeparationBetween( a, b ) )
			return;

		var posA = a.GameObject.WorldPosition;
		var posB = b.GameObject.WorldPosition;
		var minDist = a.GetBodyRadius() + b.GetBodyRadius() + SeparationPadding;
		var delta = new Vector3( posB.x - posA.x, posB.y - posA.y, 0f );
		var dist = delta.Length;

		if ( dist >= minDist )
			return;

		Vector3 dir;
		if ( dist <= 0.001f )
			dir = Vector3.Random.WithZ( 0f ).Normal;
		else
			dir = delta / dist;

		var push = minDist - dist;
		if ( !a.IsMounted )
			SetAnimalPosition( a, terrain, posA - dir * push * 0.5f );
		if ( !b.IsMounted )
			SetAnimalPosition( b, terrain, posB + dir * push * 0.5f );
	}

	static Vector3 PushOutOfOverlaps( ThornsAnimalBrain self, Vector3 position, float selfRadius )
	{
		var resolved = position;
		var animals = ThornsAnimalManager.AnimalRegistry;
		for ( var i = 0; i < animals.Count; i++ )
		{
			var other = animals[i];
			if ( !IsLive( other ) || other == self || ShouldBypassSeparationBetween( self, other ) )
				continue;

			var otherPos = other.GameObject.WorldPosition;
			var minDist = selfRadius + other.GetBodyRadius() + SeparationPadding;
			var delta = new Vector3( resolved.x - otherPos.x, resolved.y - otherPos.y, 0f );
			var dist = delta.Length;

			if ( dist >= minDist )
				continue;

			if ( dist <= 0.001f )
				delta = Vector3.Random.WithZ( 0f ).Normal;
			else
				delta /= dist;

			resolved = otherPos + delta * minDist;
		}

		return resolved;
	}

	static bool OverlapsAnyAnimal(
		Vector3 position,
		float probeRadius,
		ThornsAnimalBrain ignore,
		out ThornsAnimalBrain blocker )
	{
		blocker = null;
		var animals = ThornsAnimalManager.AnimalRegistry;

		for ( var i = 0; i < animals.Count; i++ )
		{
			var other = animals[i];
			if ( !IsLive( other ) || other == ignore )
				continue;

			var minDist = probeRadius + other.GetBodyRadius() + SeparationPadding;
			var flatDist = new Vector3( position.x - other.GameObject.WorldPosition.x, position.y - other.GameObject.WorldPosition.y, 0f ).Length;
			if ( flatDist < minDist )
			{
				blocker = other;
				return true;
			}
		}

		return false;
	}

	static void SetAnimalPosition( ThornsAnimalBrain brain, Terrain terrain, Vector3 position )
	{
		if ( terrain.IsValid() && ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, position, out var snapped ) )
			position = snapped;

		brain.GameObject.WorldPosition = position;
		brain.SyncAgentToPosition( position );
	}

	static bool IsLive( ThornsAnimalBrain brain )
	{
		return brain is not null
		       && brain.IsValid()
		       && !brain.IsDead
		       && brain.GameObject.IsValid();
	}

	static bool ShouldBypassSeparationBetween( ThornsAnimalBrain a, ThornsAnimalBrain b )
	{
		if ( AreActivelyMeleeEngaged( a, b ) )
			return true;

		if ( IsEngagedWith( a, b.GameObject ) || IsEngagedWith( b, a.GameObject ) )
			return true;

		return false;
	}

	static bool IsEngagedWith( ThornsAnimalBrain self, GameObject otherRoot )
	{
		if ( !IsLive( self ) || !otherRoot.IsValid() || !self.Target.IsValid() )
			return false;

		if ( self.AiState is not (ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee) )
			return false;

		if ( self.Target == otherRoot )
			return true;

		var otherBrain = otherRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( IsLive( otherBrain ) && self.Target == otherBrain.GameObject )
			return true;

		var otherRootObj = self.Target.Root;
		return otherRootObj.IsValid() && otherRootObj == otherRoot.Root;
	}

	static bool AreActivelyMeleeEngaged( ThornsAnimalBrain a, ThornsAnimalBrain b )
		=> IsAttackingOrChasing( a, b ) || IsAttackingOrChasing( b, a );

	static bool IsAttackingOrChasing( ThornsAnimalBrain self, ThornsAnimalBrain other )
	{
		if ( self is null || other is null || self == other )
			return false;

		if ( self.AiState is not (ThornsAnimalState.Chase or ThornsAnimalState.Attack) )
			return false;

		if ( !self.Target.IsValid() )
			return false;

		var targetBrain = self.Target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return IsLive( targetBrain ) && targetBrain == other;
	}
}
