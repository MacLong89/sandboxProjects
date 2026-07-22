namespace FinalOutpost;

public enum GamePhase { MainMenu, Day, Night, NightRecap, SeasonRecap, GameOver, Victory }

public enum NightCombatKind
{
	Zombies,
	RivalBaseAttack,
	RivalPlotAssault
}

/// <summary>Drives night spawning and combat ticks. Player manually starts each night.</summary>
public sealed class NightController : Component
{
	public static NightController Instance { get; private set; }

	public int ZombiesRemainingToSpawn { get; private set; }
	public float SpawnTimer { get; private set; }
	public NightCombatKind CombatKind { get; private set; } = NightCombatKind.Zombies;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void BeginNight( int night )
	{
		CombatKind = NightCombatKind.Zombies;
		HostileForceSystem.Instance?.ClearAll();
		ZombiesRemainingToSpawn = ZombiesForNight( night );
		SpawnTimer = 0f;
		_roundStallTimer = 0f;
		_roundAliveSnapshot = -1;
		_bossKind = BossKind.None;
		_spawnOrdinal = 0;
		Sfx.Play( Sfx.WaveStart );
	}

	public void BeginThreat( int zombieCount, int threatIndex, BossKind bossKind = BossKind.None )
	{
		CombatKind = NightCombatKind.Zombies;
		HostileForceSystem.Instance?.ClearAll();
		ZombiesRemainingToSpawn = zombieCount;
		SpawnTimer = 0f;
		_roundStallTimer = 0f;
		_roundAliveSnapshot = -1;
		Sfx.Play( Sfx.WaveStart );
		_threatIndex = threatIndex;
		_bossKind = bossKind;
		_spawnOrdinal = 0;
	}

	public void BeginRivalBaseAttack( int hostileCount )
	{
		CombatKind = NightCombatKind.RivalBaseAttack;
		ZombiesRemainingToSpawn = 0;
		SpawnTimer = 0f;
		_roundStallTimer = 0f;
		_roundAliveSnapshot = -1;
		_bossKind = BossKind.None;
		_spawnOrdinal = 0;
		HostileForceSystem.Instance?.BeginBaseAttack( hostileCount );
		Sfx.Play( Sfx.WaveStart );
	}

	public void BeginRivalPlotAssault( int plotX, int plotY )
	{
		CombatKind = NightCombatKind.RivalPlotAssault;
		ZombiesRemainingToSpawn = 0;
		SpawnTimer = 0f;
		_roundStallTimer = 0f;
		_roundAliveSnapshot = -1;
		_bossKind = BossKind.None;
		_spawnOrdinal = 0;
		HostileForceSystem.Instance?.BeginPlotAssault( plotX, plotY );
		HostileForceSystem.Instance?.RallyPlayerRecruitsToAssault();
		PlotManager.Instance?.RebuildVisuals();
		Sfx.Play( Sfx.WaveStart );
	}

