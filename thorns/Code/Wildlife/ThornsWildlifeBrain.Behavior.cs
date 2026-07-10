namespace Sandbox;

/// <summary>Pack/herd coordination, relationship-driven reactions, and peer separation helpers.</summary>
public sealed partial class ThornsWildlifeBrain
{
	public Vector3 HostApplyWildlifePeerSeparationToWish( Vector3 wishPlanar, float strengthMul = 1f )
	{
		if ( !HostTryGetWildlifePeerSeparationWish( out var sep ) || sep.LengthSquared < 1f )
			return wishPlanar;

		return wishPlanar + sep * strengthMul;
	}

	/// <summary>Pack mate inherits hunter focus (wolves).</summary>
	public void HostTryInheritPackHuntTarget( GameObject prey, GameObject hunter )
	{
		if ( !Networking.IsHost || prey is null || !prey.IsValid() || GameObject == hunter )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() || id.HostIsTamed || id.HostIsDead )
			return;

		var profile = ThornsAnimalBehaviorProfile.Get( id.Species );
		if ( profile.PackPreference < 0.45f )
			return;

		if ( _state is ThornsWildlifeAiState.Hunt or ThornsWildlifeAiState.Chase
		     or ThornsWildlifeAiState.Attack or ThornsWildlifeAiState.Stalk )
			return;

		var def = id.Definition;
		var dist = ( prey.WorldPosition.WithZ( 0 ) - GameObject.WorldPosition.WithZ( 0 ) ).Length;
		if ( dist > def.LoseRadius * 1.05f )
			return;

		_focusTarget = prey;
		_huntAbandonAfterRealtime = Time.Now + def.HuntCommitSeconds;
		_predatorPeaceUntilRealtime = 0;

		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			_stateMachineContext.FocusTarget = prey;
			_stateMachineContext.HuntAbandonAfterRealtime = _huntAbandonAfterRealtime;
			_stateMachineContext.PredatorPeaceUntilRealtime = 0;
			_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Hunt, "pack-inherit-hunt" );
			SyncBrainFieldsFromContext();
		}
		else
		{
			SetState( ThornsWildlifeAiState.Hunt );
		}
	}

	/// <summary>Herd mate inherits panic flee (elk/deer).</summary>
	public void HostTryInheritHerdFlee( GameObject threat, GameObject source )
	{
		if ( !Networking.IsHost || threat is null || !threat.IsValid() || GameObject == source )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() || id.HostIsTamed || id.HostIsDead || id.Definition.IsPredator )
			return;

		var profile = ThornsAnimalBehaviorProfile.Get( id.Species );
		if ( profile.HerdPreference < 0.35f )
			return;

		if ( _state == ThornsWildlifeAiState.Flee && Time.Now < _fleeUntil )
			return;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		var away = flat - threat.WorldPosition.WithZ( 0 );
		_fleeThreatRoot = threat;
		_fleeUntil = Time.Now + MathF.Max( 2.8f, 3.2f + profile.Fearfulness );
		if ( away.LengthSquared > 4f )
			_fleeWishPlanar = away.Normal * id.Definition.ChaseSpeed * id.GetEffectiveSpeedMultiplier();
		else
			_fleeWishPlanar = Vector3.Zero;

		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			_stateMachineContext.FleeThreatRoot = threat;
			_stateMachineContext.FleeUntilRealtime = _fleeUntil;
			_stateMachineContext.FleeWishPlanar = _fleeWishPlanar;
			_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Flee, "herd-panic" );
			SyncBrainFieldsFromContext();
		}
		else
		{
			SetState( ThornsWildlifeAiState.Flee );
		}
	}

	internal void HostNotifyNearbyAnimalsOfDeathEvent()
	{
		if ( !Networking.IsHost )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() )
			return;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		const float notifyRadius = 1800f;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() || brain == this )
				continue;

			var otherId = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !otherId.IsValid() || otherId.HostIsTamed || otherId.HostIsDead )
				continue;

			if ( flat.Distance( brain.GameObject.WorldPosition.WithZ( 0 ) ) > notifyRadius )
				continue;

			var rel = ThornsAnimalRelationshipTable.Resolve( otherId.Species, id.Species );
			if ( rel is ThornsAnimalRelationshipKind.Fear or ThornsAnimalRelationshipKind.Avoid )
				brain.HostTryInheritHerdFlee( GameObject, GameObject );
		}
	}
}
