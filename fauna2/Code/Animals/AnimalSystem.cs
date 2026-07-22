namespace Fauna2;

/// <summary>
/// Host-authoritative animal lifecycle: purchase/sale requests from clients,
/// spawning (purchases, breeding, save loading) and throttled need simulation.
/// Animal "thinks" are budgeted per tick so hundreds of animals amortize their
/// cost across frames instead of spiking.
/// </summary>
public sealed class AnimalSystem : Component
{
	public static AnimalSystem Instance { get; private set; }

	private static readonly string[] NamePool =
	{
		"Willow", "Maple", "Clover", "Biscuit", "Juniper", "Pepper", "Hazel",
		"Frost", "Mochi", "Bramble", "Sage", "Pippin", "Acorn", "Fern",
		"Ginger", "Cocoa", "Aspen", "Birch", "Poppy", "Thistle", "River",
		"Storm", "Ember", "Shadow", "Honey", "Olive", "Rusty",
	};

	public IGeneticsService Genetics { get; set; } = new StandardGenetics();

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var clock = DebugStats.StartTimer();
		var processed = 0;

		// Budgeted, staggered thinking. Each animal thinks every
		// AnimalThinkInterval seconds; we cap work per physics tick.
		foreach ( var animal in AnimalRegistry.All )
		{
			if ( processed >= GameConstants.MaxAnimalThinksPerTick ) break;
			if ( !animal.NextThink ) continue;

			animal.Think( GameConstants.AnimalThinkInterval );
			animal.NextThink = GameConstants.AnimalThinkInterval;
			processed++;
		}

