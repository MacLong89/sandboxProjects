using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Rare, emotional atmospheric music — host picks state/track; local owner handles fades/playback only.
/// World ambience bed (<see cref="ThornsWorldAmbience"/>) stays dominant; music is sparse punctuation.
/// </summary>
[Title( "Thorns — Atmospheric music" )]
[Category( "Thorns/Audio" )]
[Icon( "music_note" )]
[Order( 47 )]
public sealed class ThornsAtmosphericMusic : Component, Component.INetworkSpawn
{
	[Property, Group( "Timing" )] public float CombatStressSeconds { get; set; } = 42f;
	[Property, Group( "Timing" )] public float PostCombatDelaySeconds { get; set; } = 14f;
	[Property, Group( "Timing" )] public float PostCombatWindowSeconds { get; set; } = 75f;
	[Property, Group( "Timing" )] public float AliveGraceSeconds { get; set; } = 95f;
	[Property, Group( "Timing" )] public float GunfireSuppressRadius { get; set; } = 3200f;
	[Property, Group( "Timing" )] public float HostileAwarenessRadius { get; set; } = 2600f;
	[Property, Group( "Timing" )] public float WorldEventSuppressRadius { get; set; } = 1400f;
	[Property, Group( "Movement" )] public float CalmMaxPlanarSpeed { get; set; } = 340f;
	[Property, Group( "Movement" )] public float SprintSuppressPlanarSpeed { get; set; } = 460f;
	[Property, Group( "Client" )] public float MusicBusVolume { get; set; } = 1f;
	[Property, Group( "Client" )] public float WorldAmbienceDuckWhenMusic { get; set; } = 0.55f;
	[Property, Group( "Debug" )] public bool DebugMusic { get; set; }

	[Sync( SyncFlags.FromHost )] public ThornsMusicState ActiveState { get; private set; }
	[Sync( SyncFlags.FromHost )] public int PlayEpoch { get; private set; }
	[Sync( SyncFlags.FromHost )] public byte PlayTrackIndex { get; private set; }
	[Sync( SyncFlags.FromHost )] public ThornsMusicSuppressionFlags SuppressionMask { get; private set; }
	[Sync( SyncFlags.FromHost )] public ThornsMusicBlockReason BlockReasonMask { get; private set; }
	[Sync( SyncFlags.FromHost )] public float CooldownRemainingSeconds { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool HostWantsClientCampfireLayer { get; private set; }

	double _silenceUntil;
	double _trackPlayingUntil;
	double _combatStressUntil;
	double _lastDamageTime;
	double _postCombatEligibleAt;
	double _postCombatWindowUntil;
	bool _postCombatConsumed;
	double _nextHostEval;
	double _spawnedAt;
	bool _wasInCombatStress;

	Random _rng;
	SoundHandle _musicHandle;
	int _clientEpoch = -1;
	ThornsMusicState _clientPlayingState;
	byte _clientTrackIndex;
	float _fadeStartVolume;
	double _fadeEndTime;
	float _fadeDuration;
	bool _fadingOut;
	string _debugWeightedSummary = "";

	public void OnNetworkSpawn( Connection owner )
	{
		if ( Networking.IsHost )
			_spawnedAt = Time.Now;
	}

	public void HostOnPlayerDamaged()
	{
		if ( !Networking.IsHost )
			return;

		_lastDamageTime = Time.Now;
		_combatStressUntil = Time.Now + CombatStressSeconds;
		_postCombatConsumed = false;
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost || !Networking.IsActive )
			_rng = new Random( HashCode.Combine( GameObject.Network.OwnerId, Game.Ident?.GetHashCode() ?? 17 ) );
	}

	protected override void OnDestroy()
	{
		StopMusicImmediate();
		RestoreWorldAmbienceVolume();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !IsValid || !GameObject.IsValid() )
			return;

