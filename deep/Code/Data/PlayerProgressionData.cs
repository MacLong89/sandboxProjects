namespace Deep;

/// <summary>
/// Permanent player progression. Temporary dive data lives in <see cref="DiveRunState"/>.
/// </summary>
public sealed class PlayerProgressionData
{
	private readonly Dictionary<string, int> _upgrades = new();
	private readonly HashSet<string> _discoveredCollectibles = new();
	private readonly HashSet<string> _discoveredZones = new();
	private readonly HashSet<string> _discoveredCreatures = new();
	private readonly HashSet<string> _discoveredStories = new();
	private readonly HashSet<string> _discoveredCheckpoints = new();

	public float DeepestEverMeters { get; private set; }
	public float Money { get; private set; } = 200f;
	public float Shells { get; private set; } = 25f;
	public float LifetimeMoneyEarned { get; private set; }
	public int SuccessfulDives { get; private set; }
	public int FailedDives { get; private set; }
	public int TotalDives => SuccessfulDives + FailedDives;
	public IReadOnlyCollection<string> DiscoveredCollectibles => _discoveredCollectibles;
	public IReadOnlyCollection<string> DiscoveredZones => _discoveredZones;
	public IReadOnlyCollection<string> DiscoveredCreatures => _discoveredCreatures;
	public IReadOnlyCollection<string> DiscoveredStories => _discoveredStories;
	public IReadOnlyCollection<string> DiscoveredCheckpoints => _discoveredCheckpoints;
	public LoadoutInventory Loadout { get; } = new();
	public DiveHistoryLog History { get; } = new();

	public bool ApplyDiveMaxDepth( float maxDepthMeters )
	{
		if ( maxDepthMeters <= DeepestEverMeters )
			return false;

		DeepestEverMeters = maxDepthMeters;
		return true;
	}

	public void RegisterSuccessfulDive() => SuccessfulDives++;
	public void RegisterFailedDive() => FailedDives++;

	public bool TrySpend( float amount )
	{
		if ( amount <= 0f ) return true;
		if ( Money + 0.001f < amount ) return false;
		Money = MathF.Max( 0f, Money - amount );
		return true;
	}

	public void AddMoney( float amount )
	{
		if ( amount <= 0f ) return;
		Money += amount;
		LifetimeMoneyEarned += amount;
	}

	public bool TrySpendShells( float amount )
	{
		if ( amount <= 0f ) return true;
		if ( Shells + 0.001f < amount ) return false;
		Shells = MathF.Max( 0f, Shells - amount );
		return true;
	}

	public void AddShells( float amount )
	{
		if ( amount <= 0f ) return;
		Shells += amount;
	}

	public int GetUpgradeLevel( string id ) =>
		_upgrades.TryGetValue( id, out var level ) ? level : 0;

	public void SetUpgradeLevel( string id, int level )
	{
		if ( string.IsNullOrWhiteSpace( id ) ) return;
		_upgrades[id] = Math.Max( 0, level );
	}

	public void RegisterDiscovery( string collectibleId )
	{
		if ( string.IsNullOrWhiteSpace( collectibleId ) ) return;
		_discoveredCollectibles.Add( collectibleId );
	}

	public bool RegisterCreature( string creatureId )
	{
		if ( string.IsNullOrWhiteSpace( creatureId ) ) return false;
		return _discoveredCreatures.Add( creatureId );
	}

	public bool RegisterStory( string storyId )
	{
		if ( string.IsNullOrWhiteSpace( storyId ) ) return false;
		return _discoveredStories.Add( storyId );
	}

	public void DiscoverCheckpoint( string checkpointId )
	{
		if ( string.IsNullOrWhiteSpace( checkpointId ) ) return;
		_discoveredCheckpoints.Add( checkpointId );
	}

	public void DiscoverZone( DepthZone zone ) =>
		_discoveredZones.Add( zone.ToString() );

	public DeepSaveData ToSaveData()
	{
		return new DeepSaveData
		{
			Version = DeepSaveData.CurrentVersion,
			DeepestEverMeters = DeepestEverMeters,
			Money = Money,
			Shells = Shells,
			LifetimeMoneyEarned = LifetimeMoneyEarned,
			SuccessfulDives = SuccessfulDives,
			FailedDives = FailedDives,
			UpgradeLevels = new Dictionary<string, int>( _upgrades ),
			DiscoveredCollectibles = _discoveredCollectibles.ToList(),
			DiscoveredZones = _discoveredZones.ToList(),
			DiscoveredCreatures = _discoveredCreatures.ToList(),
			DiscoveredStories = _discoveredStories.ToList(),
			DiscoveredCheckpoints = _discoveredCheckpoints.ToList(),
			LoadoutReserves = Loadout.ToSaveMap(),
			UnlockedTools = Loadout.ToUnlockedList(),
			DiveHistory = History.ToSaveList()
		};
	}

	public bool ApplySaveData( DeepSaveData data )
	{
		if ( data is null ) return false;

		if ( data.Version < 3 )
		{
			ResetToNewGame();
			Log.Info( "[DEEP] Migrated save to v3 — progression reset for buy-to-unlock loadout." );
			return true;
		}

		DeepestEverMeters = MathF.Max( 0f, data.DeepestEverMeters );
		Money = MathF.Max( 0f, data.Money );
		Shells = MathF.Max( 0f, data.Shells );
		LifetimeMoneyEarned = MathF.Max( Money, data.LifetimeMoneyEarned );
		SuccessfulDives = Math.Max( 0, data.SuccessfulDives );
		FailedDives = Math.Max( 0, data.FailedDives );

		_upgrades.Clear();
		if ( data.UpgradeLevels is not null )
		{
			foreach ( var (id, level) in data.UpgradeLevels )
			{
				var def = UpgradeCatalog.Get( id );
				var max = def?.MaxLevel ?? 99;
				_upgrades[id] = Math.Clamp( level, 0, max );
			}
		}

		LoadSet( _discoveredCollectibles, data.DiscoveredCollectibles );
		LoadSet( _discoveredZones, data.DiscoveredZones );
		LoadSet( _discoveredCreatures, data.DiscoveredCreatures );
		LoadSet( _discoveredStories, data.DiscoveredStories );
		LoadSet( _discoveredCheckpoints, data.DiscoveredCheckpoints );

		Loadout.ApplySaveMap( data.LoadoutReserves, data.UnlockedTools );
		History.ApplySaveList( data.DiveHistory );
		return false;
	}

	public void ResetToNewGame()
	{
		DeepestEverMeters = 0f;
		Money = 200f;
		Shells = 25f;
		LifetimeMoneyEarned = 0f;
		SuccessfulDives = 0;
		FailedDives = 0;
		_upgrades.Clear();
		_discoveredCollectibles.Clear();
		_discoveredZones.Clear();
		_discoveredCreatures.Clear();
		_discoveredStories.Clear();
		_discoveredCheckpoints.Clear();
		Loadout.ResetToDefaults();
		History.Clear();
	}

	private static void LoadSet( HashSet<string> target, List<string> source )
	{
		target.Clear();
		if ( source is null ) return;
		foreach ( var id in source )
		{
			if ( !string.IsNullOrWhiteSpace( id ) )
				target.Add( id );
		}
	}
}