		DebugStats.StopTimer( "Animals", clock );
	}

	// ── Client requests ─────────────────────────────────────

	/// <summary>Client asks to adopt an animal at a world position (inside a habitat).</summary>
	[Rpc.Host]
	public void RequestBuyAnimal( string definitionId, Vector3 position )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var def = Defs.Animal( definitionId );
		if ( def is null ) return;

		var state = ZooState.Instance;
		if ( !state.IsValid() ) return;

		if ( state.Level < def.UnlockLevel || state.Prestige < def.RequiredPrestige )
		{
			state.Notify( $"{def.DisplayName} is not unlocked yet.", "lock" );
			return;
		}

		if ( AnimalRegistry.Count >= (PlotSystem.Instance?.AnimalCap ?? int.MaxValue) )
		{
			state.Notify( "Animal capacity reached — buy more land!", "warning" );
			return;
		}

		var habitat = HabitatRegistry.FindAt( position );
		if ( habitat is null )
		{
			state.Notify( "Animals must be placed inside a habitat.", "fence" );
			return;
		}

		if ( !habitat.TryAccept( def, null, out var habitatError ) )
		{
			state.Notify( habitatError, "block" );
			return;
		}

		var cost = GetPurchaseCost( def );
		if ( !state.TrySpend( cost ) )
		{
			state.Notify( "Not enough money.", "payments" );
			return;
		}

		if ( cost == 0 && !state.TutorialAnimalClaimed )
			state.TutorialAnimalClaimed = true;

		var genome = Genetics.CreateWild( def );
		var animal = Spawn( def, genome, habitat, position );
		if ( animal is null )
		{
			state.AddMoney( cost );
			state.Notify( "Could not place that animal — try another spot in the habitat.", "block" );
			return;
		}

		state.TotalAnimalsBought++;
		state.AddXp( GameConstants.XpBuyAnimal );
		CollectionSystem.Instance?.OnAnimalAcquired( animal, bred: false );
		state.Notify( $"{animal.AnimalName} the {def.DisplayName} joined the zoo!", "pets" );

		ZooSoundNetwork.PlayAnimalPlacedForAll( Defs.IdOf( def ), animal.GameObject.WorldPosition );
	}

	/// <summary>Client asks to move an animal to another habitat (or relocate within one).</summary>
	[Rpc.Host]
	public void RequestMoveAnimal( string animalId, Vector3 position )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var animal = AnimalRegistry.Find( animalId );
		if ( animal is null ) return;

		var def = animal.Definition;
		if ( def is null ) return;

		var habitat = HabitatRegistry.FindAt( position );
		if ( habitat is null )
		{
			ZooState.Instance?.Notify( "Animals must stay inside a habitat.", "fence" );
			return;
		}

		if ( !habitat.TryAccept( def, animal, out var habitatError ) )
		{
			ZooState.Instance?.Notify( habitatError, "block" );
			return;
		}

		var transferred = animal.HabitatId != habitat.HabitatId;
		animal.HabitatId = habitat.HabitatId;
		animal.GameObject.WorldPosition = habitat.ClampInside( position ).WithZ( 0f );
		animal.Activity = AnimalActivity.Idle;

		if ( transferred )
		{
			ZooState.Instance?.Notify(
				$"{animal.AnimalName} moved to {habitat.Definition?.DisplayName ?? "another habitat"}.",
				"swap_horiz" );
		}
	}

	/// <summary>Client asks to sell an animal.</summary>
	[Rpc.Host]
	public void RequestSellAnimal( string animalId )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var animal = AnimalRegistry.Find( animalId );
		if ( animal is null ) return;

		var state = ZooState.Instance;
		var value = animal.SellValue;

		state.AddMoney( value );
		state.Notify( $"{animal.AnimalName} was rehomed for ${value:n0}.", "volunteer_activism" );

		animal.GameObject.Destroy();
	}

	// ── Spawning ────────────────────────────────────────────

	/// <summary>The tutorial animal (first of the cheapest species) is free.</summary>
	public int GetPurchaseCost( AnimalDefinition def )
	{
		var state = ZooState.Instance;
		if ( state.IsValid() && !state.TutorialAnimalClaimed && IsTutorialSpecies( def ) )
			return 0;

		return def.Cost;
	}

	public static bool IsTutorialSpecies( AnimalDefinition def ) =>
		StarterGoalGuide.IsValidStarterAdopt( def );

	/// <summary>Host only. Spawns a networked animal. Used by purchases, breeding and saves.</summary>
	public AnimalComponent Spawn( AnimalDefinition def, AnimalGenome genome, HabitatComponent habitat,
		Vector3 position, string name = null, string animalId = null, float ageSeconds = -1f )
	{
		if ( !Networking.IsHost || def is null || habitat is null ) return null;

		var go = new GameObject( true, $"Animal - {def.DisplayName}" );
		go.Tags.Add( "animal" );
		go.WorldPosition = habitat.ClampInside( position ).WithZ( 0f );
		go.WorldRotation = Rotation.Identity;

		var animal = go.AddComponent<AnimalComponent>();
		animal.AnimalId = animalId ?? Guid.NewGuid().ToString( "N" );
		animal.DefinitionId = Defs.IdOf( def );
		animal.VariantId = genome?.VariantId ?? "";
		animal.AnimalName = name ?? RandomName();
		animal.HabitatId = habitat.HabitatId;
		animal.Genome = genome ?? new AnimalGenome();
		animal.AgeSeconds = ageSeconds >= 0f ? ageSeconds : def.AdultAge;
		animal.Hunger = Game.Random.Float( 60f, 90f );

		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn();
		go.Network.SetOrphanedMode( NetworkOrphaned.Host );

		return animal;
	}

	public static string RandomName() =>
		NamePool[Game.Random.Int( 0, NamePool.Length - 1 )];

	/// <summary>Host only — place an animal caught in the wilderness.</summary>
	public bool TrySpawnCaught( AnimalDefinition def, HabitatComponent habitat, Vector3 position )
	{
		if ( !Networking.IsHost || def is null || habitat is null ) return false;

		var genome = Genetics.CreateWild( def );
		var animal = Spawn( def, genome, habitat, position );
		if ( animal is null ) return false;

		CollectionSystem.Instance?.OnAnimalAcquired( animal, bred: false );
		ZooSoundNetwork.PlayAnimalPlacedForAll( Defs.IdOf( def ), animal.GameObject.WorldPosition );
		return true;
	}
}
