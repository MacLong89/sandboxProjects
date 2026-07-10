namespace Sandbox;

/// <summary>Unified threat evaluation — alert, flee, guard, and hunt target scoring.</summary>
public static class ThornsAnimalThreatPipeline
{
	static readonly List<ThornsAnimalThreatSystem.ThreatCandidate> AlertScratch = new();

	public static bool TryEnterAlertFromPerception(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		ThornsAnimalStateMachine machine )
	{
		ThornsAnimalTargetingService.BuildAlertCandidates( ctx, director, selfFlat, AlertScratch );

		if ( !ThornsAnimalThreatSystem.TrySelectBestThreat(
			     ctx,
			     director,
			     selfFlat,
			     AlertScratch,
			     out var best,
			     out _ ) )
			return false;

		ctx.FocusTarget = best;
		return machine.TryTransition( ctx, ThornsWildlifeAiState.Alert, "perception->alert" );
	}

	public static bool TryBeginFleeFromAttacker(
		ThornsAnimalBrainContext ctx,
		Vector3 selfFlat,
		GameObject attackerRoot,
		ThornsAnimalStateMachine machine,
		string reason = "flee-attacker" )
	{
		if ( attackerRoot is null || !attackerRoot.IsValid() )
			return false;

		ctx.FleeThreatRoot = attackerRoot;
		var away = selfFlat - attackerRoot.WorldPosition.WithZ( 0 );
		if ( away.LengthSquared > 4f )
			ctx.FleeWishPlanar = away.Normal * ctx.Definition.ChaseSpeed * ctx.SpeedMultiplier;

		return machine.TryTransition( ctx, ThornsWildlifeAiState.Flee, reason );
	}

	public static bool TryBeginFleeFromThreat(
		ThornsAnimalBrainContext ctx,
		Vector3 selfFlat,
		GameObject threat,
		ThornsAnimalStateMachine machine,
		string reason = "flee-threat" )
	{
		if ( !threat.IsValid() )
			return false;

		ctx.FleeUntilRealtime = Time.Now + MathF.Max( 2.8f, 2.6f + ctx.BehaviorProfile.Fearfulness * 2.2f );
		ctx.FleeThreatRoot = threat;
		ctx.FleeWishPlanar = ( selfFlat - threat.WorldPosition.WithZ( 0 ) ).Normal
		                     * ctx.Definition.ChaseSpeed * ctx.SpeedMultiplier;

		if ( ctx.Self.IsValid() && ctx.CurrentState != ThornsWildlifeAiState.Flee )
		{
			ThornsWildlifeLog.Target( ctx.Self.Name, threat.Name );
			ThornsAnimalHerdCoordinator.BroadcastFleePanic( ctx.Self, threat );
		}

		return machine.TryTransition( ctx, ThornsWildlifeAiState.Flee, reason );
	}
}
