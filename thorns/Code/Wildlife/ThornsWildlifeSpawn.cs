namespace Sandbox;

/// <summary>Host-only factory — keeps prefab composition out of spawner tick logic.</summary>
public static class ThornsWildlifeSpawn
{
	/// <summary>Tripo/large-unit FBX panthers — ~4× reduction vs raw import (tune one place).</summary>
	public const float PantherRootUniformScale = 0.25f;

	/// <summary>Panther skinned mesh, <see cref="HostEnsureWildlifeSolidHull"/> box, and <see cref="ThornsWildlifeMotor.HostApplyCapsuleScaleMul"/> vs prior tuning.</summary>
	public const float PantherVisualAndHitboxScaleMul = 1.55f;

	/// <summary>Mesh-aligned <see cref="HostEnsureWildlifeSolidHull"/> inflation vs render AABB.</summary>
	public const float PantherHullMeshExtentScale = 1.28f;

	/// <summary>Analytic hitscan capsule center lift as a fraction of <see cref="ThornsWildlifeMotor"/> height (quadruped mesh sits above feet-centered CC).</summary>
	public const float PantherHitscanCapsuleCenterLiftFraction = 0.14f;

	/// <summary>Analytic hitscan sphere radius multiplier for panthers.</summary>
	public const float PantherHitscanSphereRadiusMul = 1.18f;

	/// <summary>Wolf FBX import scale — tune if world-size is off.</summary>
	public const float WolfRootUniformScale = 1f / 3f;

	/// <summary>Elk import scale — ~⅓ world size vs raw mesh.</summary>
	public const float ElkRootUniformScale = 1f / 3f;

	/// <summary>Deer FBX import scale — tune if world-size is off vs elk.</summary>
	public const float DeerRootUniformScale = 1f / 3f;

	/// <summary>Moose FBX import scale — tune if world-size is off.</summary>
	public const float MooseRootUniformScale = 1f / 3f;

	/// <summary>Applied to every skinned species uniform scale (1 = prior default world size).</summary>
	public const float WildlifeVisualScaleMultiplier = 0.5f;

	public const float BossWildlifeHealthMul = 10f;

	public const float BossWildlifeModelAndHitboxMul = 2f;

	public static GameObject HostCreate( Scene scene, ThornsWildlifeSpeciesKind species, Vector3 worldPosition, bool bossAnimal = false )
	{
		if ( !Networking.IsHost || scene is null || !scene.IsValid() )
			return default;

		var suffix = bossAnimal ? "_BOSS" : "";
		var go = new GameObject( true, $"Wildlife_{species}_{Random.Shared.Next( 1000, 9999 )}{suffix}" );
		go.WorldPosition = worldPosition;
		go.Tags.Add( "creature" );

		go.Components.Create<ThornsHealth>();
		var id = go.Components.Create<ThornsWildlifeIdentity>();
		id.Species = species;
		id.HostApplyDefinitionNow();

		go.Components.Create<ThornsWildlifeMotor>();
		go.Components.Create<ThornsWildlifeCombat>();
		go.Components.Create<ThornsWildlifeAnimSync>();
		go.Components.Create<ThornsWildlifeBrain>();
		go.Components.Create<ThornsWildlifeVocalization>();
		go.Components.Create<ThornsWildlifeLocomotionFootsteps>();

		HostApplyCreatureVisual( go, species );

		HostEnsureWildlifeSolidHull( go, species );

		if ( bossAnimal )
			HostApplyBossWildlifePresentation( go );

		HostTryNetworkSpawnWildlife( go );

		ThornsWildlifeLog.Spawn( bossAnimal ? $"{species} (boss)" : species.ToString(), worldPosition );
		return go;
	}

	/// <summary>After visuals + solid hull — before <see cref="HostTryNetworkSpawnWildlife"/> so joiners snapshot scaled HP and mesh.</summary>
	static void HostApplyBossWildlifePresentation( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		var id = go.Components.Get<ThornsWildlifeIdentity>();
		var hp = go.Components.Get<ThornsHealth>();
		var motor = go.Components.Get<ThornsWildlifeMotor>();
		if ( !id.IsValid() || !hp.IsValid() )
			return;

		id.IsBossWildlifeSync = true;
		hp.MaxHealth *= BossWildlifeHealthMul;
		hp.CurrentHealth = hp.MaxHealth;

		go.LocalScale *= BossWildlifeModelAndHitboxMul;
		motor?.HostMultiplyCapsuleDimensions( BossWildlifeModelAndHitboxMul );
	}

