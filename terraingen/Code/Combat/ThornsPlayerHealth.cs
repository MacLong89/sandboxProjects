namespace Terraingen.Combat;

using Terraingen;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.World;

/// <summary>Server-authoritative player health and respawn.</summary>
[Title( "Thorns Player Health" )]
[Category( "Player" )]
public sealed class ThornsPlayerHealth : Component
{
	[Property] public float MaxHealth { get; set; } = 100f;

	[Property] public float RespawnDelaySeconds { get; set; } = 3f;

	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; } = 100f;

	[Sync( SyncFlags.FromHost )] public bool IsDeadState { get; set; }

	bool _respawnPending;
	TimeUntil _respawnIn;
	bool _wasDeadState;
	Vector3? _frozenDeathPosition;
	Vector3? _frozenDeathEyePosition;
	Rotation? _frozenDeathEyeRotation;
	PlayerController _playerController;
	bool _playerControllerWasEnabled = true;

	public bool IsAlive => CurrentHealth > 0.001f && !IsDeadState;

	protected override void OnStart()
	{
		if ( !ThornsLocalPlayer.IsPlayerPawnRoot( GameObject ) )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			HostReset();
	}

	public void HostReset()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		CurrentHealth = MaxHealth;
		IsDeadState = false;
		_respawnPending = false;
		_frozenDeathPosition = null;
		_frozenDeathEyePosition = null;
		_frozenDeathEyeRotation = null;
	}

	/// <summary>Host-only heal (capped at max health).</summary>
	public void HostHeal( float amount )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0f || !IsAlive )
			return;

		CurrentHealth = Math.Min( MaxHealth, CurrentHealth + amount );
		ThornsWorldPersistence.RequestSignificantSave();
	}

	/// <summary>Legacy entry — routes through <see cref="ThornsPlayerDamageReceiver"/>.</summary>
	public bool HostTakeDamage( float amount, GameObject attackerRoot )
	{
		var receiver = Components.Get<ThornsPlayerDamageReceiver>()
		               ?? Components.Create<ThornsPlayerDamageReceiver>();
		var result = receiver.HostApplyDamage( attackerRoot, new ThornsCombatDamage.DamageInfo
		{
			Amount = amount,
			AttackerRoot = attackerRoot,
			VictimRoot = GameObject,
			DamageTypeId = "legacy",
			VictimKind = ThornsCombatDamage.VictimKind.Player,
			VictimFaction = ThornsCombatFactions.FactionKind.Player,
			AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot )
		} );
		return result.Killed;
	}

	/// <summary>Called only from <see cref="ThornsPlayerDamageReceiver"/> after permission checks.</summary>
	internal bool HostApplyDamageFromPipeline( float amount, GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0f || !IsAlive )
			return false;

		CurrentHealth = Math.Max( 0f, CurrentHealth - amount );
		Components.Get<ThornsPlayerGameplay>()?.HostNotifyDamaged();

		if ( CurrentHealth > 0f )
		{
			ThornsAnimalCompanion.NotifyOwnerThreat( GameObject, attackerRoot );
			ThornsWorldPersistence.RequestSignificantSave();
			return false;
		}

		CurrentHealth = 0f;
		IsDeadState = true;
		OnKilled( attackerRoot, info );
		return true;
	}

	void OnKilled( GameObject attacker, in ThornsCombatDamage.DamageInfo info )
	{
		var attackerLabel = attacker.IsValid()
			? attacker.Name
			: string.IsNullOrWhiteSpace( info.DamageTypeId ) ? "unknown" : info.DamageTypeId;
		Log.Warning( $"[Thorns] Player died (attacker={attackerLabel})." );

		var mount = Components.Get<ThornsPlayerMountController>();
		mount?.HostCleanupMountForDeath();

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		gameplay?.HostCleanupOpenSessions();
		if ( gameplay is not null && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
		{
			ThornsPvpKillHost.HostResetKillStreak( gameplay.AccountKey );
			ThornsMapWorldService.Instance?.HostSetLastDeath( gameplay.AccountKey, GameObject.WorldPosition );
			gameplay.PushMapSnapshotToOwner();
		}
		Components.Get<ThornsPlayerBuildingController>()?.ForceCloseBuildMode();
		Components.Get<ThornsPlayerAnimalTaming>()?.CancelHold();
		ThornsDeathCrateWorldService.Instance?.HostTrySpawnForPlayer( GameObject, gameplay, GameObject.WorldPosition );
		ThornsWorldPersistence.RequestImmediateSave( forceSync: true );

		if ( attacker.IsValid() && gameplay is not null )
		{
			var killer = attacker.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
			if ( killer is not null && killer.IsValid() && killer != gameplay )
				ThornsPvpKillHost.HostReportPlayerKill( killer, gameplay );
		}

		_respawnPending = true;
		_respawnIn = RespawnDelaySeconds;
	}

	protected override void OnUpdate()
	{
		if ( !ThornsLocalPlayer.IsPlayerPawnRoot( GameObject ) )
			return;

		var wasDead = _wasDeadState;
		var justDied = !wasDead && IsDeadState;
		var justRevived = wasDead && IsAlive;
		_wasDeadState = IsDeadState;

		if ( justDied )
			ApplyDeathPresentation();

		if ( IsDeadState )
			MaintainDeathPresentation();

		if ( justRevived )
		{
			RestoreAlivePresentation();
			if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
				ThornsSceneObserver.FocusLocalPlayer( Scene, GameObject );
		}

		if ( !_respawnPending || _respawnIn )
			return;

		_respawnPending = false;
		if ( !GameObject.IsValid() || !ThornsMultiplayer.IsHostOrOffline )
			return;

		HostReset();
		Components.Get<ThornsPlayerGameplay>()?.HostRefillHungerAndThirstOnRespawn();

		var mount = Components.Get<ThornsPlayerMountController>();
		mount?.HostCleanupMountForDeath();

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( TryResolveBedRespawn( gameplay, out var bedSpawn ) )
			GameObject.WorldPosition = bedSpawn;
		else if ( ThornsPlayerSpawnLocations.TryPickRandom( Scene, out var spawn ) )
			GameObject.WorldPosition = spawn;
		else if ( ThornsPlayerSpawnLocations.TryResolveClearSpawn( Scene, GameObject.WorldPosition, ThornsPlayerSpawnLocations.SpawnFeetClearanceInches, out var clear ) )
			GameObject.WorldPosition = clear;
		else
			GameObject.WorldPosition = ThornsPlayerSpawnLocations.SnapToTerrain( Scene, GameObject.WorldPosition );

		RestoreAlivePresentation();
		if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			ThornsSceneObserver.FocusLocalPlayer( Scene, GameObject );

		ThornsWorldPersistence.RequestImmediateSave( forceSync: true );
		Log.Info( $"[Thorns] Player respawned at {GameObject.WorldPosition:F0}." );
	}

	bool TryResolveBedRespawn( ThornsPlayerGameplay gameplay, out Vector3 spawn )
	{
		spawn = default;
		if ( gameplay is null || !gameplay.IsValid() || string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			return false;

		if ( ThornsWorldPersistence.Instance?.TryGetPlayerByAccountKey( gameplay.AccountKey, out var dto ) != true
		     || dto is null
		     || !dto.HasBedSpawn )
			return false;

		var bed = new Vector3( dto.BedSpawnX, dto.BedSpawnY, dto.BedSpawnZ );
		return ThornsPlayerSpawnLocations.TryResolveBedRespawn(
			Scene,
			bed,
			dto.BedSpawnYaw,
			ThornsPlayerSpawnLocations.SpawnFeetClearanceInches,
			out spawn );
	}

	/// <summary>Runs on every peer when <see cref="IsDeadState"/> syncs (host <see cref="OnKilled"/> alone is not enough).</summary>
	void ApplyDeathPresentation()
	{
		var mount = Components.Get<ThornsPlayerMountController>();
		mount?.HostCleanupMountForDeath();

		_frozenDeathPosition ??= GameObject.WorldPosition;
		CaptureDeathCameraPose();

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		ThornsSceneObserver.ClearCachedLocalPlayer();

		_playerController = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( _playerController.IsValid() )
		{
			_playerControllerWasEnabled = _playerController.Enabled;
			_playerController.UseInputControls = false;
			_playerController.UseLookControls = false;
			_playerController.UseCameraControls = false;
			_playerController.Enabled = false;
		}

		ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( GameObject );
		PinDeathCameraPose();
	}

	void MaintainDeathPresentation()
	{
		var mount = Components.Get<ThornsPlayerMountController>();
		if ( GameObject.Parent.IsValid() )
			mount?.CleanupPresentationAfterDeathOrRespawn();

		if ( !_frozenDeathPosition.HasValue )
			return;

		GameObject.WorldPosition = _frozenDeathPosition.Value;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			PinDeathCameraPose();
	}

	void RestoreAlivePresentation()
	{
		_frozenDeathPosition = null;
		_frozenDeathEyePosition = null;
		_frozenDeathEyeRotation = null;

		Components.Get<ThornsPlayerMountController>()?.CleanupPresentationAfterDeathOrRespawn();
		ThornsSceneObserver.ClearCachedLocalPlayer();

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		ThornsPlayerFirstPersonRig.ReleaseDeathCameraPin( GameObject );

		if ( _playerController.IsValid() )
		{
			_playerController.Enabled = _playerControllerWasEnabled;
			_playerController.UseInputControls = true;
			_playerController.UseLookControls = true;
			_playerController.UseCameraControls = true;
			var locomotion = Components.Get<ThornsPlayerLocomotion>();
			locomotion?.ConfigurePlayerController();
		}

		ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( GameObject );
	}

	void CaptureDeathCameraPose()
	{
		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
		if ( rig.IsValid() )
		{
			_frozenDeathEyePosition = rig.WorldPosition;
			_frozenDeathEyeRotation = rig.WorldRotation;
			return;
		}

		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( controller.IsValid() )
		{
			var eyeLocal = new Vector3( 0f, 0f, ThornsPlayerFirstPersonRig.DefaultEyeOffsetZ );
			_frozenDeathEyePosition = GameObject.WorldPosition + GameObject.WorldRotation * eyeLocal;
			_frozenDeathEyeRotation = GameObject.WorldRotation * controller.EyeAngles.ToRotation();
		}
	}

	void PinDeathCameraPose()
	{
		if ( !_frozenDeathEyePosition.HasValue || !_frozenDeathEyeRotation.HasValue )
			return;

		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
		if ( !rig.IsValid() )
			return;

		rig.WorldPosition = _frozenDeathEyePosition.Value;
		rig.WorldRotation = _frozenDeathEyeRotation.Value;

		var cam = rig.Components.Get<CameraComponent>();
		if ( cam.IsValid() )
		{
			cam.Enabled = true;
			cam.IsMainCamera = true;
		}

		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( Scene, GameObject );
	}
}
