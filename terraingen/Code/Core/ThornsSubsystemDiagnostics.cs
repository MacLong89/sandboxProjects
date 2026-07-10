namespace Terraingen.Core;

using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.TerrainGen;

/// <summary>Lightweight subsystem health snapshot for debug HUD and future profiling.</summary>
public static class ThornsSubsystemDiagnostics
{
	public static string BuildSummary( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return "Subsystems: invalid scene";

		var foliage = scene.GetAllComponents<ThornsFoliageFoundation>().FirstOrDefault();
		var minerals = scene.GetAllComponents<ThornsMineralFoundation>().FirstOrDefault();
		var grass = scene.GetAllComponents<ClientGrassRenderer>().FirstOrDefault();

		var foliageReady = foliage.IsValid() ? foliage.GetHudSummary() : "foliage=—";
		var mineralReady = minerals.IsValid() ? minerals.GetDebugSummary() : "minerals=—";
		var grassReady = grass.IsValid() ? grass.GetDebugSummary() : "grass=—";

		return $"{foliageReady} | {mineralReady} | {grassReady}";
	}
}
