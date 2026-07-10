using System;

namespace Sandbox;

/// <summary>
/// Owner-client hold-use on weakened wildlife → host validates HP threshold + range → sets tame owner on <see cref="ThornsWildlifeIdentity"/>.
/// </summary>
[Title( "Thorns — Wildlife tame interactor" )]
[Category( "Thorns/Wildlife" )]
[Icon( "pets" )]
[Order( 72 )]
public sealed class ThornsWildlifeTameInteractor : Component
{
	public const float HoldSecondsToTame = 2.15f;

	double _holdStartedRealtime = -1;
	Guid _holdTargetWildlifeId;

	bool UiBlocksTaming()
	{
		var hud = Components.Get<ThornsDebugHudHost>();
		var hp = Components.Get<ThornsHealth>();
		var build = Components.Get<ThornsBuildingController>();
		var shell = Components.Get<ThornsGameShell>();
		var shellBlocks = shell is not null && shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay;
		return shellBlocks
		       || (hud.IsValid() && (hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop))
		       || (hp.IsValid() && hp.IsDeadState)
		       || (build.IsValid() && build.BuildModeActive);
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( UiBlocksTaming() )
		{
			_holdStartedRealtime = -1;
			ThornsTameHoldHudBridge.Clear();
			return;
		}

		var nearRadio = ThornsRadioStation.FindBestUnderAimForPawn( GameObject.Scene, GameObject, 420f );
		if ( nearRadio.IsValid() && nearRadio.StationId != Guid.Empty && nearRadio.HostIsInRange( GameObject.WorldPosition ) )
		{
			var useHeldRadio = ThornsInputInteract.IsUseOrInteractHeld();
			if ( useHeldRadio )
			{
				_holdStartedRealtime = -1;
				ThornsTameHoldHudBridge.Clear();
				ThornsMountHoldHudBridge.Clear();
				return;
			}
		}

		var useHeld = ThornsInputInteract.IsUseOrInteractHeld();
		var threshold = ThornsWildlifeTamingRules.GetTamingThresholdForPawnRoot( GameObject );

		if ( ThornsWildlifeTamingRules.ClientTryGetTameCandidate(
			     GameObject,
			     threshold,
			     out var wid,
			     out var hpFrac ) )
		{
			if ( !wid.IsValid()
			     || wid.WildlifeId == Guid.Empty
			     || wid.HostIsTamed
			     || hpFrac > threshold + ThornsWildlifeTamingRules.ThresholdEpsilon )
			{
				_holdStartedRealtime = -1;
				ThornsTameHoldHudBridge.Clear();
				return;
			}

			var label = wid.Definition.DisplayName;
			var wId = wid.WildlifeId;
			ThornsTameHoldHudBridge.WildlifeId = wId;
			ThornsTameHoldHudBridge.CreatureLabel = label;
			ThornsTameHoldHudBridge.CreatureHp01 = hpFrac;
			ThornsTameHoldHudBridge.ThresholdHp01 = threshold;

			if ( !useHeld )
			{
				_holdStartedRealtime = -1;
				ThornsTameHoldHudBridge.Phase = ThornsTameHudPhase.ReadyHold;
				ThornsTameHoldHudBridge.HoldProgress01 = 0f;
				return;
			}

			if ( wId != _holdTargetWildlifeId || _holdStartedRealtime < 0 )
			{
				_holdTargetWildlifeId = wId;
				_holdStartedRealtime = Time.Now;
			}

			var progress01 = Math.Clamp( (float)( ( Time.Now - _holdStartedRealtime ) / HoldSecondsToTame ), 0f, 1f );
			ThornsTameHoldHudBridge.Phase = ThornsTameHudPhase.Holding;
			ThornsTameHoldHudBridge.HoldProgress01 = progress01;

			if ( Time.Now - _holdStartedRealtime < HoldSecondsToTame )
				return;

			_holdStartedRealtime = -1;
			ThornsTameHoldHudBridge.Clear();
			// Releasing E after hold-to-tame also fires Use "pressed" — block accidental mount on the same tame.
			ThornsTameHoldHudBridge.SuppressMountInteractForSeconds( 0.9f );
			RequestCompleteTame( wId );
			return;
		}

		if ( ThornsWildlifeTamingRules.TryGetRayUntamedTooHealthyForTamingUi( GameObject, threshold, out var tooHpWid, out var tooHpFrac )
		     && tooHpWid.IsValid() )
		{
			_holdStartedRealtime = -1;
			ThornsTameHoldHudBridge.WildlifeId = tooHpWid.WildlifeId;
			ThornsTameHoldHudBridge.CreatureLabel = tooHpWid.Definition.DisplayName;
			ThornsTameHoldHudBridge.CreatureHp01 = tooHpFrac;
			ThornsTameHoldHudBridge.ThresholdHp01 = threshold;
			ThornsTameHoldHudBridge.Phase = ThornsTameHudPhase.WeakenMore;
			ThornsTameHoldHudBridge.HoldProgress01 = 0f;
			return;
		}

		_holdStartedRealtime = -1;
		ThornsTameHoldHudBridge.Clear();
	}

