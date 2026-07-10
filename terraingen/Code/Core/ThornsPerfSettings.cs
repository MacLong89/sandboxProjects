namespace Terraingen.Core;

/// <summary>Runtime performance and verbosity toggles (console).</summary>
public static class ThornsPerfSettings
{
	[ConVar( "thorns_debug_hud" )]
	public static bool DebugHud { get; set; }

	[ConVar( "thorns_perf_trace" )]
	public static bool PerfTrace { get; set; }

	public static bool ShouldShowDebugHud => DebugHud;
}
