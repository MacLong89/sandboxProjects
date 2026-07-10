namespace ThinkDrink;

using ThinkDrink.Studio;

/// <summary>Orchestrates modular stage construction. Gameplay reads spawn spots and board refs from here.</summary>
public sealed class StudioEnvironment : Component
{
	public readonly record struct ContestantSpot( Vector3 Position, Rotation Rotation );

	public static StudioEnvironment Instance { get; private set; }

	public GameObject SetRoot { get; private set; }
	public GameObject ScoreboardDisplay { get; private set; }

	public Vector3 ScoreboardWorldPosition =>
		ScoreboardDisplay.IsValid() ? ScoreboardDisplay.WorldPosition : Vector3.Zero;

	public WorldPanel ScoreboardWorldPanel =>
		ScoreboardDisplay.IsValid()
			? ScoreboardDisplay.Components.Get<WorldPanel>( FindMode.EverythingInSelf )
			: null;

	public Vector3 PlayAreaFocus => StudioDimensions.PlayAreaFocus;

	private readonly List<ContestantSpot> _contestantSpots = new();
	private bool _built;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		if ( Scene.IsEditor ) return;
		if ( _built ) return;

		var result = StudioSetBuilder.Build( GameObject );
		SetRoot = result.Root;
		ScoreboardDisplay = result.MainAnswerBoard;
		_contestantSpots.AddRange( result.SpawnSpots );

		if ( Components.Get<StudioDebug>() is null )
			GameObject.AddComponent<StudioDebug>();

		if ( Components.Get<StudioBoardTuner>() is null )
			GameObject.AddComponent<StudioBoardTuner>();

		if ( Components.Get<StudioPhaseLighting>() is null )
			GameObject.AddComponent<StudioPhaseLighting>();

		_built = true;
		Log.Info( $"Think & Drink: hollow studio built ({StudioDimensions.RoomSize:0}×{StudioDimensions.RoomSize:0}×{StudioDimensions.RoomHeight:0}) — {_contestantSpots.Count} spawns." );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public int GetContestantIndex( ThinkDrinkPlayer player )
	{
		var participants = ThinkDrinkPlayer.All
			.Where( p => p.IsParticipant )
			.OrderBy( p => p.IsBot )
			.ThenBy( p => p.PlayerName )
			.ToList();

		for ( var i = 0; i < participants.Count; i++ )
		{
			if ( participants[i] == player )
				return i;
		}

		return 0;
	}

	public ContestantSpot? GetContestantSpot( int index )
	{
		if ( index < 0 || index >= _contestantSpots.Count )
			return _contestantSpots.Count > 0 ? _contestantSpots[0] : null;
		return _contestantSpots[index];
	}

	public static bool IsTeamA( int spawnIndex ) => spawnIndex < 4;
}