		try
		{
			if ( Networking.IsHost || !Networking.IsActive )
				HostTick();

			if ( ThornsPawn.IsLocalConnectionOwner( this ) )
				ClientTick();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Thorns Music] Update failed on '{GameObject.Name}': {ex.Message}" );
		}
	}

	void HostTick()
	{
		if ( !GameObject.IsValid() )
			return;

		ThornsMusicWorldSignals.HostPruneOldGunshots( GunfireMemorySeconds() );
		SuppressionMask = HostEvaluateSuppression();
		BlockReasonMask = ThornsMusicBlockReason.None;
		CooldownRemainingSeconds = MathF.Max( 0f, (float)(_silenceUntil - Time.Now) );

		var inCombat = (SuppressionMask & ThornsMusicSuppressionFlags.CombatStress) != 0;
		if ( _wasInCombatStress && !inCombat )
		{
			_postCombatEligibleAt = Time.Now + PostCombatDelaySeconds;
			_postCombatWindowUntil = Time.Now + PostCombatWindowSeconds;
			_postCombatConsumed = false;
		}

		_wasInCombatStress = inCombat;

		if ( Time.Now < _nextHostEval )
			return;

		_nextHostEval = Time.Now + 2.1f;
		HostTryScheduleTrack();
	}

	float GunfireMemorySeconds() => MathF.Max( CombatStressSeconds, 50f );

	ThornsMusicSuppressionFlags HostEvaluateSuppression()
	{
		var mask = ThornsMusicSuppressionFlags.None;
		var pos = GameObject.WorldPosition;
		var now = Time.Now;

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
			mask |= ThornsMusicSuppressionFlags.Dead;

		if ( now < _spawnedAt + AliveGraceSeconds )
			mask |= ThornsMusicSuppressionFlags.SpawnGrace;

		if ( now < _combatStressUntil || now < _lastDamageTime + CombatStressSeconds * 0.85f )
			mask |= ThornsMusicSuppressionFlags.CombatStress | ThornsMusicSuppressionFlags.RecentDamage;

		if ( ThornsMusicWorldSignals.HostHasRecentGunfireNear( pos, GunfireSuppressRadius, 44f ) )
			mask |= ThornsMusicSuppressionFlags.NearbyGunfire | ThornsMusicSuppressionFlags.CombatStress;

		if ( ThornsHostileAwarenessCache.AnyHostileWithinRadius( pos, HostileAwarenessRadius ) )
			mask |= ThornsMusicSuppressionFlags.HostileAwareness | ThornsMusicSuppressionFlags.CombatStress;

		if ( HostHasActiveWorldEventNear( pos ) )
			mask |= ThornsMusicSuppressionFlags.WorldEvent;

		if ( HostGetPlanarSpeed() >= SprintSuppressPlanarSpeed )
			mask |= ThornsMusicSuppressionFlags.SprintCombatPace;

		if ( now < _silenceUntil )
			mask |= ThornsMusicSuppressionFlags.GlobalSilenceCooldown;

		if ( now < _trackPlayingUntil )
			mask |= ThornsMusicSuppressionFlags.TrackStillPlaying;

		return mask;
	}

	bool HostHasActiveWorldEventNear( Vector3 pos )
	{
		var r = WorldEventSuppressRadius;
		var r2 = r * r;
		var beacons = ThornsDynamicSupplyBeaconPopulation.HostBeaconsReadOnly;
		for ( var i = 0; i < beacons.Count; i++ )
		{
			var beacon = beacons[i];
			if ( !beacon.IsValid() || !beacon.Enabled )
				continue;

			var beaconGo = beacon.GameObject;
			if ( !beaconGo.IsValid() )
				continue;

			var w = beaconGo.WorldPosition;
			var dx = w.x - pos.x;
			var dy = w.y - pos.y;
			if ( dx * dx + dy * dy <= r2 )
				return true;
		}

		return false;
	}

	float HostGetPlanarSpeed()
	{
		var move = Components.Get<ThornsPawnMovement>();
		if ( !move.IsValid() )
			return 0f;

		var v = ThornsPawnLocomotion.TryGetVelocity( move.GameObject );
		return new Vector3( v.x, v.y, 0f ).Length;
	}

	void EnsureHostRng()
	{
		_rng ??= new Random( HashCode.Combine( GameObject.Network.OwnerId, (int)Time.Now ) );
	}

	void HostTryScheduleTrack()
	{
		EnsureHostRng();

		var now = Time.Now;
		var postCombatWindow = !_postCombatConsumed
		                       && now >= _postCombatEligibleAt
		                       && now <= _postCombatWindowUntil;

		if ( !HostCanSchedulePlayback( postCombatWindow ) )
		{
			BlockReasonMask |= ThornsMusicBlockReason.Suppressed;
			_debugWeightedSummary = "suppressed";
			return;
		}

		var desired = HostPickDesiredState();
		if ( desired == ThornsMusicState.None )
		{
			BlockReasonMask |= ThornsMusicBlockReason.NoEligibleState;
			_debugWeightedSummary = "no eligible state";
			return;
		}

		if ( !ThornsMusicCatalog.TryGetState( desired, out var def ) )
		{
			BlockReasonMask |= ThornsMusicBlockReason.NoTracks;
			return;
		}

		if ( desired != ThornsMusicState.PostCombatReflection && _rng.NextSingle() > def.TriggerChancePerEvaluation )
		{
			BlockReasonMask |= ThornsMusicBlockReason.ProbabilityGate;
			_debugWeightedSummary = $"gate {desired} p={def.TriggerChancePerEvaluation:P0}";
			return;
		}

		if ( !ThornsMusicCatalog.TryPickTrack( desired, ref _rng, out var track, out var trackIndex ) )
		{
			BlockReasonMask |= ThornsMusicBlockReason.NoTracks;
			return;
		}

		if ( !ThornsMusicCatalog.SoundExists( track.SoundPath ) )
		{
			BlockReasonMask |= ThornsMusicBlockReason.NoTracks;
			return;
		}

		HostBeginPlay( desired, trackIndex, track.DurationSeconds, def );
		_debugWeightedSummary = $"{desired} → {track.SoundPath} ({track.Weight:0.##})";
	}

	bool HostCanSchedulePlayback( bool postCombatWindow )
	{
		if ( (SuppressionMask & ThornsMusicSuppressionFlags.Dead) != 0 )
			return false;

		if ( (SuppressionMask & ThornsMusicSuppressionFlags.TrackStillPlaying) != 0 )
			return false;

		if ( (SuppressionMask & ThornsMusicSuppressionFlags.NearbyGunfire) != 0 )
			return false;

		if ( (SuppressionMask & ThornsMusicSuppressionFlags.HostileAwareness) != 0 )
			return false;

		if ( (SuppressionMask & ThornsMusicSuppressionFlags.WorldEvent) != 0 )
			return false;

		if ( (SuppressionMask & ThornsMusicSuppressionFlags.SprintCombatPace) != 0 )
			return false;

		if ( !postCombatWindow )
		{
			if ( (SuppressionMask & ThornsMusicSuppressionFlags.SpawnGrace) != 0 )
				return false;

			if ( (SuppressionMask & ThornsMusicSuppressionFlags.CombatStress) != 0 )
				return false;

			if ( (SuppressionMask & ThornsMusicSuppressionFlags.RecentDamage) != 0 )
				return false;

			if ( (SuppressionMask & ThornsMusicSuppressionFlags.GlobalSilenceCooldown) != 0 )
				return false;
		}

		return true;
	}

	ThornsMusicState HostPickDesiredState()
	{
		var now = Time.Now;
		var pos = GameObject.WorldPosition;
		var calmMovement = HostIsCalmMovement();
		var night = HostIsNightPhase();
		var rain = HostIsRainStub();
		var storm = HostIsStormStub();
		var bloom = HostIsBloomTurfStub( pos );
		var campfire = HostIsNearCampfireStub( pos );
		var buildMode = Components.Get<ThornsBuildingController>() is { BuildModeActive: true };

		HostWantsClientCampfireLayer = campfire;

		if ( !_postCombatConsumed && now >= _postCombatEligibleAt && now <= _postCombatWindowUntil )
			return ThornsMusicState.PostCombatReflection;

		if ( storm && calmMovement )
			return ThornsMusicState.Storm;

		if ( rain && calmMovement )
			return ThornsMusicState.Rain;

		if ( bloom && calmMovement )
			return ThornsMusicState.BloomCorruption;

		if ( campfire )
			return ThornsMusicState.CampfireSafeZone;

		if ( night && calmMovement && ( buildMode || HostIsOutdoors() ) )
			return ThornsMusicState.NightExploration;

		if ( calmMovement )
			return ThornsMusicState.CalmExploration;

		return ThornsMusicState.None;
	}

	bool HostIsCalmMovement() => HostGetPlanarSpeed() <= CalmMaxPlanarSpeed;

	bool HostIsOutdoors()
	{
		// No interior volume system yet — treat as outdoors until building volumes exist.
		return true;
	}

	bool HostIsNightPhase()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		return ThornsCelestialSystem.TryGetTimeOfDay( scene, out _, out var night ) && night;
	}

	bool HostIsRainStub() => false;

	bool HostIsStormStub() => false;

	bool HostIsBloomTurfStub( Vector3 _ ) => false;

	bool HostIsNearCampfireStub( Vector3 pos )
	{
		const float r = 420f;
		var r2 = r * r;
		foreach ( var c in SnapshotActiveLootCrates() )
		{
			if ( !c.IsValid() || !c.Enabled )
				continue;

			var crateGo = c.GameObject;
			if ( !crateGo.IsValid() )
				continue;

			var name = crateGo.Name ?? "";
			if ( !name.Contains( "campfire", StringComparison.OrdinalIgnoreCase ) )
				continue;

			var w = crateGo.WorldPosition;
			var dx = w.x - pos.x;
			var dy = w.y - pos.y;
			if ( dx * dx + dy * dy <= r2 )
				return true;
		}

		return false;
	}

	static IEnumerable<ThornsLootCrate> SnapshotActiveLootCrates()
	{
		var dict = ThornsLootCrate.ActiveById;
		if ( dict is null || dict.Count == 0 )
			yield break;

		var snapshot = new ThornsLootCrate[dict.Count];
		var n = 0;
		foreach ( var c in dict.Values )
		{
			if ( c is not null )
				snapshot[n++] = c;
		}

		for ( var i = 0; i < n; i++ )
			yield return snapshot[i];
	}

	void HostBeginPlay( ThornsMusicState state, int trackIndex, float durationSeconds, ThornsMusicCatalog.StateDefinition def )
	{
		EnsureHostRng();

		ActiveState = state;
		PlayTrackIndex = (byte)trackIndex;
		PlayEpoch++;
		var silenceSpan = def.MinSilenceAfterSeconds
		                  + _rng!.NextSingle() * MathF.Max( 1f, def.MaxSilenceAfterSeconds - def.MinSilenceAfterSeconds );
		_trackPlayingUntil = Time.Now + MathF.Max( 30f, durationSeconds );
		_silenceUntil = _trackPlayingUntil + silenceSpan;

		if ( state == ThornsMusicState.PostCombatReflection )
			_postCombatConsumed = true;
	}

	void ClientTick()
	{
		if ( !GameObject.IsValid() )
			return;

		if ( ClientModalBlocksMusic() )
		{
			BeginClientFadeOut( 0.35f );
			TickClientFade();
			DuckWorldAmbience();
			return;
		}

		var shell = Components.Get<ThornsGameShell>();
		var campfireUi = shell.IsValid() && shell.CampfireUiOpen;
		var targetState = ActiveState;
		if ( campfireUi && SuppressionMask == ThornsMusicSuppressionFlags.None && PlayEpoch <= 0 )
			targetState = ThornsMusicState.CampfireSafeZone;

		if ( PlayEpoch != _clientEpoch )
		{
			_clientEpoch = PlayEpoch;
			if ( PlayEpoch > 0 && targetState != ThornsMusicState.None )
				ClientStartTrack( targetState, PlayTrackIndex );
			else
				BeginClientFadeOut( 0.5f );
		}

		TickClientFade();
		DuckWorldAmbience();
	}

	bool ClientModalBlocksMusic()
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay )
			return true;

		var hud = Components.Get<ThornsDebugHudHost>();
		if ( hud.IsValid() && ( hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop ) )
			return true;

		return false;
	}

	void ClientStartTrack( ThornsMusicState state, int trackIndex )
	{
		if ( !ThornsMusicCatalog.TryGetState( state, out var def ) )
			return;

		if ( def.Tracks is null || trackIndex < 0 || trackIndex >= def.Tracks.Length )
			return;

		var track = def.Tracks[trackIndex];
		if ( !ThornsMusicCatalog.SoundExists( track.SoundPath ) )
			return;

		StopMusicImmediate();

		var h = Sound.Play( track.SoundPath.Trim(), Vector3.Zero );
		if ( !h.IsValid() )
			return;

		h.SpacialBlend = 0f;
		h.Volume = 0f;
		_musicHandle = h;
		_clientPlayingState = state;
		_clientTrackIndex = (byte)trackIndex;
		_fadingOut = false;
		_fadeDuration = MathF.Max( 0.05f, def.FadeInSeconds );
		_fadeStartVolume = 0f;
		_targetFadeVolume = def.Volume * MusicBusVolume;
		_fadeStartVolume = 0f;
		_fadeEndTime = Time.Now + _fadeDuration;
	}

	float _targetFadeVolume = 1f;

	void TickClientFade()
	{
		if ( _musicHandle is not { IsValid: true } )
			return;

		if ( _fadeEndTime > 0 )
		{
			var remaining = (float)(_fadeEndTime - Time.Now);
			var t = 1f - Math.Clamp( remaining / MathF.Max( 0.05f, _fadeDuration ), 0f, 1f );
			if ( _fadingOut )
				_musicHandle.Volume = MathF.Max( 0f, _fadeStartVolume * (1f - t) );
			else
				_musicHandle.Volume = MathF.Max( 0f, _targetFadeVolume * t );
		}

		if ( _fadingOut && _musicHandle.Volume <= 0.001f )
			StopMusicImmediate();

		if ( !_fadingOut && _musicHandle is { IsValid: true, IsPlaying: false } )
		{
			// Track ended — do not restart; host enforces silence window.
			StopMusicImmediate();
		}

		if ( SuppressionMask != ThornsMusicSuppressionFlags.None && !_fadingOut )
		{
			if ( ThornsMusicCatalog.TryGetState( _clientPlayingState, out var def ) )
				BeginClientFadeOut( def.FadeOutSeconds );
			else
				BeginClientFadeOut( 1.2f );
		}
	}

	void BeginClientFadeOut( float seconds )
	{
		if ( _musicHandle is not { IsValid: true } )
			return;

		if ( _fadingOut )
			return;

		_fadingOut = true;
		_fadeStartVolume = _musicHandle.Volume;
		_fadeDuration = MathF.Max( 0.05f, seconds );
		_fadeEndTime = Time.Now + _fadeDuration;
	}

	void StopMusicImmediate()
	{
		var h = _musicHandle;
		_musicHandle = default;
		_fadeEndTime = 0;
		_fadingOut = false;
		if ( h is { IsValid: true } )
			h.Stop( 0f );
	}

	void DuckWorldAmbience()
	{
		var playing = _musicHandle is { IsValid: true, IsPlaying: true } && _musicHandle.Volume > 0.04f;
		if ( !TryGetWorldAmbience( out var amb ) )
			return;

		amb.RuntimeVolumeMultiplier = playing ? WorldAmbienceDuckWhenMusic : 1f;
	}

	void RestoreWorldAmbienceVolume()
	{
		if ( TryGetWorldAmbience( out var amb ) )
			amb.RuntimeVolumeMultiplier = 1f;
	}

	bool TryGetWorldAmbience( out ThornsWorldAmbience amb )
	{
		amb = default;
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var mgr in scene.GetAllComponents<ThornsGameManager>() )
		{
			if ( !mgr.IsValid() )
				continue;

			amb = mgr.Components.Get<ThornsWorldAmbience>();
			return amb.IsValid();
		}

		return false;
	}

	/// <summary>Debug overlay lines (F1 developer panel).</summary>
	public void AppendDebugLines( List<string> lines )
	{
		if ( lines is null )
			return;

		lines.Add( $"Music state: {ActiveState}  epoch={PlayEpoch}  track={PlayTrackIndex}" );
		lines.Add( $"Suppression: {SuppressionMask}" );
		lines.Add( $"Block: {BlockReasonMask}" );
		lines.Add( $"Silence cooldown: {CooldownRemainingSeconds:0}s" );
		lines.Add( $"Weighted pick: {_debugWeightedSummary}" );
		if ( _musicHandle is { IsValid: true } )
			lines.Add( $"Client bus: vol={_musicHandle.Volume:0.00} playing={_musicHandle.IsPlaying}" );
	}
}
