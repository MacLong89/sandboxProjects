namespace RunGun;

/// <summary>
/// The visible crowd of runners that trails the leader. Its size tracks <see cref="RunState.Squad"/>
/// (capped for performance): bodies pop in when you hit a growth gate and poof out when a threat
/// culls the crowd. This is the game's core dopamine loop made physical — you watch your army
/// balloon at an x2 gate and bleed when you eat a hazard.
/// </summary>
public sealed class SquadFormation : Component
{
	private sealed class Trooper
	{
		public GameObject Go;
		public float Scale;      // 0..1 pop animation
		public float Phase;      // bob offset for organic motion
		public bool Dying;
	}

	private readonly List<Trooper> _troopers = new();
	private static readonly Color TeamBody = new( 0.28f, 0.6f, 1f );
	private static readonly Color TeamHead = new( 0.85f, 0.7f, 0.55f );

	public void Reset()
	{
		foreach ( var t in _troopers ) t.Go?.Destroy();
		_troopers.Clear();
	}

	protected override void OnDestroy() => Reset();

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		var run = core.Run;
		var target = run.Active ? Math.Clamp( run.SquadInt, 0, GameConstants.MaxVisibleSquad ) : 0;

		ReconcileCount( target );
		Layout();
	}

	private void ReconcileCount( int target )
	{
		var living = _troopers.Count( t => !t.Dying );

		// Grow: revive a fading body first, otherwise spawn a fresh one.
		while ( living < target )
		{
			var revive = _troopers.FirstOrDefault( t => t.Dying );
			if ( revive is not null ) revive.Dying = false;
			else AddTrooper();
			living++;
		}

		// Shrink: mark the newest living bodies as dying so they animate out.
		for ( var i = _troopers.Count - 1; i >= 0 && living > target; i-- )
		{
			if ( _troopers[i].Dying ) continue;
			_troopers[i].Dying = true;
			living--;
		}
	}

	private void Layout()
	{
		var dt = Time.Delta;
		var leader = GameObject.WorldPosition;
		var limit = GameConstants.LaneHalf - 14f;

		// Assign formation slots only to the living bodies, in list order, so slots stay stable.
		var livingIndex = 0;
		var livingTotal = _troopers.Count( t => !t.Dying );
		var rows = Math.Max( 1, (int)MathF.Ceiling( livingTotal / (float)GameConstants.SquadPerRow ) );

		for ( var i = _troopers.Count - 1; i >= 0; i-- )
		{
			var t = _troopers[i];
			if ( !t.Go.IsValid() ) { _troopers.RemoveAt( i ); continue; }

			// Pop animation.
			var goalScale = t.Dying ? 0f : 1f;
			t.Scale = MathX.Lerp( t.Scale, goalScale, MathF.Min( 1f, dt * GameConstants.SquadPopSpeed ) );
			if ( t.Dying && t.Scale < 0.05f )
			{
				t.Go.Destroy();
				_troopers.RemoveAt( i );
				continue;
			}

			t.Go.LocalScale = Vector3.One * MathF.Max( 0.001f, t.Scale );
		}

		// Second pass positions living bodies into a centered block around the leader.
		foreach ( var t in _troopers )
		{
			if ( t.Dying ) continue;

			var slot = livingIndex++;
			var row = slot / GameConstants.SquadPerRow;
			var col = slot % GameConstants.SquadPerRow;
			var itemsInRow = Math.Min( GameConstants.SquadPerRow, livingTotal - row * GameConstants.SquadPerRow );

			var xOff = (row - (rows - 1) * 0.5f) * GameConstants.SquadRowDepth;
			var yOff = (col - (itemsInRow - 1) * 0.5f) * GameConstants.SquadColSpacing;
			var bob = MathF.Sin( Time.Now * 11f + t.Phase ) * GameConstants.SquadBob;

			var goal = new Vector3(
				leader.x + xOff,
				Math.Clamp( leader.y + yOff, -limit, limit ),
				MathF.Max( 0f, bob ) );

			t.Go.WorldPosition = Vector3.Lerp( t.Go.WorldPosition, goal, MathF.Min( 1f, dt * GameConstants.SquadFollowLerp ) );
			t.Go.WorldRotation = Rotation.Identity;
		}
	}

	private void AddTrooper()
	{
		var go = new GameObject( true, "Trooper" );
		go.WorldPosition = GameObject.WorldPosition;
		go.LocalScale = Vector3.One * 0.001f;

		var body = new GameObject( go, true, "Body" );
		body.LocalPosition = new Vector3( 0f, 0f, 22f );
		body.LocalScale = MeshPrimitives.BoxScale( new Vector3( 22f, 20f, 44f ) );
		var br = body.Components.Create<ModelRenderer>();
		br.Model = MeshPrimitives.Box;
		br.MaterialOverride = MeshPrimitives.Mat;
		br.Tint = TeamBody;

		var head = new GameObject( go, true, "Head" );
		head.LocalPosition = new Vector3( 0f, 0f, 52f );
		head.LocalScale = MeshPrimitives.BoxScale( new Vector3( 15f, 15f, 15f ) );
		var hr = head.Components.Create<ModelRenderer>();
		hr.Model = MeshPrimitives.Box;
		hr.MaterialOverride = MeshPrimitives.Mat;
		hr.Tint = TeamHead;

		_troopers.Add( new Trooper { Go = go, Scale = 0.001f, Phase = Game.Random.Float( 0f, 10f ) } );
	}
}
