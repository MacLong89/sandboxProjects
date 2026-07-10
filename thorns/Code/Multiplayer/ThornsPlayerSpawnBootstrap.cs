namespace Sandbox;

/// <summary>
/// Player prefab / code-built hierarchy: component wiring for join and default spawn.
/// Keeps <see cref="ThornsGameManager"/> focused on lobby, persistence, and respawn policy.
/// </summary>
public static class ThornsPlayerSpawnBootstrap
{
	/// <summary>Idempotent: ensures all gameplay components exist on the pawn root (prefab or code-built).</summary>
	public static void EnsureGameplayComponents( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		EnsurePlayerCollisionTags( root );
		WarnIfPawnNotOnNetworkRoot( root );
		EnsurePawnWorldModel( root );
		EnsureDefaultWeaponWorld( root );

		ThornsPawnComponentEnsure.Ensure<ThornsHealth>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsPlayerUpgrades>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsVitals>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsInventory>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsWallet>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsPlayerMilestones>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsArmorEquipment>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsHotbarEquipment>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsCharacterProgression>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsDeathCrateInteractor>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsDebugUiBridge>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsDebugHudHost>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsCollisionDebugDriver>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsGameShell>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsHotTipDirector>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsProximityInteractionHints>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsMinimapHud>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsWaterProximityAudio>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsAtmosphericMusic>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsOpenWaterDrinkInteractor>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsHarvestInteractor>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsGuildRoster>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsWildlifeTameInteractor>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsWildlifeMountInteractor>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsRadioShopInteractor>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsBuildingController>( root );
		ThornsPawnComponentEnsure.Ensure<ThornsConsumableUseInput>( root );

		if ( ThornsInventoryDev.EnableDevRpcs )
		{
			ThornsPawnComponentEnsure.Ensure<ThornsInventoryDevControls>( root );
			ThornsPawnComponentEnsure.Ensure<ThornsArmorDevControls>( root );
		}
	}

	/// <summary>One networked root: ThornsPlayer + ThornsPawn + movement + visuals (Body, View, WeaponWorld + ThornsWeapon).</summary>
	public static GameObject BuildDefaultPlayerHierarchy( GameObject parent, string displayName )
	{
		var playerGo = new GameObject( true, $"Player - {displayName}" );
		playerGo.SetParent( parent );
		playerGo.LocalPosition = Vector3.Zero;
		playerGo.LocalRotation = Rotation.Identity;
		playerGo.LocalScale = Vector3.One;
		playerGo.Tags.Add( "player" );

		_ = playerGo.Components.Create<ThornsPlayer>();
		_ = playerGo.Components.Create<ThornsPawn>();
		_ = playerGo.Components.Create<ThornsPawnMovement>();
		_ = playerGo.Components.Create<ThornsHealth>();
		_ = playerGo.Components.Create<ThornsPlayerUpgrades>();
		_ = playerGo.Components.Create<ThornsVitals>();
		_ = playerGo.Components.Create<ThornsInventory>();
		_ = playerGo.Components.Create<ThornsWallet>();
		_ = playerGo.Components.Create<ThornsPlayerMilestones>();
		_ = playerGo.Components.Create<ThornsArmorEquipment>();
		_ = playerGo.Components.Create<ThornsHotbarEquipment>();
		_ = playerGo.Components.Create<ThornsCharacterProgression>();
		_ = playerGo.Components.Create<ThornsDeathCrateInteractor>();
		_ = playerGo.Components.Create<ThornsDebugUiBridge>();
		_ = playerGo.Components.Create<ThornsDebugHudHost>();
		_ = playerGo.Components.Create<ThornsCollisionDebugDriver>();
		_ = playerGo.Components.Create<ThornsGameShell>();
		_ = playerGo.Components.Create<ThornsMinimapHud>();
		_ = playerGo.Components.Create<ThornsHarvestInteractor>();
		_ = playerGo.Components.Create<ThornsGuildRoster>();
		_ = playerGo.Components.Create<ThornsWildlifeTameInteractor>();
		_ = playerGo.Components.Create<ThornsWildlifeMountInteractor>();
		_ = playerGo.Components.Create<ThornsRadioShopInteractor>();
		_ = playerGo.Components.Create<ThornsBuildingController>();
		_ = playerGo.Components.Create<ThornsConsumableUseInput>();

		if ( ThornsInventoryDev.EnableDevRpcs )
		{
			_ = playerGo.Components.Create<ThornsInventoryDevControls>();
			_ = playerGo.Components.Create<ThornsArmorDevControls>();
		}

		var viewGo = new GameObject( true, "View" );
		viewGo.SetParent( playerGo );
		viewGo.LocalPosition = Vector3.Zero;
		viewGo.LocalRotation = Rotation.Identity;
		viewGo.LocalScale = Vector3.One;
		_ = viewGo.Components.Create<ThornsPawnCamera>();

		ThornsCitizenRig.SetupCitizenBody( playerGo );

		var weaponWorld = new GameObject( true, ThornsWeapon.WorldVisualChildName );
		weaponWorld.SetParent( playerGo );
		weaponWorld.LocalPosition = Vector3.Zero;
		var wVis = weaponWorld.Components.Create<SkinnedModelRenderer>();
		wVis.UseAnimGraph = false;
		ThornsWeapon.ResetThirdPersonWeaponWorldVisual( weaponWorld );
		_ = weaponWorld.Components.Create<ThornsWeaponWorldVisual>();

		_ = playerGo.Components.Create<ThornsWeapon>();

		return playerGo;
	}

