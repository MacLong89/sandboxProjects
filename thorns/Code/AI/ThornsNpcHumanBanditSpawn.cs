namespace Sandbox;

/// <summary>Shared M4 citizen-bandit construction for airdrop guards, city defenders, and wanderers.</summary>
public static class ThornsNpcHumanBanditSpawn
{
	public sealed class Config
	{
		public string ObjectName { get; init; } = "ThornsHumanBandit";
		public string Tag { get; init; } = "thorns_human_bandit";
		public ThornsBanditType BanditType { get; init; } = ThornsBanditType.Scavenger;
		public ThornsBanditArchetypeConfig Archetype { get; init; }
		public bool UseLeashAnchor { get; init; }
		public Vector3 LeashAnchorWorld { get; init; }
		public float LeashRadius { get; init; } = 400f;
		public float WanderRadius { get; init; } = 420f;
		public bool AnchorWanderGoalsToCurrentPosition { get; init; }
		public float AggroRadius { get; init; } = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld;
		public float LoseRadius { get; init; } = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f;
		public float AttackRange { get; init; } = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld;
		public bool AwardWildlifeKillXp { get; init; } = true;
		public bool SpawnGuardLootCrateOnDeath { get; init; } = true;
		/// <summary>Per-shot hitscan damage chance (gunshot still plays on misses).</summary>
		public float HitChance { get; init; } = ThornsBanditCombat.HumanNpcPlayerHitChanceDefault;
		public float ExtraSpreadHalfAngleDegrees { get; init; } = 0.55f;
	}

	public static Config AirdropGuard( Vector3 leashAnchorWorld ) =>
		new()
		{
			ObjectName = "ThornsAirdropGuard",
			Tag = "thorns_airdrop_guard",
			BanditType = ThornsBanditType.AirdropDefender,
			Archetype = ThornsBanditArchetypeConfig.AirdropDefender(),
			UseLeashAnchor = true,
			LeashAnchorWorld = leashAnchorWorld,
			LeashRadius = 400f,
			WanderRadius = 420f,
			AggroRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
			LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f,
			AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld
		};

	public static Config CityDefender( Vector3 buildingAnchorWorld, float leashRadius, float wanderRadius ) =>
		new()
		{
			ObjectName = "ThornsCityDefender",
			Tag = "thorns_city_defender",
			BanditType = ThornsBanditType.CityDefender,
			Archetype = ThornsBanditArchetypeConfig.CityDefender(),
			UseLeashAnchor = true,
			LeashAnchorWorld = buildingAnchorWorld,
			LeashRadius = leashRadius,
			WanderRadius = wanderRadius,
			AggroRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
			LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f,
			AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld
		};

	public static Config Wanderer() =>
		new()
		{
			ObjectName = "ThornsWandererBandit",
			Tag = "thorns_wanderer_bandit",
			BanditType = ThornsBanditType.Scavenger,
			Archetype = ThornsBanditArchetypeConfig.Scavenger(),
			UseLeashAnchor = false,
			AnchorWanderGoalsToCurrentPosition = true,
			WanderRadius = 2400f,
			AggroRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
			LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f,
			AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld
		};

	/// <summary>Host-only networked bandit (see <see cref="ThornsAirdropGuardSpawner"/>).</summary>
	public static void HostSpawnM4Citizen( Scene scene, Vector3 worldPos, Random rnd, in Config cfg )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( scene is null || !scene.IsValid() )
			return;

		ThornsGameManager.EnsureThornsBanditDirectorForScene( scene );

		var root = new GameObject( true, cfg.ObjectName );
		var spawnPos = worldPos;
		ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref spawnPos );
		root.WorldPosition = spawnPos;
		root.WorldRotation = Rotation.FromYaw( rnd.NextSingle() * 360f );
		root.Tags.Add( cfg.Tag );
		root.Tags.Add( ThornsTraceLayers.Creature );

		var viewGo = new GameObject( true, "View" );
		viewGo.SetParent( root );
		viewGo.LocalPosition = Vector3.Zero;
		viewGo.LocalRotation = Rotation.Identity;

		var hp = root.Components.Create<ThornsHealth>();
		hp.MaxHealth = 100f;
		hp.CurrentHealth = 100f;

		root.Components.Create<ThornsBanditMotor>();

		var brain = root.Components.Create<ThornsBanditBrain>();
		brain.BanditType = cfg.BanditType;
		brain.AwardWildlifeKillXp = cfg.AwardWildlifeKillXp;
		brain.SpawnGuardLootCrateOnDeath = cfg.SpawnGuardLootCrateOnDeath;
		brain.UseLeashAnchor = cfg.UseLeashAnchor;
		brain.LeashAnchorWorld = cfg.LeashAnchorWorld;
		brain.LeashRadius = cfg.LeashRadius;
		brain.WanderRadius = cfg.WanderRadius;
		brain.AnchorWanderGoalsToCurrentPosition = cfg.AnchorWanderGoalsToCurrentPosition;
		if ( cfg.Archetype is not null )
			brain.ApplyArchetypeConfig( cfg.Archetype );
		else
		{
			brain.AggroRadius = cfg.AggroRadius;
			brain.LoseRadius = cfg.LoseRadius;
			brain.AttackRange = cfg.AttackRange;
		}

		brain.HostCompleteSpawnSetup( cfg );

		var combat = root.Components.Create<ThornsBanditCombat>();
		combat.HitChance = cfg.HitChance;
		combat.ExtraSpreadHalfAngleDegrees = cfg.ExtraSpreadHalfAngleDegrees;

		var cc = root.Components.Get<CharacterController>();
		if ( cc.IsValid() )
		{
			cc.Height = 72f;
			cc.Radius = 20f;
			cc.StepHeight = ThornsBanditMotor.DefaultStepHeight;
			cc.GroundAngle = 86f;
		}

		ThornsCitizenRig.SetupCitizenBody( root );

		var weaponWorld = new GameObject( true, ThornsWeapon.WorldVisualChildName );
		weaponWorld.SetParent( root );
		weaponWorld.LocalScale = ThornsWeapon.WorldMeshLocalScaleWeapon;
		var smr = ThornsWeapon.GetOrCreateWorldSkinnedModelRenderer( weaponWorld );
		if ( ThornsItemRegistry.TryGet( "m4", out var m4def ) && !string.IsNullOrEmpty( m4def.WorldModelAsset ) )
		{
			smr.Model = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback( m4def.WorldModelAsset, "npc m4", out var usedFallback );
			smr.Tint = usedFallback ? new Color( 0.85f, 0.45f, 0.12f, 1f ) : Color.White;
			smr.UseAnimGraph = false;
			smr.Enabled = true;
			smr.MaterialOverride = default;
		}

		weaponWorld.Components.Create<ThornsNpcBanditWeaponVisual>();

		// Same ordering as wildlife / pawns: network-spawn only after View + Body + weapon exist, and mark the whole subtree
		// NetworkMode.Object so joiners receive SkinnedModelRenderer snapshots (spawning an empty root first leaves proxies invisible).
		if ( Networking.IsActive
		     && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( root ) )
			Log.Warning( "[Thorns] Bandit NetworkSpawn failed — joiners may not see or interact with this NPC correctly." );
	}
}
