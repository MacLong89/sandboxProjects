namespace FinalOutpost;

public enum GamePhase { MainMenu, Day, Night, NightRecap, SeasonRecap, GameOver, Victory }

/// <summary>Drives night spawning and combat ticks. Player manually starts each night.</summary>
public sealed class NightController : Component
{
	public static NightController Instance { get; private set; }

	public int ZombiesRemainingToSpawn { get; private set; }
	public float SpawnTimer { get; private set; }

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void BeginNight( int night )
	{
		ZombiesRemainingToSpawn = ZombiesForNight( night );
		SpawnTimer = 0f;
		Sfx.Play( Sfx.WaveStart );
	}

	public void BeginThreat( int zombieCount, int threatIndex )
	{
		ZombiesRemainingToSpawn = zombieCount;
		SpawnTimer = 0f;
		Sfx.Play( Sfx.WaveStart );
		_threatIndex = threatIndex;
	}

	private int _threatIndex = 1;

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Night ) return;

		TickSpawns( core );
		TickDefenses( core );

		if ( ZombiesRemainingToSpawn <= 0 && CombatSystem.Instance?.AliveCount == 0 )
		{
			if ( core.IsCure )
				core.OnThreatSurvived();
			else
				core.OnNightSurvived();
		}
	}

	private void TickSpawns( GameCore core )
	{
		if ( ZombiesRemainingToSpawn <= 0 ) return;

		SpawnTimer -= Time.Delta;
		if ( SpawnTimer > 0f ) return;

		SpawnTimer = GameConstants.SpawnStagger;
		ZombiesRemainingToSpawn--;

		var night = core.IsCure ? Math.Max( 1, _threatIndex ) : core.Save.CurrentNight;
		var typeDef = ZombieCatalog.PickForNight( night );

		CombatSystem.Instance?.SpawnZombie( PickSpawnPoint(), night, typeDef );
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
		// Spawn on the perimeter of a SQUARE just beyond the claimed territory. A square ring fully
		// encloses the square walls, so enemies can never appear inside them (the old circular ring
		// clipped the wall corners). The frontier pushes outward as the player claims more plots.
		var claimedHalf = PlotManager.Instance?.ClaimedHalfExtent ?? GameConstants.ArenaHalf;
		var half = MathF.Max( GameConstants.ArenaHalf, claimedHalf ) + GameConstants.SpawnMargin;
		half = MathF.Min( half, GameConstants.TerrainHalfExtent - 60f );

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
