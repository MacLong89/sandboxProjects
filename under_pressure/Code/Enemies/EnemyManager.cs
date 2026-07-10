namespace UnderPressure;

/// <summary>
/// Spawns and tracks the pests for the current job. Rebuilds the roster whenever a new job
/// loads, awards a cash bounty when a pest is defeated, and handles timed respawns for
/// wave-style pressure. Lives for the whole session as a single component.
/// </summary>
public sealed class EnemyManager : Component
{
	public static EnemyManager Instance { get; private set; }

	private int _lastGeneration = -1;
	private readonly List<Enemy> _active = new();
	private readonly List<(EnemyDef def, Vector3 origin, float at)> _respawns = new();
	private bool _midWaveSpawned;
	private GameObject _root;

	/// <summary>Number of pests currently harassing the job.</summary>
	public int ActiveCount
	{
		get
		{
			_active.RemoveAll( e => !e.IsValid() );
			return _active.Count;
		}
	}

	/// <summary>Whether the current job has any pests at all (for HUD flavor).</summary>
	public bool JobHasEnemies { get; private set; }

	/// <summary>Distinct active pests with their counts, ordered by the tool that beats them, so
	/// the HUD can show a "which tool for which pest" guide.</summary>
	public List<(EnemyDef Def, int Count)> ActivePests()
	{
		_active.RemoveAll( e => !e.IsValid() );
		return _active
			.GroupBy( e => e.Def.Kind )
			.Select( g => (g.First().Def, g.Count()) )
			.OrderBy( t => (int)t.Item1.DamagedBy )
			.ThenBy( t => t.Item1.Name )
			.ToList();
	}

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null )
			return;

		if ( core.Jobs.LoadGeneration != _lastGeneration )
		{
			_lastGeneration = core.Jobs.LoadGeneration;
			Respawn( core );
		}

		if ( core.IsWorldFrozen )
			return;

		TickRespawns( core );
		TickMidJobWave( core );
	}

	private void Respawn( GameCore core )
	{
		_root?.Destroy();
		_active.Clear();
		_respawns.Clear();
		_midWaveSpawned = false;

		_root = new GameObject( true, "Enemies" );

		var spawns = core.Jobs.Current?.Enemies ?? new List<EnemySpawnDef>();
		JobHasEnemies = spawns.Count > 0;

		var difficulty = PestDifficulty( core.Jobs.Index );
		foreach ( var spawn in spawns )
		{
			if ( IsContractKind( spawn.Kind ) )
			{
				if ( !core.Save.HitmanContractUnlocked )
					continue;
				if ( core.Jobs.Index < GameConstants.HitmanContractJobIndex )
					continue;
			}

			Spawn( EnemyCatalog.Get( spawn.Kind ), spawn.Position, difficulty );
		}
	}

	private static bool IsContractKind( EnemyKind kind ) =>
		kind is EnemyKind.ContractTarget or EnemyKind.ContractBodyguard;

	/// <summary>Later jobs make pests tougher and more aggressive.</summary>
	public static float PestDifficulty( int jobIndex ) =>
		1f + Math.Max( 0, jobIndex - GameConstants.PestAttackUnlockJob ) * GameConstants.PestDifficultyPerJob;

	private void Spawn( EnemyDef def, Vector3 origin, float difficulty = 1f )
	{
		var go = new GameObject( _root, true, $"Enemy_{def.Kind}" );
		var enemy = go.Components.Create<Enemy>();
		enemy.Init( def, origin, this, difficulty );
		_active.Add( enemy );
	}

	private void TickRespawns( GameCore core )
	{
		if ( _respawns.Count == 0 )
			return;

		// Don't repopulate a finished job — let the player leave in peace.
		if ( core.AwaitingDeparture )
		{
			_respawns.Clear();
			return;
		}

		for ( var i = _respawns.Count - 1; i >= 0; i-- )
		{
			if ( Time.Now < _respawns[i].at )
				continue;

			var entry = _respawns[i];
			_respawns.RemoveAt( i );
			if ( _root.IsValid() )
				Spawn( entry.def, entry.origin, PestDifficulty( core.Jobs.Index ) );
		}
	}

	/// <summary>Halfway through late jobs, a rival crew member crashes the site.</summary>
	private void TickMidJobWave( GameCore core )
	{
		if ( _midWaveSpawned || core.AwaitingDeparture || core.Jobs.Index < 4 )
			return;

		if ( core.Jobs.Progress < 0.5f )
			return;

		_midWaveSpawned = true;
		if ( !_root.IsValid() )
			return;

		var center = core.Jobs.Current?.WorkCenter ?? Vector3.Zero;
		var offset = new Vector3( Game.Random.Float( -180f, 180f ), Game.Random.Float( -80f, 120f ), 0f );
		Spawn( EnemyCatalog.Get( EnemyKind.RivalWasher ), center + offset, PestDifficulty( core.Jobs.Index ) + 0.25f );
		core.PulseHarassment( "Rival crew incoming — hose fight!" );
	}

	/// <summary>Called by a pest when it's defeated: pay the bounty and maybe schedule a return.</summary>
	public void OnDefeated( Enemy enemy, EnemyDef def, Vector3 origin )
	{
		_active.Remove( enemy );

		var core = GameCore.Instance;
		if ( core is not null )
		{
			var payout = def.Bounty * core.Upgrades.CashMultiplier * core.Upgrades.VanMultiplier * core.Prestige.Multiplier;
			core.Wallet.Earn( payout );

			if ( def.Family == EnemyFamily.Contract )
			{
				var msg = def.Kind == EnemyKind.ContractTarget
					? "Target eliminated — pressure-wash the blood evidence!"
					: "Bodyguard down — clean the scene!";
				core.PulseHarassment( msg );
			}
		}

		if ( def.RespawnDelay > 0f )
			_respawns.Add( (def, origin, Time.Now + def.RespawnDelay) );
	}
}