	public static void EnsurePawnWorldModel( GameObject root )
	{
		if ( !ThornsPawnComponentEnsure.TryGetPawnGameObject( root, out var pawnGo ) )
			return;

		foreach ( var mr in pawnGo.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( mr.IsValid() )
				return;
		}

		ThornsCitizenRig.SetupCitizenBody( pawnGo );
	}

	public static void EnsureDefaultWeaponWorld( GameObject root )
	{
		if ( !ThornsPawnComponentEnsure.TryGetPawnGameObject( root, out var playerGo ) )
			return;

		GameObject primaryWeaponWorld = default;

		void CollectAndDeduplicate( GameObject current )
		{
			foreach ( var ch in current.Children )
			{
				if ( !ch.IsValid() )
					continue;

				if ( ch.Name == ThornsWeapon.WorldVisualChildName )
				{
					if ( !primaryWeaponWorld.IsValid() )
						primaryWeaponWorld = ch;
					else
					{
						ch.Destroy();
						continue;
					}
				}

				CollectAndDeduplicate( ch );
			}
		}

		CollectAndDeduplicate( playerGo );

		if ( !primaryWeaponWorld.IsValid() )
		{
			var weaponWorld = new GameObject( true, ThornsWeapon.WorldVisualChildName );
			weaponWorld.SetParent( playerGo );
			weaponWorld.LocalPosition = Vector3.Zero;
			var wVis = weaponWorld.Components.Create<SkinnedModelRenderer>();
			wVis.UseAnimGraph = false;
			ThornsWeapon.ResetThirdPersonWeaponWorldVisual( weaponWorld );
			_ = weaponWorld.Components.Create<ThornsWeaponWorldVisual>();
		}
		else
		{
			ThornsWeapon.ResetThirdPersonWeaponWorldVisual( primaryWeaponWorld );
			if ( !primaryWeaponWorld.Components.Get<ThornsWeaponWorldVisual>( FindMode.EnabledInSelf ).IsValid() )
				_ = primaryWeaponWorld.Components.Create<ThornsWeaponWorldVisual>();
		}

		if ( !playerGo.Components.Get<ThornsWeapon>( FindMode.EnabledInSelf ).IsValid() )
			_ = playerGo.Components.Create<ThornsWeapon>();
	}

