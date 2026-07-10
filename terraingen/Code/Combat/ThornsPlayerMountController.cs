namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Riding a tamed deer or moose — movement rides on the mount, look stays first-person.</summary>
[Title( "Thorns Player Mount Controller" )]
[Category( "Player" )]
[Order( 120 )]
public sealed class ThornsPlayerMountController : Component
{
	[Sync( SyncFlags.FromHost )] public Guid MountedAnimalId { get; private set; }

	bool _mountControlsApplied;
	bool _seatPoseInitialized;
	double _lastMountMoveRpcTime;
	const double MountMoveRpcMinInterval = 0.05;

	public bool IsMounted => MountedAnimalId != Guid.Empty;

	public ThornsAnimalBrain ResolveMountedBrain() =>
		MountedAnimalId == Guid.Empty ? null : ThornsAnimalManager.TryGetByObjectId( MountedAnimalId );

	protected override void OnUpdate()
	{
		var health = Components.Get<ThornsPlayerHealth>();
		if ( health.IsValid() && ( !health.IsAlive || health.IsDeadState ) )
		{
			CleanupPresentationAfterDeathOrRespawn();
			return;
		}

		if ( GameObject.Parent.IsValid()
		     && GameObject.Parent.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelf ).IsValid()
		     && !IsMounted )
		{
			CleanupPresentationAfterDeathOrRespawn();
		}

		if ( !IsMounted )
		{
			if ( _mountControlsApplied || _seatPoseInitialized )
			{
				ApplyDismountPresentation();
				_mountControlsApplied = false;
				_seatPoseInitialized = false;
			}

			return;
		}

		var brain = ResolveMountedBrain();
		if ( brain is null || !brain.IsValid() || brain.IsDead || !brain.IsMounted )
		{
			if ( ThornsMultiplayer.IsHostOrOffline )
				HostForceDismount();
			else
				HostClearMountPresentation();
			return;
		}

		if ( !_mountControlsApplied )
		{
			ApplyMountedInputFlags();
			_mountControlsApplied = true;
		}

		if ( IsLocallyControlled() && Input.Pressed( "Duck" ) )
		{
			if ( Networking.IsActive && !Networking.IsHost )
				RpcRequestDismount();
			else
				HostTryDismount();
			return;
		}

		if ( IsLocallyControlled() )
		{
			var move = ReadMoveInput();
			var jump = Input.Pressed( "Jump" );
			if ( Networking.IsActive && !Networking.IsHost )
				RpcSendMountMove( move, jump );
			else
				brain.HostApplyRiderMoveInput( GameObject, move, jump );
		}

