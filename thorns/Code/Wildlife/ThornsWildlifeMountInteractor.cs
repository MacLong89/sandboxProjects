using System;

namespace Sandbox;

/// <summary>
/// Owner: tap Use (E) on your mountable tame to mount; Jump hops the tame while riding; crouch or tap Use on the same tame to dismount. Host parents pawn to tame; wildlife AI is bypassed while mounted — motor follows rider steer RPCs only.
/// </summary>
[Title( "Thorns — Wildlife mount interactor" )]
[Category( "Thorns/Wildlife" )]
[Icon( "directions_bike" )]
[Order( 74 )]
public sealed class ThornsWildlifeMountInteractor : Component
{
	[Sync( SyncFlags.FromHost )] public string MountedWildlifeIdSync { get; set; } = "";

	public Guid MountedWildlifeId =>
		string.IsNullOrWhiteSpace( MountedWildlifeIdSync )
			? Guid.Empty
			: Guid.TryParse( MountedWildlifeIdSync, out var g )
				? g
				: Guid.Empty;

	Guid _mountSteerSessionWildlifeId = Guid.Empty;
	Vector3 _lastSentQuantizedSteer = new Vector3( 9999f, 9999f, 0f );
	double _lastMountSteerSendTime = -1e9;
	bool _mountSteerNeedImmediateSend;
	Guid _presentationMountId = Guid.Empty;

	bool UiBlocksMountInput()
	{
		var hud = Components.Get<ThornsDebugHudHost>();
		var hp = Components.Get<ThornsHealth>();
		var build = Components.Get<ThornsBuildingController>();
		var shell = Components.Get<ThornsGameShell>();
		var shellBlocks = shell is not null && shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay;
		return shellBlocks
		       || (hud.IsValid() && (hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop))
		       || (hp.IsValid() && hp.IsDeadState)
		       || (build.IsValid() && build.BuildModeActive);
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( MountedWildlifeId != Guid.Empty )
		{
			ThornsMountHoldHudBridge.Clear();

			if ( Input.Pressed( "jump" ) )
			{
				if ( !UiBlocksMountInput() )
				{
					if ( Networking.IsHost )
						HostTryApplyMountJumpFromOwner( MountedWildlifeId );
					else
						RpcRequestMountJump( MountedWildlifeId );

					var citizenDriver = Components.Get<ThornsCitizenBodyDriver>();
					if ( citizenDriver.IsValid() )
						citizenDriver.NotifyJumpAnim();
				}

				return;
			}

			if ( Input.Pressed( "duck" ) || Input.Pressed( "Duck" ) )
			{
				ThornsWildlifeMountDebug.Write( $"dismount requested duck pawn={GameObject.Name}" );
				RpcRequestDismountOnly();
				return;
			}

			if ( ThornsInputInteract.IsUseOrInteractPressed() && !UiBlocksMountInput() )
			{
				if ( ThornsWildlifeMountRules.ClientTryGetMountTapTarget( GameObject, out var aimWid, out _ )
				     && aimWid.IsValid()
				     && aimWid.WildlifeId == MountedWildlifeId )
				{
					ThornsWildlifeMountDebug.Write( $"dismount requested use on mount wildlifeId={MountedWildlifeId}" );
					RpcRequestDismountOnly();
				}
			}

			return;
		}

		if ( UiBlocksMountInput() )
		{
			ThornsMountHoldHudBridge.Clear();
			return;
		}

		if ( ThornsTameHoldHudBridge.IsMountInteractSuppressed()
		     || ThornsTameHoldHudBridge.Phase is ThornsTameHudPhase.ReadyHold or ThornsTameHudPhase.Holding )
		{
			ThornsMountHoldHudBridge.Clear();
			return;
		}

		var nearRadio = ThornsRadioStation.FindBestUnderAimForPawn( GameObject.Scene, GameObject, 420f );
		if ( nearRadio.IsValid() && nearRadio.StationId != Guid.Empty && nearRadio.HostIsInRange( GameObject.WorldPosition ) )
		{
			if ( ThornsInputInteract.IsUseOrInteractPressed() )
			{
				ThornsMountHoldHudBridge.Clear();
				ThornsWildlifeMountDebug.Write( "Use tap ignored (near radio station)" );
				return;
			}
		}

		if ( !ThornsInputInteract.IsUseOrInteractPressed() )
			return;

		if ( !ThornsWildlifeMountRules.ClientTryGetMountTapTarget( GameObject, out var wid, out var reject ) || !wid.IsValid() )
		{
			ThornsMountHoldHudBridge.Clear();
			ThornsWildlifeMountDebug.Write( $"mount tap rejected: {reject}" );
			return;
		}

		ThornsMountHoldHudBridge.Clear();
		ThornsWildlifeMountDebug.Write( $"mount tap OK wildlifeId={wid.WildlifeId} species={wid.Species} → complete mount" );
		HostTryRequestCompleteMount( wid.WildlifeId );
	}

