namespace Sandbox;

/// <summary>Console diagnostics for FP viewmodel movement graph params — filter for "VM Move".</summary>
public static class AimboxViewModelMovementDebug
{
	public static bool Enabled { get; set; } = false;

	const float LogIntervalSeconds = 0.45f;

	static TimeSince _moveLogTimer;
	static TimeSince _blockedLogTimer;
	static string _lastMoveSnapshot = "";
	static string _lastBlockedReason = "";

	public readonly record struct MovementGraphProbe(
		bool GraphValid,
		bool HasMoveBob,
		bool HasSprint,
		bool HasGrounded,
		bool HasDuckLevel,
		bool HasBDuck );

	public static MovementGraphProbe ProbeGraph( Model model )
	{
		if ( !model.IsValid() || model.IsError )
			return default;

		var graph = model.AnimGraph;
		if ( graph is null || graph.IsError )
			return default;

		return new MovementGraphProbe(
			true,
			graph.TryGetParameterIndex( "move_bob", out _ ),
			graph.TryGetParameterIndex( "b_sprint", out _ ),
			graph.TryGetParameterIndex( "b_grounded", out _ ),
			graph.TryGetParameterIndex( "duck_level", out _ ),
			graph.TryGetParameterIndex( "b_duck", out _ ) );
	}

	public static void LogSpawn(
		string modelPath,
		bool useAnimGraph,
		bool usedFallbackGeometry,
		bool hasAnimator,
		in MovementGraphProbe probe )
	{
		if ( !Enabled )
			return;

		var shortName = ShortModelName( modelPath );
		if ( !useAnimGraph || usedFallbackGeometry )
		{
			Log.Warning(
				$"[Aimbox VM Move] Spawn {shortName}: UseAnimGraph={useAnimGraph} fallback={usedFallbackGeometry} — no movement graph drive." );
			return;
		}

		if ( !hasAnimator )
		{
			Log.Warning( $"[Aimbox VM Move] Spawn {shortName}: UseAnimGraph=true but no AimboxViewModelFpAnimator." );
			return;
		}

		Log.Info(
			$"[Aimbox VM Move] Spawn {shortName}: graph={probe.GraphValid} params move_bob={probe.HasMoveBob} b_sprint={probe.HasSprint} b_grounded={probe.HasGrounded} duck={probe.HasDuckLevel || probe.HasBDuck}" );
	}

	public static void LogEquipReady( string modelPath, bool skinUseAnimGraph, in MovementGraphProbe probe )
	{
		if ( !Enabled )
			return;

		Log.Info(
			$"[Aimbox VM Move] Equip ready {ShortModelName( modelPath )}: skin.UseAnimGraph={skinUseAnimGraph} graph={probe.GraphValid} move_bob={probe.HasMoveBob} b_sprint={probe.HasSprint}" );
	}

	public static void LogTickBlocked( string reason )
	{
		if ( !Enabled || string.Equals( _lastBlockedReason, reason, StringComparison.Ordinal ) )
		{
			if ( _blockedLogTimer < LogIntervalSeconds * 2f )
				return;
		}

		_lastBlockedReason = reason;
		_blockedLogTimer = 0f;
		Log.Info( $"[Aimbox VM Move] Tick blocked: {reason}" );
	}

	public static void LogNoAnimator( string modelPath, bool skinValid, bool skinUseAnimGraph )
	{
		if ( !Enabled )
			return;

		LogTickBlocked(
			$"no animator (model={ShortModelName( modelPath )} skinValid={skinValid} skin.UseAnimGraph={skinUseAnimGraph})" );
	}

	public static void LogMovementTick(
		string modelPath,
		bool equipDone,
		bool skinUseAnimGraph,
		in MovementGraphProbe probe,
		float speed,
		bool sprintHeld,
		bool moving,
		float moveBob,
		bool sprintActive,
		bool grounded,
		bool ads,
		bool reloading )
	{
		if ( !Enabled )
			return;

		if ( !equipDone )
		{
			LogTickBlocked( "equip animation not finished (_equipPlaybackDone=false)" );
			return;
		}

		var snapshot =
			$"{ShortModelName( modelPath )}|spd={speed:F0}|sprintKey={sprintHeld}|moving={moving}|bob={moveBob:F2}|b_sprint={sprintActive}|ads={ads}|reload={reloading}|graph={probe.GraphValid}|useGraph={skinUseAnimGraph}|hasBob={probe.HasMoveBob}|hasSprint={probe.HasSprint}";

		if ( string.Equals( snapshot, _lastMoveSnapshot, StringComparison.Ordinal ) && _moveLogTimer < LogIntervalSeconds )
			return;

		_lastMoveSnapshot = snapshot;
		_moveLogTimer = 0f;
		Log.Info( $"[Aimbox VM Move] {snapshot}" );
	}

	static string ShortModelName( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return "(none)";

		var p = modelPath.Trim().Replace( '\\', '/' );
		var slash = p.LastIndexOf( '/' );
		return slash >= 0 ? p[( slash + 1 )..] : p;
	}
}
