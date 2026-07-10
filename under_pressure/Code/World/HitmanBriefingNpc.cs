namespace UnderPressure;

/// <summary>
/// One-shot fixer NPC on job 3 (index 2). Walks toward the player, then opens the
/// classified briefing conversation that unlocks the hitman gun line in the van.
/// </summary>
public sealed class HitmanBriefingNpc : Component
{
	public static HitmanBriefingNpc Instance { get; private set; }

	private bool _triggered;
	private float _cooldown;
	private CitizenHumanoid _humanoid;
	private bool _usesCitizen;

	public static void EnsureForJob( Scene scene, int jobIndex, SaveData save )
	{
		foreach ( var existing in scene.GetAllComponents<HitmanBriefingNpc>() )
			existing.GameObject.Destroy();

		if ( save.HitmanBriefingSeen || jobIndex != GameConstants.HitmanBriefingJobIndex || jobIndex < 0 )
			return;

		var jobs = GameCore.Instance?.Jobs;
		var spawn = jobs?.SpawnPosition ?? Vector3.Zero;
		var go = new GameObject( true, "Fixer" );
		go.WorldPosition = spawn + GameConstants.FixerBriefingSpawnOffset;
		go.Components.Create<HitmanBriefingNpc>();
	}

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart() => Build();

	protected override void OnUpdate()
	{
		if ( _triggered )
			return;

		var core = GameCore.Instance;
		var player = PressurePlayer.Instance;
		if ( core is null || player is null || core.IsFixerApproachBlocked )
			return;

		_cooldown = Math.Max( 0f, _cooldown - Time.Delta );
		if ( _cooldown > 0f )
			return;

		var toPlayer = player.WorldPosition - WorldPosition;
		var flat = toPlayer.WithZ( 0f );
		var dist = flat.Length;

		if ( dist > GameConstants.FixerBriefingStopDistance )
		{
			var step = flat.Normal * 46f * Time.Delta;
			WorldPosition += step.WithZ( 0f );
			if ( flat.Length > 0.01f )
				WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( flat.Normal ), Time.Delta * 4f );

			if ( _usesCitizen && _humanoid is not null )
				_humanoid.TickLocomotion( flat.Normal * 46f );
		}
		else
		{
			_humanoid?.TickIdle();

			if ( !_triggered )
			{
				_triggered = true;
				_humanoid?.TickLookAt( player.WorldPosition );
				core.StartHitmanBriefing();
			}
		}

		if ( _usesCitizen )
			_humanoid?.TickLookAt( player.WorldPosition );
	}

	private void Build()
	{
		_humanoid = Components.Create<CitizenHumanoid>();
		_usesCitizen = _humanoid.TrySetup( GameConstants.CitizenHeightScale );
		if ( _usesCitizen )
		{
			var h = GameConstants.CitizenHeightScale;
			var col = Components.Create<BoxCollider>();
			col.Center = new Vector3( 0, 0, 36f * h );
			col.Scale = new Vector3( 28f * h, 28f * h, 72f * h );
			col.Static = true;
			return;
		}

		BuildBoxFixer();
	}

	private void BuildBoxFixer()
	{
		var skin = new Color( 0.84f, 0.68f, 0.52f );
		var suit = new Color( 0.16f, 0.18f, 0.24f );
		var tie = new Color( 0.78f, 0.12f, 0.14f );
		Scenery.Box( GameObject, "Legs", new Vector3( 0, 0, 16 ), new Vector3( 24, 28, 34 ), suit, default, GameMaterials.Metal );
		Scenery.Box( GameObject, "Torso", new Vector3( 0, 0, 48 ), new Vector3( 30, 34, 36 ), suit, default, GameMaterials.Metal );
		Scenery.Box( GameObject, "Head", new Vector3( 2, 0, 74 ), new Vector3( 22, 22, 22 ), skin, default, GameMaterials.Metal );
		Scenery.Box( GameObject, "Hat", new Vector3( 2, 0, 86 ), new Vector3( 26, 26, 8 ), new Color( 0.10f, 0.10f, 0.12f ), default, GameMaterials.Metal );
		Scenery.Box( GameObject, "Tie", new Vector3( 10, 0, 56 ), new Vector3( 6, 6, 22 ), tie, default, GameMaterials.Metal );
		Scenery.Box( GameObject, "Case", new Vector3( 22, 10, 28 ), new Vector3( 26, 10, 18 ), tie, default, GameMaterials.Metal );

		var col = Components.Create<BoxCollider>();
		col.Center = new Vector3( 0, 0, 40f );
		col.Scale = new Vector3( 40f, 40f, 80f );
		col.Static = true;
	}
}
