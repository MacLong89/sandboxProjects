namespace Sandbox;

/// <summary>Pack leader / member spacing and shared target inheritance.</summary>
public static class ThornsAnimalPackSystem
{
	public static bool TryGetPackLeader( ThornsWildlifeBrain self, out GameObject leader )
	{
		leader = default;
		if ( !self.IsValid() )
			return false;

		var id = self.Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() )
			return false;

		// Future: explicit PackLeaderId on identity. For now use nearest same-species predator within follow band.
		var flat = self.GameObject.WorldPosition.WithZ( 0 );
		var best = float.MaxValue;
		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() || brain == self )
				continue;

			var otherId = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !otherId.IsValid() || otherId.Species != id.Species )
				continue;

			if ( otherId.HostIsTamed != id.HostIsTamed )
				continue;

			var d2 = ( brain.GameObject.WorldPosition.WithZ( 0 ) - flat ).LengthSquared;
			if ( d2 > 2400f * 2400f || d2 >= best )
				continue;

			best = d2;
			leader = brain.GameObject;
		}

		return leader.IsValid();
	}

	public static void InheritLeaderTarget( ThornsAnimalBrainContext ctx, GameObject leader )
	{
		if ( !leader.IsValid() )
			return;

		var leaderBrain = leader.Components.Get<ThornsWildlifeBrain>();
		if ( !leaderBrain.IsValid() )
			return;

		var leaderCtx = leaderBrain.StateMachineContext;
		if ( leaderCtx?.FocusTarget is { } t && t.IsValid() )
		{
			ctx.FocusTarget = t;
			ctx.CurrentTarget = t;
		}
	}
}
