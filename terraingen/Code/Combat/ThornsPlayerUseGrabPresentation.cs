namespace Terraingen.Combat;

using Sandbox;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>FP viewmodel grab gestures for Use (E) interactions — Facepunch <c>grab_action</c> / <c>b_grab</c>.</summary>
public static class ThornsPlayerUseGrabPresentation
{
	public static void PlayAction( GameObject playerRoot, ThornsFpGrabAction action ) =>
		ThornsViewModelController.TryPlayOwnerUseGrabPresentation( playerRoot, action );

	public static void PlayOpenLoot( GameObject playerRoot ) =>
		PlayAction( playerRoot, ThornsFpGrabAction.SweepDown );

	public static void PlayPushButton( GameObject playerRoot ) =>
		PlayAction( playerRoot, ThornsFpGrabAction.PushButton );
}

/// <summary>While holding Use on charge interactions, keep the FP grab stance active.</summary>
[Title( "Thorns Player Use Grab Stance" )]
[Category( "Player" )]
public sealed class ThornsPlayerUseGrabStanceDriver : Component
{
	protected override void OnUpdate()
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		var wantsStance = Input.Down( "Use" ) && ResolveHoldUseInteraction( GameObject );
		ThornsViewModelController.TrySetOwnerGrabStance( GameObject, wantsStance );
	}

	static bool ResolveHoldUseInteraction( GameObject playerRoot )
	{
		if ( ShouldBlockHoldGrabStance( playerRoot ) )
			return false;

		var taming = playerRoot.Components.Get<ThornsPlayerAnimalTaming>();
		if ( taming is not null && taming.HasTameTargetInFront() )
			return true;

		var mount = playerRoot.Components.Get<ThornsPlayerMountController>();
		if ( mount is not null && mount.IsMounted )
			return true;

		var mountUse = playerRoot.Components.Get<ThornsPlayerMountUse>();
		if ( mountUse is not null && mountUse.HasMountTargetInFront() )
			return true;

		if ( ThornsPlayerBloomSeedUse.HasBloomSeedTargetInFront( playerRoot ) )
			return true;

		var guild = playerRoot.Components.Get<ThornsPlayerNpcGuildCoreUse>();
		if ( guild is not null && guild.IsClaiming )
			return true;

		return ThornsNaturalWaterDrink.CanDrinkAt( playerRoot.Scene, playerRoot );
	}

	static bool ShouldBlockHoldGrabStance( GameObject playerRoot )
	{
		if ( ThornsDeathCrateWorldService.Instance?.HasTargetInFront( playerRoot ) == true )
			return true;

		return ThornsAirdropWorldService.Instance?.HasTargetInFront( playerRoot ) == true;
	}
}
