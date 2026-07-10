namespace Sandbox;

/// <summary>
/// Server-authoritative survival vitals (THORNS_EVERYTHING_DOCUMENT §vitals / §progression).
/// Client sends movement intent only; host drains/regenerates stats on fixed cadence (not every render frame).
/// Hooks for future poison, cold, comfort, Thorn bloom (modifiers kept isolated below).
/// </summary>
[Title( "Thorns — Vitals" )]
[Category( "Thorns" )]
[Icon( "monitor_heart" )]
[Order( 45 )]
public sealed class ThornsVitals : Component
{
	const float VitalsSimTickSeconds = 1f;

	/// <summary>Baseline drain to 0 from full (~30 min).</summary>
	const float HungerDrainPerSecond = 100f / (30f * 60f);

	/// <summary>Baseline drain to 0 from full (~15 min).</summary>
	const float ThirstDrainPerSecond = 100f / (15f * 60f);

	/// <summary>Standing sprint vs walk — used by <see cref="GetMoveSpeedMultiplier"/>; keep aligned with <see cref="ThornsWildlifeVsPlayerBalance.HumanNominalSprintSpeed"/>.</summary>
	/// <summary>Matches terraingen <c>SprintSpeedMultiplier</c> (1.75× walk).</summary>
	public const float SprintSpeedMultiplier = ThornsTerraingenParity.PlayerSprintSpeedMultiplier;

	const float CrouchSpeedMultiplier = 0.5f;

	const float StaminaDrainPerSecondSprinting = 18f;
	const float StaminaRegenPerSecond = 14f;

	/// <summary>Inspector/designer baselines captured before <see cref="ThornsPlayerUpgrades"/> bonuses apply.</summary>
	float _designMaxHunger;
	float _designMaxThirst;
	float _designMaxStamina;

	const float PassiveRegenHpPerSecond = 0.35f;

	const float StarvationDamageOneStatZeroPerTick = 1f;
	const float StarvationDamageBothStatsZeroPerTick = 2f;

	/// <summary>Prefab / network deserialization can overwrite defaults with 0 — treat as invalid.</summary>
	const float DefaultSurvivalCap = 100f;

	// Caps must replicate; HUD + bootstrap need non-zero max on owner and joiners.
	[Property, Sync( SyncFlags.FromHost )] public float MaxHunger { get; set; } = 100f;
	[Property, Sync( SyncFlags.FromHost )] public float MaxThirst { get; set; } = 100f;
	[Property, Sync( SyncFlags.FromHost )] public float MaxStamina { get; set; } = 100f;

