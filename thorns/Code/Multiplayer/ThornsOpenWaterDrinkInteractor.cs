using System;

namespace Sandbox;

/// <summary>
/// Local owner: hold Use (E) while in the open-water band (sea level from <see cref="ThornsTerrainSystem"/>, same as the water sheet / swim plane)
/// to sip and restore thirst — host validates zone + rate-limits (<see cref="ThornsVitals.RpcRequestSipOpenWater"/>).
/// </summary>
[Title( "Thorns — Open water drink" )]
[Category( "Thorns/Multiplayer" )]
[Icon( "water_drop" )]
[Order( 77 )]
public sealed class ThornsOpenWaterDrinkInteractor : Component
{
	[Property] public float SipIntervalSeconds { get; set; } = 0.2f;

	/// <summary>Thirst restored per sip RPC (host clamps). ~5 sips/s at default interval fills from empty.</summary>
	[Property] public float ThirstPerSip { get; set; } = 22f;

	double _nextSipSendTime = -1e9;

	bool UiBlocksDrink()
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell is not null && shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay )
			return true;

		var hud = Components.Get<ThornsDebugHudHost>();
		if ( hud.IsValid() && ( hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop ) )
			return true;

		return false;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( UiBlocksDrink() )
			return;

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
			return;

		var mountIx = Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
			return;

		var move = Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() && !move.EnableWaterInteraction )
			return;

		var radio = ThornsRadioStation.FindBestUnderAimForPawn( GameObject.Scene, GameObject, 420f );
		if ( radio.IsValid() && radio.StationId != Guid.Empty && radio.HostIsInRange( GameObject.WorldPosition ) )
			return;

		var tameThreshold = ThornsWildlifeTamingRules.GetTamingThresholdForPawnRoot( GameObject );
		if ( ThornsWildlifeTamingRules.ClientTryGetTameCandidate( GameObject, tameThreshold, out var tameWid, out var tameHpFrac )
		     && tameWid.IsValid()
		     && tameWid.WildlifeId != Guid.Empty
		     && !tameWid.HostIsTamed
		     && tameHpFrac <= tameThreshold + ThornsWildlifeTamingRules.ThresholdEpsilon )
			return;

		if ( !ThornsTerrainSystem.IsPawnInOpenWaterDrinkZone( GameObject.Scene, GameObject, out _ ) )
			return;

		if ( !ThornsInputInteract.IsUseOrInteractHeld() )
			return;

		var vitals = Components.Get<ThornsVitals>();
		if ( !vitals.IsValid() )
			return;

		if ( vitals.MaxThirst > 0.01f && vitals.Thirst >= vitals.MaxThirst - 0.75f )
			return;

		var now = Time.Now;
		if ( now < _nextSipSendTime )
			return;

		_nextSipSendTime = now + Math.Max( 0.08, SipIntervalSeconds );
		vitals.RpcRequestSipOpenWater( ThirstPerSip );
	}
}
