namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Hud;
using Terraingen.World;

/// <summary>Hold Use (E) on an owned tamed deer or moose to mount; hold E again or Ctrl to dismount.</summary>
[Title( "Thorns Player Mount Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerMountUse : Component
{
	ThornsAnimalBrain _holdTarget;
	float _holdSeconds;

	public float MountHoldFraction =>
		ThornsAnimalMounting.MountHoldSeconds <= 0f
			? 0f
			: Math.Clamp( _holdSeconds / ThornsAnimalMounting.MountHoldSeconds, 0f, 1f );

	public bool HasMountTargetInFront()
	{
		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return false;

		return FindMountTargetInFront() is { } brain && brain.IsValid;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		var mount = Components.Get<ThornsPlayerMountController>();
		if ( mount is null )
			return;

		if ( !Input.Down( "Use" ) )
		{
			ResetHold();
			return;
		}

		if ( ThornsDamageFlashState.WasRecentlyDamaged )
		{
			ResetHold();
			return;
		}

		if ( ThornsDeathCrateWorldService.Instance?.HasTargetInFront( GameObject ) == true )
		{
			ResetHold();
			return;
		}

		if ( ThornsAirdropWorldService.Instance?.HasTargetInFront( GameObject ) == true )
		{
			ResetHold();
			return;
		}

		ThornsAnimalBrain candidate;
		if ( mount.IsMounted )
		{
			candidate = mount.ResolveMountedBrain();
			if ( candidate is null || !candidate.IsValid() )
			{
				ResetHold();
				return;
			}
		}
		else
		{
			candidate = FindMountTargetInFront();
			if ( candidate is null || !candidate.IsValid() )
			{
				ResetHold();
				return;
			}
		}

		if ( candidate != _holdTarget )
		{
			_holdTarget = candidate;
			_holdSeconds = 0f;
		}

		_holdSeconds += Time.Delta;
		ThornsInteractionPrompt.Invalidate();

		if ( _holdSeconds < ThornsAnimalMounting.MountHoldSeconds )
			return;

		ResetHold();

		ThornsPlayerUseGrabPresentation.PlayAction( GameObject, ThornsFpGrabAction.SweepDown );

		if ( mount.IsMounted )
		{
			if ( Networking.IsActive && !Networking.IsHost )
				RpcRequestDismount();
			else
				mount.HostTryDismount();
			return;
		}

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestMount( candidate.GameObject.Id );
		else
			mount.HostTryMount( candidate );
	}

	void ResetHold()
	{
		if ( _holdTarget is not null )
			ThornsInteractionPrompt.Invalidate();

		_holdTarget = null;
		_holdSeconds = 0f;
	}

	ThornsAnimalBrain FindMountTargetInFront()
	{
		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay is null )
			return null;

		if ( !ThornsInteractAimPick.TryResolveCrosshairAimRay( GameObject, out var origin, out var forward ) )
			return null;

		var accountKey = gameplay.AccountKey;
		if ( ThornsAnimalHitUtil.TryPickBrainAlongRay(
			     Scene,
			     origin,
			     forward,
			     ThornsAnimalMounting.MountMaxRange,
			     GameObject,
			     out var brain,
			     candidate => ThornsAnimalMounting.IsMountableSpecies( candidate )
			                  && ThornsAnimalMounting.IsOwnedByAccount( candidate, accountKey )
			                  && ( !candidate.IsMounted || candidate.MountedRiderId == GameObject.Id ) ) )
			return brain;

		return null;
	}

	[Rpc.Host]
	void RpcRequestMount( Guid animalGameObjectId )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		var mount = Components.Get<ThornsPlayerMountController>();
		if ( mount is null )
			return;

		var brain = ThornsAnimalManager.TryGetByObjectId( animalGameObjectId );
		if ( brain is not null )
			mount.HostTryMount( brain );
	}

	[Rpc.Host]
	void RpcRequestDismount()
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		Components.Get<ThornsPlayerMountController>()?.HostTryDismount();
	}

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocallyControlledPawn( GameObject );
}
