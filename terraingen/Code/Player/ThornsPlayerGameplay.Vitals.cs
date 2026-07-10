namespace Terraingen.Player;

using Sandbox;
using Sandbox.Network;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.GameData;
using Terraingen.TerrainGen;
using Terraingen.UI;

/// <summary>Survival vitals, drain, and consumable use (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	ThornsPlayerHealth _cachedHealth;
	ThornsPlayerMountController _cachedMountController;
	ThornsVitalsSnapshotDto _lastLocalVitalsUi = new();
	bool _hostRemoteSprintHeld;
	bool _wasStarving;

	/// <summary>Host-side sprint intent from owning client (listen-server local player uses input directly).</summary>
	public bool HostReportedSprintHeld => _hostRemoteSprintHeld;

	void TickVitalsRevealTimers()
	{
		if ( _healthRevealTimer > 0f )
			_healthRevealTimer -= Time.Delta;
		if ( _foodRevealTimer > 0f )
			_foodRevealTimer -= Time.Delta;
		if ( _waterRevealTimer > 0f )
			_waterRevealTimer -= Time.Delta;
		if ( _tempRevealTimer > 0f )
			_tempRevealTimer -= Time.Delta;
	}

	public void HostNotifyHungry() => _foodRevealTimer = 12f;
	public void HostNotifyThirsty() => _waterRevealTimer = 12f;

	/// <summary>Host-only: restore hunger and thirst after death respawn.</summary>
	public void HostRefillHungerAndThirstOnRespawn()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		HostApplySurvivalCaps();
		_vitals.Food = _vitals.MaxFood;
		_vitals.Water = _vitals.MaxWater;
		_starveDamageTimer = 0f;
		HostRefreshVitals( forceShowHealth: false );
	}
	public void HostNotifyTemperature() => _tempRevealTimer = 10f;

	void HostRefreshVitals( bool forceShowHealth, bool forceSync = false, bool skipOwnerPush = false )
	{
		if ( _cachedHealth is null || !_cachedHealth.IsValid )
			_cachedHealth = Components.Get<ThornsPlayerHealth>();

		var health = _cachedHealth;
		_vitals.Health = health?.CurrentHealth ?? _vitals.Health;
		_vitals.MaxHealth = health?.MaxHealth ?? _vitals.MaxHealth;
		_vitals.ShowHealth = true;
		_vitals.ShowStamina = true;
		_vitals.ShowFood = true;
		_vitals.ShowWater = true;
		_vitals.ShowTemperature = _tempRevealTimer > 0f || forceShowHealth;

		if ( IsLocalPlayer() )
		{
			if ( forceShowHealth || !ThornsPlayerVitalsNetwork.VitalsEqual( _lastLocalVitalsUi, _vitals ) )
			{
				_lastLocalVitalsUi = CloneVitals( _vitals );
				ThornsUiClientState.ApplyPartialVitals( _vitals );
			}
		}
		else if ( !skipOwnerPush && CanPushOwnerRpcs() )
			_vitalsNetwork.HostPush( this, _vitals, forceShowHealth || forceSync );
	}

	static ThornsVitalsSnapshotDto CloneVitals( ThornsVitalsSnapshotDto v ) => new()
	{
		Health = v.Health,
		MaxHealth = v.MaxHealth,
		Stamina = v.Stamina,
		MaxStamina = v.MaxStamina,
		Food = v.Food,
		MaxFood = v.MaxFood,
		Water = v.Water,
		MaxWater = v.MaxWater,
		TemperatureC = v.TemperatureC,
		ShowHealth = v.ShowHealth,
		ShowStamina = v.ShowStamina,
		ShowFood = v.ShowFood,
		ShowWater = v.ShowWater,
		ShowTemperature = v.ShowTemperature,
		HasCampfireWarmth = v.HasCampfireWarmth
	};

	[Rpc.Owner]
	public void RpcSyncVitalsToOwner( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsVitalsSnapshotDto vitals ) )
			return;

		ThornsUiClientState.ApplyPartialVitals( vitals );
	}

	const float SprintStaminaThreshold = 0.01f;
	const float SprintResumeStamina = 10f;

	bool _sprintExhausted;

	public bool CanSprint
	{
		get
		{
			if ( _cachedMountController is null || !_cachedMountController.IsValid() )
				_cachedMountController = Components.Get<ThornsPlayerMountController>();

			return _cachedMountController?.IsMounted != true
			       && !_sprintExhausted
			       && GetEffectiveStamina() > SprintStaminaThreshold;
		}
	}

	public float GetEffectiveStamina()
	{
		// Host simulates vitals — never read the UI snapshot here (causes sprint/UI feedback loops).
		if ( ThornsMultiplayer.IsHostOrOffline )
			return _vitals.Stamina;

		if ( IsLocalPlayer() )
		{
			var vitals = ThornsUiClientState.Snapshot?.Vitals;
			if ( vitals is not null )
				return vitals.Stamina;
		}

		return _vitals.Stamina;
	}

	void TickSprintStaminaGate()
	{
		if ( _vitals.Stamina <= SprintStaminaThreshold )
			_sprintExhausted = true;
		else if ( _vitals.Stamina >= SprintResumeStamina )
			_sprintExhausted = false;
	}

	void HostApplySurvivalCaps()
	{
		_vitals.MaxFood = ThornsPlayerSurvivalStats.MaxFood( _skills );
		_vitals.MaxWater = ThornsPlayerSurvivalStats.MaxWater( _skills );
		_vitals.MaxStamina = ThornsPlayerSurvivalStats.MaxStamina( _skills );
		_vitals.Food = Math.Clamp( _vitals.Food, 0f, _vitals.MaxFood );
		_vitals.Water = Math.Clamp( _vitals.Water, 0f, _vitals.MaxWater );
		_vitals.Stamina = Math.Clamp( _vitals.Stamina, 0f, _vitals.MaxStamina );

		var maxHealth = ThornsPlayerSurvivalStats.MaxHealth( _skills );
		var health = Components.Get<ThornsPlayerHealth>();
		if ( health is not null )
		{
			health.MaxHealth = maxHealth;
			health.CurrentHealth = Math.Clamp( health.CurrentHealth, 0f, maxHealth );
		}

		_vitals.MaxHealth = maxHealth;
	}

	void TickSurvival( float delta )
	{
		if ( delta <= 0f )
			return;

		_vitals.Food = Math.Max( 0f, _vitals.Food - ThornsPlayerSurvivalStats.FoodDrainPerSecond( _skills ) * delta );
		_vitals.Water = Math.Max( 0f, _vitals.Water - ThornsPlayerSurvivalStats.WaterDrainPerSecond( _skills ) * delta );

		TickSprintStaminaGate();

		if ( HostShouldDrainStamina() )
			_vitals.Stamina = Math.Max( 0f, _vitals.Stamina - ThornsPlayerSurvivalStats.StaminaDrainPerSecond( _skills ) * delta );
		else
			_vitals.Stamina = Math.Min( _vitals.MaxStamina,
				_vitals.Stamina + ThornsPlayerSurvivalStats.StaminaRegenPerSecond( _skills ) * delta );

		if ( _vitals.Food <= 0f || _vitals.Water <= 0f )
		{
			if ( !_wasStarving )
			{
				_wasStarving = true;
				ThornsWorldPersistence.RequestSignificantSave();
			}

			_starveDamageTimer += delta;
			if ( _starveDamageTimer >= 1f )
			{
				_starveDamageTimer = 0f;
				ThornsCombatDamage.HostApplyDamage( null, GameObject, new ThornsCombatDamage.DamageInfo
				{
					Amount = ThornsPlayerSurvivalStats.StarvationDamagePerSecond,
					VictimRoot = GameObject,
					DamageTypeId = "starvation",
					VictimKind = ThornsCombatDamage.VictimKind.Player,
					VictimFaction = ThornsCombatFactions.FactionKind.Player,
					AttackerFaction = ThornsCombatFactions.FactionKind.World
				} );
			}
		}
		else
		{
			if ( _wasStarving )
			{
				_wasStarving = false;
				ThornsWorldPersistence.RequestSignificantSave();
			}

			_starveDamageTimer = 0f;
		}

		if ( _vitals.Food < 35f )
			HostNotifyHungry();
		if ( _vitals.Water < 35f )
			HostNotifyThirsty();

		_vitals.HasCampfireWarmth = ThornsCampfireWarmth.IsPlayerNearCampfire( GameObject );
		TickPassiveHealthRegen( delta );
	}

	void TickPassiveHealthRegen( float delta )
	{
		if ( delta <= 0f )
			return;

		var wellFed = ThornsPlayerSurvivalStats.IsWellFedAndHydrated(
			_vitals.Food, _vitals.MaxFood, _vitals.Water, _vitals.MaxWater );

		if ( !_vitals.HasCampfireWarmth && !wellFed )
			return;

		if ( _cachedHealth is null || !_cachedHealth.IsValid )
			_cachedHealth = Components.Get<ThornsPlayerHealth>();

		if ( _cachedHealth is null || !_cachedHealth.IsValid || !_cachedHealth.IsAlive )
			return;

		if ( _cachedHealth.CurrentHealth >= _cachedHealth.MaxHealth - 0.01f )
			return;

		var regenRate = ThornsCampfireWarmth.ResolveHealthRegenPerSecond(
			_vitals.HasCampfireWarmth, wellFed, _skills );

		if ( regenRate <= 0f )
			return;

		_cachedHealth.HostHeal( regenRate * delta );
	}

	/// <summary>Host-only stamina drain: local input on listen-server, replicated intent + velocity for remote owners.</summary>
	bool HostShouldDrainStamina()
	{
		if ( !CanSprint )
			return false;

		if ( IsLocalPlayer() )
			return Input.Down( "Run" );

		if ( !_hostRemoteSprintHeld )
			return false;

		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		var planarSpeed = controller.Velocity.WithZ( 0f ).Length;
		var movingThreshold = MathF.Max( 24f, controller.WalkSpeed * 1.04f );
		return planarSpeed >= movingThreshold;
	}

	public void RequestConsumeSurvivalItem()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcConsumeSurvivalItem();
		else
			HostTryConsumeSurvivalItem();
	}

	public void RequestDrinkFromNaturalWater()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcDrinkFromNaturalWater();
		else
			HostDrinkFromNaturalWater();
	}

	[Rpc.Host]
	void RpcDrinkFromNaturalWater()
	{
		if ( !ValidateCaller() )
			return;

		HostDrinkFromNaturalWater();
	}

	public void HostDrinkFromNaturalWater()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || !ThornsNaturalWaterDrink.CanDrinkAt( Scene, GameObject ) )
			return;

		_vitals.Water = _vitals.MaxWater;
		HostNotifyMilestoneEvent( "drink" );
		HostRefreshVitals( forceShowHealth: false, forceSync: true );
		HostPersistPlayerState();
	}

	[Rpc.Host]
	void RpcReportSprintHeld( bool sprintHeld )
	{
		if ( !ValidateCaller() )
			return;

		_hostRemoteSprintHeld = sprintHeld;
	}

	[Rpc.Host]
	void RpcConsumeSurvivalItem()
	{
		if ( !ValidateCaller() )
			return;

		HostTryConsumeSurvivalItem();
	}

	public void HostTryConsumeSurvivalItem()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		foreach ( var kind in new[] { ThornsContainerKind.Hotbar, ThornsContainerKind.Inventory } )
		{
			var max = kind == ThornsContainerKind.Hotbar
				? ThornsInventoryContainer.HotbarSlotCount
				: ThornsInventoryContainer.InventorySlotCount;

			for ( var i = 0; i < max; i++ )
			{
				if ( HostTryConsumeFromSlot( kind, i ) )
					return;
			}
		}
	}

	static readonly string[] TameFeedItemIds =
	{
		"apple", "field_rations", "canned_stew", "raw_meat", "food"
	};

	static bool IsSurvivalConsumable( string itemId ) => ThornsSurvivalConsumables.IsConsumable( itemId );

	static bool IsTameFeedItem( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		foreach ( var id in TameFeedItemIds )
		{
			if ( string.Equals( id, itemId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public bool HostTryConsumeOneTameFood( out string consumedItemId )
	{
		consumedItemId = null;
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return false;

		foreach ( var kind in new[] { ThornsContainerKind.Hotbar, ThornsContainerKind.Inventory } )
		{
			var max = kind == ThornsContainerKind.Hotbar
				? ThornsInventoryContainer.HotbarSlotCount
				: ThornsInventoryContainer.InventorySlotCount;

			for ( var i = 0; i < max; i++ )
			{
				var stack = _inventory.GetSlot( kind, i );
				if ( stack.IsEmpty || !IsTameFeedItem( stack.ItemId ) )
					continue;

				consumedItemId = stack.ItemId;
				stack.Count--;
				if ( stack.Count <= 0 )
					stack = ThornsItemStack.EmptyStack;

				_inventory.SetSlot( kind, i, stack );
				ThornsMilestoneTracker.OnInventoryChanged( this );
				PushInventoryToOwner();
				return true;
			}
		}

		return false;
	}

	bool HostApplyConsumable( string itemId )
	{
		switch ( itemId.ToLowerInvariant() )
		{
			case "food" or "apple" or "field_rations" or "canned_stew" or "raw_meat":
				_vitals.Food = Math.Min( _vitals.MaxFood,
					_vitals.Food + ThornsPlayerSurvivalStats.RestoreFood( _skills, 32f ) );
				HostNotifyMilestoneEvent( "eat_food" );
				return true;
			case "water" or "water_bottle" or "clean_water" or "electrolytes":
				_vitals.Water = Math.Min( _vitals.MaxWater,
					_vitals.Water + ThornsPlayerSurvivalStats.RestoreWater( _skills, 32f ) );
				HostNotifyMilestoneEvent( "drink" );
				return true;
			case "bandage":
				Components.Get<ThornsPlayerHealth>()?.HostHeal( 18f );
				return true;
			case "medkit":
				Components.Get<ThornsPlayerHealth>()?.HostHeal( 45f );
				return true;
			case "morphine_pen":
				Components.Get<ThornsPlayerHealth>()?.HostHeal( 28f );
				return true;
			default:
				return false;
		}
	}
}
