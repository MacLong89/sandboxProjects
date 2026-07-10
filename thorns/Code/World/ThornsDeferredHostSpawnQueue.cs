namespace Sandbox;

/// <summary>
/// Spreads host-only spawn work (resource nodes, boulders, etc.) across frames to avoid load hitches.
/// </summary>
[Title( "Thorns — Deferred Host Spawn Queue" )]
[Category( "Thorns/World" )]
[Icon( "hourglass_empty" )]
public sealed class ThornsDeferredHostSpawnQueue : Component
{
	public static ThornsDeferredHostSpawnQueue Instance { get; private set; }

	readonly Queue<Action> _pending = new();
	bool _loggedMineralSpawnSummary;

	[Property] public int WorkBudgetPerFrame { get; set; } = 64;

	[Property] public int MaxPending { get; set; } = 20000;

	public int PendingCount => _pending.Count;

	public bool IsIdle => _pending.Count == 0;

	public static ThornsDeferredHostSpawnQueue EnsureOn( GameObject host, int workBudgetPerFrame = 64 )
	{
		if ( host is null || !host.IsValid() )
			return default;

		var queue = host.Components.Get<ThornsDeferredHostSpawnQueue>();
		if ( !queue.IsValid() )
			queue = host.Components.Create<ThornsDeferredHostSpawnQueue>();

		queue.WorkBudgetPerFrame = Math.Max( 1, workBudgetPerFrame );
		Instance = queue;
		return queue;
	}

	public bool TryEnqueue( Action work )
	{
		if ( work is null )
			return false;

		if ( _pending.Count >= MaxPending )
			return false;

		_pending.Enqueue( work );
		return true;
	}

	public void EnqueueOrRunNow( Action work )
	{
		if ( work is null )
			return;

		if ( !TryEnqueue( work ) )
			work();
	}

	/// <summary>Allow another mineral spawn summary when a new scatter pass enqueues work.</summary>
	public void ArmMineralSpawnSummaryLog() => _loggedMineralSpawnSummary = false;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		var budget = Math.Max( 1, WorkBudgetPerFrame );
		var processed = 0;
		using ( ThornsPerfDebug.Scope( "DeferredHostSpawnQueue" ) )
		{
			while ( budget-- > 0 && _pending.Count > 0 )
			{
				_pending.Dequeue()?.Invoke();
				processed++;
			}
		}

		ThornsPerfDebug.DeferredSpawnsThisFrame = processed;
		ThornsPerfDebug.DeferredQueuePending = _pending.Count;

		if ( _pending.Count == 0 && !_loggedMineralSpawnSummary )
		{
			_loggedMineralSpawnSummary = true;
			ThornsResourceNode.HostLogMineralSpawnSummaryIfAny();
		}

		if ( _pending.Count == 0 && ReferenceEquals( Instance, this ) )
			Instance = null;
	}

	protected override void OnDestroy()
	{
		if ( ReferenceEquals( Instance, this ) )
			Instance = null;
	}
}
