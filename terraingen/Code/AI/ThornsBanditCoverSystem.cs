namespace Terraingen.AI;

/// <summary>Local-radius cover selection — no world-wide scans.</summary>
public static class ThornsBanditCoverSystem
{
	const int SampleCount = 10;
	const float MinRecentCoverSeparation = 96f;

	static readonly Vector3[] SampleScratch = new Vector3[SampleCount];

	public static bool TryFindCover(
		ThornsBanditBrain brain,
		GameObject threat,
		float searchRadius,
		ReadOnlySpan<Vector3> recentCover,
		out Vector3 coverPoint )
	{
		coverPoint = default;
		if ( brain is null || !brain.IsValid() )
			return false;

		var scene = brain.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var selfFlat = brain.GameObject.WorldPosition.WithZ( 0 );
		Vector3 away = Vector3.Right;
		if ( threat.IsValid() )
		{
			away = selfFlat - threat.WorldPosition.WithZ( 0 );
			if ( away.LengthSquared < 4f )
				away = brain.GameObject.WorldRotation.Forward.WithZ( 0 );
		}

		if ( away.LengthSquared < 1e-4f )
			away = Vector3.Right;
		else
			away = away.Normal;

		var bestScore = float.MinValue;
		var best = selfFlat;
		var found = false;

		for ( var i = 0; i < SampleCount; i++ )
		{
			var yaw = i * ( 360f / SampleCount ) + ( brain.GroupId * 17 + brain.GameObject.Id.GetHashCode() ) % 24;
			var dir = Rotation.FromYaw( yaw ).Forward.WithZ( 0 ).Normal;
			var dist = searchRadius * ( 0.35f + 0.65f * ( i / (float)( SampleCount - 1 ) ) );
			var candidate = selfFlat + dir * dist + away * ( searchRadius * 0.15f );
			candidate = brain.HostClampToLeash( candidate );

			if ( IsTooCloseToRecent( candidate, recentCover ) )
				continue;

			var score = ScoreCover( scene, brain.GameObject, threat, selfFlat, candidate );
			if ( score <= bestScore )
				continue;

			bestScore = score;
			best = candidate;
			found = true;
			SampleScratch[i] = candidate;
		}

		if ( !found )
			return false;

		coverPoint = best.WithZ( brain.GameObject.WorldPosition.z );
		return true;
	}

	static bool IsTooCloseToRecent( Vector3 candidateFlat, ReadOnlySpan<Vector3> recentCover )
	{
		for ( var i = 0; i < recentCover.Length; i++ )
		{
			if ( recentCover[i] == default )
				continue;

			if ( ( candidateFlat - recentCover[i].WithZ( 0 ) ).Length < MinRecentCoverSeparation )
				return true;
		}

		return false;
	}

	static float ScoreCover( Scene scene, GameObject self, GameObject threat, Vector3 selfFlat, Vector3 candidateFlat )
	{
		var distSelf = ( candidateFlat - selfFlat ).Length;
		var distScore = 1f - Math.Clamp( distSelf / 520f, 0f, 1f );

		var blockScore = 0f;
		if ( threat.IsValid() )
		{
			var threatEye = ThornsBanditPerception.ResolveAimPoint( threat );
			var coverPos = candidateFlat.WithZ( self.WorldPosition.z ) + Vector3.Up * 36f;
			var toCover = coverPos - threatEye;
			var len = toCover.Length;
			if ( len > 16f )
			{
				var tr = ThornsBanditTraceUtil.RunRay(
					scene,
					new Ray( threatEye, toCover.Normal ),
					len - 8f,
					ThornsBanditTraceUtil.LosProfile,
					self );

				if ( tr.Hit && tr.GameObject != self && tr.GameObject != threat )
					blockScore = 1f;
			}
		}

		return distScore * 0.45f + blockScore * 0.55f;
	}
}