	[Sync( SyncFlags.FromHost )] public float Hunger { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float Thirst { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float Stamina { get; set; } = 100f;

	[Sync( SyncFlags.FromHost )] public int TotalXp { get; set; }

	[Sync( SyncFlags.FromHost )] public int CharacterLevel { get; set; } = 1;

	[Sync( SyncFlags.FromHost )] public bool ServerSprinting { get; set; }

	[Sync( SyncFlags.FromHost )] public bool ServerCrouching { get; set; }

	[Sync( SyncFlags.FromHost )] public float PoisonLevel { get; set; }

	const float PoisonDamagePerTickScale = 0.07f;
	const float PoisonDecayPerSecond = 3.5f;

	float _vitalsSimAccumulator;

	/// <summary>Owner: last level from <see cref="RpcNotifyOwnerVitalsSnapshot"/> — used for level-up stinger only (not Sync timing).</summary>
	int _lastOwnerVitalsRpcLevel = -1;

	/// <summary>
	/// During pawn NetworkSpawn, <see cref="Networking.IsHost"/> can still be false in <see cref="OnStart"/> —
	/// synced vitals stay at deserialization defaults (0) and the first drain tick applies starvation damage.
	/// Bootstrap again from the first host <see cref="OnFixedUpdate"/> before simulating.
	/// </summary>
	bool _hostSurvivalBootstrapped;

	static float SanitizeDesignCap( float deserialized ) =>
		deserialized > 0.01f ? deserialized : DefaultSurvivalCap;

	protected override void OnStart()
	{
		_designMaxHunger = SanitizeDesignCap( MaxHunger );
		_designMaxThirst = SanitizeDesignCap( MaxThirst );
		_designMaxStamina = SanitizeDesignCap( MaxStamina );

		TryHostBootstrapSurvivalFromAuthority();
	}

	/// <summary>Host-only: fill hunger/thirst/stamina caps once authority is known.</summary>
	void TryHostBootstrapSurvivalFromAuthority()
	{
		if ( !Networking.IsHost || _hostSurvivalBootstrapped )
			return;

		if ( ThornsWorldPersistence.Instance is { } wp && wp.HostSuppressVitalsBootstrapForPawn( GameObject ) )
			return;

		_hostSurvivalBootstrapped = true;

		HostRefreshSurvivalCapsFromUpgrades();
		Hunger = MaxHunger;
		Thirst = MaxThirst;
		Stamina = MaxStamina;
		UpdateDerivedLevelFromXpHost();
		SyncProgressionStubMirror();
		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
	}

	/// <summary>Host-only: applied immediately after <see cref="GameObject.NetworkSpawn(Connection)"/> when loading <see cref="ThornsWorldPersistence"/> player blob.</summary>
	public void HostApplyPersistedSurvivalState(
		float hunger,
		float thirst,
		float stamina,
		float poisonLevel,
		int totalXp,
		bool sprinting,
		bool crouching )
	{
		if ( !Networking.IsHost )
			return;

		TotalXp = Math.Max( 0, totalXp );
		UpdateDerivedLevelFromXpHost();

		HostRefreshSurvivalCapsFromUpgrades();

		Hunger = Math.Clamp( hunger, 0f, MaxHunger );
		Thirst = Math.Clamp( thirst, 0f, MaxThirst );
		Stamina = Math.Clamp( stamina, 0f, MaxStamina );
		PoisonLevel = Math.Clamp( poisonLevel, 0f, 100f );

		ServerSprinting = sprinting;
		ServerCrouching = crouching;

		_hostSurvivalBootstrapped = true;

		SyncProgressionStubMirror();
		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
		Log.Info(
			$"[Thorns] Vitals restored from persistence pawn='{GameObject.Name}' hunger={Hunger:F1} thirst={Thirst:F1} xp={TotalXp}" );
	}

	/// <summary>Host-only: recompute survival caps from <see cref="ThornsPlayerUpgrades"/> and clamp currents.</summary>
	public void HostRefreshSurvivalCapsFromUpgrades()
	{
		if ( !Networking.IsHost )
			return;

		var ups = Components.Get<ThornsPlayerUpgrades>();

		MaxHunger = SanitizeDesignCap( _designMaxHunger );
		MaxThirst = SanitizeDesignCap( _designMaxThirst );
		MaxStamina = SanitizeDesignCap( _designMaxStamina );

		if ( ups.IsValid() )
			MaxStamina += ups.EnduranceRank * ThornsUpgradeBalance.EnduranceStaminaMaxBonusPerRank;

		Hunger = Math.Min( Hunger, MaxHunger );
		Thirst = Math.Min( Thirst, MaxThirst );
		Stamina = Math.Min( Stamina, MaxStamina );
	}

	protected override void OnFixedUpdate()
	{
		var hp = Components.Get<ThornsHealth>();
		var alive = !hp.IsValid() || hp.IsAlive && !hp.IsDeadState;

		// Only the owning client may send movement intent (listen server: host must not RPC on remote pawns).
		var localConn = Connection.Local;
		if ( localConn is not null && GameObject.Network.OwnerId == localConn.Id )
		{
			var sprintHeld = IsSprintInputHeld();
			var crouchHeld = IsCrouchInputHeld();
			var analogLen = Input.AnalogMove.WithZ( 0f ).Length;
			SubmitMovementIntent( sprintHeld, crouchHeld, analogLen );
		}

		if ( Networking.IsHost )
			HostFixedSimulate( hp, alive );
	}

	/// <summary>Movement multiplier using replicated server flags (all simulating peers).</summary>
	public float GetMoveSpeedMultiplier()
	{
		var mountIx = Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
			return 1f;

		var m = 1f;
		if ( ServerCrouching )
			m *= CrouchSpeedMultiplier;
		if ( ServerSprinting )
			m *= SprintSpeedMultiplier;
		return m;
	}

	public float GetCrouchHeightMultiplier() => ServerCrouching ? 0.72f : 1f;

	/// <summary>
	/// Future: poison stack, cold stress, campfire/core comfort — multiply hunger/thirst drain.
	/// </summary>
	float HostGetHungerDrainMultiplier()
	{
		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( !ups.IsValid() || ups.IronGutRank <= 0 )
			return 1f;
		return Math.Max(
			0.38f,
			1f - ups.IronGutRank * ThornsUpgradeBalance.IronGutHungerDrainReductionPerRank );
	}

	float HostGetThirstDrainMultiplier()
	{
		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( !ups.IsValid() || ups.HydrationRank <= 0 )
			return 1f;
		return Math.Max(
			0.38f,
			1f - ups.HydrationRank * ThornsUpgradeBalance.HydrationThirstDrainReductionPerRank );
	}

	float HostGetStaminaStressMultiplier()
	{
		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( !ups.IsValid() || ups.EnduranceRank <= 0 )
			return 1f;
		return Math.Max(
			0.55f,
			1f - ups.EnduranceRank * ThornsUpgradeBalance.EnduranceStaminaDrainReductionPerRank );
	}

	/// <summary>Host-only: consumables / environmental (future advanced poison rules replace magnitude math).</summary>
	public void HostAddPoison( float amount, string source )
	{
		if ( !Networking.IsHost || amount <= 0f )
			return;

		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( ups.IsValid() && ups.StrongStomachRank > 0 )
			amount *= Math.Max(
				0.12f,
				1f - ups.StrongStomachRank * ThornsUpgradeBalance.StrongStomachPoisonTakenReductionPerRank );

		var before = PoisonLevel;
		PoisonLevel = Math.Min( 100f, PoisonLevel + amount );
		Log.Info( $"[Thorns] Poison added source={source} +{amount:F1} (was {before:F1} → now {PoisonLevel:F1}) pawn='{GameObject.Name}'" );
	}

	void HostTickPoison( ThornsHealth hp )
	{
		if ( PoisonLevel <= 0f )
			return;

		var tick = VitalsSimTickSeconds;
		var dmg = PoisonLevel * PoisonDamagePerTickScale * tick;
		if ( hp.IsValid() && hp.IsAlive && dmg > 0f )
			hp.HostApplyEnvironmentalDamage( dmg, "poison" );

		PoisonLevel = Math.Max( 0f, PoisonLevel - PoisonDecayPerSecond * tick );
	}

	public void HostRestoreHunger( float amount, string source )
	{
		if ( !Networking.IsHost || amount <= 0f )
			return;

		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( ups.IsValid() && ups.IronGutRank > 0 )
			amount *= 1f + ups.IronGutRank * ThornsUpgradeBalance.IronGutFoodRestoreBonusPerRank;

		Hunger = Math.Min( MaxHunger, Hunger + amount );
		Log.Info( $"[Thorns] Effect applied hunger +{amount:F1} (now {Hunger:F1}) source={source}" );
	}

	public void HostRestoreThirst( float amount, string source )
	{
		if ( !Networking.IsHost || amount <= 0f )
			return;

		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( ups.IsValid() && ups.HydrationRank > 0 )
			amount *= 1f + ups.HydrationRank * ThornsUpgradeBalance.HydrationLiquidRestoreBonusPerRank;

		Thirst = Math.Min( MaxThirst, Thirst + amount );
		if ( source != "open_water" )
			Log.Info( $"[Thorns] Effect applied thirst +{amount:F1} (now {Thirst:F1}) source={source}" );
	}

	double _lastHostOpenWaterSipTime = -1e9;

	/// <summary>Owner client: periodic sips while holding Use in open water — host validates position and rate-limits.</summary>
	[Rpc.Host]
	public void RpcRequestSipOpenWater( float requestedAmount )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
			return;

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
			return;

		if ( !ThornsTerrainSystem.IsPawnInOpenWaterDrinkZone( GameObject.Scene, GameObject, out _ ) )
			return;

		var now = Time.Now;
		if ( now < _lastHostOpenWaterSipTime + 0.11 )
			return;

		_lastHostOpenWaterSipTime = now;

		var add = Math.Clamp( requestedAmount, 0f, 36f );
		if ( add < 0.25f )
			return;

		if ( Thirst >= MaxThirst - 0.02f )
			return;

		HostRestoreThirst( add, "open_water" );
		GameObject.Components.Get<ThornsPlayerMilestones>()?.HostRecordEvent( ThornsMilestoneEventTokens.DrinkOpenWater );
		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
	}

	// ---------- XP (document: cumulative to enter level L is 50 * L * (L-1)) ----------

	public static int CumulativeXpToEnterLevel( int level )
	{
		if ( level <= 1 )
			return 0;
		return 50 * level * (level - 1);
	}

	public static int ComputeLevelFromTotalXp( int totalXp )
	{
		var level = 1;
		while ( CumulativeXpToEnterLevel( level + 1 ) <= totalXp )
			level++;
		return level;
	}

	/// <summary>Host-only: grants XP (combat, milestones, etc.). Clients cannot gain XP through RPC.</summary>
	public void AddXp( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 )
			return;

		UpdateDerivedLevelFromXpHost();
		var levelBefore = CharacterLevel;
		TotalXp += amount;
		UpdateDerivedLevelFromXpHost();
		var levelsGained = CharacterLevel - levelBefore;
		if ( levelsGained > 0 )
		{
			var ups = Components.Get<ThornsPlayerUpgrades>();
			if ( ups.IsValid() )
				ups.HostGrantUpgradePointsForLevelsGained( levelsGained );
		}

		Log.Info( $"[Thorns] XP gained +{amount} → total={TotalXp}, level={CharacterLevel} pawn='{GameObject.Name}'" );
		SyncProgressionStubMirror();
		Log.Info( $"[Thorns] Vitals snapshot sent (host→owner, XP) pawn='{GameObject.Name}'" );
		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
	}

