namespace ThinkDrink;

/// <summary>Marker for the physical studio buzzer hit target.</summary>
public sealed class StudioBuzzerButton : Component
{
	private static readonly Color NormalTint = new( 0.95f, 0.04f, 0.05f );
	private static readonly Color HoverTint = new( 1f, 0.22f, 0.10f );
	private static readonly Color PressTint = new( 1f, 0.55f, 0.15f );

	private ModelRenderer _renderer;
	private Vector3 _baseScale;
	private bool _hovered;
	private TimeUntil _pressReset;

	protected override void OnStart()
	{
		_renderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		_baseScale = GameObject.LocalScale;
		SetHovered( false );
	}

	protected override void OnUpdate()
	{
		if ( MatchManager.Instance?.Phase != MatchPhase.BuzzIn )
			SetHovered( false );

		if ( _pressReset )
		{
			_pressReset = 0;
			if ( _renderer.IsValid() && !_hovered )
				_renderer.Tint = NormalTint;
			GameObject.LocalScale = _hovered ? _baseScale * 1.18f : _baseScale;
		}
	}

	public void SetHovered( bool hovered )
	{
		if ( _hovered == hovered && _renderer.IsValid() ) return;

		_hovered = hovered;

		if ( _renderer.IsValid() )
			_renderer.Tint = hovered ? HoverTint : NormalTint;

		GameObject.LocalScale = hovered ? _baseScale * 1.18f : _baseScale;
	}

	public void Press( ThinkDrinkPlayer player )
	{
		if ( player is null || player.IsSpectator ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.BuzzIn ) return;

		if ( _renderer.IsValid() )
			_renderer.Tint = PressTint;

		GameObject.LocalScale = _baseScale * 0.82f;
		_pressReset = 0.18f;

		GameEvents.RaiseAudio( AudioEventId.BuzzerPress );
		player.RequestBuzz();
		Log.Info( $"[ThinkDrink][Buzzer] {player.PlayerName} pressed the physical buzzer." );
	}
}
