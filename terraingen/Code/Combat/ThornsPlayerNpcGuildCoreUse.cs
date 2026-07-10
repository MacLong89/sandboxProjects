namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.NpcGuild;
using Terraingen.Player;
using Terraingen.World;

/// <summary>Hold Use (E) on an NPC guild outpost core to claim and destroy it.</summary>
public sealed class ThornsPlayerNpcGuildCoreUse : Component
{
	float _holdSeconds;
	ThornsNpcGuildCore _targetCore;

	public static bool HasCoreTargetInFront( GameObject playerRoot )
		=> ThornsNpcGuildCore.TryPickAlongRay( playerRoot, out _, out _ );

	public bool IsClaiming => _holdSeconds > 0f && _targetCore is not null && _targetCore.IsValid();

	public float ClaimHoldFraction =>
		ThornsNpcGuildCore.ClaimHoldSeconds <= 0f
			? 0f
			: Math.Clamp( _holdSeconds / ThornsNpcGuildCore.ClaimHoldSeconds, 0f, 1f );

	public bool TryGetClaimPrompt( out string verb, out float holdFraction )
	{
		verb = "";
		holdFraction = 0f;
		if ( !ThornsNpcGuildCore.TryPickAlongRay( GameObject, out var core, out _ ) || core is null )
			return false;

		_targetCore = core;
		verb = core.IsHeadquarters ? "claim rival HQ core" : "claim outpost core";
		holdFraction = ClaimHoldFraction;
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
		{
			ResetHold();
			return;
		}

		if ( ShouldBlockClaim() || !ThornsNpcGuildCore.TryPickAlongRay( GameObject, out var core, out _ ) || core is null )
		{
			ResetHold();
			return;
		}

		_targetCore = core;

		if ( !Input.Down( "Use" ) )
		{
			ResetHold();
			return;
		}

		_holdSeconds += Time.Delta;
		if ( _holdSeconds < ThornsNpcGuildCore.ClaimHoldSeconds )
			return;

		var claimedCore = core;
		ResetHold();

		var gameplay = Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );
		if ( gameplay is null || !gameplay.IsValid() )
			return;

		gameplay.RequestClaimNpcGuildCore( claimedCore );
		ThornsPlayerUseGrabPresentation.PlayPushButton( GameObject );
	}

	bool ShouldBlockClaim()
	{
		return Components.Get<ThornsPlayerMountController>()?.IsMounted == true
		       || ThornsDeathCrateWorldService.Instance?.HasTargetInFront( GameObject ) == true
		       || ThornsAirdropWorldService.Instance?.HasTargetInFront( GameObject ) == true
		       || Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true;
	}

	void ResetHold()
	{
		_holdSeconds = 0f;
		_targetCore = null;
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