		TickSeatLookPose( brain );
	}

	public bool HostTryMount( ThornsAnimalBrain brain )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsMounted || brain is null || !brain.IsValid() )
			return false;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !ThornsAnimalMounting.IsOwnedByAccount( brain, gameplay.AccountKey ) )
			return false;

		if ( !brain.HostTryMount( GameObject ) )
			return false;

		MountedAnimalId = brain.GameObject.Id;
		_mountControlsApplied = false;
		_seatPoseInitialized = false;
		ApplyMountPresentation( brain );
		return true;
	}

	public bool HostTryDismount()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsMounted )
			return false;

		var brain = ResolveMountedBrain();
		brain?.HostTryDismount( GameObject );
		MountedAnimalId = Guid.Empty;
		ApplyDismountPresentation();
		return true;
	}

	public void HostForceDismount()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var wasMounted = IsMounted || _mountControlsApplied;
		var brain = ResolveMountedBrain();
		brain?.HostTryDismount( GameObject );
		MountedAnimalId = Guid.Empty;

		if ( wasMounted )
			ApplyDismountPresentation();
		else
			CleanupPresentationAfterDeathOrRespawn();
	}

	/// <summary>Release mount sync on the host and always reset rider presentation.</summary>
	public void HostCleanupMountForDeath()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
		{
			var brain = ResolveMountedBrain();
			brain?.HostTryDismount( GameObject );
			MountedAnimalId = Guid.Empty;
		}

		CleanupPresentationAfterDeathOrRespawn();
	}

	/// <summary>Local presentation reset — unparent, restore FP controls, never warp to a stale mount save point.</summary>
	public void CleanupPresentationAfterDeathOrRespawn()
	{
		RestorePlayerControlsAndStopBody();

		if ( GameObject.Parent.IsValid() )
			GameObject.Parent = null;

		_mountControlsApplied = false;
		_seatPoseInitialized = false;
	}

	public void HostClearMountPresentation()
	{
		MountedAnimalId = Guid.Empty;
		ApplyDismountPresentation();
		_mountControlsApplied = false;
	}

	[Rpc.Host]
	void RpcRequestDismount()
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryDismount();
	}

	[Rpc.Host]
	void RpcSendMountMove( Vector2 move, bool jumpPressed )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		if ( Time.Now - _lastMountMoveRpcTime < MountMoveRpcMinInterval )
			return;

		_lastMountMoveRpcTime = Time.Now;

		var brain = ResolveMountedBrain();
		brain?.HostApplyRiderMoveInput( GameObject, move, jumpPressed );
	}

	static Vector2 ReadMoveInput()
	{
		// Mount expects (x = turn, y = forward). AnalogMove is (x = forward, y = strafe) in s&box.
		var analog = Input.AnalogMove;
		if ( analog.Length > 0.05f )
			return new Vector2( analog.y, analog.x );

		var turn = (Input.Down( "Right" ) ? 1f : 0f) - (Input.Down( "Left" ) ? 1f : 0f);
		var forward = (Input.Down( "Forward" ) ? 1f : 0f) - (Input.Down( "Backward" ) ? 1f : 0f);
		return new Vector2( turn, forward );
	}

	void ApplyMountPresentation( ThornsAnimalBrain brain )
	{
		ApplyMountedInputFlags();
		EnsureSeatParented( brain );
		TickSeatLookPose( brain );
	}

	void ApplyDismountPresentation()
	{
		var mountRoot = GameObject.Parent;
		var dismountWorldPosition = ResolveDismountWorldPosition( mountRoot, GameObject.WorldPosition );
		var dismountWorldRotation = GameObject.WorldRotation;

		RestorePlayerControlsAndStopBody();

		GameObject.Parent = null;
		GameObject.WorldPosition = dismountWorldPosition;
		GameObject.WorldRotation = dismountWorldRotation;

		_mountControlsApplied = false;
		_seatPoseInitialized = false;
	}

	static Vector3 ResolveDismountWorldPosition( GameObject mountRoot, Vector3 fallbackWorldPosition )
	{
		if ( !mountRoot.IsValid() )
			return fallbackWorldPosition + Vector3.Up * 8f;

		var side = mountRoot.WorldRotation.Right.WithZ( 0f );
		if ( side.LengthSquared < 1e-4f )
			side = Vector3.Right;
		else
			side = side.Normal;

		return mountRoot.WorldPosition + side * 72f + Vector3.Up * 16f;
	}

	void RestorePlayerControlsAndStopBody()
	{
		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		controller.UseInputControls = true;
		controller.UseLookControls = true;
		controller.UseCameraControls = true;

		var body = controller.Body;
		if ( body.IsValid() )
			body.Velocity = Vector3.Zero;
	}

	void ApplyMountedInputFlags()
	{
		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		controller.UseInputControls = false;
		controller.UseLookControls = true;
		controller.UseCameraControls = true;
	}

	void EnsureSeatParented( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.GameObject.IsValid() )
			return;

		var mountRoot = brain.GameObject;
		var seatOffset = ThornsAnimalMounting.SeatLocalOffset( brain.SpeciesId );

		if ( !_seatPoseInitialized || GameObject.Parent != mountRoot )
		{
			GameObject.Parent = mountRoot;
			GameObject.LocalPosition = seatOffset;
			_seatPoseInitialized = true;
			return;
		}

		if ( (GameObject.LocalPosition - seatOffset).LengthSquared > 0.25f )
			GameObject.LocalPosition = seatOffset;
	}

	void TickSeatLookPose( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.GameObject.IsValid() )
			return;

		EnsureSeatParented( brain );

		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( controller.IsValid() )
		{
			var body = controller.Body;
			if ( body.IsValid() )
				body.Velocity = Vector3.Zero;

			// Counter-rotate against the mount so mouse look stays in world space while the animal moves beneath.
			var lookWorld = controller.EyeAngles.ToRotation();
			GameObject.LocalRotation = brain.GameObject.WorldRotation.Inverse * lookWorld;
		}
		else
		{
			GameObject.LocalRotation = Rotation.Identity;
		}
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
