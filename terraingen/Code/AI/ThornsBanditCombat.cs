namespace Terraingen.AI;

using System.Collections.Generic;
using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Host bandit burst shooting — aimbox-style spread, trace, and effective weapon stats.</summary>
[Title( "Thorns Bandit Combat" )]
[Category( "Thorns/AI" )]
[Icon( "gps_fixed" )]
public sealed class ThornsBanditCombat : Component
{
	public static readonly string[] WeaponPool = ["m4", "mp5", "usp", "sniper", "shotgun"];

	const float HumanNpcHitscanDamageMul = 1f;
	public const float HumanNpcPlayerHitChanceDefault = 0.30f;
	public const float HumanNpcMaxEngagementRangeWorld = 1500f;

	[Property] public string CombatWeaponDefinitionId { get; set; } = "m4";
	[Property] public ThornsBanditSkillLevel Skill { get; set; } = ThornsBanditSkillLevel.Average;

	public IReadOnlyList<ThornsAttachmentId> Attachments => _attachments;

	readonly List<ThornsAttachmentId> _attachments = new( ThornsAttachmentCatalog.MaxSlotsPerWeapon );

	int _burstShotsPlanned;
	int _burstShotsFired;
	double _burstNextShotRealtime;
	double _burstPauseUntilRealtime;
	double _reactionReadyRealtime;
	double _nextNpcGunshotRpcRealtime;
	bool _repositionPending;

	double _hostRecoilLastShotTime;
	int _hostRecoilPatternIndex;
	int _hostRecoilSprayOrdinal;

	public bool HostIsInBurstPause => Time.Now < _burstPauseUntilRealtime;
	public bool HostRepositionPending => _repositionPending;
	public bool HostIsReactionReady => Time.Now >= _reactionReadyRealtime;
	public bool HostWillStartBurstThisShot => _burstShotsFired <= 0 && !HostIsInBurstPause;

	public static string HostRollRandomWeaponId( Random rnd ) =>
		WeaponPool[rnd.Next( 0, WeaponPool.Length )];

	public void HostAssignRandomAttachments( Random rnd )
	{
		_attachments.Clear();
		var weaponId = ThornsAttachmentCatalog.NormalizeCombatWeaponId( CombatWeaponDefinitionId );
		var compatible = ThornsAttachmentCatalog.GetCompatibleAttachments( weaponId );
		if ( compatible.Count <= 0 )
			return;

		var slotBudget = rnd.Next( 0, Math.Min( ThornsAttachmentCatalog.MaxSlotsPerWeapon, compatible.Count ) + 1 );
		var pool = compatible.ToList();
		for ( var slot = 0; slot < slotBudget && pool.Count > 0; slot++ )
		{
			var pick = pool[rnd.Next( 0, pool.Count )];
			pool.Remove( pick );
			_attachments.Add( pick );
		}

		var sanitized = ThornsAttachmentCatalog.SanitizeForWeapon( weaponId, _attachments );
		_attachments.Clear();
		_attachments.AddRange( sanitized );
	}

	public void HostSetLoadout( string combatWeaponId, IEnumerable<ThornsAttachmentId> attachments )
	{
		CombatWeaponDefinitionId = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId );
		if ( string.IsNullOrWhiteSpace( CombatWeaponDefinitionId ) )
			CombatWeaponDefinitionId = "m4";

