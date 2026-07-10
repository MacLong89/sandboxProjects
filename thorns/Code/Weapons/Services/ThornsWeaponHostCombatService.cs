#nullable disable

using System;

namespace Sandbox;

public sealed class ThornsWeaponHostCombatService
{
	readonly IThornsWeaponCombatHost _host;

	public ThornsWeaponHostCombatService( IThornsWeaponCombatHost host ) => _host = host;

	public void HandleRequestFire( Vector3 directionWorld, int attackVariant, bool aimDownSights )
	{
		var isHeavyMeleeAttack = attackVariant == 1;

		if ( !Networking.IsHost )
			return;

		var equip = _host.GameObject.Components.Get<ThornsHotbarEquipment>();

		if ( attackVariant != 0 && attackVariant != 1 )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( _host.GameObject ) )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		var health = _host.GameObject.Components.Get<ThornsHealth>();
		if ( !health.IsValid() || !health.IsAlive || health.IsDeadState )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		var dir = directionWorld.Normal;
		if ( dir.Length < 0.95f )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( _host.GameObject, out var eyePos, out var eyeRot ) )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !ThornsCombatAuthority.IsDirectionWithinAimTolerance( dir, eyeRot, _host.AimDotMin ) )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !_host.IsOriginPlausible( eyePos ) )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		var inv = _host.GameObject.Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || !equip.IsValid() )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		var hotbar = equip.ServerGetSelectedHotbarIndex();
		if ( hotbar < 0 || !inv.TryGetHostSlot( hotbar, out var slot ) )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		ThornsItemRegistry.ThornsItemDefinition itemDef;
		string authoritativeCombatId;

		if ( slot.IsEmpty )
		{
			authoritativeCombatId = ThornsToolMeleeCombat.CombatIdPrimitive;
			itemDef = ThornsItemRegistry.PrimitiveToolDefinition;
		}
		else
		{
			if ( !_host.Weapon.TryResolveWeaponItemDefResilient( slot.ItemId, out itemDef ) )
			{
				_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
				return;
			}

			if ( itemDef.ItemType == ThornsItemType.Tool )
				authoritativeCombatId = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( slot.ItemId )?.Trim() ?? "";
			else
				authoritativeCombatId = ( string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId )
					? slot.ItemId
					: itemDef.CombatWeaponDefinitionId )?.Trim() ?? "";
			if ( string.IsNullOrEmpty( authoritativeCombatId ) )
			{
				_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
				return;
			}
		}

		var def = ThornsWeaponDefinitions.Get( authoritativeCombatId );

		var weaponDmgMul = 1f;
		var weaponFireMul = 1f;
		if ( ThornsGearRoll.TryParseWeapon( slot.WeaponRollPayload ?? "", out _, out weaponDmgMul, out weaponFireMul ) )
		{
			weaponDmgMul = Math.Clamp( weaponDmgMul, 0.5f, 2f );
			weaponFireMul = Math.Clamp( weaponFireMul, 0.5f, 2f );
		}

		if ( isHeavyMeleeAttack )
		{
			if ( !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, authoritativeCombatId )
			     || !ThornsWeaponDefinitions.HasSecondaryMeleeResolved( def, authoritativeCombatId ) )
			{
				_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
				return;
			}
		}

		if ( ThornsWeapon.IsWeaponBrokenInSlot( slot ) )
		{
			_host.ClientNotifyWeaponBroken();
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( _host.IsReloadBlockingFire() )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		var now = Time.Now;
		var isMelee = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, authoritativeCombatId );

		if ( attackVariant != 0 && !isMelee )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( isHeavyMeleeAttack )
		{
			if ( now < _host.NextMeleeHeavyAllowedHostTime )
			{
				_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
				return;
			}
		}
		else if ( now < _host.NextFireAllowedHostTime )
		{
			_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !isMelee )
		{
			if ( slot.WeaponLoadedAmmo <= 0 )
			{
				_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
				return;
			}

			if ( !_host.TryConsumeRangedShotAmmo( hotbar, inv, ref slot, def, now, out _ ) )
			{
				_host.SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, ThornsWeaponImpactSurfaceKind.None, false );
				return;
			}
		}
		else
		{
			if ( isHeavyMeleeAttack )
				_host.NextMeleeHeavyAllowedHostTime = now + def.SecondaryAttackFireIntervalSeconds / weaponFireMul;
			else if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( authoritativeCombatId ) )
			{
				var interval = Math.Max(
					ThornsToolMeleeCombat.ToolMeleeLightSwingCooldownSeconds,
					def.FireIntervalSeconds / weaponFireMul );
				_host.HostApplyPrimaryMeleeCooldownSeconds( interval );
			}
			else
				_host.NextFireAllowedHostTime = now + def.FireIntervalSeconds / weaponFireMul;

			var upsLuckMelee = _host.GameObject.Components.Get<ThornsPlayerUpgrades>();
			var didMeleeWear = false;
			if ( !slot.IsEmpty && itemDef.ItemType == ThornsItemType.Tool && itemDef.ToolMaxDurability > 0.001f )
			{
				ThornsInventory.HostApplyToolDurabilityLoss(
					ref slot,
					itemDef,
					itemDef.ToolDurabilityLossPerStrike,
					upsLuckMelee );
				didMeleeWear = true;
			}
			else if ( def.DurabilityLossPerShot > 0.0001f )
			{
				var wl = isHeavyMeleeAttack ? def.DurabilityLossPerShot * 3f : def.DurabilityLossPerShot;
				if ( upsLuckMelee.IsValid() && upsLuckMelee.ReinforcedRank > 0 )
					wl *= upsLuckMelee.GetReinforcedDurabilityLossMultiplier();
				slot.HasDurability = true;
				slot.Durability = Math.Max( 0f, slot.Durability - wl );
				didMeleeWear = true;
			}

			if ( didMeleeWear )
			{
				inv.ServerWriteSlot( hotbar, slot );
				_host.PushWeaponHudToOwnerHost();
				if ( ThornsWeapon.IsWeaponBrokenInSlot( slot ) )
					_host.ClientNotifyWeaponBroken();
			}
		}

		var dmgBaseForHit =
			(isHeavyMeleeAttack ? def.SecondaryAttackBaseDamage : def.BaseDamage) * weaponDmgMul;

		var fpPresentationMelee = isHeavyMeleeAttack ? 2 : 1;


		var fireDir = dir;
		var clientKickPitch = 0f;
		var clientKickYaw = 0f;
		if ( !isMelee )
		{
			var vel = ThornsPawnLocomotion.TryGetVelocity( _host.GameObject );
			var planarLen = vel.WithZ( 0f ).Length;
			var movingRm = planarLen > 55f;
			var vitalsRm = _host.GameObject.Components.Get<ThornsVitals>();
			var crouchedRm = vitalsRm.IsValid() && vitalsRm.ServerCrouching;

			var lastShot = _host.HostRecoilLastShotTime;
			var patternIdx = _host.HostRecoilPatternIndex;
			var sprayOrd = _host.HostRecoilSprayOrdinal;

			fireDir = ThornsWeaponRecoilSolve.SolveAuthoritativeFireDirection(
				dir,
				eyeRot,
				def,
				ref lastShot,
				ref patternIdx,
				ref sprayOrd,
				now,
				aimDownSights,
				movingRm,
				crouchedRm,
				out _,
				out clientKickPitch,
				out clientKickYaw,
				out _ );

			_host.HostRecoilLastShotTime = lastShot;
			_host.HostRecoilPatternIndex = patternIdx;
			_host.HostRecoilSprayOrdinal = sprayOrd;

		}

		var range = def.MaxRange;
		var start = eyePos;
		var pelletCount = Math.Max( 1, def.PelletCount );

		if ( pelletCount <= 1 )
		{
			var fpKindSingle = isMelee ? fpPresentationMelee : 0;
			var dirN = fireDir.Normal;

			if ( !_host.HostTryResolveHitscanDamageTarget(
				     start,
				     fireDir,
				     range,
				     isMelee ? ThornsSharedHostHitscan.MeleeMaxAbsVerticalSeparationFeetDefault : 0f,
				     out var tr,
				     out var hitGo,
				     out var victimPawn,
				     out var victimHealth,
				     out var usedAnalyticFallback,
				     out var analyticHitPos ) )
			{
				var surfMiss = ThornsWeaponImpactSurfaceKind.None;
				var endMiss = _host.HostFeedbackEndpointWorldTrace( start, dirN, range, out surfMiss );
				var feedbackMiss = !isMelee || surfMiss != ThornsWeaponImpactSurfaceKind.None;
				_host.SendRpcFireOutcome(
					true,
					false,
					0f,
					false,
					fpKindSingle,
					isMelee ? 0f : clientKickPitch,
					isMelee ? 0f : clientKickYaw,
					feedbackMiss,
					feedbackMiss ? endMiss : null,
					surfMiss,
					false );
				return;
			}

			if ( !hitGo.IsValid() )
			{
				var surfMiss2 = ThornsWeaponImpactSurfaceKind.None;
				var endMiss2 = _host.HostFeedbackEndpointWorldTrace( start, dirN, range, out surfMiss2 );
				var feedbackMiss2 = !isMelee || surfMiss2 != ThornsWeaponImpactSurfaceKind.None;
				_host.SendRpcFireOutcome(
					true,
					false,
					0f,
					false,
					fpKindSingle,
					isMelee ? 0f : clientKickPitch,
					isMelee ? 0f : clientKickYaw,
					feedbackMiss2,
					feedbackMiss2 ? endMiss2 : null,
					surfMiss2,
					false );
				return;
			}

			var headshot = !isMelee
			               && ThornsCombatAuthority.TryHeadshotForWeaponHit( usedAnalyticFallback, tr, analyticHitPos, victimHealth );
			var crit = !headshot
			           && ThornsCombatAuthority.HostTryRollPlayerWeaponCriticalHit( def, authoritativeCombatId, victimHealth.GameObject );
			var bonusMult = 1f;
			if ( headshot )
				bonusMult = def.HeadshotMultiplier;
			else if ( crit )
				bonusMult = ThornsWeaponDefinitions.ResolveCriticalDamageMultiplier( def );
			var dmg = dmgBaseForHit * bonusMult;

			var hitMarkerHighlight = headshot || crit;

			var killingBlow = victimHealth.TakeDamage( dmg, new DamageContext
			{
				AttackerRoot = _host.GameObject,
				Headshot = headshot,
				CriticalHit = crit,
				Kind = isMelee ? "melee" : "hitscan",
				CombatLosVerified = true
			} );

			var wid = hitGo.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( wid.IsValid() )
				ThornsWildlifeLog.PlayerWeaponDamage( hitGo.Name, wid.Species.ToString(), dmg, isMelee ? "melee" : "hitscan", killingBlow );

			var hitPos = usedAnalyticFallback ? analyticHitPos : tr.HitPosition;
			_host.SendRpcFireOutcome(
				true,
				true,
				dmg,
				hitMarkerHighlight,
				fpKindSingle,
				isMelee ? 0f : clientKickPitch,
				isMelee ? 0f : clientKickYaw,
				true,
				hitPos,
				ThornsWeaponImpactSurfaceKind.Player,
				killingBlow );
			return;
		}

		var totalDamageDealt = 0f;
		var anyDamage = false;
		var anyHitMarkerHighlight = false;
		var anyKillPellet = false;
		Vector3? firstPelletBloodPos = null;

		for ( var p = 0; p < pelletCount; p++ )
		{
			var pelletDir = ThornsSharedHostHitscan.SamplePelletDirection( fireDir, def.PelletSpreadHalfAngleDegrees );
			if ( !_host.HostTryResolveHitscanDamageTarget( start, pelletDir, range, 0f, out var trP, out var hitGoP, out var victimPawnP, out var victimHealthP, out var usedAnalyticFallbackP, out var analyticHitPosP ) )
				continue;

			if ( !hitGoP.IsValid() )
				continue;

			var headshotP = ThornsCombatAuthority.TryHeadshotForWeaponHit(
				usedAnalyticFallbackP,
				trP,
				analyticHitPosP,
				victimHealthP );
			var critP = !headshotP
			            && ThornsCombatAuthority.HostTryRollPlayerWeaponCriticalHit( def, authoritativeCombatId, victimHealthP.GameObject );
			var pelletMult = 1f;
			if ( headshotP )
				pelletMult = def.HeadshotMultiplier;
			else if ( critP )
				pelletMult = ThornsWeaponDefinitions.ResolveCriticalDamageMultiplier( def );
			var dmgP = def.BaseDamage * weaponDmgMul * pelletMult;

			var killP = victimHealthP.TakeDamage( dmgP, new DamageContext
			{
				AttackerRoot = _host.GameObject,
				Headshot = headshotP,
				CriticalHit = critP,
				Kind = "pellet",
				CombatLosVerified = true
			} );

			var widP = hitGoP.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( widP.IsValid() )
				ThornsWildlifeLog.PlayerWeaponDamage( hitGoP.Name, widP.Species.ToString(), dmgP, "pellet", killP );

			if ( killP )
				anyKillPellet = true;

			totalDamageDealt += dmgP;
			anyDamage = true;
			if ( headshotP || critP )
				anyHitMarkerHighlight = true;

			if ( !firstPelletBloodPos.HasValue )
				firstPelletBloodPos = usedAnalyticFallbackP ? analyticHitPosP : trP.HitPosition;
		}

		var dirPellet = fireDir.Normal;
		ThornsWeaponImpactSurfaceKind surfPellet;
		Vector3 endPellet;
		if ( firstPelletBloodPos.HasValue )
		{
			endPellet = firstPelletBloodPos.Value;
			surfPellet = ThornsWeaponImpactSurfaceKind.Player;
		}
		else
		{
			endPellet = _host.HostFeedbackEndpointWorldTrace( start, dirPellet, range, out surfPellet );
		}

		_host.SendRpcFireOutcome(
			true,
			anyDamage,
			totalDamageDealt,
			anyHitMarkerHighlight,
			0,
			clientKickPitch,
			clientKickYaw,
			true,
			endPellet,
			surfPellet,
			anyKillPellet );
	}
}