	void HostStripIntraLevelXpOnDeathInternal()
	{
		var lvl = ComputeLevelFromTotalXp( TotalXp );
		TotalXp = CumulativeXpToEnterLevel( lvl );
		UpdateDerivedLevelFromXpHost();
		Log.Info( $"[Thorns] Death XP rule: intra-level progress stripped → totalXp={TotalXp}, level={CharacterLevel}" );
	}

	void UpdateDerivedLevelFromXpHost()
	{
		CharacterLevel = ComputeLevelFromTotalXp( TotalXp );
	}

	void SyncProgressionStubMirror()
	{
		var prog = Components.Get<ThornsCharacterProgression>();
		if ( prog.IsValid() )
			prog.CharacterLevel = CharacterLevel;
	}

	// ---------- Host simulation ----------

	void HostFixedSimulate( ThornsHealth hp, bool alive )
	{
		if ( !Networking.IsHost )
			return;

		TryHostBootstrapSurvivalFromAuthority();

		if ( !alive )
			return;

		_vitalsSimAccumulator += Time.Delta;
		while ( _vitalsSimAccumulator >= VitalsSimTickSeconds )
		{
			_vitalsSimAccumulator -= VitalsSimTickSeconds;
			HostSimTickOneSecond( hp );
		}

		HostUpdateStaminaMidFrame( hp, alive );
	}

