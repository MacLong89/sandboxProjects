namespace Sandbox;

/// <summary>Generic threat scoring — highest score becomes <see cref="ThornsAnimalBrainContext.CurrentTarget"/>.</summary>
public static class ThornsAnimalThreatSystem
{
	public readonly struct ThreatCandidate
	{
		public GameObject Root { get; init; }
		public float Score { get; init; }
	}

	public static bool TrySelectBestThreat(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		IReadOnlyList<ThreatCandidate> candidates,
		out GameObject best,
		out float bestScore )
	{
		best = default;
		bestScore = 0f;
		if ( candidates is null || candidates.Count == 0 )
			return false;

		var modeMul = ctx.BehaviorMode switch
		{
			ThornsAnimalBehaviorMode.Aggressive => 1.25f,
			ThornsAnimalBehaviorMode.Predator => 1.35f,
			ThornsAnimalBehaviorMode.Defensive => 0.95f,
			_ => 0.75f,
		};

		for ( var i = 0; i < candidates.Count; i++ )
		{
			var c = candidates[i];
			if ( !c.Root.IsValid() )
				continue;

			var score = c.Score * modeMul;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			best = c.Root;
		}

		if ( !best.IsValid() )
			return false;

		ctx.CurrentTarget = best;
		ctx.ThreatScore = bestScore;
		return true;
	}

	public static float ScoreByDistance( float dist, float maxRange, float weight = 1f )
	{
		if ( maxRange <= 1f )
			return 0f;

		var t = 1f - Math.Clamp( dist / maxRange, 0f, 1f );
		return t * t * 100f * weight;
	}

	public static float ScoreRecentDamage( bool wasDamagedRecently, float weight = 140f ) =>
		wasDamagedRecently ? weight : 0f;

	public static float ScoreOwnerAttacked( bool ownerUnderThreat, float weight = 120f ) =>
		ownerUnderThreat ? weight : 0f;
}