	[Rpc.Host]
	public void RequestCompleteTame( Guid wildlifeInstanceId )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null )
			return;

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		var callerId = Rpc.Caller.Id;

		if ( !ThornsWildlifeIdentity.ActiveByHost.TryGetValue( wildlifeInstanceId, out var wid ) || !wid.IsValid() )
			return;

		if ( wid.HostIsTamed )
			return;

		var hpW = wid.Components.Get<ThornsHealth>();
		if ( !hpW.IsValid() || !hpW.IsAlive || hpW.IsDeadState )
			return;

		var threshold = ThornsWildlifeTamingRules.GetTamingThresholdForPawnRoot( GameObject );
		if ( hpW.CurrentHealth / hpW.MaxHealth > threshold + ThornsWildlifeTamingRules.ThresholdEpsilon )
			return;

		var dist = ( GameObject.WorldPosition - wid.GameObject.WorldPosition ).Length;
		var maxTame = ThornsWildlifeTamingRules.GetMaxTameDistanceFor( wid );
		if ( dist > maxTame )
			return;

		if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( GameObject, wid.GameObject, maxTame ) )
			return;

		wid.TameOwnerConnectionIdSync = callerId.ToString( "D" );
		wid.TameOwnerAccountKeySync = ThornsPersistenceIdentity.TryGetStableAccountKeyForConnection( callerId, out var ak )
			? ak
			: "";
		wid.TameFollowOwnerSync = true;
		wid.TameDisplayNameSync = "";
		wid.HostBondedAtRealtime = Time.Now;

		ThornsTameTierRoll.HostApplyRollToIdentity( wid, Random.Shared );
		wid.HostRefreshTameDerivedStatsFromXp();
		hpW.CurrentHealth = hpW.MaxHealth;

		HostPlaceNewlyTamedWildlifeBesideOwner( GameObject, wid.GameObject );
		wid.Components.Get<ThornsWildlifeBrain>()?.HostClearCombatHostilityOnTameBond();
		ThornsWildlifeMountHost.HostUnstickPawnOrphanedWildlifeParent( GameObject );
		if ( !string.IsNullOrEmpty( wid.TameRiderConnectionIdSync ) )
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( wid );
		if ( !wid.Definition.AllowPlayerMount )
			wid.TameRiderConnectionIdSync = "";

		Components.Get<ThornsPlayerMilestones>()?.HostRecordTameCompleted( 1 );
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCache();
		wid.HostRefreshTameRegistryMembership();
		RpcTameSucceededLocal(
			wildlifeInstanceId,
			wid.Species.ToString(),
			wid.Definition.DisplayName,
			wid.TameQualityTierSync,
			wid.TameLegendaryAbilitySync );
	}

	[Rpc.Owner]
	void RpcTameSucceededLocal(
		Guid wildlifeInstanceId,
		string species,
		string displayName,
		byte tameQualityTierSync,
		byte tameLegendaryAbilitySync )
	{
		_ = wildlifeInstanceId;
		_ = species;

		if ( Game.IsPlaying )
			ThornsGameplaySfx.PlayAtPawnEar( GameObject, ThornsGameplaySfx.Tame );

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		shell.SetGameplayInteractionHint( "" );
		ThornsTameHoldHudBridge.SuppressMountInteractForSeconds( 0.9f );
		var tier = (ThornsLootRarity)Math.Clamp( tameQualityTierSync, (byte)0, (byte)4 );
		var tierName = tier.DisplayName();
		var leg = (ThornsTameLegendaryAbility)Math.Clamp( tameLegendaryAbilitySync, (byte)0, (byte)5 );
		var title = $"Taming complete — {tierName} {displayName}.";
		var subtitle = "Press TAB → Tames for bloodlines, gifts, and stats.";
		if ( tier == ThornsLootRarity.Legendary && leg != ThornsTameLegendaryAbility.None )
			subtitle = $"Legendary gift: {ThornsTameLegendaryAbilityDefs.DisplayName( leg )}.\n{subtitle}";
		shell.PushTameHudBanner( title, subtitle, 6.8f );
	}

	[Rpc.Host]
	public void RequestSetTameFollow( Guid wildlifeInstanceId, bool follow )
	{
		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !TryGetCallerTame( wildlifeInstanceId, Rpc.Caller.Id, out var wid ) )
			return;

		wid.TameFollowOwnerSync = follow;
		var brain = wid.Components.Get<ThornsWildlifeBrain>();
		if ( brain.IsValid() )
		{
			if ( follow )
				ThornsAnimalCommandSystem.TryApplyCommand( brain, ThornsAnimalCommandKind.Follow, GameObject );
			else
				ThornsAnimalCommandSystem.TryApplyCommand( brain, ThornsAnimalCommandKind.Stay );
		}
		else if ( !follow )
			wid.Components.Get<ThornsWildlifeBrain>()?.HostResetPassiveLeashAnchorToCurrentPosition();
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCache();
	}

	[Rpc.Host]
	public void RequestSummonTame( Guid wildlifeInstanceId )
	{
		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !TryGetCallerTame( wildlifeInstanceId, Rpc.Caller.Id, out var wid ) )
			return;

		var tameGo = wid.GameObject;
		if ( !tameGo.IsValid() )
			return;

		var flatFwd = GameObject.WorldRotation.Forward.WithZ( 0 );
		if ( flatFwd.LengthSquared < 0.0001f )
			flatFwd = Vector3.Forward;
		else
			flatFwd = flatFwd.Normal;

		var summonDist = ThornsWildlifeBrain.HostTameUsesBulkyFollowSpacing( wid.Species ) ? 118f : 72f;
		var dest = GameObject.WorldPosition + flatFwd * summonDist + Vector3.Up * 8f;
		tameGo.Components.Get<ThornsWildlifeMotor>()?.HostTeleportToWorldPosition( dest );
		wid.TameFollowOwnerSync = true;
		var summonBrain = tameGo.Components.Get<ThornsWildlifeBrain>();
		if ( summonBrain.IsValid() )
			ThornsAnimalCommandSystem.TryApplyCommand( summonBrain, ThornsAnimalCommandKind.Follow, GameObject );
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCache();
	}

	[Rpc.Host]
	public void RequestFeedTame( Guid wildlifeInstanceId )
	{
		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !TryGetCallerTame( wildlifeInstanceId, Rpc.Caller.Id, out var wid ) )
		{
			RpcTameFeedOutcome( false, "That creature isn't yours to feed." );
			return;
		}

		var tameGo = wid.GameObject;
		if ( !tameGo.IsValid() )
			return;

		const float maxFeedDistance = 1400f;
		if ( ( GameObject.WorldPosition - tameGo.WorldPosition ).Length > maxFeedDistance )
		{
			RpcTameFeedOutcome( false, "Creature is too far away to feed." );
			return;
		}

		var hp = tameGo.Components.Get<ThornsHealth>();
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
		{
			RpcTameFeedOutcome( false, "Can't feed this creature right now." );
			return;
		}

		if ( hp.CurrentHealth >= hp.MaxHealth - 0.5f )
		{
			RpcTameFeedOutcome( false, "Already at full health." );
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || !inv.HostTryRemoveOneRandomFoodItem( out var foodDef ) )
		{
			RpcTameFeedOutcome( false, "No food items in your inventory." );
			return;
		}

		var heal = ComputeTameFeedHeal( foodDef, hp.MaxHealth );
		hp.HostApplyHealing( heal, "tame_feed" );
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCache();
		RpcTameFeedOutcome( true, $"Fed your tame (+{heal:F0} HP)." );
	}

	static float ComputeTameFeedHeal( ThornsItemRegistry.ThornsItemDefinition foodDef, float tameMaxHp )
	{
		var basis = foodDef.HungerRestore > 0.01f ? foodDef.HungerRestore : 24f;
		return Math.Clamp( basis * 1.35f, 14f, tameMaxHp * 0.5f );
	}

	[Rpc.Owner]
	void RpcTameFeedOutcome( bool ok, string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		shell.PushGameplayToast(
			message.Trim(),
			ok ? 3.2f : 4.2f,
			ok ? ThornsGameplayToastKind.Positive : ThornsGameplayToastKind.Hint );
	}

	[Rpc.Host]
	public void RequestApplyTameStatUpgrade( Guid wildlifeInstanceId, int statKind )
	{
		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !TryGetCallerTame( wildlifeInstanceId, Rpc.Caller.Id, out var wid ) )
			return;

		if ( wid.TameUnspentUpgradePoints <= 0 )
			return;

		switch ( statKind )
		{
			case 0:
				wid.TameHpUpgradeSteps++;
				break;
			case 1:
				wid.TameDmgUpgradeSteps++;
				break;
			case 2:
				wid.TameSpdUpgradeSteps++;
				break;
			default:
				return;
		}

		wid.TameUnspentUpgradePoints--;
		wid.HostRefreshTameDerivedStatsFromXp();
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCache();
	}

	[Rpc.Host]
	public void RequestSetTameDisplayName( Guid wildlifeInstanceId, string rawName )
	{
		if ( Rpc.Caller is null || !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !TryGetCallerTame( wildlifeInstanceId, Rpc.Caller.Id, out var wid ) )
			return;

		wid.TameDisplayNameSync = SanitizeTameDisplayName( rawName );
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCache();
	}

	internal static bool TryGetCallerTame( Guid wildlifeInstanceId, Guid callerId, out ThornsWildlifeIdentity wid )
	{
		wid = default;
		if ( !ThornsWildlifeIdentity.ActiveByHost.TryGetValue( wildlifeInstanceId, out wid ) || !wid.IsValid() )
			return false;
		if ( !ThornsWildlifeIdentity.HostCallerOwnsTame( callerId, wid ) )
			return false;
		return wid.HostIsTamed;
	}

	static string SanitizeTameDisplayName( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
			return "";

		var s = raw.Trim().Replace( '\r', ' ' ).Replace( '\n', ' ' );
		const int max = 28;
		return s.Length <= max ? s : s[..max];
	}

	/// <summary>Host: separate tame from owner capsule after bond — avoids CC overlap / phantom "riding" at tame spot.</summary>
	static void HostPlaceNewlyTamedWildlifeBesideOwner( GameObject ownerRoot, GameObject tameGo )
	{
		if ( !Networking.IsHost || ownerRoot is null || !ownerRoot.IsValid() || tameGo is null || !tameGo.IsValid() )
			return;

		var id = tameGo.Components.Get<ThornsWildlifeIdentity>();
		var placementDist = id.IsValid() && ThornsWildlifeBrain.HostTameUsesBulkyFollowSpacing( id.Species )
			? 145f
			: 110f;
		var ownerPos = ownerRoot.WorldPosition;

		var fwd = ownerRoot.WorldRotation.Forward.WithZ( 0f );
		if ( fwd.LengthSquared < 1e-4f )
			fwd = Vector3.Forward;
		else
			fwd = fwd.Normal;

		var right = ownerRoot.WorldRotation.Right.WithZ( 0f );
		if ( right.LengthSquared < 1e-4f )
			right = new Vector3( fwd.y, -fwd.x, 0f );
		else
			right = right.Normal;

		var sideSign = ( id.IsValid() ? id.WildlifeId.GetHashCode() : tameGo.Id.GetHashCode() ) & 1;
		var offset = ( -fwd * 0.35f + right * ( sideSign == 0 ? 1f : -1f ) * 0.94f ).Normal;

		var dest = ownerPos + offset * placementDist + Vector3.Up * 8f;
		tameGo.Components.Get<ThornsWildlifeMotor>()?.HostTeleportToWorldPosition( dest );
	}
}

