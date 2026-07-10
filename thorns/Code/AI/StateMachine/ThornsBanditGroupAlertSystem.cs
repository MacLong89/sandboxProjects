namespace Sandbox;

public static class ThornsBanditGroupAlertSystem
{
	public static void BroadcastAlert(
		ThornsBanditBrainContext sourceCtx,
		GameObject target,
		Vector3 lastKnown )
	{
		if ( !Networking.IsHost || sourceCtx?.Brain is null || !sourceCtx.Brain.IsValid() )
			return;

		var selfFlat = sourceCtx.Self.WorldPosition.WithZ( 0 );
		var alertR = sourceCtx.Archetype.AlertRadiusWorld;
		var r2 = alertR * alertR;

		ThornsBanditHearingHub.HostRegisterAllyAlert( lastKnown );

		foreach ( var ally in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( !ally.IsValid() || ally == sourceCtx.Brain )
				continue;

			var allyFlat = ally.GameObject.WorldPosition.WithZ( 0 );
			if ( ( allyFlat - selfFlat ).LengthSquared > r2 )
				continue;

			var allyCtx = ally.StateMachineContext;
			if ( allyCtx is null )
				continue;

			if ( sourceCtx.GroupId != 0 && allyCtx.GroupId != sourceCtx.GroupId )
				continue;

			allyCtx.AlertLevel = Math.Max( allyCtx.AlertLevel, sourceCtx.AlertLevel + 1 );
			allyCtx.AlertedBy = sourceCtx.Self;
			allyCtx.LastKnownTargetPosition = lastKnown;
			if ( target.IsValid() )
				allyCtx.CurrentTarget = target;

			ally.StateMachine.TryTransition( allyCtx, ThornsBanditAiState.Alert, "group-alert" );
		}
	}
}