	void HostSimTickOneSecond( ThornsHealth hp )
	{
		var poisonDrainMul = 1f;
		Hunger = Math.Max( 0f, Hunger - HungerDrainPerSecond * VitalsSimTickSeconds * HostGetHungerDrainMultiplier() * poisonDrainMul );
		Thirst = Math.Max( 0f, Thirst - ThirstDrainPerSecond * VitalsSimTickSeconds * HostGetThirstDrainMultiplier() * poisonDrainMul );

		HostTickPoison( hp );

		var allowRegen = Hunger > 0f && Thirst > 0f;
		if ( allowRegen && hp.IsValid() && hp.IsAlive && hp.CurrentHealth < hp.MaxHealth )
			hp.HostApplyPassiveRegen( PassiveRegenHpPerSecond * VitalsSimTickSeconds );

		if ( Hunger <= 0f || Thirst <= 0f )
		{
			var dmg = (Hunger <= 0f && Thirst <= 0f) ? StarvationDamageBothStatsZeroPerTick : StarvationDamageOneStatZeroPerTick;
			if ( hp.IsValid() && hp.IsAlive )
				hp.HostApplyEnvironmentalDamage( dmg, "starvation_or_thirst" );
		}

		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
	}

	void HostUpdateStaminaMidFrame( ThornsHealth hp, bool alive )
	{
		if ( !Networking.IsHost || !alive )
			return;

		var mountIx = Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
		{
			ServerSprinting = false;
			ServerCrouching = false;
			return;
		}

		var sprintDesired = _hostSprintIntent;
		var crouchDesired = _hostCrouchIntent;
		if ( crouchDesired )
			sprintDesired = false;

		var analog = Math.Clamp( _hostAnalogMove, 0f, 1f );
		var moving = analog > 0.08f;
		var canSprint = sprintDesired && moving && Stamina > 0f && !crouchDesired;

		var sprintEffective = canSprint;

		if ( sprintEffective && !(hp.IsValid() && hp.IsAlive) )
			sprintEffective = false;

		if ( sprintEffective && !_wasSprintingEffective )
			GameObject.Components.Get<ThornsPlayerMilestones>()?.HostRecordEvent( ThornsMilestoneEventTokens.SprintShift );

		ServerSprinting = sprintEffective;
		ServerCrouching = crouchDesired;

		if ( sprintEffective )
		{
			var drain = StaminaDrainPerSecondSprinting * Time.Delta * HostGetStaminaStressMultiplier();
			Stamina = Math.Max( 0f, Stamina - drain );
		}
		else
		{
			var regen = StaminaRegenPerSecond * Time.Delta;
			Stamina = Math.Min( MaxStamina, Stamina + regen );
		}

		if ( Stamina <= 0f )
			ServerSprinting = false;

		_wasSprintingEffective = ServerSprinting;
	}

