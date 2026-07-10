using System.Linq;

namespace Sandbox;

/// <summary>World-gen diagnostics for <see cref="ThornsInteriorFurnitureScatter"/>.</summary>
public static class ThornsInteriorFurnitureScatterDebug
{
	/// <summary>Set false to silence per-building interior furniture logs.</summary>
	public static bool VerboseLogging { get; set; } = true;

	public enum RejectReason
	{
		None,
		InvalidArgs,
		CatalogMissing,
		TooSmallForCenterTile,
		NoWalkableCells,
		NoDecorCandidate,
		FootprintOutOfBounds,
		FootprintNoFloor,
		FootprintDoorOrRamp,
		FootprintInteriorWall,
		OverlapPeer,
		ModelLoadFailed,
		NetworkSpawnFailed
	}

	public sealed class StoryStats
	{
		public int WalkableCells;
		public int LayoutHostPresent;
		public int TargetMin;
		public int Placed;
		public int LayoutNormalOk;
		public int LayoutNormalFail;
		public int LayoutRelaxedOk;
		public int LayoutRelaxedFail;
		public int FillAttempts;
		public int FillOk;
		public int SpreadFillOk;
		public int AnchorFillOk;
		public readonly Dictionary<RejectReason, int> Rejects = new();

		public void AddReject( RejectReason reason )
		{
			if ( reason == RejectReason.None )
				return;

			Rejects.TryGetValue( reason, out var n );
			Rejects[reason] = n + 1;
		}

		public string FormatRejects() =>
			Rejects.Count == 0
				? "rejects=none"
				: "rejects=" + string.Join( ", ", Rejects.OrderByDescending( kv => kv.Value ).Select( kv => $"{kv.Key}={kv.Value}" ) );
	}

	public sealed class BuildingStats
	{
		public ThornsProcBuildingType Type;
		public int WidthCells;
		public int DepthCells;
		public int Stories;
		public Vector3 WorldPos;
		public readonly List<StoryStats> Floors = new();
		public int TotalPlaced;

		public void LogSummary()
		{
			if ( !VerboseLogging )
				return;

			var floorSummary = Floors.Count == 0
				? "floors=0"
				: string.Join( ", ", Floors.Select( ( f, i ) => $"s{i}={f.Placed}/{f.TargetMin}" ) );

			Log.Info(
				$"[Thorns InteriorFurniture] {Type} {WidthCells}x{DepthCells}x{Stories} total={TotalPlaced} {floorSummary} @ {WorldPos}" );

			for ( var s = 0; s < Floors.Count; s++ )
			{
				var floor = Floors[s];
				if ( floor.Placed >= floor.TargetMin && floor.Rejects.Count == 0 )
					continue;

				Log.Info(
					$"[Thorns InteriorFurniture]   story={s} detail placed={floor.Placed}/{floor.TargetMin} "
					+ $"walkable={floor.WalkableCells} layoutHost={floor.LayoutHostPresent != 0} "
					+ $"layoutOk={floor.LayoutNormalOk}+{floor.LayoutRelaxedOk} layoutFail={floor.LayoutNormalFail}+{floor.LayoutRelaxedFail} "
					+ $"spreadFill={floor.SpreadFillOk}/{floor.FillAttempts} {floor.FormatRejects()}" );
			}

			if ( TotalPlaced == 0 )
			{
				Log.Warning(
					$"[Thorns InteriorFurniture] ZERO props in {Type} {WidthCells}x{DepthCells}x{Stories} layoutHost={Floors.FirstOrDefault()?.LayoutHostPresent ?? 0} @ {WorldPos}" );
			}
		}
	}

	public static int CountWalkableFloorCells(
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex )
	{
		var count = 0;
		for ( var gx = 0; gx < widthCells; gx++ )
		for ( var gy = 0; gy < depthCells; gy++ )
		{
			if ( ThornsProcBuildingInteriorSample.CellHasFloorSlab(
				     storyIndex,
				     gx,
				     gy,
				     widthCells,
				     depthCells,
				     stories,
				     buildingRoot ) )
				count++;
		}

		return count;
	}
}
