using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Alone-only abilities: Q dash, E paranoia (hunters debuffed), F mimic (appear as a hunter). Host-authoritative.
/// </summary>
[Title( "YouAreNotAlone — Alone abilities" )]
[Category( "YouAreNotAlone" )]
[Icon( "sports_martial_arts" )]
[Order( 400 )]
public sealed class YaAloneMechanics : Component
{
	/// <summary>1 = dash ready, 0–1 = cooldown refilling (host-written, HUD).</summary>
	[Sync( SyncFlags.FromHost )] public float DashHudCooldownFill01 { get; set; } = 1f;

	/// <summary>1 = paranoia ready, 0–1 = cooldown refilling (host-written, HUD).</summary>
	[Sync( SyncFlags.FromHost )] public float ParanoiaHudCooldownFill01 { get; set; } = 1f;

	/// <summary>1 = mimic ready, 0–1 = cooldown refilling (host-written, HUD).</summary>
	[Sync( SyncFlags.FromHost )] public float MimicHudCooldownFill01 { get; set; } = 1f;

	/// <summary>Host <see cref="Time.Now"/> when mimic may be used again (HUD computes fill locally every frame).</summary>
	[Sync( SyncFlags.FromHost )] public double MimicHudCooldownEndsAt { get; set; }

	/// <summary>Host <see cref="Time.Now"/> when dash may be used again.</summary>
	[Sync( SyncFlags.FromHost )] public double DashHudCooldownEndsAt { get; set; }

	/// <summary>Host <see cref="Time.Now"/> when paranoia may be used again.</summary>
	[Sync( SyncFlags.FromHost )] public double ParanoiaHudCooldownEndsAt { get; set; }

	[Property] public float TeleportCooldownSeconds { get; set; } = 2.75f;

	/// <summary>Forward ray length for dash obstruction checks (world units).</summary>
	[Property] public float TeleportMaxRange { get; set; } = 420f;

	/// <summary>Horizontal velocity impulse for Q dash (<see cref="CharacterController.Punch"/>).</summary>
	[Property] public float DashHorizontalImpulse { get; set; } = 680f;

	/// <summary>If a wall is closer than this along the dash ray, the dash is cancelled.</summary>
	[Property] public float DashMinClearDistance { get; set; } = 44f;

	/// <summary>Skin inset from hit surface when scaling dash strength near walls.</summary>
	[Property] public float DashWallPadding { get; set; } = 32f;

	[Property] public float ParanoiaCooldownSeconds { get; set; } = 14f;

	[Property] public float MimicCooldownSeconds { get; set; } = 22f;

	[Property] public float MimicDurationSeconds { get; set; } = 10f;

	/// <summary>Optional 3D cue when Paranoia fires — assign a valid <c>.sound</c> resource.</summary>
	[Property] public string ParanoiaFootstepSoundPath { get; set; } = "sounds/paranoia_sound.sound";

	public const string DashSoundResource = "sounds/gun_deploy.sound";

	/// <summary>While true, other players see Alone as a hunter (TP tint + weapon mesh). Local FP/HUD stay unchanged.</summary>
	[Sync( SyncFlags.FromHost )] public bool MimicPresentationActive { get; set; }

	/// <summary>Host-updated seconds left on mimic (HUD).</summary>
	[Sync( SyncFlags.FromHost )] public float MimicHudSecondsRemaining { get; set; }

	/// <summary>Duration of the active mimic window (host snapshot when mimic starts; HUD bar denominator).</summary>
	[Sync( SyncFlags.FromHost )] public float MimicHudActiveDurationSeconds { get; set; }

	/// <summary>Hunter connection id to mirror for body tint.</summary>
	[Sync( SyncFlags.FromHost )] public Guid MimicVisualCopyConnectionId { get; set; }

	/// <summary>Hunter hotbar combat id to mirror for TP gun mesh for other players (e.g. m4, shotgun).</summary>
	[Sync( SyncFlags.FromHost )] public string MimicMirrorCombatId { get; set; } = "";

	double _hostNextTeleportAllowed;
	double _hostNextParanoiaAllowed;
	double _hostNextMimicAllowed;
	double _hostMimicEndTime;

	/// <summary>Host: set in <see cref="HostTryTeleport"/>, consumed in <see cref="YaPawnMovement.OnFixedUpdate"/> before <see cref="CharacterController.Move"/>.</summary>
	Vector3? _hostPendingAloneDashImpulse;

