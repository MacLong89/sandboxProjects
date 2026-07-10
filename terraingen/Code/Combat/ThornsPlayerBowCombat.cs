using Sandbox;
using Sandbox.Network;
using Terraingen;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.Multiplayer;

namespace Terraingen.Combat;

/// <summary>Hold LMB to nock and draw the bow; release at full draw to loose an arrow (no manual reload).</summary>
[Title( "Thorns — Player Bow Combat" )]
[Category( "Thorns/Combat" )]
[Icon( "sports_martial_arts" )]
[Order( 49 )]
public sealed class ThornsPlayerBowCombat : Component
{
	public const float ChargeSeconds = 1.15f;
	/// <summary>FOV multiplier at full draw (1.5 = 50% narrower FOV / 1.5× zoom).</summary>
	public const float DrawFovZoomMultiplier = 1.5f;
	public const float MinChargeToFire = 0.98f;

	public float ChargeFraction { get; private set; }
	public bool IsCharging { get; private set; }

	bool _attackWasDown;

	public static bool IsBowEquipped( GameObject pawn )
	{
		if ( pawn is null || !pawn.IsValid() )
			return false;

		var gameplay = pawn.Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarItemId( out var itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) || def.ItemType != ThornsItemType.Weapon )
			return false;

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, itemId );
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		return ThornsWeaponDefinitions.IsBowWeapon( wdef, combatId );
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		if ( !IsBowEquipped( GameObject ) )
		{
			ResetCharge();
			_attackWasDown = false;
			return;
		}

		if ( IsPlacementModeActive() )
			return;

		var attackDown = Input.Down( "Attack1" ) || Input.Down( "attack1" );
		var attackPressed = Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" );

		if ( attackDown )
		{
			if ( attackPressed )
				Components.Get<ThornsPlayerWeaponCombat>()?.RequestBowNockIfNeeded();

			if ( ThornsPlayerMovementDefaults.IsSprintMoving( GameObject ) || !CanDrawBow() )
			{
				ResetCharge();
				_attackWasDown = attackDown;
				return;
			}

			var startingDraw = !IsCharging && ChargeFraction <= 0.0001f;
			IsCharging = true;
			ChargeFraction = Math.Min( 1f, ChargeFraction + (float)(Time.Delta / ChargeSeconds) );
			if ( startingDraw )
				ThornsGameplaySfx.PlayBowDraw( GameObject );
		}
		else if ( _attackWasDown && ChargeFraction >= MinChargeToFire )
		{
			TryReleaseShot();
			ResetCharge();
		}
		else if ( !attackDown )
		{
			ResetCharge();
		}

		_attackWasDown = attackDown;
	}

	bool CanDrawBow()
	{
		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return false;

		if ( !gameplay.TryGetActiveHotbarItemId( out var itemId )
		     || !ThornsItemRegistry.TryGet( itemId, out var idef ) )
			return false;

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( idef, itemId );
		var def = ThornsWeaponDefinitions.Get( combatId );
		var stack = gameplay.GetHotbarSlot( hotbar );
		if ( stack.IsEmpty || stack.IsWeaponBroken( combatId ) )
			return false;

		if ( stack.WeaponLoadedAmmo > 0 )
			return true;

		var weaponCombat = Components.Get<ThornsPlayerWeaponCombat>();
		if ( weaponCombat.IsValid() && weaponCombat.MirrorLoadedAmmo > 0 )
			return true;

		return ThornsInventoryWeaponState.CountAmmoInContainer( gameplay.Inventory, def.AmmoTypeId ) > 0;
	}

	void TryReleaseShot()
	{
		if ( !ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out var origin, out var direction, useScreenCenter: true ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarItemId( out var activeItemId ) )
			return;

		if ( !ThornsItemRegistry.TryGet( activeItemId, out var idef ) )
			return;

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( idef, activeItemId );
		ThornsViewModelController.TryPlayOwnerAttackPresentation( GameObject, activeItemId, combatId );

		var weaponCombat = Components.Get<ThornsPlayerWeaponCombat>();
		if ( !weaponCombat.IsValid() )
			return;

		weaponCombat.RequestBowReleaseFire( origin, direction );
	}

	void ResetCharge()
	{
		IsCharging = false;
		ChargeFraction = 0f;
	}

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocalConnectionOwner( this );

	bool IsPlacementModeActive()
	{
		var building = Components.Get<ThornsPlayerBuildingController>();
		return building.IsValid() && building.UsesPrimaryFireForPlacement;
	}
}
