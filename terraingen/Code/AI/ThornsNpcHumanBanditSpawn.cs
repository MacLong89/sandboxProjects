namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Shared M4 citizen-bandit construction for wanderers and future defenders.</summary>
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
		public float HitChance { get; init; } = ThornsBanditCombat.HumanNpcPlayerHitChanceDefault;
		public float ExtraSpreadHalfAngleDegrees { get; init; } = 0.55f;
	}

	public static Config Wanderer() =>
		new()
		{
			ObjectName = "ThornsWandererBandit",
			Tag = "thorns_wanderer_bandit",
			BanditType = ThornsBanditType.Scavenger,
			Archetype = ThornsBanditArchetypeConfig.Scavenger(),
			UseLeashAnchor = true,
			AnchorWanderGoalsToCurrentPosition = false,
			LeashRadius = 520f,
			WanderRadius = 380f,
			AggroRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
			LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f,
			AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld
		};

	public static Config AirdropDefender( Vector3 anchor ) =>
		new()
		{
			ObjectName = "AirdropDefender",
			Tag = "airdrop_defender",
			BanditType = ThornsBanditType.AirdropDefender,
			Archetype = ThornsBanditArchetypeConfig.AirdropDefender(),
			UseLeashAnchor = true,
			LeashAnchorWorld = anchor,
			LeashRadius = 520f,
			WanderRadius = 280f,
			AggroRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
			LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f,
			AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld
		};

	public static GameObject HostSpawnM4Citizen(
		Scene scene,
		Vector3 worldPos,
		Random rnd,
		in Config cfg,
		int groupId = 0,
		int spawnIndex = 0,
		int spawnCount = 1 )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return null;

		if ( scene is null || !scene.IsValid() )
			return null;

		if ( !ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( scene, worldPos, cfg.WanderRadius, out var spawnPos, out _ ) )
			return null;

		ThornsBanditDirector.EnsureForScene( scene );

		var root = scene.CreateObject( true );
		root.Name = cfg.ObjectName;
		ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref spawnPos );
		root.WorldPosition = spawnPos;
		root.WorldRotation = Rotation.FromYaw( rnd.NextSingle() * 360f );
		root.Tags.Add( cfg.Tag );
		root.Tags.Add( "bandit" );

		var viewGo = scene.CreateObject( true );
		viewGo.Name = "View";
		viewGo.SetParent( root );
		viewGo.LocalPosition = Vector3.Zero;
		viewGo.LocalRotation = Rotation.Identity;

		var hp = root.Components.Create<ThornsBanditHealth>();
		hp.MaxHealth = 100f;
		hp.CurrentHealth = 100f;
		ThornsBanditDamageReceiver.EnsureOn( root );

		root.Components.Create<ThornsBanditMotor>();

		var brain = root.Components.Create<ThornsBanditBrain>();
		brain.BanditType = cfg.BanditType;
		brain.GroupId = groupId;
		brain.UseLeashAnchor = true;
		brain.LeashAnchorWorld = cfg.UseLeashAnchor && cfg.LeashAnchorWorld != default ? cfg.LeashAnchorWorld : spawnPos;
		brain.AnchorWanderGoalsToCurrentPosition = false;
		if ( cfg.Archetype is not null )
			brain.ApplyArchetypeConfig( cfg.Archetype );

		brain.HostCompleteSpawnSetup( cfg );
		brain.HostApplySpawnPatrolStagger( spawnIndex, spawnCount );

		var combat = root.Components.Create<ThornsBanditCombat>();
		var weaponId = ThornsBanditCombat.HostRollRandomWeaponId( rnd );
		combat.HostSetLoadout( weaponId, null );
		combat.HostAssignRandomAttachments( rnd );
		if ( cfg.Archetype is not null )
			combat.ApplySkill( cfg.Archetype.Skill );

		var cc = root.Components.Get<CharacterController>();
		if ( cc.IsValid() )
		{
			cc.Height = 72f;
			cc.Radius = 20f;
			cc.StepHeight = ThornsBanditMotor.DefaultStepHeight;
			cc.GroundAngle = 86f;
		}

		ThornsCitizenRig.SetupCitizenBody( root );

		var weaponWorld = scene.CreateObject( true );
		weaponWorld.Name = ThornsBanditUtil.WorldWeaponChildName;
		weaponWorld.SetParent( root );
		weaponWorld.LocalScale = ThornsBanditUtil.WorldWeaponLocalScale;
		weaponWorld.Components.Create<ThornsNpcBanditWeaponVisual>();

		if ( Networking.IsActive
		     && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( root ) )
			Log.Warning( "[Thorns Bandits] NetworkSpawn failed — joiners may not see this bandit." );

		return root;
	}
}
