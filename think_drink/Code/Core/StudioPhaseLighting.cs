namespace ThinkDrink;



/// <summary>Shifts studio accent lighting with match phase for broadcast atmosphere.</summary>

public sealed class StudioPhaseLighting : Component

{

	PointLight _accentA;

	PointLight _accentB;

	Color _targetA = Studio.StudioPalette.AccentPink;

	Color _targetB = Studio.StudioPalette.AccentGold;



	protected override void OnStart()

	{

		if ( Scene.IsEditor ) return;

		CreateAccents();

		GameEvents.PhaseChanged += OnPhaseChanged;

		GameEvents.RandomEventTriggered += OnRandomEvent;

		ApplyTargets( MatchManager.Instance?.Phase ?? MatchPhase.Lobby );

	}



	protected override void OnDestroy()

	{

		GameEvents.PhaseChanged -= OnPhaseChanged;

		GameEvents.RandomEventTriggered -= OnRandomEvent;

	}



	void CreateAccents()

	{

		var half = Studio.StudioDimensions.Half;

		var height = Studio.StudioDimensions.HeightHalf;



		var left = new GameObject( GameObject, true, "Accent Light Left" );

		left.WorldPosition = new Vector3( -half * 0.65f, 0f, height * 0.85f );

		_accentA = left.AddComponent<PointLight>();

		_accentA.LightColor = _targetA;

		_accentA.Radius = half * 0.9f;

		_accentA.Shadows = false;



		var right = new GameObject( GameObject, true, "Accent Light Right" );

		right.WorldPosition = new Vector3( half * 0.65f, 0f, height * 0.85f );

		_accentB = right.AddComponent<PointLight>();

		_accentB.LightColor = _targetB;

		_accentB.Radius = half * 0.9f;

		_accentB.Shadows = false;

	}



	void OnPhaseChanged( MatchPhase phase ) => ApplyTargets( phase );



	void OnRandomEvent( RandomEventType evt )

	{

		if ( evt == RandomEventType.None ) return;

		_targetA = Studio.StudioPalette.AccentPink;

		_targetB = Studio.StudioPalette.AccentCyan;

	}



	void ApplyTargets( MatchPhase phase )

	{

		switch ( phase )

		{

			case MatchPhase.BuzzIn:

				_targetA = Studio.StudioPalette.AccentPink;

				_targetB = Studio.StudioPalette.AccentPink;

				break;

			case MatchPhase.Answering:

			case MatchPhase.AnswerReveal:

				_targetA = Studio.StudioPalette.AccentGreen;

				_targetB = Studio.StudioPalette.AccentGold;

				break;

			case MatchPhase.StealAttempt:

				_targetA = new Color( 1f, 0.55f, 0.26f );

				_targetB = Studio.StudioPalette.AccentGold;

				break;

			case MatchPhase.ScoreboardReveal:

				_targetA = Studio.StudioPalette.AccentGold;

				_targetB = Studio.StudioPalette.AccentCyan;

				break;

			case MatchPhase.RandomEvent:

				_targetA = Studio.StudioPalette.AccentPink;

				_targetB = Studio.StudioPalette.AccentCyan;

				break;

			case MatchPhase.MatchEnd:

			case MatchPhase.PostMatch:

				_targetA = Studio.StudioPalette.AccentGold;

				_targetB = Studio.StudioPalette.AccentGold;

				break;

			default:

				_targetA = Studio.StudioPalette.AccentCyan;

				_targetB = Studio.StudioPalette.AccentGold;

				break;

		}

	}



	protected override void OnUpdate()

	{

		if ( !_accentA.IsValid() || !_accentB.IsValid() ) return;



		var dt = Time.Delta * 4f;

		_accentA.LightColor = Color.Lerp( _accentA.LightColor, _targetA, dt );

		_accentB.LightColor = Color.Lerp( _accentB.LightColor, _targetB, dt );

	}

}