	/// <summary>Non-host owner: replicated dash impulse from host so local CC gets the same <see cref="CharacterController.Punch"/> as the server sim.</summary>
	Vector3? _ownerClientDashImpulse;

	static bool _qPrevAloneDashOrQDown;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( Networking.IsHost )
		{
			HostTickMimicExpire();
			HostRefreshTeleportCooldownHud();
			HostRefreshParanoiaCooldownHud();
			HostRefreshMimicCooldownHud();
			HostRefreshMimicHud();
		}

		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !roleCmp.IsValid() || roleCmp.Role != YaPlayerRole.Alone )
			return;

		if ( !YaRoundGate.MayUseWeapons() )
			return;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && hp.IsDeadState )
			return;

		// Dash (Q) hotkey is read from <see cref="YaPawnMovement.OnUpdate"/> (same frame phase as jump / movement input).

		if ( EPressed() )
		{
			if ( Networking.IsHost )
				HostTryParanoia();
			else
				RequestParanoiaRpc();
		}

		if ( FPressed() )
		{
			if ( Networking.IsHost )
				HostTryMimic();
			else
				RequestMimicRpc();
		}
	}

	static bool FPressed()
	{
		return Input.Pressed( "f" )
		       || Input.Pressed( "F" )
		       || Input.Keyboard.Pressed( "f" )
		       || Input.Keyboard.Pressed( "F" );
	}

	static bool EPressed()
	{
		return Input.Pressed( "E" )
		       || Input.Pressed( "e" )
		       || Input.Keyboard.Pressed( "E" )
		       || Input.Keyboard.Pressed( "e" );
	}

	internal static bool ConsumeAloneTeleportPressed()
	{
		// Input.config: Q → action "AloneDash". Match jump/movement: raw keyboard + action + rising edge.
		var down = Input.Down( "AloneDash" ) || Input.Down( "alonedash" )
		           || Input.Keyboard.Down( "Q" ) || Input.Keyboard.Down( "q" );
		var pressed = Input.Pressed( "AloneDash" ) || Input.Pressed( "alonedash" )
		              || Input.Pressed( "Q" ) || Input.Pressed( "q" )
		              || Input.Keyboard.Pressed( "Q" ) || Input.Keyboard.Pressed( "q" );
		var rising = down && !_qPrevAloneDashOrQDown;
		_qPrevAloneDashOrQDown = down;
		return pressed || rising;
	}

	/// <summary>Called from <see cref="YaPawnMovement"/> so teleport uses the same update phase as movement/jump.</summary>
	public void TryTeleportFromMovementInput()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !roleCmp.IsValid() || roleCmp.Role != YaPlayerRole.Alone )
			return;

		if ( !YaRoundGate.MayUseAloneTeleport() )
			return;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
			return;

		if ( !ConsumeAloneTeleportPressed() )
			return;

		if ( Networking.IsHost )
			HostTryTeleport();
		else
			RequestTeleportRpc();
	}

	void HostTickMimicExpire()
	{
		if ( !Networking.IsHost )
			return;

		if ( !MimicPresentationActive )
			return;

		if ( Time.Now < _hostMimicEndTime )
			return;

		HostClearMimic();
	}

	void HostClearMimic()
	{
		MimicPresentationActive = false;
		MimicMirrorCombatId = "";
		MimicVisualCopyConnectionId = default;
		MimicHudSecondsRemaining = 0f;
		MimicHudActiveDurationSeconds = 0f;
	}

	void HostRefreshMimicHud()
	{
		if ( !Networking.IsHost )
			return;

		if ( !MimicPresentationActive )
		{
			MimicHudSecondsRemaining = 0f;
			return;
		}

		MimicHudSecondsRemaining = (float)Math.Max( 0.0, _hostMimicEndTime - Time.Now );
	}

	void HostRefreshTeleportCooldownHud()
	{
		DashHudCooldownEndsAt = _hostNextTeleportAllowed;
		DashHudCooldownFill01 = GetDashCooldownFill01();
	}

	void HostRefreshParanoiaCooldownHud()
	{
		ParanoiaHudCooldownEndsAt = _hostNextParanoiaAllowed;
		ParanoiaHudCooldownFill01 = GetParanoiaCooldownFill01();
	}

	void HostRefreshMimicCooldownHud()
	{
		MimicHudCooldownEndsAt = _hostNextMimicAllowed;
		MimicHudCooldownFill01 = GetMimicCooldownFill01();
	}

	/// <summary>HUD: 0 = on cooldown, 1 = ready (uses synced end time so fill animates smoothly on clients).</summary>
	public float GetDashCooldownFill01() =>
		ComputeHudCooldownFill01( DashHudCooldownEndsAt, TeleportCooldownSeconds );

	public float GetParanoiaCooldownFill01() =>
		ComputeHudCooldownFill01( ParanoiaHudCooldownEndsAt, ParanoiaCooldownSeconds );

	public float GetMimicCooldownFill01() =>
		ComputeHudCooldownFill01( MimicHudCooldownEndsAt, MimicCooldownSeconds );

	static float ComputeHudCooldownFill01( double cooldownEndsAt, float cooldownSeconds )
	{
		var now = Time.Now;
		if ( cooldownEndsAt <= 1e-6 || now >= cooldownEndsAt )
			return 1f;

		var cd = Math.Max( 0.25, cooldownSeconds );
		var start = cooldownEndsAt - cd;
		var t = cd > 1e-5f ? (float)((now - start) / cd) : 1f;
		return Math.Clamp( t, 0f, 1f );
	}

	[Rpc.Host]
	public void RequestTeleportRpc()
	{
		HostTryTeleport();
	}

	/// <summary>Host only: single consumer in <see cref="YaPawnMovement.OnFixedUpdate"/> (before CC <see cref="CharacterController.Move"/>).</summary>
	internal bool HostConsumePendingAloneDashImpulse( out Vector3 horizontalImpulse )
	{
		horizontalImpulse = default;
		if ( !_hostPendingAloneDashImpulse.HasValue )
			return false;
		horizontalImpulse = _hostPendingAloneDashImpulse.Value;
		_hostPendingAloneDashImpulse = null;
		return true;
	}

	/// <summary>Non-host local owner: consume replicated dash impulse (see <see cref="RpcNotifyOwnerDashImpulse"/>).</summary>
	internal bool TryConsumeOwnerClientDashImpulse( out Vector3 horizontalImpulse )
	{
		horizontalImpulse = default;
		if ( !_ownerClientDashImpulse.HasValue )
			return false;
		horizontalImpulse = _ownerClientDashImpulse.Value;
		_ownerClientDashImpulse = null;
		return true;
	}

	[Rpc.Owner]
	void RpcNotifyOwnerDashImpulse( Vector3 impulse )
	{
		// Listen-server host already queues <see cref="_hostPendingAloneDashImpulse"/>; avoid duplicating on self.
		if ( Networking.IsHost )
			return;

		_ownerClientDashImpulse = impulse;
		TryPlayDashCueLocal();
	}

	void HostTryTeleport()
	{
		if ( !Networking.IsHost )
			return;

		if ( !YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !roleCmp.IsValid() || roleCmp.Role != YaPlayerRole.Alone )
			return;

		if ( !YaRoundGate.MayUseAloneTeleport() )
			return;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
			return;

		var now = Time.Now;
		if ( now < _hostNextTeleportAllowed )
			return;

		var cc = Components.Get<CharacterController>();
		if ( !cc.IsValid() )
			return;

		Vector3 eyePos;
		Rotation eyeRot;
		if ( !YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out eyePos, out eyeRot ) )
		{
			eyePos = GameObject.WorldPosition + Vector3.Up * 48f;
			eyeRot = Rotation.LookAt( GameObject.WorldRotation.Forward );
		}

		var dir = eyeRot.Forward.WithZ( 0f );
		if ( dir.Length < 0.15f )
			dir = GameObject.WorldRotation.Forward.WithZ( 0f );
		dir = dir.Normal;

		var maxCheck = Math.Max( 40f, TeleportMaxRange );
		var feet = GameObject.WorldPosition + Vector3.Up * 28f;
		var tr = Scene.Trace.Ray( feet, feet + dir * maxCheck )
			.UseHitPosition( true )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var impulse = Math.Max( 120f, DashHorizontalImpulse );
		var impulseMul = 1f;
		if ( tr.Hit )
		{
			var hitDist = (tr.HitPosition - feet).Length;
			if ( hitDist < Math.Max( 12f, DashMinClearDistance ) )
				return;

			var scaled = (hitDist - Math.Max( 8f, DashWallPadding )) / maxCheck;
			impulseMul = Math.Clamp( scaled, 0.28f, 1f );
		}

		// Horizontal punch — applied next FixedUpdate on host + replicated to owner client (see YaPawnMovement).
		var finalImpulse = dir * (impulse * impulseMul);
		_hostPendingAloneDashImpulse = finalImpulse;
		RpcNotifyOwnerDashImpulse( finalImpulse );
		TryPlayDashCue( GameObject.WorldPosition );
		if ( Networking.IsHost )
			RpcBroadcastDashVfx( GameObject.WorldPosition );

		_hostNextTeleportAllowed = now + Math.Max( 0.25f, TeleportCooldownSeconds );
		DashHudCooldownEndsAt = _hostNextTeleportAllowed;
		DashHudCooldownFill01 = 0f;
		Log.Info( $"[YA] Alone dash (host) queued impulse={finalImpulse.Length:F0} mul={impulseMul:F2}." );
	}

	[Rpc.Host]
	public void RequestParanoiaRpc()
	{
		HostTryParanoia();
	}

	void HostTryParanoia()
	{
		if ( !Networking.IsHost )
			return;

		if ( !YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		if ( !HostCanUseParanoiaNow() )
			return;

		var now = Time.Now;
		if ( now < _hostNextParanoiaAllowed )
			return;

		_hostNextParanoiaAllowed = now + Math.Max( 1f, ParanoiaCooldownSeconds * GetParanoiaCooldownMul() );
		ParanoiaHudCooldownEndsAt = _hostNextParanoiaAllowed;
		ParanoiaHudCooldownFill01 = 0f;
		HostApplyParanoiaDebuffNow();
		RpcNotifyAloneParanoiaSpreadLocal();
	}

	/// <summary>Host: practice Alone bot — periodic paranoia while human plays Not Alone (no player input).</summary>
	public void HostTryPracticeAutoParanoia()
	{
		if ( !Networking.IsHost )
			return;

		if ( !HostCanUseParanoiaNow() )
			return;

		HostApplyParanoiaDebuffNow();
	}

	bool HostCanUseParanoiaNow()
	{
		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !roleCmp.IsValid() || roleCmp.Role != YaPlayerRole.Alone )
			return false;

		if ( !YaRoundGate.MayUseWeapons() )
			return false;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
			return false;

		return true;
	}

	void TryPlayDashCue( Vector3 pos )
	{
		if ( string.IsNullOrWhiteSpace( DashSoundResource ) )
			return;

		try
		{
			var h = Sound.Play( DashSoundResource, pos );
			if ( h is { IsValid: true } snd )
				snd.Volume = 0.55f;
		}
		catch
		{
			// Missing asset in dev — non-fatal.
		}
	}

	void TryPlayDashCueLocal()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		TryPlayDashCue( GameObject.WorldPosition + Vector3.Up * 40f );
	}

	[Rpc.Owner]
	void RpcNotifyAloneParanoiaSpreadLocal() => NotifyAloneParanoiaSpreadLocalPresentation();

	void NotifyAloneParanoiaSpreadLocalPresentation()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		var hud = GameObject.Components.GetInDescendantsOrSelf<YaPlayerHud>( true );
		hud?.NotifyFloatingMessageLocal( "FEAR SPREAD" );
	}

	void HostApplyParanoiaDebuffNow()
	{
		var flow = YaGameStateSystem.Instance;
		if ( flow is not null && flow.IsValid() )
			flow.HostApplyParanoiaDebuff();

		var pos = GameObject.WorldPosition + Vector3.Up * 8f;
		TryPlayParanoiaCue( pos );
		if ( Networking.IsHost )
			RpcBroadcastParanoiaVfx( pos );
		Log.Info( "[YA] Alone paranoia — hunters debuffed." );
	}

	static float GetParanoiaCooldownMul()
	{
		var mut = YaWeeklyMutatorSystem.Instance;
		if ( mut is not { IsValid: true } || mut.AloneParanoiaCooldownMul <= 0.01f )
			return 1f;
		return mut.AloneParanoiaCooldownMul;
	}

	[Rpc.Broadcast]
	void RpcBroadcastDashVfx( Vector3 pos ) => YaAloneAbilityVfx.SpawnDashBurstLocal( pos );

	[Rpc.Broadcast]
	void RpcBroadcastParanoiaVfx( Vector3 pos ) => YaAloneAbilityVfx.SpawnParanoiaPulseLocal( pos );

	[Rpc.Broadcast]
	void RpcBroadcastMimicVfx( Vector3 pos ) => YaAloneAbilityVfx.SpawnMimicShimmerLocal( pos );

	[Rpc.Host]
	public void RequestMimicRpc()
	{
		HostTryMimic();
	}

	void HostTryMimic()
	{
		if ( !Networking.IsHost )
			return;

		if ( !YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !roleCmp.IsValid() || roleCmp.Role != YaPlayerRole.Alone )
			return;

		if ( !YaRoundGate.MayUseWeapons() )
			return;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
			return;

		var now = Time.Now;
		if ( now < _hostNextMimicAllowed )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var combatId = "m4";
		var mirrorConn = default( Guid );

		if ( TryPickAliveHunterMimicSource( scene, out var pick ) )
		{
			var hb = pick.Components.Get<YaHotbarEquipment>( FindMode.EnabledInSelf );
			if ( hb.IsValid() && !string.IsNullOrWhiteSpace( hb.ObserversCombatWeaponDefinitionId ) )
				combatId = hb.ObserversCombatWeaponDefinitionId.Trim();

			mirrorConn = pick.Network.OwnerId;
		}

		HostStartMimic( now, combatId, mirrorConn );
	}

	/// <summary>Prefer a live human hunter; otherwise a practice hunter bot. Returns false for generic mimic.</summary>
	bool TryPickAliveHunterMimicSource( Scene scene, out GameObject pick )
	{
		pick = null;
		var candidates = new List<GameObject>();

		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			if ( root is null || !root.IsValid() || root == GameObject )
				continue;
			if ( YaTeamSystem.GetRole( root ) != YaPlayerRole.NotAlone )
				continue;

			var h = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( h is not { IsValid: true, IsAlive: true } || h.IsDeadState )
				continue;

			candidates.Add( root );
		}

		foreach ( var bot in scene.GetAllComponents<YaBotBrain>() )
		{
			if ( !bot.IsValid() || bot.BotRole != YaPlayerRole.NotAlone )
				continue;

			var root = bot.GameObject;
			if ( root is null || !root.IsValid() || root == GameObject )
				continue;

			var h = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( h is not { IsValid: true, IsAlive: true } || h.IsDeadState )
				continue;

			candidates.Add( root );
		}

		if ( candidates.Count == 0 )
			return false;

		pick = candidates[Random.Shared.Next( candidates.Count )];
		return true;
	}

	void HostStartMimic( double now, string combatId, Guid mirrorConnectionId )
	{
		if ( string.IsNullOrWhiteSpace( combatId ) )
			combatId = "m4";

		MimicVisualCopyConnectionId = mirrorConnectionId;
		MimicMirrorCombatId = combatId.Trim();
		MimicPresentationActive = true;
		var md = Math.Max( 1f, MimicDurationSeconds );
		MimicHudActiveDurationSeconds = md;
		MimicHudSecondsRemaining = md;
		_hostMimicEndTime = now + md;
		_hostNextMimicAllowed = now + Math.Max( 1f, MimicCooldownSeconds );
		MimicHudCooldownEndsAt = _hostNextMimicAllowed;
		MimicHudCooldownFill01 = 0f;
		if ( Networking.IsHost )
			RpcBroadcastMimicVfx( GameObject.WorldPosition );

		if ( mirrorConnectionId != default )
			Log.Info( $"[YA] Mimic active — mirror conn={mirrorConnectionId}, combat={MimicMirrorCombatId}" );
		else
			Log.Info( $"[YA] Mimic active — generic hunter, combat={MimicMirrorCombatId}" );
	}

	void TryPlayParanoiaCue( Vector3 pos )
	{
		if ( string.IsNullOrWhiteSpace( ParanoiaFootstepSoundPath ) )
			return;

		try
		{
			Sound.Play( ParanoiaFootstepSoundPath, pos );
		}
		catch
		{
			// Missing asset in dev — non-fatal.
		}
	}

	/// <summary>True while F mimic is active — hunter bots must not target or shoot this pawn as Alone.</summary>
	public static bool IsMimicActive( GameObject root )
	{
		if ( root is null || !root.IsValid() )
			return false;

		var mech = root.Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		return mech is { IsValid: true, MimicPresentationActive: true };
	}
}