	private int _threatIndex = 1;
	private BossKind _bossKind;
	private int _spawnOrdinal;
	private int _roundAliveSnapshot = -1;
	private float _roundStallTimer;

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Night ) return;

		TickSpawns( core );
		HostileForceSystem.Instance?.Tick( Time.Delta );
		TickDefenses( core );
		TickRoundFailsafe( core );

		var hostilesClear = HostileForceSystem.Instance is null || !HostileForceSystem.Instance.AnyActive;
		if ( ZombiesRemainingToSpawn <= 0 && (CombatSystem.Instance?.AliveCount ?? 0) == 0 && hostilesClear )
		{
			if ( CombatKind == NightCombatKind.RivalPlotAssault )
				core.OnRivalAssaultWon();
			else if ( core.IsCure )
				core.OnThreatSurvived();
			else
				core.OnNightSurvived();
		}
	}

	private void TickSpawns( GameCore core )
	{
		if ( CombatKind != NightCombatKind.Zombies ) return;
		if ( ZombiesRemainingToSpawn <= 0 ) return;

		SpawnTimer -= Time.Delta;
		if ( SpawnTimer > 0f ) return;

		SpawnTimer = GameConstants.SpawnStagger;
		ZombiesRemainingToSpawn--;

		var night = core.CombatProgressionNight;
		var typeDef = PickThreatType( night, _spawnOrdinal++ );

		CombatSystem.Instance?.SpawnZombie( PickSpawnPoint(), night, typeDef );
	}

	private ZombieTypeDef PickThreatType( int night, int ordinal )
	{
		if ( _bossKind == BossKind.None )
			return ZombieCatalog.PickForNight( night );

		return _bossKind switch
		{
			BossKind.Giant when ordinal % 5 == 0 => ZombieCatalog.Get( ZombieKind.Giant ),
			BossKind.Giant => ZombieCatalog.Get( ZombieKind.Brute ),
			BossKind.MutantBeast when ordinal % 3 == 0 => ZombieCatalog.Get( ZombieKind.Runner ),
			BossKind.MutantBeast => ZombieCatalog.Get( ZombieKind.Swarm ),
			BossKind.MilitaryConvoy when ordinal % 4 == 0 => ZombieCatalog.Get( ZombieKind.Bomber ),
			BossKind.MilitaryConvoy => ZombieCatalog.Get( ZombieKind.Armored ),
			BossKind.InfectedHive when ordinal % 3 == 0 => ZombieCatalog.Get( ZombieKind.Splitter ),
			BossKind.InfectedHive => ZombieCatalog.Get( ZombieKind.Swarm ),
			_ => ZombieCatalog.PickForNight( night )
		};
	}

	private void TickRoundFailsafe( GameCore core )
	{
		if ( ZombiesRemainingToSpawn > 0 || (HostileForceSystem.Instance?.SpawnRemaining ?? 0) > 0 )
		{
			_roundStallTimer = 0f;
			_roundAliveSnapshot = -1;
			return;
		}

		var alive = (CombatSystem.Instance?.AliveCount ?? 0) + (HostileForceSystem.Instance?.AliveCount ?? 0);
		if ( alive <= 0 )
		{
			_roundStallTimer = 0f;
			_roundAliveSnapshot = 0;
			return;
		}

		if ( alive < _roundAliveSnapshot )
			_roundStallTimer = 0f;

		_roundAliveSnapshot = alive;
		_roundStallTimer += Time.Delta;

		if ( _roundStallTimer < GameConstants.NightRoundFailsafeDelay )
			return;

		CombatSystem.Instance?.FailsafeClearRemainingZombies( core );
		HostileForceSystem.Instance?.ClearAll();
		_roundStallTimer = 0f;
	}

	private void TickDefenses( GameCore core )
	{
		var combat = CombatSystem.Instance;
		if ( combat is null ) return;

		var upgrades = core.Upgrades;
		DefenderManager.Instance?.TickCombat( combat, upgrades );

		var build = BuildManager.Instance;
		if ( build is null ) return;

		foreach ( var b in build.Buildings )
			b.TickCombat( combat, upgrades );
	}

	private static int ZombiesForNight( int night ) => GameConstants.ZombiesForNight( night );

	private static Vector3 PickSpawnPoint()
	{
		var claimedHalf = PlotManager.Instance?.ClaimedHalfExtent ?? GameConstants.ArenaHalf;
		var half = MathF.Max( GameConstants.ArenaHalf, claimedHalf ) + GameConstants.SpawnMargin;
		half = MathF.Min( half, GameConstants.ActiveTerrainHalfExtent - GameConstants.U( 60f ) );

		var t = Game.Random.Float( -half, half );
		return Game.Random.Int( 0, 3 ) switch
		{
			0 => new Vector3( t, half, 0f ),
			1 => new Vector3( t, -half, 0f ),
			2 => new Vector3( half, t, 0f ),
			_ => new Vector3( -half, t, 0f ),
		};
	}
}
