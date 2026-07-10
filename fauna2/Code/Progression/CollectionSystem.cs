namespace Fauna2;

/// <summary>
/// The Zoo Codex: which species/variants have been discovered, owned and bred.
/// Backbone of completionist play. Synced so every player browses the same
/// codex; persisted in saves.
/// </summary>
public sealed class CollectionSystem : Component
{
	public static CollectionSystem Instance { get; private set; }

	[Flags]
	public enum DiscoveryFlags
	{
		None = 0,
		Seen = 1,
		Owned = 2,
		Bred = 4,
	}

	/// <summary>speciesId → flags.</summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, int> Species { get; set; } = new();

	/// <summary>"speciesId:variantId" → discovered.</summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, bool> Variants { get; set; } = new();

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	// ── Queries ─────────────────────────────────────────────

	public DiscoveryFlags FlagsFor( string speciesId ) =>
		Species.TryGetValue( speciesId, out var f ) ? (DiscoveryFlags)f : DiscoveryFlags.None;

	public bool IsDiscovered( string speciesId ) => FlagsFor( speciesId ).HasFlag( DiscoveryFlags.Owned );
	public bool HasBred( string speciesId ) => FlagsFor( speciesId ).HasFlag( DiscoveryFlags.Bred );
	public bool HasVariant( string speciesId, string variantId ) =>
		Variants.TryGetValue( $"{speciesId}:{variantId}", out var v ) && v;

	public int DiscoveredSpeciesCount => Defs.Animals.Count( a => IsDiscovered( Defs.IdOf( a ) ) );
	public int DiscoveredVariantCount => Variants.Count( kv => kv.Value );

	/// <summary>Overall codex completion (species 70%, variants 30%).</summary>
	public float CompletionPercent
	{
		get
		{
			var totalSpecies = Defs.Animals.Count();
			var totalVariantSlots = Defs.Animals.Sum( a => Defs.Variants.Count( v => v.AppliesTo( a ) ) );

			var speciesPart = totalSpecies == 0 ? 0f : (float)DiscoveredSpeciesCount / totalSpecies;
			var variantPart = totalVariantSlots == 0 ? 0f : (float)DiscoveredVariantCount / totalVariantSlots;

			return (speciesPart * 0.7f + variantPart * 0.3f) * 100f;
		}
	}

	// ── Host updates ────────────────────────────────────────

	public void OnAnimalAcquired( AnimalComponent animal, bool bred )
	{
		if ( !Networking.IsHost || animal?.Definition is null ) return;

		var state = ZooState.Instance;
		var speciesId = animal.DefinitionId;
		var flags = FlagsFor( speciesId );

		if ( !flags.HasFlag( DiscoveryFlags.Owned ) )
		{
			flags |= DiscoveryFlags.Seen | DiscoveryFlags.Owned;
			Species[speciesId] = (int)flags;

			state.AddXp( GameConstants.XpDiscoverSpecies );
			state.AddPrestige( GameConstants.PrestigeSpeciesDiscovered );
			GameEvents.RaiseSpeciesDiscovered( speciesId );
			state.Notify( $"New species discovered: {animal.Definition.DisplayName}!", "menu_book" );
			UI.UiState.ShowCelebration( "Species Discovered!", animal.Definition.DisplayName, "menu_book" );
		}

		if ( bred && !flags.HasFlag( DiscoveryFlags.Bred ) )
		{
			flags |= DiscoveryFlags.Bred;
			Species[speciesId] = (int)flags;
		}

		if ( !string.IsNullOrEmpty( animal.VariantId ) )
		{
			var key = $"{speciesId}:{animal.VariantId}";
			if ( !Variants.TryGetValue( key, out var had ) || !had )
			{
				Variants[key] = true;
				state.AddXp( GameConstants.XpDiscoverVariant );
				state.AddPrestige( GameConstants.PrestigeVariantDiscovered );
				GameEvents.RaiseVariantDiscovered( key );
				state.Notify( $"Rare variant discovered: {animal.Variant?.DisplayName} {animal.Definition.DisplayName}!", "auto_awesome" );
				UI.UiState.ShowCelebration( "Rare Variant!", $"{animal.Variant?.DisplayName} {animal.Definition.DisplayName}", "auto_awesome" );
			}
		}
	}

	/// <summary>Host only — used by save loading.</summary>
	public void Restore( Dictionary<string, int> species, Dictionary<string, bool> variants )
	{
		if ( !Networking.IsHost ) return;

		Species.Clear();
		Variants.Clear();
		if ( species is not null )
			foreach ( var kv in species ) Species[kv.Key] = kv.Value;
		if ( variants is not null )
			foreach ( var kv in variants ) Variants[kv.Key] = kv.Value;
	}
}
