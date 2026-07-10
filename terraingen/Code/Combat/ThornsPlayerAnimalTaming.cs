namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Hud;
using Terraingen.World;

/// <summary>Hold Use (E) on a low-HP animal to request host taming.</summary>
[Title( "Thorns Player Animal Taming" )]
[Category( "Player" )]
public sealed class ThornsPlayerAnimalTaming : Component
{
	ThornsAnimalBrain _holdTarget;
	float _holdSeconds;

	public float TameHoldFraction =>
		ThornsAnimalTaming.UseHoldSeconds <= 0f
			? 0f
			: Math.Clamp( _holdSeconds / ThornsAnimalTaming.UseHoldSeconds, 0f, 1f );

	public void CancelHold() => ResetHold();

	public bool TryGetPrompt( out string verbPhrase )
	{
		verbPhrase = "";
		var brain = FindTameableInFront();
		if ( !brain.IsValid() )
			return false;

		var speciesName = brain.Species?.DisplayName;
		verbPhrase = string.IsNullOrWhiteSpace( speciesName ) ? "Tame" : $"Tame {speciesName}";
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
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

		if ( ThornsAirdropWorldService.Instance?.HasTargetInFront( GameObject ) == true )
		{
			ResetHold();
			return;
		}

		if ( ThornsDeathCrateWorldService.Instance?.HasTargetInFront( GameObject ) == true )
		{
			ResetHold();
			return;
		}

		var candidate = FindTameableInFront();
		if ( !candidate.IsValid() )
		{
			ResetHold();
			return;
		}

		if ( candidate != _holdTarget )
		{
			_holdTarget = candidate;
			_holdSeconds = 0f;
		}

		_holdSeconds += Time.Delta;
		ThornsInteractionPrompt.Invalidate();

		if ( _holdSeconds < ThornsAnimalTaming.UseHoldSeconds )
			return;

		ResetHold();

		ThornsPlayerUseGrabPresentation.PlayAction( GameObject, ThornsFpGrabAction.SweepDown );

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestTame( candidate.GameObject.Id );
		else if ( TryHostTame( candidate ) )
			PlayTameSfx();
	}

	void ResetHold()
	{
		if ( _holdTarget is not null )
			ThornsInteractionPrompt.Invalidate();

		_holdTarget = null;
		_holdSeconds = 0f;
	}

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocallyControlledPawn( GameObject );

	public bool HasTameTargetInFront() => FindTameableInFront() is { } brain && brain.IsValid;

	ThornsAnimalBrain FindTameableInFront()
	{
		if ( !ThornsInteractAimPick.TryResolveCrosshairAimRay( GameObject, out var origin, out var forward ) )
			return null;

		if ( ThornsAnimalHitUtil.TryPickBrainAlongRay(
			     Scene,
			     origin,
			     forward,
			     ThornsAnimalTaming.UseMaxRange,
			     GameObject,
			     out var brain,
			     candidate => candidate.IsAwaitingTame && !candidate.IsTamed ) )
			return brain;

		return null;
	}

	[Rpc.Host]
	void RpcRequestTame( Guid animalGameObjectId )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		var brain = ThornsAnimalManager.TryGetByObjectId( animalGameObjectId );
		if ( brain is not null && TryHostTame( brain ) )
			PlayTameSfx();
	}

	bool TryHostTame( ThornsAnimalBrain brain ) =>
		brain.IsValid() && brain.HostTryTame( GameObject, Rpc.Caller ?? Connection.Local );

	void PlayTameSfx() =>
		ThornsGameplaySfx.PlayNetworkedPawnSound(
			GameObject,
			ThornsGameplaySfx.Tame,
			ThornsSpatialSfxCategory.PlayerInteraction );
}