		_attachments.Clear();
		if ( attachments is not null )
		{
			var sanitized = ThornsAttachmentCatalog.SanitizeForWeapon( CombatWeaponDefinitionId, attachments );
			_attachments.AddRange( sanitized );
		}
	}

	public bool HostTryShootToward(
		GameObject targetRoot,
		bool wantsAds )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !targetRoot.IsValid() )
		{
			HostResetBurstCadence();
			return false;
		}

		var now = Time.Now;
		if ( now < _reactionReadyRealtime || now < _burstPauseUntilRealtime )
			return false;

		if ( _burstShotsFired > 0 && now < _burstNextShotRealtime )
			return false;

		var selfHp = Components.Get<ThornsBanditHealth>();
		if ( selfHp.IsValid() && ( !selfHp.IsAlive || selfHp.IsDeadState ) )
		{
			HostResetBurstCadence();
			return false;
		}

		if ( _burstShotsFired <= 0 )
		{
			_burstShotsPlanned = Game.Random.Int(
				ThornsBanditCombatTuning.BurstMinRounds,
				ThornsBanditCombatTuning.BurstMaxRounds );
			_burstNextShotRealtime = now;
		}

		var def = ThornsWeaponDefinitions.Get( CombatWeaponDefinitionId );
		var effective = ThornsWeaponEffectiveStats.Resolve( def, CombatWeaponDefinitionId, _attachments );

		if ( !TryResolveBanditEye( out var eyePos, out var eyeRot, out var aimForward ) )
			return false;

		HostPlayGunshotFx( eyePos, effective.NoiseLoudnessMultiplier );
		ThornsBanditCommunication.HostRegisterGunshot( eyePos, effective.NoiseLoudnessMultiplier );

		var cc = Components.Get<CharacterController>();
		var moving = cc.IsValid() && cc.Velocity.WithZ( 0f ).Length > 55f;

		var bloomMul = ThornsAttachmentModifiers.BloomMultiplier( effective.Attachments );
		if ( wantsAds )
			bloomMul *= ThornsBanditCombatTuning.BotAdsPelletSpreadMultiplier;

		var fireDir = ThornsWeaponRecoilSolve.SolveAuthoritativeFireDirection(
			aimForward,
			eyeRot,
			def,
			ref _hostRecoilLastShotTime,
			ref _hostRecoilPatternIndex,
			ref _hostRecoilSprayOrdinal,
			now,
			wantsAds,
			moving,
			false,
			out var bloomUsed,
			out _,
			out _,
			out _,
			effective.RecoilKickMultiplier,
			bloomMul );

		var range = Math.Min( def.MaxRange, HumanNpcMaxEngagementRangeWorld );
		var aimPoint = ThornsBanditPerception.ResolveAimPoint( targetRoot );
		var useDirectTargetDamage = ThornsBanditCombatUtil.IsDirectCombatTarget( targetRoot );

		if ( useDirectTargetDamage )
		{
			ThornsBanditCombatUtil.TryApplyCombatTargetDamage(
				GameObject,
				targetRoot,
				aimPoint,
				def,
				HumanNpcHitscanDamageMul );

			var pelletCount = Math.Max( 1, def.PelletCount );
			for ( var p = 0; p < pelletCount; p++ )
			{
				var pelletDir = pelletCount <= 1
					? fireDir
					: ThornsBanditCombatUtil.SamplePelletDirection(
						fireDir,
						def.PelletSpreadHalfAngleDegrees + bloomUsed * 0.35f );

				ThornsCombatTracerWorldService.HostBroadcastShotFromAttacker(
					GameObject.Scene,
					GameObject,
					eyePos,
					pelletDir,
					range,
					ThornsCombatTracerSource.Npc,
					CombatWeaponDefinitionId );
			}
		}
		else
		{
			var pelletCount = Math.Max( 1, def.PelletCount );
			for ( var p = 0; p < pelletCount; p++ )
			{
				var pelletDir = pelletCount <= 1
					? fireDir
					: ThornsBanditCombatUtil.SamplePelletDirection(
						fireDir,
						def.PelletSpreadHalfAngleDegrees + bloomUsed * 0.35f );

				ThornsBanditCombatUtil.TryApplyPelletDamage(
					GameObject,
					eyePos,
					pelletDir,
					range,
					def,
					HumanNpcHitscanDamageMul );

				ThornsCombatTracerWorldService.HostBroadcastShotFromAttacker(
					GameObject.Scene,
					GameObject,
					eyePos,
					pelletDir,
					range,
					ThornsCombatTracerSource.Npc,
					CombatWeaponDefinitionId );
			}
		}

		_burstShotsFired++;
		if ( _burstShotsFired >= _burstShotsPlanned )
		{
			_burstPauseUntilRealtime = now + Game.Random.Float(
				ThornsBanditCombatTuning.BurstPauseMinSeconds,
				ThornsBanditCombatTuning.BurstPauseMaxSeconds );
			_burstShotsFired = 0;
			_burstShotsPlanned = 0;
		}
		else
		{
			_burstNextShotRealtime = now + def.FireIntervalSeconds;
		}

		return true;
	}

	public void HostCancelReposition()
	{
		_repositionPending = false;
	}

	public bool HostConsumeRepositionSignal()
	{
		if ( !_repositionPending || HostIsInBurstPause )
			return false;

		_repositionPending = false;
		return true;
	}

	public void HostPrepareCombatReaction( ThornsBanditArchetypeConfig cfg, float extraDelaySeconds = 0f )
	{
		var min = MathF.Min( cfg.ReactionTimeMinSeconds, cfg.ReactionTimeMaxSeconds );
		var max = MathF.Max( cfg.ReactionTimeMinSeconds, cfg.ReactionTimeMaxSeconds );
		var delay = Game.Random.Float( min, max ) + extraDelaySeconds;
		delay *= Skill switch
		{
			ThornsBanditSkillLevel.Poor => 1.18f,
			ThornsBanditSkillLevel.Veteran => 0.86f,
			_ => 1f
		};

		if ( extraDelaySeconds <= 0.01f )
			delay += Game.Random.Float( 0f, 0.05f );
		else
			delay += Game.Random.Float( 0.06f, 0.18f );

		_reactionReadyRealtime = Time.Now + delay;
		_repositionPending = false;
		HostResetBurstCadence();
	}

	public void ApplySkill( ThornsBanditSkillLevel skill ) => Skill = skill;

	bool TryResolveBanditEye( out Vector3 eyePos, out Rotation eyeRot, out Vector3 aimForward )
	{
		eyePos = default;
		eyeRot = Rotation.Identity;
		aimForward = Vector3.Forward;

		var view = ThornsBanditUtil.FindChild( GameObject, "View" );
		if ( view.IsValid() )
		{
			eyeRot = view.WorldRotation;
			aimForward = eyeRot.Forward.Normal;
			eyePos = view.WorldPosition;
			return aimForward.LengthSquared > 1e-6f;
		}

		if ( ThornsLocalPlayer.TryGetAuthoritativeEye( GameObject, out eyePos, out eyeRot ) )
		{
			aimForward = eyeRot.Forward.Normal;
			return aimForward.LengthSquared > 1e-6f;
		}

		eyePos = GameObject.WorldPosition + Vector3.Up * 64f;
		aimForward = GameObject.WorldRotation.Forward.Normal;
		eyeRot = Rotation.LookAt( aimForward );
		return aimForward.LengthSquared > 1e-6f;
	}

	void HostResetBurstCadence()
	{
		_burstShotsFired = 0;
		_burstShotsPlanned = 0;
		_burstNextShotRealtime = 0;
		_burstPauseUntilRealtime = 0;
	}

	void HostPlayGunshotFx( Vector3 eyePos, float noiseMul )
	{
		var firePath = ThornsGameplaySfx.FireSoundForCombatId( CombatWeaponDefinitionId );
		if ( string.IsNullOrWhiteSpace( firePath ) )
			firePath = ThornsGameplaySfx.M4Fire;

		if ( Time.Now < _nextNpcGunshotRpcRealtime )
			return;

		_nextNpcGunshotRpcRealtime = Time.Now + 0.2f;
		Terraingen.Audio.ThornsAudioWorldService.BroadcastFollowing(
			GameObject.Id,
			firePath,
			Vector3.Up * 64f,
			ThornsSpatialSfxCategory.NpcGunshot,
			noiseMul,
			eyePos );
	}
}