	protected override void OnFixedUpdate()
	{
		if ( Game.IsPlaying )
			ThornsMountInputNetMetrics.TickWindowIfNeeded();

		if ( !Game.IsPlaying )
			return;

		if ( Networking.IsHost )
			ThornsWildlifeMountHost.HostUnstickPawnOrphanedWildlifeParent( GameObject );

		if ( ThornsPawn.IsLocalConnectionOwner( this ) )
			TickLocalMountPresentation();

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( MountedWildlifeId == Guid.Empty )
		{
			_mountSteerSessionWildlifeId = Guid.Empty;
			return;
		}

		if ( MountedWildlifeId != _mountSteerSessionWildlifeId )
		{
			_mountSteerSessionWildlifeId = MountedWildlifeId;
			_lastSentQuantizedSteer = new Vector3( 9999f, 9999f, 0f );
			_lastMountSteerSendTime = -1e9;
			_mountSteerNeedImmediateSend = true;
		}

		var raw = Input.AnalogMove;
		var rawFlat = new Vector3( raw.x, raw.y, 0f );
		if ( rawFlat.LengthSquared < ThornsPerformanceBudgets.MountInputAnalogDeadzone * ThornsPerformanceBudgets.MountInputAnalogDeadzone )
			rawFlat = Vector3.Zero;
		else if ( rawFlat.Length > 1f )
			rawFlat = rawFlat.Normal;

		var movement = Components.Get<ThornsPawnMovement>();
		var steerYaw = movement.IsValid() ? movement.LookAngles.yaw : GameObject.WorldRotation.Yaw();
		var wish = Rotation.FromYaw( steerYaw ) * rawFlat;
		wish = wish.WithZ( 0f );
		if ( wish.Length > 1f )
			wish = wish.Normal;

		var quantized = ThornsMountInputQuantizer.QuantizePlanarSteer(
			wish,
			ThornsPerformanceBudgets.MountInputQuantizationSteps );

		var now = Time.Now;
		var minInterval = ThornsPerformanceBudgets.MountInputSendInterval;
		var forceEvery = ThornsPerformanceBudgets.MountInputForceSendInterval;
		var elapsed = now - _lastMountSteerSendTime;

		var changed = (quantized - _lastSentQuantizedSteer).LengthSquared > 1e-8f;
		var forceDue = elapsed >= forceEvery;
		var minDue = elapsed >= minInterval - 1e-4f;

		var shouldSend = _mountSteerNeedImmediateSend || forceDue || (changed && minDue);
		if ( !shouldSend )
			return;

		if ( Networking.IsHost )
			HostTryApplyMountSteerFromOwner( MountedWildlifeId, quantized );
		else
			RpcSubmitMountSteer( MountedWildlifeId, quantized );

		ThornsMountInputNetMetrics.RecordClientSent();
		_lastSentQuantizedSteer = quantized;
		_lastMountSteerSendTime = now;
		_mountSteerNeedImmediateSend = false;
	}

	void TickLocalMountPresentation()
	{
		var mountId = MountedWildlifeId;
		if ( mountId == Guid.Empty )
		{
			if ( _presentationMountId != Guid.Empty )
			{
				ThornsWildlifeMountHost.LocalEnsureRiderDetachedFromMount( GameObject );
				_presentationMountId = Guid.Empty;
			}

			return;
		}

		if ( !ThornsWildlifeIdentity.TryFindByWildlifeId( GameObject.Scene, mountId, out var wid ) || !wid.IsValid() )
			return;

		ThornsWildlifeMountHost.LocalSyncRiderMountPresentation( GameObject, wid );
		_presentationMountId = mountId;
	}

