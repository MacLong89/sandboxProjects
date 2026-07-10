namespace Terraingen.AI;

using System.Collections.Generic;

/// <summary>Bandit AI debug toggles, gizmo drawing, and behavior logging.</summary>
public static class ThornsBanditDebug
{
	public static bool DebugEnabled { get; internal set; }
	public static bool ShowCover { get; internal set; }
	public static bool ShowTargets { get; internal set; }
	public static bool ShowPerception { get; internal set; }

	[ConVar( "ai_bandit_log" )]
	public static bool LogBehaviors { get; set; }

	/// <summary>Min seconds between periodic tick logs per bandit (state changes always log).</summary>
	[ConVar( "ai_bandit_log_interval" )]
	public static float LogIntervalSeconds { get; set; } = 0.85f;

	/// <summary>Optional name filter (substring). Empty = all bandits.</summary>
	[ConVar( "ai_bandit_log_filter" )]
	public static string LogNameFilter { get; set; } = "";

	static readonly Dictionary<int, double> NextPeriodicLogByBrainId = new();

	[ConCmd( "ai_debug_bandits" )]
	public static void CmdDebugBandits()
	{
		DebugEnabled = !DebugEnabled;
		Log.Info( $"[Thorns Bandits] ai_debug_bandits={DebugEnabled}" );
	}

	[ConCmd( "ai_bandit_log_on" )]
	public static void CmdBanditLogOn()
	{
		LogBehaviors = true;
		Log.Info( "[Thorns Bandits] ai_bandit_log=1 — filter console for [BanditAI]. Toggle: ai_bandit_log 0" );
	}

	[ConCmd( "ai_show_cover" )]
	public static void CmdShowCover()
	{
		ShowCover = !ShowCover;
		DebugEnabled |= ShowCover;
		Log.Info( $"[Thorns Bandits] ai_show_cover={ShowCover}" );
	}

	[ConCmd( "ai_show_targets" )]
	public static void CmdShowTargets()
	{
		ShowTargets = !ShowTargets;
		DebugEnabled |= ShowTargets;
		Log.Info( $"[Thorns Bandits] ai_show_targets={ShowTargets}" );
	}

	[ConCmd( "ai_show_perception" )]
	public static void CmdShowPerception()
	{
		ShowPerception = !ShowPerception;
		DebugEnabled |= ShowPerception;
		Log.Info( $"[Thorns Bandits] ai_show_perception={ShowPerception}" );
	}

	public static void DrawForBrain( ThornsBanditBrain brain ) =>
		brain?.HostDrawDebugOverlay();

	public static void LogState( ThornsBanditBrain brain, ThornsBanditAiState from, ThornsBanditAiState to, string reason )
	{
		if ( !ShouldLog( brain ) )
			return;

		var target = brain.DebugTargetLabel;
		Log.Info( $"[BanditAI] {FormatBrain( brain )} STATE {from} -> {to} ({reason}) target={target}" );
	}

	public static void LogEvent( ThornsBanditBrain brain, string category, string detail, bool force = false )
	{
		if ( !ShouldLog( brain ) )
			return;

		if ( !force && !IsDueForPeriodicLog( brain ) )
			return;

		Log.Info( $"[BanditAI] {FormatBrain( brain )} {category}: {detail}" );
	}

	public static void LogPath( ThornsBanditBrain brain, string action, string detail )
	{
		if ( !ShouldLog( brain ) )
			return;

		if ( !IsDueForPeriodicLog( brain ) )
			return;

		Log.Info( $"[BanditAI] {FormatBrain( brain )} PATH {action}: {detail}" );
	}

	public static void LogVision( ThornsBanditBrain brain, string detail, bool force = false )
	{
		if ( !ShouldLog( brain ) )
			return;

		if ( !force && !IsDueForPeriodicLog( brain ) )
			return;

		Log.Info( $"[BanditAI] {FormatBrain( brain )} VISION: {detail}" );
	}

	public static void LogMotor( ThornsBanditBrain brain, Vector3 wish, string detail )
	{
		if ( !ShouldLog( brain ) )
			return;

		if ( !IsDueForPeriodicLog( brain ) )
			return;

		var speed = wish.WithZ( 0 ).Length;
		Log.Info( $"[BanditAI] {FormatBrain( brain )} MOTOR wish={speed:F0} u/s {detail}" );
	}

	static bool ShouldLog( ThornsBanditBrain brain )
	{
		if ( !LogBehaviors || brain is null || !brain.IsValid() )
			return false;

		var filter = LogNameFilter?.Trim() ?? "";
		if ( filter.Length == 0 )
			return true;

		return brain.GameObject.Name.Contains( filter, StringComparison.OrdinalIgnoreCase );
	}

	static bool IsDueForPeriodicLog( ThornsBanditBrain brain )
	{
		var id = brain.GameObject.Id.GetHashCode();
		var now = Time.Now;
		var interval = Math.Max( 0.15f, LogIntervalSeconds );

		if ( NextPeriodicLogByBrainId.TryGetValue( id, out var next ) && now < next )
			return false;

		NextPeriodicLogByBrainId[id] = now + interval;
		return true;
	}

	static string FormatBrain( ThornsBanditBrain brain )
	{
		var pos = brain.GameObject.WorldPosition;
		return $"{brain.GameObject.Name}#{brain.GameObject.Id.ToString()[..8]} {brain.State} lod={brain.LodTier} @({pos.x:F0},{pos.y:F0})";
	}
}
