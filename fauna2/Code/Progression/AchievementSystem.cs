namespace Fauna2;

/// <summary>One-time achievement rewards that fire passively as you play.</summary>
public sealed class AchievementSystem : Component
{
	public static AchievementSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int UnlockedFlags { get; set; }

	private TimeUntil _nextCheck;

	private void CheckVoid() => Check();

	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.SpeciesDiscovered += OnSpeciesDiscovered;
		GameEvents.VariantDiscovered += OnVariantDiscovered;
		GameEvents.AnimalBred += OnAnimalBred;
		GameEvents.LevelUp += OnLevelUp;
		GameEvents.PlotPurchased += CheckVoid;
		GameEvents.HabitatPlaced += CheckVoid;
		GameEvents.ZooModified += CheckVoid;
	}

	protected override void OnDestroy()
	{
		GameEvents.SpeciesDiscovered -= OnSpeciesDiscovered;
		GameEvents.VariantDiscovered -= OnVariantDiscovered;
		GameEvents.AnimalBred -= OnAnimalBred;
		GameEvents.LevelUp -= OnLevelUp;
		GameEvents.PlotPurchased -= CheckVoid;
		GameEvents.HabitatPlaced -= CheckVoid;
		GameEvents.ZooModified -= CheckVoid;

		if ( Instance == this ) Instance = null;
	}

	private void OnSpeciesDiscovered( string _ ) => Check();
	private void OnVariantDiscovered( string _ ) => Check();
	private void OnAnimalBred( AnimalComponent _ ) => Check();
	private void OnLevelUp( int _ ) => Check();

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextCheck ) return;
		_nextCheck = 3f;
		Check();
	}

	public void Check()
	{
		if ( !Networking.IsHost ) return;

		Try( 1, (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) >= 1,
			"First discovery", "Recorded your first species in the codex", 150, 30 );
		Try( 2, (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) >= 3,
			"Collector", "Discovered 3 species", 500, 80 );
		Try( 4, (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) >= 10,
			"Naturalist", "Discovered 10 species", 1200, 150 );
		Try( 8, (CollectionSystem.Instance?.DiscoveredVariantCount ?? 0) >= 1,
			"Variant hunter", "Found a rare color variant", 800, 120 );
		Try( 16, (CollectionSystem.Instance?.DiscoveredVariantCount ?? 0) >= 5,
			"Color curator", "Discovered 5 rare variants", 1500, 200 );
		Try( 32, (ZooState.Instance?.TotalAnimalsBred ?? 0) >= 1,
			"Breeder", "Bred your first animal", 600, 90 );
		Try( 64, (ZooState.Instance?.TotalAnimalsBred ?? 0) >= 5,
			"Dynasty builder", "Bred 5 animals", 1400, 180 );
		Try( 128, (GuestSystem.Instance?.ZooRating ?? 0f) >= 4f,
			"Four stars", "Reached a 4-star zoo rating", 1000, 120 );
		Try( 256, (GuestSystem.Instance?.ZooRating ?? 0f) >= 5f,
			"Five stars", "Reached a 5-star zoo rating", 2000, 250 );
		Try( 512, (PlotSystem.Instance?.PlotCount ?? 1) >= 2,
			"Land owner", "Expanded to a second plot", 600, 80 );
		Try( 1024, (PlotSystem.Instance?.PlotCount ?? 1) >= 3,
			"Land baron", "Own 3 or more plots", 1200, 150 );
		Try( 2048, HabitatRegistry.Count >= 3,
			"Habitat architect", "Built 3 habitats", 700, 90 );
		Try( 4096, AnimalRegistry.Count >= 10,
			"Busy sanctuary", "Care for 10 animals at once", 900, 110 );
		Try( 8192, (GuestSystem.Instance?.GuestCount ?? 0) >= 50,
			"Local favorite", "Hosted 50 guests at once", 800, 100 );
		Try( 16384, (CollectionSystem.Instance?.CompletionPercent ?? 0f) >= 99f,
			"Codex master", "Completed the species codex", 3000, 350 );
	}

	private void Try( int bit, bool done, string title, string desc, int money, int xp )
	{
		if ( !done || (UnlockedFlags & bit) != 0 ) return;

		UnlockedFlags |= bit;
		var state = ZooState.Instance;
		if ( !state.IsValid() ) return;

		state.AddMoney( money );
		state.AddXp( xp );
		state.Notify( $"Achievement: {title} — {desc} (+${money:n0})", "emoji_events" );
		UI.UiState.ShowCelebration( title, desc, "emoji_events" );
	}

	public void Restore( int flags )
	{
		if ( !Networking.IsHost ) return;
		UnlockedFlags = flags;
	}
}
