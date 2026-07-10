namespace ThinkDrink;

/// <summary>Periodic console diagnostics for studio spawn, camera, and scoreboard visibility.</summary>
public sealed class StudioDebug : Component
{
	[Property] public float LogIntervalSeconds { get; set; } = 3f;

	private TimeSince _sinceLog;

	protected override void OnStart()
	{
		if ( Scene.IsEditor ) return;
		LogSnapshot( "startup" );
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;
		if ( _sinceLog < LogIntervalSeconds ) return;
		_sinceLog = 0f;
		LogSnapshot( "tick" );
	}

	void LogSnapshot( string reason )
	{
		var env = StudioEnvironment.Instance;
		var phase = MatchManager.Instance?.Phase.ToString() ?? "none";
		var question = MatchManager.Instance?.QuestionText ?? "";
		var boardPos = env?.ScoreboardWorldPosition ?? Vector3.Zero;
		var boardValid = env?.ScoreboardDisplay?.IsValid() == true;
		var wp = env?.ScoreboardWorldPanel;
		var wpInfo = wp.IsValid()
			? $"wpSize={wp.PanelSize} wpScale={wp.RenderScale} wpGame={wp.RenderOptions.Game}"
			: "wp=missing";

		var pawn = FindLocalPawn();
		if ( pawn is null )
		{
			Log.Info( $"[ThinkDrink][StudioDebug:{reason}] phase={phase} boardValid={boardValid} boardPos={boardPos} {wpInfo} pawn=missing questionLen={question.Length}" );
			return;
		}

		var eyePos = pawn.GetEyeWorldPosition();
		var eyeForward = pawn.GetEyeForward();
		var toBoard = boardPos - eyePos;
		var dist = toBoard.Length;
		var facing = dist > 1f ? Vector3.Dot( eyeForward, toBoard.Normal ) : 0f;
		var look = pawn.GetLookAngles();

		Log.Info(
			$"[ThinkDrink][StudioDebug:{reason}] phase={phase} pawnPos={pawn.GameObject.WorldPosition} eyePos={eyePos} look=({look.pitch:0.#},{look.yaw:0.#}) " +
			$"boardPos={boardPos} boardDist={dist:0.#} boardFacingDot={facing:0.00} boardValid={boardValid} {wpInfo} questionLen={question.Length}" );
	}

	static PlayerPawn FindLocalPawn()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return null;

		foreach ( var pawn in scene.GetAllComponents<PlayerPawn>() )
		{
			if ( pawn.IsValid() && pawn.IsLocalOwner )
				return pawn;
		}
		return null;
	}
}
