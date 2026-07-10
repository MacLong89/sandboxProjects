namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen;
using Terraingen.Buildings;
using Terraingen.Foliage;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Primary-fire tree harvesting with axe / hatchet (host-authoritative).</summary>
[Title( "Thorns Player Tree Chop" )]
[Category( "Player" )]
public sealed class ThornsPlayerTreeChopUse : Component
{
	[ConVar( "tree_chop_debug" )]
	public static bool Debug { get; set; }

	const float ChopCooldownSeconds = 0.42f;
	double _nextChopTime;

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		if ( ThornsPlayerWeaponCombat.IsRangedWeaponEquipped( GameObject )
		     || ThornsPlayerBowCombat.IsBowEquipped( GameObject ) )
			return;

		if ( ThornsPlayerBuildingController.Local?.UsesPrimaryFireForPlacement == true )
			return;

		if ( !(Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" )) )
			return;

		if ( !ThornsAxeTools.PlayerHasAxeEquipped( GameObject ) )
			return;

		if ( Time.Now < _nextChopTime )
			return;

		if ( !TryResolveAimRay( out var origin, out var direction ) )
		{
			LogChop( "no aim ray" );
			return;
		}

		if ( !ThornsTreeHitUtil.TryPickTreeAlongRay( Scene, origin, direction, ThornsGatheringRange.Inches, GameObject, out _ ) )
			return;

		_nextChopTime = Time.Now + ChopCooldownSeconds;

		ThornsAxeTools.TryGetEquippedItemId( GameObject, out var itemId );
		ThornsViewModelController.TryPlayOwnerAttackPresentation(
			GameObject,
			itemId,
			ThornsFpToolCombat.GetCombatDefinitionIdForToolItemId( itemId ) );

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestChop( origin, direction );
		else
			HostTryChop( origin, direction );
	}

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocalConnectionOwner( this );

	bool TryResolveAimRay( out Vector3 origin, out Vector3 direction )
	{
		if ( ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out origin, out direction, useScreenCenter: true ) )
			return true;

		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = GameObject.WorldPosition + Vector3.Up * 64f;
		direction = controller.EyeAngles.ToRotation().Forward.Normal;
		return direction.Length >= 0.95f;
	}

	[Rpc.Host]
	void RpcRequestChop( Vector3 origin, Vector3 direction )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryChop( origin, direction );
	}

	void HostTryChop( Vector3 origin, Vector3 direction )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !ThornsCombatFireValidation.TryResolveAuthoritativeShot(
			     GameObject,
			     origin,
			     direction,
			     ThornsGatheringRange.Inches,
			     out origin,
			     out direction ) )
		{
			LogChop( "shot validation failed" );
			return;
		}

		if ( !ThornsAxeTools.PlayerHasAxeEquipped( GameObject ) )
		{
			LogChop( "no axe on hotbar" );
			return;
		}

		ThornsAxeTools.TryGetEquippedItemId( GameObject, out var itemId );
		if ( !ThornsTreeHitUtil.TryPickTreeAlongRay( Scene, origin, direction, ThornsGatheringRange.Inches, GameObject, out var treeId ) )
		{
			var service = ThornsTreeWorldService.ResolveInstance();
			if ( service is not null && service.IsValid() )
			{
				if ( Debug )
					service.DebugLogChopFailure( GameObject.WorldPosition, origin, direction, ThornsGatheringRange.Inches, GameObject );
				else
				{
					var nearest = service.DebugNearestTrunkDistance( GameObject.WorldPosition, out _ );
					LogChop(
						$"no tree in aim (trace={ThornsGatheringRange.TraceInches:F0} forgive={ThornsGatheringRange.MeleeForgivenessInches():F0} in, registered={service.DebugTreeCount}, nearestTrunk={nearest:F0} in, tree_chop_debug=1)" );
				}
			}
			else
				LogChop( "no tree in aim (tree service missing)" );

			return;
		}

		var treeService = ThornsTreeWorldService.ResolveInstance();
		if ( treeService is null || !treeService.IsValid() )
		{
			LogChop( "tree service missing" );
			return;
		}

		if ( !treeService.HostTryChop( GameObject, treeId, origin, direction ) )
		{
			LogChop( $"HostTryChop rejected tree #{treeId}" );
			return;
		}

		Components.Get<ThornsPlayerGameplay>()?.PushCrosshairHitFeedbackToOwner();
		ThornsGameplaySfx.PlayToolStrikeForActiveItem( GameObject, itemId );
		ThornsImpactSparkFx.Spawn( Scene, origin + direction * 52f, new Color( 0.86f, 0.58f, 0.18f ) );

		LogChop( $"chopped tree #{treeId} (+wood)", success: true );
	}

	void LogChop( string detail, bool success = false )
	{
		ThornsAxeTools.TryGetEquippedItemId( GameObject, out var itemId );
		var msg = $"[Thorns Chop] {detail} (item='{itemId}', pos={GameObject.WorldPosition:F0})";

		if ( success )
		{
			if ( Debug )
				Log.Info( msg );

			return;
		}

		Log.Warning( msg );
	}
}