	static void EnsurePlayerCollisionTags( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		AddTagIfMissing( root, "player" );

		var pawn = root.Components.GetInDescendantsOrSelf<ThornsPawn>( true );
		if ( pawn.IsValid() )
			AddTagIfMissing( pawn.GameObject, "player" );
	}

	static void AddTagIfMissing( GameObject go, string tag )
	{
		if ( !go.IsValid() )
			return;

		foreach ( var t in go.Tags )
		{
			if ( t == tag )
				return;
		}

		go.Tags.Add( tag );
	}

	static void WarnIfPawnNotOnNetworkRoot( GameObject root )
	{
		var session = root.Components.GetInDescendantsOrSelf<ThornsPlayer>( true );
		var pawn = root.Components.GetInDescendantsOrSelf<ThornsPawn>( true );
		if ( !session.IsValid() || !pawn.IsValid() )
			return;

		if ( session.GameObject != pawn.GameObject )
		{
			Log.Warning( "[Thorns] ThornsPawn is not on the same GameObject as ThornsPlayer (network root). Remote clients may not see movement — flatten the prefab so session + pawn + movement share one networked root." );
		}
	}

	// Prefab compatibility — forward to ThornsPawnComponentEnsure.Ensure<T>.
	public static void EnsureThornsHealth( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsHealth>( root );
	public static void EnsureThornsPlayerUpgrades( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsPlayerUpgrades>( root );
	public static void EnsureThornsVitals( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsVitals>( root );
	public static void EnsureThornsInventory( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsInventory>( root );
	public static void EnsureThornsWallet( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsWallet>( root );
	public static void EnsureThornsRadioShopInteractor( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsRadioShopInteractor>( root );
	public static void EnsureThornsPlayerMilestones( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsPlayerMilestones>( root );
	public static void EnsureThornsHotbarEquipment( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsHotbarEquipment>( root );
	public static void EnsureThornsArmorEquipment( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsArmorEquipment>( root );
	public static void EnsureThornsCharacterProgression( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsCharacterProgression>( root );
	public static void EnsureThornsDeathCrateInteractor( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsDeathCrateInteractor>( root );
	public static void EnsureThornsDebugUiBridge( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsDebugUiBridge>( root );
	public static void EnsureThornsDebugHudHost( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsDebugHudHost>( root );
	public static void EnsureThornsCollisionDebugDriver( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsCollisionDebugDriver>( root );
	public static void EnsureThornsGameShell( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsGameShell>( root );
	public static void EnsureThornsHotTipDirector( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsHotTipDirector>( root );
	public static void EnsureThornsProximityInteractionHints( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsProximityInteractionHints>( root );
	public static void EnsureThornsMinimapHud( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsMinimapHud>( root );
	public static void EnsureThornsWaterProximityAudio( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsWaterProximityAudio>( root );
	public static void EnsureThornsAtmosphericMusic( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsAtmosphericMusic>( root );
	public static void EnsureThornsOpenWaterDrinkInteractor( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsOpenWaterDrinkInteractor>( root );
	public static void EnsureThornsHarvestInteractor( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsHarvestInteractor>( root );
	public static void EnsureThornsGuildRoster( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsGuildRoster>( root );
	public static void EnsureThornsWildlifeMountInteractor( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsWildlifeMountInteractor>( root );
	public static void EnsureThornsWildlifeTameInteractor( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsWildlifeTameInteractor>( root );
	public static void EnsureThornsBuildingController( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsBuildingController>( root );
	public static void EnsureThornsConsumableUseInput( GameObject root ) => ThornsPawnComponentEnsure.Ensure<ThornsConsumableUseInput>( root );

	public static void EnsureThornsArmorDevControls( GameObject root )
	{
		if ( !ThornsInventoryDev.EnableDevRpcs )
			return;

		ThornsPawnComponentEnsure.Ensure<ThornsArmorDevControls>( root );
	}
}
