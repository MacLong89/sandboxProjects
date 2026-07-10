namespace Fauna2;

/// <summary>One line of breeding history, shown in the codex and persisted.</summary>
public sealed class BreedingRecord
{
	public long UnixTime { get; set; }
	public string Species { get; set; } = "";
	public string VariantId { get; set; } = "";
	public string OffspringName { get; set; } = "";
	public string OffspringId { get; set; } = "";
	public string ParentA { get; set; } = "";
	public string ParentB { get; set; } = "";
	public string ParentAId { get; set; } = "";
	public string ParentBId { get; set; } = "";
	public int Generation { get; set; }
	public string BloodlineName { get; set; } = "";
}

/// <summary>
/// Host-side breeding. Periodically scans habitats for compatible, happy,
/// well-housed pairs and produces offspring through the pluggable genetics
/// service (which also owns variant rolls and the hybrid extension point).
/// </summary>
public sealed class BreedingSystem : Component
{
	public static BreedingSystem Instance { get; private set; }

	/// <summary>Recent breeding history (host authoritative, persisted in saves).</summary>
	public List<BreedingRecord> History { get; } = new();

	[Sync( SyncFlags.FromHost )] public int TotalBredCount { get; set; }

	private TimeUntil _nextScan;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		if ( !_nextScan ) return;
		_nextScan = GameConstants.BreedScanDuration;

		var clock = DebugStats.StartTimer();
		ScanForPairs();
		DebugStats.StopTimer( "Breeding", clock );
	}

	private void ScanForPairs()
	{
		if ( AnimalRegistry.Count >= PlotSystem.Instance.AnimalCap ) return;

		foreach ( var habitat in HabitatRegistry.All )
		{
			if ( habitat.Score < GameConstants.BreedMinHabitatScore ) continue;

			var animals = AnimalRegistry.InHabitat( habitat.HabitatId ).ToList();

			foreach ( var group in animals.GroupBy( a => a.DefinitionId ) )
				TryBreedGroup( habitat, group.ToList() );
		}
	}

	private void TryBreedGroup( HabitatComponent habitat, List<AnimalComponent> animals )
	{
		var eligible = animals.Where( IsEligible ).ToList();
		if ( eligible.Count < 2 ) return;

		var def = eligible[0].Definition;
		if ( def is null ) return;

		// One attempt per habitat-species per scan keeps growth gentle.
		var a = eligible[0];
		var b = eligible[1];

		var chance = GameConstants.BreedBaseChance;
		chance *= habitat.Score / 100f;
		chance *= 1f - def.BreedingDifficulty;
		chance *= a.Genome.Stat( "fertility" ) * b.Genome.Stat( "fertility" );
		chance *= 1f + (FranchiseSystem.Instance?.LegacyBreedingBonus ?? 0f);

		if ( Game.Random.Float() >= chance ) return;
		if ( !habitat.HasRoomFor( def ) ) return;

		Breed( a, b, habitat );
	}

	private bool IsEligible( AnimalComponent animal )
	{
		if ( animal.Definition is null ) return false;
		if ( !animal.IsAdult || animal.IsElder ) return false;
		if ( animal.Happiness < animal.Definition.BreedingHappiness ) return false;
		if ( animal.TimeSinceBred < GameConstants.BreedCooldownDuration ) return false;
		return true;
	}

	/// <summary>Host only. Produces one offspring from a pair.</summary>
	public AnimalComponent Breed( AnimalComponent a, AnimalComponent b, HabitatComponent habitat )
	{
		var genetics = AnimalSystem.Instance.Genetics;

		var offspringDef = genetics.ResolveOffspringSpecies( a.Definition, b.Definition );
		if ( offspringDef is null ) return null;
		var genome = genetics.Breed( a, b );

		var midpoint = (a.GameObject.WorldPosition + b.GameObject.WorldPosition) * 0.5f;
		var baby = AnimalSystem.Instance.Spawn( offspringDef, genome, habitat, midpoint, ageSeconds: 0f );
		if ( baby is null ) return null;

		a.TimeSinceBred = 0;
		b.TimeSinceBred = 0;

		var state = ZooState.Instance;
		state.TotalAnimalsBred++;
		TotalBredCount++;
		state.AddXp( GameConstants.XpBreedAnimal );

		History.Insert( 0, new BreedingRecord
		{
			UnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Species = offspringDef.ResourceName,
			VariantId = genome.VariantId,
			OffspringName = baby.AnimalName,
			OffspringId = baby.AnimalId,
			ParentA = a.AnimalName,
			ParentB = b.AnimalName,
			ParentAId = a.AnimalId,
			ParentBId = b.AnimalId,
			Generation = genome.Generation,
			BloodlineName = genome.BloodlineName,
		} );
		if ( History.Count > 100 ) History.RemoveAt( History.Count - 1 );

		BroadcastHistoryEntry( baby.AnimalName, baby.AnimalId, offspringDef.ResourceName, genome.VariantId,
			a.AnimalName, b.AnimalName, a.AnimalId, b.AnimalId, genome.Generation, genome.BloodlineName );

		CollectionSystem.Instance?.OnAnimalAcquired( baby, bred: true );
		GameEvents.RaiseAnimalBred( baby );

		var variantText = baby.Variant is not null ? $" — a {baby.Variant.DisplayName} variant!" : "!";
		var hybridText = offspringDef.Rarity == AnimalRarity.Legendary ? " A legendary discovery!" : "";
		state.Notify( $"{a.AnimalName} & {b.AnimalName} had a baby: {baby.AnimalName}{variantText}{hybridText}", "favorite" );

		return baby;
	}

	/// <summary>Keeps client-side codex history panels in sync without polling.</summary>
	[Rpc.Broadcast]
	private void BroadcastHistoryEntry( string babyName, string babyId, string species, string variantId,
		string parentA, string parentB, string parentAId, string parentBId, int generation, string bloodline )
	{
		if ( Networking.IsHost ) return; // host already added the entry

		History.Insert( 0, new BreedingRecord
		{
			UnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Species = species,
			VariantId = variantId,
			OffspringName = babyName,
			OffspringId = babyId,
			ParentA = parentA,
			ParentB = parentB,
			ParentAId = parentAId,
			ParentBId = parentBId,
			Generation = generation,
			BloodlineName = bloodline,
		} );
		if ( History.Count > 100 ) History.RemoveAt( History.Count - 1 );
	}
}
