namespace Terraingen.Core;

/// <summary>Runtime performance and verbosity toggles (console).</summary>
public static class ThornsPerfSettings
{
	[ConVar( "thorns_debug_hud" )]
	public static bool DebugHud { get; set; }

	[ConVar( "thorns_perf_trace" )]
	public static bool PerfTrace { get; set; }

	/// <summary>Hide gameplay HUD chrome — console <c>thorns_hide_hud 1</c> or hotkey 0.</summary>
	[ConVar( "thorns_hide_hud" )]
	public static bool HideHud { get; set; }

	public static bool ShouldShowDebugHud => DebugHud;
}
