namespace Terraingen.Buildings.Settlement;

using Terraingen.Buildings;

/// <summary>Street-first vs legacy polar scatter for a single POI.</summary>
public enum SettlementLayoutMode
{
	Scatter,
	SmallTown,
	LargeSettlement
}

/// <summary>Candidate building pad facing a road segment.</summary>
public sealed class SettlementFrontageLot
{
	public Vector3 Position;
	public Rotation Rotation;
	public int RoadIndex;
	public float FrontageWidth;
	public int LotSpanWidth = 1;
	public int LotSpanDepth = 1;
	public int WidthCells = ThornsProcBuildingInterior.GridCells;
	public int DepthCells = ThornsProcBuildingInterior.GridCells;
	public ThornsProcBuildingType? ForcedBuildingType;
	public ThornsPoiIdentity Identity;
	public SettlementRoadType RoadType;
	public bool Accepted;
}

/// <summary>Site input for street graph generation (mirrors town center plan fields).</summary>
public readonly struct SettlementSitePlan
{
	public readonly Vector3 Center;
	public readonly ThornsPoiIdentity Identity;
	public readonly int TargetBuildingCount;
	public readonly float RadiusInches;
	public readonly float MinLotSpacingInches;
	public readonly bool IsMicro;

	public SettlementSitePlan(
		Vector3 center,
		ThornsPoiIdentity identity,
		int targetBuildingCount,
		float radiusInches,
		float minLotSpacingInches,
		bool isMicro )
	{
		Center = center;
		Identity = identity;
		TargetBuildingCount = targetBuildingCount;
		RadiusInches = radiusInches;
		MinLotSpacingInches = minLotSpacingInches;
		IsMicro = isMicro;
	}
}

/// <summary>Generated street graph and frontage candidates for one POI center.</summary>
public sealed class SettlementLayout
{
	public int SettlementIndex;
	public Vector3 Center;
	public ThornsPoiIdentity Identity;
	public SettlementLayoutMode Mode;
	public int TargetBuildingCount;
	public float BoundsRadius;
	public readonly List<SettlementRoadSegment> Roads = new();
	public readonly List<SettlementFrontageLot> Lots = new();

	public float TotalRoadLength
	{
		get
		{
			var sum = 0f;
			for ( var i = 0; i < Roads.Count; i++ )
			{
				var seg = Roads[i];
				sum += (seg.End - seg.Start).Length;
			}

			return sum;
		}
	}
}

public static class SettlementLayoutClassifier
{
	public static SettlementLayoutMode Classify(
		ThornsPoiIdentity identity,
		int targetBuildingCount,
		bool streetFirstEnabled )
	{
		if ( !streetFirstEnabled || targetBuildingCount < SettlementGridConstants.MiniGridMinBuildings )
			return SettlementLayoutMode.Scatter;

		if ( targetBuildingCount >= SettlementGridConstants.FullBlockMinBuildings )
			return SettlementLayoutMode.LargeSettlement;

		return SettlementLayoutMode.SmallTown;
	}

	public static bool IsSmallPoi( ThornsPoiIdentity identity ) =>
		identity is ThornsPoiIdentity.CabinSite or ThornsPoiIdentity.Farmstead;
}

public sealed class SettlementLayoutMetrics
{
	public int ScatterSettlements;
	public int SmallTownSettlements;
	public int LargeSettlements;
	public int TotalRoads;
	public int TotalFrontageLots;
	public int AcceptedLots;
	public int RejectedLots;
	public float TotalRoadLength;
	public float ReliefSum;
	public int ReliefSamples;
	public double GenerationMs;

	public float AverageRelief => ReliefSamples > 0 ? ReliefSum / ReliefSamples : 0f;

	public void LogSummary()
	{
		Log.Info(
			$"[Thorns Settlement] Layout summary: scatter={ScatterSettlements}, smallTown={SmallTownSettlements}, large={LargeSettlements}, "
			+ $"roads={TotalRoads}, frontageLots={TotalFrontageLots}, accepted={AcceptedLots}, rejected={RejectedLots}, "
			+ $"avgRelief={AverageRelief:0.0}, roadLength={TotalRoadLength:0}, genMs={GenerationMs:0.0}." );
	}
}
