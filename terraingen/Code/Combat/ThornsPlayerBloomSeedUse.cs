namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Player;
using Terraingen.World;

/// <summary>Hold Use (E) on a Bloom Seed to purify it.</summary>
[Title( "Thorns Player Bloom Seed Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerBloomSeedUse : Component
{
	float _holdSeconds;
	int _targetSeedId;

	public static bool HasBloomSeedTargetInFront( GameObject playerRoot ) =>
		ThornsBloomSeedWorldService.Instance?.HasTargetInFront( playerRoot ) == true;

	public float PurifyHoldFraction =>
		ThornsBloomSeedWorldService.PurifyHoldSeconds <= 0f
			? 0f
			: Math.Clamp( _holdSeconds / ThornsBloomSeedWorldService.PurifyHoldSeconds, 0f, 1f );

	public bool TryGetPurifyPrompt( out string verb, out float holdFraction )
	{
		verb = "";
		holdFraction = 0f;

		if ( ThornsBloomSeedWorldService.Instance?.TryPickAlongRay( GameObject, out var seedId, out _ ) != true )
			return false;

		_targetSeedId = seedId;
		verb = "purify Bloom Seed";
		holdFraction = PurifyHoldFraction;
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
		{
			ResetHold();
			return;
		}

		if ( ShouldBlockPurify()
		     || ThornsBloomSeedWorldService.Instance?.TryPickAlongRay( GameObject, out var seedId, out _ ) != true )
		{
			ResetHold();
			return;
		}

		if ( _targetSeedId != seedId )
		{
			_targetSeedId = seedId;
			_holdSeconds = 0f;
		}

		if ( !Input.Down( "Use" ) )
		{
			ResetHold();
			return;
		}

		_holdSeconds += Time.Delta;
		if ( _holdSeconds < ThornsBloomSeedWorldService.PurifyHoldSeconds )
			return;

		ResetHold();
		var gameplay = Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );
		if ( gameplay.IsValid() )
		{
			gameplay.RequestPurifyBloomSeed();
			ThornsPlayerUseGrabPresentation.PlayAction( GameObject, ThornsFpGrabAction.SweepDown );
		}
	}

	bool ShouldBlockPurify()
	{
		return Components.Get<ThornsPlayerMountController>()?.IsMounted == true
		       || ThornsDeathCrateWorldService.Instance?.HasTargetInFront( GameObject ) == true
		       || ThornsAirdropWorldService.Instance?.HasTargetInFront( GameObject ) == true
		       || Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true;
	}

	void ResetHold()
	{
		_holdSeconds = 0f;
		_targetSeedId = 0;
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