	bool _hostSprintIntent;
	bool _hostCrouchIntent;
	bool _wasSprintingEffective;
	float _hostAnalogMove;

	static bool IsSprintInputHeld()
	{
		if ( Input.Down( "Run" ) || Input.Down( "run" ) )
			return true;
		if ( Input.Keyboard.Down( "Shift" ) )
			return true;
		return false;
	}

	static bool IsCrouchInputHeld()
	{
		if ( Input.Down( "Duck" ) || Input.Down( "duck" ) )
			return true;
		if ( Input.Keyboard.Down( "Ctrl" ) )
			return true;
		return false;
	}

	[Rpc.Host]
	public void SubmitMovementIntent( bool sprintDesired, bool crouchDesired, float analogMoveLength )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] SubmitMovementIntent rejected: caller does not own pawn" );
			return;
		}

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && (hp.IsDeadState || !hp.IsAlive) )
			return;

		_hostSprintIntent = sprintDesired;
		_hostCrouchIntent = crouchDesired;
		_hostAnalogMove = analogMoveLength;
	}

	bool ValidateRpcCallerOwnsPawn() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	/// <summary>Host-only: respawn restores baseline survival stats.</summary>
	public void HostResetAfterRespawn()
	{
		if ( !Networking.IsHost )
			return;

		HostRefreshSurvivalCapsFromUpgrades();
		Hunger = MaxHunger;
		Thirst = MaxThirst;
		Stamina = MaxStamina;
		PoisonLevel = 0f;
		ServerSprinting = false;
		Log.Info( $"[Thorns] Vitals reset after respawn pawn='{GameObject.Name}'" );
		Log.Info( $"[Thorns] Vitals snapshot sent (host→owner, respawn) pawn='{GameObject.Name}'" );
		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
	}

	/// <summary>Called from <see cref="ThornsCharacterProgression.HostApplyDeathXpPlaceholderRule"/> on death.</summary>
	public void HostApplyDeathXpPlaceholderRule()
	{
		if ( !Networking.IsHost )
			return;

		HostStripIntraLevelXpOnDeathInternal();
		SyncProgressionStubMirror();
		Log.Info( $"[Thorns] Vitals snapshot sent (host→owner, death XP strip) pawn='{GameObject.Name}'" );
		RpcNotifyOwnerVitalsSnapshot( Hunger, Thirst, Stamina, TotalXp, CharacterLevel, ServerSprinting, ServerCrouching );
	}

	[Rpc.Owner]
	void RpcNotifyOwnerVitalsSnapshot( float hunger, float thirst, float stamina, int totalXp, int level, bool sprinting, bool crouching )
	{
		// Intentionally no Log — periodic sync was spamming the console; host logs combat-window vitals in HostSimTickOneSecond when applicable.
		if ( _lastOwnerVitalsRpcLevel >= 0 && level > _lastOwnerVitalsRpcLevel && Game.IsPlaying )
		{
			ThornsGameplaySfx.PlayAtPawnEar( GameObject, ThornsGameplaySfx.LevelUp );
			var gained = level - _lastOwnerVitalsRpcLevel;
			var shell = Components.Get<ThornsGameShell>();
			if ( shell.IsValid() )
			{
				var msg = gained == 1
					? $"Level up!\nYou reached level {level}."
					: $"Level up! +{gained} levels\nNow level {level}.";
				shell.PushGameplayToast( msg, 4.2f, ThornsGameplayToastKind.LevelUp );
				shell.NotifyLocalLevelUpFlash();
			}
		}

		_lastOwnerVitalsRpcLevel = level;
	}
}
