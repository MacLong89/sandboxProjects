namespace RunGun;

/// <summary>Lightweight floating combat feedback — damage numbers, gate pops, hit flashes.</summary>
public sealed class VfxManager : Component
{
	public static VfxManager Instance { get; private set; }

	private readonly List<FloatingText> _texts = new();

	private sealed class FloatingText
	{
		public GameObject Go;
		public UI.WorldLabel Label;
		public float Life;
		public Vector3 Velocity;
	}

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		ClearAll();
	}

	protected override void OnUpdate()
	{
		var dt = Time.Delta;
		for ( var i = _texts.Count - 1; i >= 0; i-- )
		{
			var t = _texts[i];
			t.Life -= dt;
			if ( t.Life <= 0f || !t.Go.IsValid() )
			{
				t.Go?.Destroy();
				_texts.RemoveAt( i );
				continue;
			}

			t.Go.WorldPosition += t.Velocity * dt;
			t.Velocity = t.Velocity.WithZ( t.Velocity.z + 40f * dt );
		}
	}

	public void SpawnDamageNumber( Vector3 pos, float damage, bool crit )
	{
		var color = crit ? new Color( 1f, 0.35f, 0.9f ) : new Color( 1f, 0.92f, 0.35f );
		var text = crit ? $"{(int)damage}!" : $"{(int)damage}";
		SpawnText( pos.WithZ( GameConstants.BodyHeight ), text, color, 0.7f, new Vector3( 0f, 0f, 80f ) );
	}

	public void SpawnGatePop( Vector3 pos, string text, Color color ) =>
		SpawnText( pos.WithZ( GameConstants.GateHeight + 40f ), text, color, 0.9f, new Vector3( 0f, 0f, 120f ), big: false, width: 640f );

	public void SpawnCrewLoss( Vector3 pos, int lost ) =>
		SpawnText( pos, $"-{lost}", new Color( 1f, 0.3f, 0.3f ), 0.85f, new Vector3( 0f, 0f, 120f ), big: true, width: 260f );

	public void SpawnMilestone( Vector3 pos, string text ) =>
		SpawnText( pos.WithZ( 140f ), text, new Color( 1f, 0.85f, 0.25f ), 1.4f, new Vector3( 0f, 0f, 60f ), big: false, width: 520f );

	private void SpawnText( Vector3 pos, string text, Color color, float life, Vector3 velocity, bool big = true, float width = 240f )
	{
		var go = new GameObject( true, "VfxText" );
		go.WorldPosition = pos;

		var wp = go.Components.Create<Sandbox.WorldPanel>();
		wp.PanelSize = new Vector2( width, 160f );
		wp.RenderScale = 1f;
		wp.LookAtCamera = true;
		wp.InteractionRange = 0f;
		wp.RenderOptions.Game = true;

		var label = go.Components.Create<UI.WorldLabel>();
		label.Text = text;
		label.Accent = color;
		label.Big = big;

		_texts.Add( new FloatingText { Go = go, Label = label, Life = life, Velocity = velocity } );
	}

	private void ClearAll()
	{
		foreach ( var t in _texts )
			t.Go?.Destroy();
		_texts.Clear();
	}
}