	/// <summary>Restore tamed wildlife from <see cref="ThornsWorldPersistence"/> before owners reconnect.</summary>
	public static GameObject HostCreateFromSave(
		Scene scene,
		ThornsWildlifeSpeciesKind species,
		Vector3 worldPosition,
		Rotation worldRotation,
		Guid wildlifeId,
		float currentHealth,
		string tameOwnerAccountKey,
		bool tameFollowOwner,
		string tameDisplayName,
		int tameTotalXp,
		int tameUnspentUpgradePoints = 0,
		int tameHpUpgradeSteps = 0,
		int tameDmgUpgradeSteps = 0,
		int tameSpdUpgradeSteps = 0,
		byte tameQualityTier = 0,
		float tameAffinityHp = 0f,
		float tameAffinityDmg = 0f,
		float tameAffinitySpd = 0f,
		byte tameLegendaryAbility = 0 )
	{
		if ( !Networking.IsHost || scene is null || !scene.IsValid() || wildlifeId == Guid.Empty )
			return default;

		var go = new GameObject( true, $"Wildlife_{species}_{wildlifeId:N}" );
		go.WorldPosition = worldPosition;
		go.WorldRotation = worldRotation;
		go.Tags.Add( "creature" );

		go.Components.Create<ThornsHealth>();
		var id = go.Components.Create<ThornsWildlifeIdentity>();
		id.Species = species;
		id.WildlifeIdSync = wildlifeId.ToString( "D" );
		id.TameOwnerAccountKeySync = tameOwnerAccountKey ?? "";
		id.TameOwnerConnectionIdSync = "";
		id.TameFollowOwnerSync = tameFollowOwner;
		id.TameDisplayNameSync = tameDisplayName ?? "";
		id.TameTotalXp = Math.Max( 0, tameTotalXp );
		id.TameUnspentUpgradePoints = Math.Max( 0, tameUnspentUpgradePoints );
		id.TameHpUpgradeSteps = Math.Max( 0, tameHpUpgradeSteps );
		id.TameDmgUpgradeSteps = Math.Max( 0, tameDmgUpgradeSteps );
		id.TameSpdUpgradeSteps = Math.Max( 0, tameSpdUpgradeSteps );
		id.TameQualityTierSync = tameQualityTier;
		id.TameAffinityHpSync = tameAffinityHp;
		id.TameAffinityDmgSync = tameAffinityDmg;
		id.TameAffinitySpdSync = tameAffinitySpd;
		id.TameLegendaryAbilitySync = tameLegendaryAbility;

		var hp = go.Components.Get<ThornsHealth>();
		var def = id.Definition;
		if ( hp.IsValid() )
		{
			hp.MaxHealth = def.MaxHealth;
			hp.CurrentHealth = Math.Clamp( currentHealth, 1f, def.MaxHealth );
		}

		id.HostApplyDefinitionNow();

		go.Components.Create<ThornsWildlifeMotor>();
		go.Components.Create<ThornsWildlifeCombat>();
		go.Components.Create<ThornsWildlifeAnimSync>();
		go.Components.Create<ThornsWildlifeBrain>();
		go.Components.Create<ThornsWildlifeVocalization>();
		go.Components.Create<ThornsWildlifeLocomotionFootsteps>();

		HostApplyCreatureVisual( go, species );

		HostEnsureWildlifeSolidHull( go, species );

		HostTryNetworkSpawnWildlife( go );

		ThornsWildlifeLog.Spawn( species.ToString(), worldPosition );
		return go;
	}