/// <summary>Owner-client tame HUD mirror — <see cref="ThornsGameShell"/> reads this each frame.</summary>
public enum ThornsTameHudPhase : byte
{
	Hidden = 0,
	WeakenMore = 1,
	ReadyHold = 2,
	Holding = 3
}

/// <summary>Hold-to-tame + at-aim prompts (owner pawn writes; shell renders).</summary>
public static class ThornsTameHoldHudBridge
{
	public static ThornsTameHudPhase Phase;

	public static Guid WildlifeId;

	public static float HoldProgress01;

	public static string CreatureLabel = "";

	public static float CreatureHp01;

	public static float ThresholdHp01;

	/// <summary>After hold-to-tame, ignore the next Use tap so E release does not mount the new tame.</summary>
	public static double BlockMountInteractUntilRealtime;

	public static void SuppressMountInteractForSeconds( float seconds )
	{
		var until = Time.Now + Math.Max( 0.1f, seconds );
		if ( until > BlockMountInteractUntilRealtime )
			BlockMountInteractUntilRealtime = until;
	}

	public static bool IsMountInteractSuppressed() => Time.Now < BlockMountInteractUntilRealtime;

	public static void Clear()
	{
		Phase = ThornsTameHudPhase.Hidden;
		WildlifeId = Guid.Empty;
		HoldProgress01 = 0f;
		CreatureLabel = "";
		CreatureHp01 = 0f;
		ThresholdHp01 = 0f;
	}
}
