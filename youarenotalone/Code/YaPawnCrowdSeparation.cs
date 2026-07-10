using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host: nudges overlapping practice bots apart (and bots away from the human) so pawns do not visually phase.
/// Does not damp player velocity — that was causing near-zero walk speed in practice.
/// </summary>
[Title( "YouAreNotAlone — Pawn crowd separation" )]
[Category( "YouAreNotAlone" )]
[Icon( "groups" )]
[Order( 200 )]
public sealed class YaPawnCrowdSeparation : Component
{
	[Property] public float ExtraPadding { get; set; } = 4f;

	[Property] public int SolverIterations { get; set; } = 2;

	readonly List<SeparationAgent> _agents = new();

	sealed class SeparationAgent
	{
		public GameObject Root;
		public bool IsBot;
		public Vector3 Position;
		public float Radius;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !ShouldRunSeparation( scene ) )
			return;

		GatherAgents( scene );
		if ( _agents.Count < 2 )
			return;

		for ( var iter = 0; iter < SolverIterations; iter++ )
			ResolvePass();
	}

	static bool ShouldRunSeparation( Scene scene )
	{
		var practice = YaPracticeModeSystem.Instance;
		if ( practice is { IsValid: true, PracticeActive: true } )
			return true;

		foreach ( var brain in scene.GetAllComponents<YaBotBrain>() )
		{
			if ( brain.IsValid() )
				return true;
		}

		return false;
	}

	void GatherAgents( Scene scene )
	{
		_agents.Clear();

		void TryAdd( GameObject root )
		{
			if ( root is null || !root.IsValid() )
				return;

			for ( var i = 0; i < _agents.Count; i++ )
			{
				if ( _agents[i].Root == root )
					return;
			}

			var hp = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( hp is not { IsValid: true, IsAlive: true } || hp.IsDeadState )
				return;

			var cc = root.Components.Get<CharacterController>( FindMode.EnabledInSelf );
			if ( !cc.IsValid() )
				return;

			var isBot = root.Components.Get<YaBotBrain>( FindMode.EnabledInSelf ).IsValid();

			_agents.Add( new SeparationAgent
			{
				Root = root,
				IsBot = isBot,
				Position = root.WorldPosition,
				Radius = Math.Max( 16f, cc.Radius )
			} );
		}

		foreach ( var brain in scene.GetAllComponents<YaBotBrain>() )
		{
			if ( brain.IsValid() )
				TryAdd( brain.GameObject );
		}

		var practice = YaPracticeModeSystem.Instance;
		if ( practice is { IsValid: true, PracticeActive: true } )
		{
			foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
				TryAdd( root );
		}
	}

	void ResolvePass()
	{
		var movedBot = new bool[_agents.Count];

		for ( var i = 0; i < _agents.Count; i++ )
		{
			for ( var j = i + 1; j < _agents.Count; j++ )
			{
				var a = _agents[i];
				var b = _agents[j];

				var delta = b.Position - a.Position;
				delta.z = 0f;
				var dist = delta.Length;
				var minDist = a.Radius + b.Radius + ExtraPadding;

				if ( dist >= minDist )
					continue;

				Vector3 dir;
				if ( dist < 0.5f )
				{
					var yaw = Random.Shared.Float( 0f, 360f );
					dir = Rotation.FromYaw( yaw ) * Vector3.Forward;
				}
				else
					dir = delta / dist;

				var overlap = minDist - dist;

				// Human: never teleport — CC + player tags handle blocking; only nudge bots.
				if ( a.IsBot && !b.IsBot )
				{
					a.Position -= dir * overlap;
					movedBot[i] = true;
				}
				else if ( !a.IsBot && b.IsBot )
				{
					b.Position += dir * overlap;
					movedBot[j] = true;
				}
				else
				{
					var push = overlap * 0.5f;
					a.Position -= dir * push;
					b.Position += dir * push;
					if ( a.IsBot )
						movedBot[i] = true;
					if ( b.IsBot )
						movedBot[j] = true;
				}
			}
		}

		for ( var k = 0; k < _agents.Count; k++ )
		{
			if ( !movedBot[k] || !_agents[k].IsBot )
				continue;

			var agent = _agents[k];
			var from = agent.Root.WorldPosition;
			var to = agent.Position;
			var scene = GameObject.Scene;
			if ( scene is { IsValid: true }
			     && YaPawnPlacement.TrySlideHorizontal( scene, agent.Root, from, to, out var resolved ) )
			{
				agent.Position = resolved;
				YaPawnPlacement.ApplyFeetPosition( agent.Root, resolved );
			}
			else
				agent.Position = from;
		}
	}
}
