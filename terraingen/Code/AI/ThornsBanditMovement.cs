namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.TerrainGen;

/// <summary>Planar locomotion helpers for bandit brains.</summary>
public static class ThornsBanditMovement
{
	public static void Stop( ThornsBanditBrain brain )
	{
		brain?.Motor?.HostSetWishWorld( Vector3.Zero );
		ThornsBanditDebug.LogMotor( brain, Vector3.Zero, "stop" );
	}

	public static void MoveToward(
		ThornsBanditBrain brain,
		Vector3 goalWorld,
		float speed,
		GameObject faceTarget = null,
		float arrivalDistance = 48f,
		float turnDeltaSeconds = 0.05f,
		bool rotateBodyTowardMovement = true )
	{
		if ( brain is null || !brain.IsValid() )
			return;

		var self = brain.GameObject.WorldPosition.WithZ( 0 );
		var tgt = brain.HostResolveMoveGoal( goalWorld ).WithZ( 0 );
		if ( HostGoalIsUnderwater( brain, goalWorld ) )
		{
			Stop( brain );
			return;
		}

		var delta = tgt - self;
		var dist = delta.Length;
		if ( dist < arrivalDistance * 0.9f )
		{
			var rawDist = ( goalWorld.WithZ( 0 ) - self ).Length;
			if ( rawDist > dist + 32f )
			{
				tgt = goalWorld.WithZ( 0 );
				delta = tgt - self;
				dist = delta.Length;
			}
		}

		if ( dist < arrivalDistance )
		{
			Stop( brain );
			if ( faceTarget.IsValid() )
				brain.HostFaceWorldPoint( ThornsBanditPerception.ResolveAimPoint( faceTarget ) );
			return;
		}

		var slowOuter = arrivalDistance * 2.4f;
		if ( dist < slowOuter )
		{
			var t = Math.Clamp( ( dist - arrivalDistance * 0.35f ) / MathF.Max( 12f, slowOuter - arrivalDistance * 0.35f ), 0.2f, 1f );
			speed *= t;
		}

		var dir = delta / dist;
		if ( rotateBodyTowardMovement )
			TurnBodyToward( brain, dir, turnDeltaSeconds );

		var wish = dir * speed;
		brain.Motor?.HostSetWishWorld( wish );
		ThornsBanditDebug.LogMotor( brain, wish, $"move dist={dist:F0} spd={speed:F0} rotateBody={rotateBodyTowardMovement}" );
	}

	public static void TurnBodyToward( ThornsBanditBrain brain, Vector3 flatDirection, float deltaSeconds )
	{
		if ( brain is null || !brain.IsValid() )
			return;

		var flat = flatDirection.WithZ( 0 );
		if ( flat.LengthSquared < 1e-8f )
			return;

		var target = Rotation.LookAt( flat.Normal );
		var t = Math.Clamp( deltaSeconds * 9f, 0.04f, 1f );
		brain.GameObject.WorldRotation = Rotation.Slerp( brain.GameObject.WorldRotation, target, t );
	}

	public static bool IsNear( ThornsBanditBrain brain, Vector3 goalWorld, float radius )
	{
		if ( brain is null || !brain.IsValid() )
			return false;

		var self = brain.GameObject.WorldPosition.WithZ( 0 );
		var tgt = goalWorld.WithZ( 0 );
		return ( tgt - self ).Length <= radius;
	}

	static bool HostGoalIsUnderwater( ThornsBanditBrain brain, Vector3 goalWorld )
	{
		var scene = brain.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );
		if ( !terrain.IsValid() || config is null )
			return false;

		if ( !ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, goalWorld, out var snapped ) )
			return false;

		return ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, snapped );
	}
}
