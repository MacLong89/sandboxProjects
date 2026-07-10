namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Hold Use (E) while aiming at open water to drink and restore thirst.</summary>
[Title( "Thorns Player Water Drink Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerWaterDrinkUse : Component
{
	float _holdSeconds;

	public bool IsDrinking => _holdSeconds > 0f && ThornsNaturalWaterDrink.CanDrinkAt( Scene, GameObject );

	public float DrinkHoldFraction =>
		ThornsNaturalWaterDrink.DrinkHoldSeconds <= 0f
			? 0f
			: Math.Clamp( _holdSeconds / ThornsNaturalWaterDrink.DrinkHoldSeconds, 0f, 1f );

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
		{
			_holdSeconds = 0f;
			return;
		}

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true
		     || ThornsDeathCrateWorldService.Instance?.HasTargetInFront( GameObject ) == true
		     || ThornsAirdropWorldService.Instance?.HasTargetInFront( GameObject ) == true
		     || Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true )
		{
			ResetHold();
			return;
		}

		if ( !ThornsNaturalWaterDrink.CanDrinkAt( Scene, GameObject ) )
		{
			ResetHold();
			return;
		}

		if ( !Input.Down( "Use" ) )
		{
			ResetHold();
			return;
		}

		_holdSeconds += Time.Delta;
		if ( _holdSeconds < ThornsNaturalWaterDrink.DrinkHoldSeconds )
			return;

		ResetHold();

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay is null )
			return;

		gameplay.RequestDrinkFromNaturalWater();
		ThornsPlayerUseGrabPresentation.PlayAction( GameObject, ThornsFpGrabAction.SweepDown );
	}

	void ResetHold()
	{
		_holdSeconds = 0f;
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