	[Rpc.Host]
	public void RpcRequestDismountOnly()
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
		{
			ThornsWildlifeMountDebug.Write( "RpcRequestDismountOnly: rejected (not host or caller/owner mismatch)" );
			return;
		}

		ThornsWildlifeMountDebug.Write( $"RpcRequestDismountOnly: caller={Rpc.Caller.Id} pawn={GameObject.Name}" );
		ThornsWildlifeMountHost.HostDismountPawnIfMounted( GameObject );
	}

	[Rpc.Host]
	public void RpcRequestMountJump( Guid wildlifeInstanceId )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !HostTryApplyMountJumpCore( wildlifeInstanceId, Rpc.Caller.Id ) )
			ThornsWildlifeMountDebug.Write( $"RpcRequestMountJump({wildlifeInstanceId}): rejected" );
	}

	void HostTryApplyMountJumpFromOwner( Guid wildlifeInstanceId )
	{
		if ( !Networking.IsHost )
			return;

		var ownerId = GameObject.Network.OwnerId;
		if ( ownerId == Guid.Empty )
			return;

		_ = HostTryApplyMountJumpCore( wildlifeInstanceId, ownerId );
	}

	bool HostTryApplyMountJumpCore( Guid wildlifeInstanceId, Guid riderConnectionId )
	{
		if ( wildlifeInstanceId == Guid.Empty )
			return false;

		if ( !ThornsWildlifeIdentity.ActiveByHost.TryGetValue( wildlifeInstanceId, out var wid ) || !wid.IsValid() )
			return false;

		if ( wid.TameRiderConnectionId != riderConnectionId )
			return false;

		if ( !wid.Definition.AllowPlayerMount )
			return false;

		var hpW = wid.GameObject.Components.Get<ThornsHealth>();
		if ( !hpW.IsValid() || !hpW.IsAlive || hpW.IsDeadState )
			return false;

		var motor = wid.GameObject.Components.Get<ThornsWildlifeMotor>();
		if ( !motor.IsValid() )
			return false;

		return motor.HostTryApplyRiderJumpImpulse();
	}

	void HostTryRequestCompleteMount( Guid wildlifeInstanceId )
	{
		if ( Networking.IsHost )
		{
			var ownerId = GameObject.Network.OwnerId;
			if ( ownerId == Guid.Empty )
				return;

			HostTryCompleteMountCore( wildlifeInstanceId, ownerId );
			return;
		}

		RpcRequestCompleteMount( wildlifeInstanceId );
	}

	[Rpc.Host]
	public void RpcRequestCompleteMount( Guid wildlifeInstanceId )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
		{
			ThornsWildlifeMountDebug.Write(
				$"RpcRequestCompleteMount({wildlifeInstanceId}): rejected caller/owner (caller={(Rpc.Caller is null ? "null" : Rpc.Caller.Id.ToString())} pawnOwner={GameObject.Network.OwnerId})" );
			return;
		}

		HostTryCompleteMountCore( wildlifeInstanceId, Rpc.Caller.Id );
	}

	void HostTryCompleteMountCore( Guid wildlifeInstanceId, Guid riderConnectionId )
	{
		if ( !Networking.IsHost )
			return;

		if ( wildlifeInstanceId == Guid.Empty )
		{
			ThornsWildlifeMountDebug.Write( "HostTryCompleteMountCore: rejected empty wildlife id" );
			return;
		}

		if ( !ThornsWildlifeTameInteractor.TryGetCallerTame( wildlifeInstanceId, riderConnectionId, out var wid ) || !wid.IsValid() )
		{
			ThornsWildlifeMountDebug.Write(
				$"HostTryCompleteMountCore({wildlifeInstanceId}): TryGetCallerTame failed caller={riderConnectionId} activeByHost={ThornsWildlifeIdentity.ActiveByHost.ContainsKey( wildlifeInstanceId )}" );
			return;
		}

		if ( !wid.Definition.AllowPlayerMount )
		{
			ThornsWildlifeMountDebug.Write( $"HostTryCompleteMountCore({wildlifeInstanceId}): species not mountable species={wid.Species}" );
			return;
		}

		var mountIx = GameObject.Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId == wildlifeInstanceId )
		{
			ThornsWildlifeMountDebug.Write( $"HostTryCompleteMountCore({wildlifeInstanceId}): already mounted on this tame" );
			return;
		}

		var tameGo = wid.GameObject;
		if ( !tameGo.IsValid() )
		{
			ThornsWildlifeMountDebug.Write( $"HostTryCompleteMountCore({wildlifeInstanceId}): invalid tame GameObject" );
			return;
		}

		var hpW = tameGo.Components.Get<ThornsHealth>();
		if ( !hpW.IsValid() || !hpW.IsAlive || hpW.IsDeadState )
		{
			ThornsWildlifeMountDebug.Write(
				$"HostTryCompleteMountCore({wildlifeInstanceId}): tame not rideable hpValid={hpW.IsValid()} alive={hpW.IsValid() && hpW.IsAlive} deadState={hpW.IsValid() && hpW.IsDeadState}" );
			return;
		}

		if ( !ThornsWildlifeMountRules.PawnCanMountTargetTame( GameObject, wid ) )
		{
			ThornsWildlifeMountDebug.Write( $"HostTryCompleteMountCore({wildlifeInstanceId}): rejected not_aiming_at_tame" );
			return;
		}

		if ( wid.HostBondedAtRealtime > 0 && Time.Now - wid.HostBondedAtRealtime < 1.25 )
		{
			ThornsWildlifeMountDebug.Write( $"HostTryCompleteMountCore({wildlifeInstanceId}): rejected recent_tame_bond" );
			return;
		}

		ThornsWildlifeMountHost.HostUnstickPawnOrphanedWildlifeParent( GameObject );

		ThornsWildlifeMountDebug.Write( $"HostTryCompleteMountCore({wildlifeInstanceId}): applying mount tame={tameGo.Name} rider={GameObject.Name}" );
		ThornsWildlifeMountHost.HostApplyMount( wid, GameObject );
	}

	[Rpc.Host( NetFlags.Unreliable )]
	public void RpcSubmitMountSteer( Guid wildlifeInstanceId, Vector3 planarWish )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( wildlifeInstanceId == Guid.Empty )
			return;

		if ( !ThornsWildlifeIdentity.ActiveByHost.TryGetValue( wildlifeInstanceId, out var wid ) || !wid.IsValid() )
			return;

		if ( wid.TameRiderConnectionId != Rpc.Caller.Id )
		{
			ThornsWildlifeMountDebug.Write(
				$"RpcSubmitMountSteer({wildlifeInstanceId}): rider mismatch tameRider={wid.TameRiderConnectionId} caller={Rpc.Caller.Id}" );
			return;
		}

		if ( !wid.Definition.AllowPlayerMount )
		{
			ThornsWildlifeMountDebug.Write( $"RpcSubmitMountSteer({wildlifeInstanceId}): species not mountable — dismount." );
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( wid );
			return;
		}

		var clamped = ThornsMountInputQuantizer.ClampHostPlanarSteer( planarWish );
		wid.HostMountSteerPlanar = clamped;
		wid.HostLastMountSteerReceiveTime = Time.Now;
		ThornsMountInputNetMetrics.RecordHostRecv();
	}

	/// <summary>Listen-server path: same validation as <see cref="RpcSubmitMountSteer"/> using pawn owner id (no <see cref="Rpc.Caller"/>).</summary>
	void HostTryApplyMountSteerFromOwner( Guid wildlifeInstanceId, Vector3 planarWish )
	{
		if ( !Networking.IsHost )
			return;

		var ownerId = GameObject.Network.OwnerId;
		if ( ownerId == Guid.Empty || wildlifeInstanceId == Guid.Empty )
			return;

		if ( !ThornsWildlifeIdentity.ActiveByHost.TryGetValue( wildlifeInstanceId, out var wid ) || !wid.IsValid() )
			return;

		if ( wid.TameRiderConnectionId != ownerId )
			return;

		if ( !wid.Definition.AllowPlayerMount )
		{
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( wid );
			return;
		}

		var clamped = ThornsMountInputQuantizer.ClampHostPlanarSteer( planarWish );
		wid.HostMountSteerPlanar = clamped;
		wid.HostLastMountSteerReceiveTime = Time.Now;
		ThornsMountInputNetMetrics.RecordHostRecv();
	}
}