	/// <summary>Joiners only received host-authoritative fauna once the hierarchy is network-spawned; subtree <see cref="NetworkMode.Object"/> avoids snapshot gaps on children (same idea as <see cref="ThornsGameManager"/> pawn setup).</summary>
	static void HostTryNetworkSpawnWildlife( GameObject go )
	{
		if ( !go.IsValid() || !Networking.IsActive )
			return;

		if ( !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
			Log.Warning( "[Thorns] Wildlife NetworkSpawn failed — joiners may not see or interact with this creature correctly." );
	}

	/// <summary>
	/// CharacterController drives the root; a child carries a static <see cref="BoxCollider"/> hull that blocks other fauna
	/// (<c>thorns_wildlife_hull</c> × <c>creature</c> / hull in Collision.config).
	/// </summary>
	static void HostEnsureWildlifeSolidHull( GameObject root, ThornsWildlifeSpeciesKind species )
	{
		if ( !root.IsValid() )
			return;

		const string hullName = "WildlifeSolidHull";
		foreach ( var c in root.Children )
		{
			if ( c.IsValid() && c.Name == hullName )
				return;
		}

		var hull = new GameObject( true, hullName );
		hull.SetParent( root );
		hull.LocalPosition = Vector3.Zero;
		hull.LocalRotation = Rotation.Identity;
		hull.LocalScale = Vector3.One;

		var model = ThornsWildlifeVisualModels.LoadForSpecies( species, out var usedFallback );
		if ( !usedFallback && model.IsValid() )
		{
			var extent = species == ThornsWildlifeSpeciesKind.Panther
				? PantherHullMeshExtentScale
				: 1.12f;
			ThornsAnchoredWorldPhysics.EnsureWildlifeHullBoxPhysicsMatchVisualMesh( hull, model, hullExtentScale: extent );
		}
		else
			ThornsAnchoredWorldPhysics.EnsureWildlifeHullBoxPhysics( hull, ThornsAnchoredWorldPhysics.DevBoxCollisionModel, hullExtentScale: 1.12f );
	}

	static Vector3 WildlifeUniformWorldScale( float authoredSpeciesUniformScale )
	{
		var u = authoredSpeciesUniformScale * WildlifeVisualScaleMultiplier;
		return new Vector3( u, u, u );
	}

	static void HostApplyCreatureVisual( GameObject go, ThornsWildlifeSpeciesKind species ) =>
		ApplyCreatureVisualToRoot( go, species );

	static void ApplyCreatureVisualToRoot( GameObject go, ThornsWildlifeSpeciesKind species )
	{
		var model = ThornsWildlifeVisualModels.LoadForSpecies( species, out var usedFallback );
		if ( species == ThornsWildlifeSpeciesKind.Wolf && !usedFallback )
		{
			go.LocalScale = WildlifeUniformWorldScale( WolfRootUniformScale );
			var skin = go.Components.Create<SkinnedModelRenderer>();
			skin.Model = model;
			skin.Tint = Color.White;
			skin.UseAnimGraph = false;
			var anim = go.Components.Create<ThornsWildlifeElkAnimDriver>();
			anim.IdleSequenceName = "wolf_idle";
			anim.WalkSequenceName = "wolf_walk";
			anim.RunSequenceName = "wolf_run";
			anim.AttackSequenceName = "wolf_attack";
			anim.DeathSequenceName = "wolf_death";
			ApplyCreatureMeshTextures( go, model );
			return;
		}

		if ( species == ThornsWildlifeSpeciesKind.Panther && !usedFallback )
		{
			go.LocalScale = WildlifeUniformWorldScale( PantherRootUniformScale * PantherVisualAndHitboxScaleMul );
			var skin = go.Components.Create<SkinnedModelRenderer>();
			skin.Model = model;
			skin.Tint = Color.White;
			// Sequence-only vmdl — SkinnedModelRenderer.Sequence (no .vanmgrph; DirectPlayback does not apply).
			skin.UseAnimGraph = false;
			go.Components.Create<ThornsWildlifePantherAnimDriver>();
			go.Components.Get<ThornsWildlifeMotor>()?.HostApplyCapsuleScaleMul( PantherVisualAndHitboxScaleMul );
			ApplyCreatureMeshTextures( go, model );
			return;
		}

		if ( species == ThornsWildlifeSpeciesKind.Elk && !usedFallback )
		{
			go.LocalScale = WildlifeUniformWorldScale( ElkRootUniformScale );
			var skin = go.Components.Create<SkinnedModelRenderer>();
			skin.Model = model;
			skin.Tint = Color.White;
			skin.UseAnimGraph = false;
			go.Components.Create<ThornsWildlifeElkAnimDriver>();
			ApplyCreatureMeshTextures( go, model );
			return;
		}

		if ( species == ThornsWildlifeSpeciesKind.Deer && !usedFallback )
		{
			go.LocalScale = WildlifeUniformWorldScale( DeerRootUniformScale );
			var skin = go.Components.Create<SkinnedModelRenderer>();
			skin.Model = model;
			skin.Tint = Color.White;
			skin.UseAnimGraph = false;
			var anim = go.Components.Create<ThornsWildlifeElkAnimDriver>();
			anim.IdleSequenceName = "deer_idle";
			anim.WalkSequenceName = "deer_walk";
			anim.RunSequenceName = "deer_run";
			anim.AttackSequenceName = "deer_attack";
			anim.DeathSequenceName = "deer_death";
			ApplyCreatureMeshTextures( go, model );
			return;
		}

		if ( species == ThornsWildlifeSpeciesKind.Moose && !usedFallback )
		{
			go.LocalScale = WildlifeUniformWorldScale( MooseRootUniformScale );
			var skin = go.Components.Create<SkinnedModelRenderer>();
			skin.Model = model;
			skin.Tint = Color.White;
			skin.UseAnimGraph = false;
			var anim = go.Components.Create<ThornsWildlifeElkAnimDriver>();
			anim.IdleSequenceName = "moose_idle";
			anim.WalkSequenceName = "moose_walk";
			anim.RunSequenceName = "moose_run";
			anim.AttackSequenceName = "moose_attack";
			anim.DeathSequenceName = "moose_death";
			ApplyCreatureMeshTextures( go, model );
			return;
		}

		go.LocalScale = WildlifeUniformWorldScale( 1f );
		var vis = go.Components.Create<ModelRenderer>();
		vis.Model = model;
		vis.Tint = usedFallback ? SpeciesTint( species ) : Color.White;
		ApplyCreatureMeshTextures( go, model );
	}

	static void ApplyCreatureMeshTextures( GameObject root, Model model )
	{
		if ( !root.IsValid() )
			return;

		var path = model.IsValid() ? model.Name : null;
		ThornsModelMaterialUvScale.ApplyToHierarchy( root, includeChildren: true, path );
	}

	static Color SpeciesTint( ThornsWildlifeSpeciesKind species ) =>
		species switch
		{
			ThornsWildlifeSpeciesKind.Wolf => new Color( 0.62f, 0.50f, 0.42f, 1f ),
			ThornsWildlifeSpeciesKind.Deer => new Color( 0.42f, 0.62f, 0.36f, 1f ),
			ThornsWildlifeSpeciesKind.Fox => new Color( 0.90f, 0.46f, 0.22f, 1f ),
			ThornsWildlifeSpeciesKind.Bear => new Color( 0.50f, 0.34f, 0.25f, 1f ),
			ThornsWildlifeSpeciesKind.Cougar => new Color( 0.72f, 0.62f, 0.46f, 1f ),
			ThornsWildlifeSpeciesKind.Panther => new Color( 0.62f, 0.58f, 0.52f, 1f ),
			ThornsWildlifeSpeciesKind.Boar => new Color( 0.46f, 0.34f, 0.30f, 1f ),
			ThornsWildlifeSpeciesKind.Rabbit => new Color( 0.84f, 0.80f, 0.74f, 1f ),
			ThornsWildlifeSpeciesKind.Elk => new Color( 0.48f, 0.58f, 0.38f, 1f ),
			ThornsWildlifeSpeciesKind.Moose => new Color( 0.40f, 0.30f, 0.24f, 1f ),
			ThornsWildlifeSpeciesKind.Bison => new Color( 0.36f, 0.28f, 0.22f, 1f ),
			_ => new Color( 0.35f, 0.55f, 0.28f, 1f )
		};

	/// <summary>Re-binds creature vmdl when proxies lack a valid mesh reference (joiners / late replication).</summary>
	public static void EnsureLocalCreatureVisual( GameObject go, ThornsWildlifeSpeciesKind species )
	{
		if ( !go.IsValid() || !Game.IsPlaying )
			return;

		var skin = go.Components.Get<SkinnedModelRenderer>( FindMode.EnabledInSelf );
		var mr = go.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		var skinOk = skin.IsValid() && ModelLooksRenderable( skin.Model );
		var mrOk = mr.IsValid() && ModelLooksRenderable( mr.Model );
		if ( skinOk || mrOk )
			return;

		StripRootVisualDrivers( go );
		ApplyCreatureVisualToRoot( go, species );
		var idBoss = go.Components.Get<ThornsWildlifeIdentity>();
		if ( idBoss.IsValid() && idBoss.IsBossWildlifeSync )
			HostReapplyBossVisualAfterCreatureVisualRebuild( go );
	}

	static void HostReapplyBossVisualAfterCreatureVisualRebuild( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		go.LocalScale *= BossWildlifeModelAndHitboxMul;
		go.Components.Get<ThornsWildlifeMotor>()?.HostMultiplyCapsuleDimensions( BossWildlifeModelAndHitboxMul );
	}

	static bool ModelLooksRenderable( Model m ) =>
		m.IsValid() && !m.IsError;

	static void StripRootVisualDrivers( GameObject go )
	{
		foreach ( var c in go.Components.GetAll<ThornsWildlifePantherAnimDriver>( FindMode.EnabledInSelf ).ToArray() )
			c.Destroy();
		foreach ( var c in go.Components.GetAll<ThornsWildlifeElkAnimDriver>( FindMode.EnabledInSelf ).ToArray() )
			c.Destroy();
		foreach ( var c in go.Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelf ).ToArray() )
			c.Destroy();
		foreach ( var c in go.Components.GetAll<ModelRenderer>( FindMode.EnabledInSelf ).ToArray() )
			c.Destroy();
	}
}
