using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Large-radius settlement site evaluation: horizon sectors, regional continuity, retainability.
/// Answers "should a settlement exist here?" before any terrain shaping runs.
/// </summary>
public static class ThornsWorldSettlementSiteAnalysis
{
	public const int HorizonSectorCount = 8;

	public static bool CollectDebug { get; set; }

	public static ThornsWorldSettlementSiteEvaluation? LastCityEvaluation { get; private set; }
	public static IReadOnlyList<ThornsWorldSettlementSiteCandidateDebug> LastCityCandidates { get; private set; } =
		Array.Empty<ThornsWorldSettlementSiteCandidateDebug>();

	static readonly List<ThornsWorldSettlementSiteCandidateDebug> _cityCandidates = new( 160 );

	public static ThornsWorldSettlementSiteEvaluation EvaluateCity(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly )
	{
		var profile = CityProfile;
		return Evaluate( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, in profile );
	}

	public static ThornsWorldSettlementSiteEvaluation EvaluateTown(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly )
	{
		var profile = TownProfile;
		return Evaluate( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, in profile );
	}

	public static void ClearDebug()
	{
		_cityCandidates.Clear();
		LastCityCandidates = Array.Empty<ThornsWorldSettlementSiteCandidateDebug>();
		LastCityEvaluation = null;
	}

	public static void RecordCityCandidate(
		float lx,
		float ly,
		in ThornsWorldSettlementSiteEvaluation eval,
		bool selected )
	{
		if ( !CollectDebug || _cityCandidates.Count >= 140 )
			return;

		_cityCandidates.Add( new ThornsWorldSettlementSiteCandidateDebug
		{
			LocalX = lx,
			LocalY = ly,
			Accepted = eval.Acceptable,
			Selected = selected,
			RejectReason = eval.RejectReason,
			CompositeScore = eval.CompositeScore,
			HorizonBalance = eval.HorizonBalance,
			Continuity = eval.Continuity,
			BasinQuality = eval.BasinQuality,
			Retainability = eval.Retainability,
			SectorMeans = eval.SectorMeans
		} );
	}

	public static void FinalizeCityDebug( in ThornsWorldSettlementSiteEvaluation chosen )
	{
		LastCityEvaluation = chosen;
		LastCityCandidates = _cityCandidates.ToArray();
	}

	static readonly SiteProfile CityProfile = new()
	{
		LocalFlatRadius = 520f,
		LocalMaxDelta = 38f,
		MacroInner = 1100f,
		MacroMid = 2000f,
		MacroOuter = 3400f,
		HorizonDistances = new[] { 650f, 1200f, 1800f, 2500f, 3200f },
		MaxRegionalDelta = 58f,
		MaxMacroDelta = 92f,
		MaxSectorOpposingDelta = 48f,
		MaxOneSidedDrop = 78f,
		MaxCenterShelfLift = 42f,
		MinRetainability = 0.42f,
		MaxCliffFraction = 0.28f,
		MaxRidgeScore = 0.62f,
		MinContinuity = 0.38f,
		MinHorizonBalance = 0.34f
	};

	static readonly SiteProfile TownProfile = new()
	{
		LocalFlatRadius = 380f,
		LocalMaxDelta = 36f,
		MacroInner = 820f,
		MacroMid = 1400f,
		MacroOuter = 2400f,
		HorizonDistances = new[] { 480f, 900f, 1350f, 1900f, 2400f },
		MaxRegionalDelta = 48f,
		MaxMacroDelta = 72f,
		MaxSectorOpposingDelta = 56f,
		MaxOneSidedDrop = 88f,
		MaxCenterShelfLift = 48f,
		MinRetainability = 0.32f,
		MaxCliffFraction = 0.34f,
		MaxRidgeScore = 0.72f,
		MinContinuity = 0.30f,
		MinHorizonBalance = 0.28f
	};

	static ThornsWorldSettlementSiteEvaluation Evaluate(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		in SiteProfile profile )
	{
		var result = new ThornsWorldSettlementSiteEvaluation
		{
			LocalX = lx,
			LocalY = ly
		};

		if ( !TryLocalFlatness( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, profile.LocalFlatRadius, profile.LocalMaxDelta, out var localFlat ) )
		{
			result.RejectReason = ThornsWorldSettlementSiteRejectReason.LocalNotFlat;
			return result;
		}

		result.LocalFlatness = localFlat;

		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, out var centerH ) )
		{
			result.RejectReason = ThornsWorldSettlementSiteRejectReason.InvalidSample;
			return result;
		}

		result.CenterHeight = centerH;

		var macro = ScoreMacroRegion(
			heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, centerH, in profile, out var macroReject );
		result.MacroVariance = macro.Variance01;
		result.MacroDelta = macro.MaxDelta;
		if ( macroReject != ThornsWorldSettlementSiteRejectReason.None )
		{
			result.RejectReason = macroReject;
			return result;
		}

		var horizon = ScoreHorizon(
			heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, centerH, in profile );
		result.HorizonBalance = horizon.Balance01;
		result.SectorMeans = horizon.SectorMeans;
		result.MaxSectorDrop = horizon.MaxDrop;
		result.HorizonAsymmetry = horizon.Asymmetry01;

		if ( horizon.Reject != ThornsWorldSettlementSiteRejectReason.None )
		{
			result.RejectReason = horizon.Reject;
			return result;
		}

		var continuity = ScoreContinuity(
			heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, centerH, in profile );
		result.Continuity = continuity.Score01;
		result.RidgeScore = continuity.Ridge01;
		result.BasinQuality = continuity.Basin01;

		if ( continuity.Reject != ThornsWorldSettlementSiteRejectReason.None )
		{
			result.RejectReason = continuity.Reject;
			return result;
		}

		var retain = ScoreRetainability( centerH, in macro, in horizon, in continuity, in profile );
		result.Retainability = retain.Score01;
		result.EstimatedShapingWork = retain.WorkUnits;

		if ( retain.Reject != ThornsWorldSettlementSiteRejectReason.None )
		{
			result.RejectReason = retain.Reject;
			return result;
		}

		if ( TryLegacyShelfReject( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, centerH, in profile ) )
		{
			result.RejectReason = ThornsWorldSettlementSiteRejectReason.ShelfOrSidehill;
			return result;
		}

		result.CompositeScore =
			localFlat * 0.85f
			+ continuity.Score01 * 1.55f
			+ continuity.Basin01 * 1.25f
			+ horizon.Balance01 * 1.35f
			+ retain.Score01 * 2.1f
			- horizon.Asymmetry01 * 0.55f
			- continuity.Ridge01 * 0.65f;

		result.Acceptable = true;
		result.RejectReason = ThornsWorldSettlementSiteRejectReason.None;
		return result;
	}

	static bool TryLocalFlatness(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float radius,
		float maxDelta,
		out float score01 )
	{
		score01 = 0f;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, out var c ) )
			return false;

		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + radius, ly, out var e ) ) return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx - radius, ly, out var w ) ) return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly + radius, out var n ) ) return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly - radius, out var s ) ) return false;

		var delta = MathF.Max( MathF.Max( e, w ), MathF.Max( n, s ) ) - MathF.Min( MathF.Min( e, w ), MathF.Min( n, s ) );
		if ( delta > maxDelta )
			return false;

		score01 = Math.Clamp( 1f - delta / maxDelta, 0f, 1f );
		return true;
	}

	static MacroRegionResult ScoreMacroRegion(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float centerH,
		in SiteProfile profile,
		out ThornsWorldSettlementSiteRejectReason reject )
	{
		reject = ThornsWorldSettlementSiteRejectReason.None;
		var samples = new List<float>( 72 );
		const int ringSamples = 20;

		SampleRing( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, profile.MacroInner, ringSamples, samples );
		SampleRing( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, profile.MacroMid, ringSamples, samples );
		SampleRing( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, profile.MacroOuter, ringSamples, samples );

		if ( samples.Count < 24 )
		{
			reject = ThornsWorldSettlementSiteRejectReason.InvalidSample;
			return default;
		}

		var minH = centerH;
		var maxH = centerH;
		var cliffSteps = 0;
		for ( var i = 0; i < samples.Count; i++ )
		{
			var h = samples[i];
			minH = MathF.Min( minH, h );
			maxH = MathF.Max( maxH, h );
			if ( MathF.Abs( h - centerH ) > profile.MaxRegionalDelta * 0.8f )
				cliffSteps++;
		}

		var delta = maxH - minH;
		var cliffFrac = cliffSteps / (float)samples.Count;

		if ( delta > profile.MaxMacroDelta )
		{
			reject = ThornsWorldSettlementSiteRejectReason.RegionalDiscontinuity;
			return new MacroRegionResult { MaxDelta = delta, Variance01 = 0f };
		}

		if ( cliffFrac > profile.MaxCliffFraction )
		{
			reject = ThornsWorldSettlementSiteRejectReason.RegionalCliffBand;
			return new MacroRegionResult { MaxDelta = delta, Variance01 = 0f };
		}

		return new MacroRegionResult
		{
			MaxDelta = delta,
			Variance01 = Math.Clamp( 1f - delta / profile.MaxMacroDelta, 0f, 1f ),
			OuterMean = AverageRing( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, profile.MacroOuter ),
			MidMean = AverageRing( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, profile.MacroMid ),
			CliffFraction = cliffFrac
		};
	}

	static HorizonResult ScoreHorizon(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float centerH,
		in SiteProfile profile )
	{
		var sectorMeans = new float[HorizonSectorCount];
		var sectorDrops = new float[HorizonSectorCount];
		var sectorCliffs = new int[HorizonSectorCount];
		var maxDrop = 0f;
		var asymmetrySum = 0f;

		for ( var s = 0; s < HorizonSectorCount; s++ )
		{
			var ang = s * ( MathF.PI * 2f / HorizonSectorCount );
			var dirX = MathF.Cos( ang );
			var dirY = MathF.Sin( ang );
			var sum = 0f;
			var count = 0;
			var prevH = centerH;
			var cliffs = 0;

			for ( var d = 0; d < profile.HorizonDistances.Length; d++ )
			{
				var dist = profile.HorizonDistances[d];
				if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + dirX * dist, ly + dirY * dist, out var h ) )
					continue;

				sum += h;
				count++;
				var step = MathF.Abs( h - prevH );
				var stepDist = d == 0 ? dist : dist - profile.HorizonDistances[d - 1];
				if ( step > profile.MaxRegionalDelta * 0.75f && stepDist < dist * 0.55f )
					cliffs++;

				prevH = h;
			}

			if ( count < 2 )
				continue;

			var mean = sum / count;
			sectorMeans[s] = mean;
			sectorDrops[s] = centerH - mean;
			sectorCliffs[s] = cliffs;
			maxDrop = MathF.Max( maxDrop, MathF.Max( 0f, centerH - mean ) );
		}

		for ( var s = 0; s < HorizonSectorCount / 2; s++ )
		{
			var opp = s + HorizonSectorCount / 2;
			var delta = MathF.Abs( sectorMeans[s] - sectorMeans[opp] );
			asymmetrySum += delta;
			if ( delta > profile.MaxSectorOpposingDelta
			     && ( sectorCliffs[s] > 0 || sectorCliffs[opp] > 0 ) )
			{
				return new HorizonResult
				{
					Reject = ThornsWorldSettlementSiteRejectReason.HorizonAsymmetry,
					SectorMeans = sectorMeans
				};
			}
		}

		var asymmetry01 = Math.Clamp( asymmetrySum / ( profile.MaxSectorOpposingDelta * 4f ), 0f, 1f );
		var highSectors = 0;
		var lowSectors = 0;
		for ( var s = 0; s < HorizonSectorCount; s++ )
		{
			if ( sectorDrops[s] > profile.MaxOneSidedDrop * 0.55f )
				highSectors++;
			if ( sectorDrops[s] < -profile.MaxOneSidedDrop * 0.35f )
				lowSectors++;
		}

		if ( highSectors >= 2 && lowSectors <= 1 && maxDrop > profile.MaxOneSidedDrop )
		{
			return new HorizonResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.OneSidedCliff,
				SectorMeans = sectorMeans,
				MaxDrop = maxDrop,
				Asymmetry01 = asymmetry01
			};
		}

		var centerLift = centerH - Average( sectorMeans );
		if ( centerLift > profile.MaxCenterShelfLift && highSectors >= 3 )
		{
			return new HorizonResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.TerrainShelf,
				SectorMeans = sectorMeans,
				MaxDrop = maxDrop,
				Asymmetry01 = asymmetry01
			};
		}

		var balance01 = Math.Clamp(
			1f - asymmetry01 * 0.65f - maxDrop / profile.MaxOneSidedDrop * 0.35f,
			0f,
			1f );

		if ( balance01 < profile.MinHorizonBalance )
		{
			return new HorizonResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.HorizonAsymmetry,
				SectorMeans = sectorMeans,
				Balance01 = balance01,
				MaxDrop = maxDrop,
				Asymmetry01 = asymmetry01
			};
		}

		return new HorizonResult
		{
			SectorMeans = sectorMeans,
			Balance01 = balance01,
			MaxDrop = maxDrop,
			Asymmetry01 = asymmetry01
		};
	}

	static ContinuityResult ScoreContinuity(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float centerH,
		in SiteProfile profile )
	{
		var gridSpan = profile.MacroOuter * 0.72f;
		const int gridSteps = 5;
		var step = gridSpan * 2f / gridSteps;
		var heightsGrid = new List<float>( gridSteps * gridSteps );
		var totalCells = 0;

		for ( var iz = -gridSteps; iz <= gridSteps; iz++ )
		for ( var ix = -gridSteps; ix <= gridSteps; ix++ )
		{
			var px = lx + ix * step;
			var py = ly + iz * step;
			if ( px * px + py * py > gridSpan * gridSpan )
				continue;

			if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, px, py, out var h ) )
				continue;

			heightsGrid.Add( h );
			totalCells++;
		}

		if ( totalCells < 12 )
		{
			return new ContinuityResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.InvalidSample
			};
		}

		var minH = heightsGrid[0];
		var maxH = heightsGrid[0];
		var sumH = 0f;
		for ( var i = 0; i < heightsGrid.Count; i++ )
		{
			var h = heightsGrid[i];
			minH = MathF.Min( minH, h );
			maxH = MathF.Max( maxH, h );
			sumH += h;
		}

		var mean = sumH / heightsGrid.Count;
		var variance = 0f;
		for ( var i = 0; i < heightsGrid.Count; i++ )
		{
			var d = heightsGrid[i] - mean;
			variance += d * d;
		}

		variance /= heightsGrid.Count;
		var std = MathF.Sqrt( variance );
		var ridge01 = Math.Clamp( ( maxH - minH ) / profile.MaxMacroDelta, 0f, 1f );
		var basin01 = Math.Clamp( ( mean - minH ) / MathF.Max( std, 8f ) * 0.35f + ( centerH <= mean + 12f ? 0.25f : 0f ), 0f, 1f );

		if ( ridge01 > profile.MaxRidgeScore )
		{
			return new ContinuityResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.RidgeOrMountainEdge,
				Ridge01 = ridge01
			};
		}

		var continuity01 = Math.Clamp(
			1f - ridge01 * 0.55f - std / profile.MaxMacroDelta * 0.45f,
			0f,
			1f );

		if ( continuity01 < profile.MinContinuity )
		{
			return new ContinuityResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.LowContinuity,
				Score01 = continuity01,
				Ridge01 = ridge01,
				Basin01 = basin01
			};
		}

		return new ContinuityResult
		{
			Score01 = continuity01,
			Ridge01 = ridge01,
			Basin01 = basin01
		};
	}

	static RetainabilityResult ScoreRetainability(
		float centerH,
		in MacroRegionResult macro,
		in HorizonResult horizon,
		in ContinuityResult continuity,
		in SiteProfile profile )
	{
		var shave = MathF.Max( 0f, centerH - macro.OuterMean );
		var fill = MathF.Max( 0f, macro.MidMean - centerH ) * 0.65f;
		var work =
			shave * 1.15f
			+ fill
			+ macro.CliffFraction * 140f
			+ horizon.Asymmetry01 * 85f
			+ horizon.MaxDrop * 0.55f
			+ continuity.Ridge01 * 70f;

		var maxWork = profile.MaxMacroDelta * 2.2f + 95f;
		var score01 = Math.Clamp( 1f - work / maxWork, 0f, 1f );

		if ( score01 < profile.MinRetainability )
		{
			return new RetainabilityResult
			{
				Reject = ThornsWorldSettlementSiteRejectReason.LowRetainability,
				Score01 = score01,
				WorkUnits = work
			};
		}

		return new RetainabilityResult
		{
			Score01 = score01,
			WorkUnits = work
		};
	}

	static bool TryLegacyShelfReject(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float centerH,
		in SiteProfile profile )
	{
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly + profile.MacroMid, out var n ) )
			return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly - profile.MacroMid, out var s ) )
			return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + profile.MacroMid, ly, out var e ) )
			return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx - profile.MacroMid, ly, out var w ) )
			return false;

		var ns = MathF.Abs( n - s );
		var ew = MathF.Abs( e - w );
		var axisMin = MathF.Min( ns, ew );
		var axisMax = MathF.Max( ns, ew );
		if ( axisMin > 22f && axisMax > axisMin * 1.7f && axisMax > profile.MaxRegionalDelta * 0.65f )
			return true;

		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly + profile.MacroOuter, out var nOut ) )
			return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly - profile.MacroOuter, out var sOut ) )
			return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + profile.MacroOuter, ly, out var eOut ) )
			return false;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx - profile.MacroOuter, ly, out var wOut ) )
			return false;

		var ringMean = (nOut + sOut + eOut + wOut) * 0.25f;
		var lift = centerH - ringMean;
		var spread = MathF.Max(
			MathF.Max( nOut, sOut ),
			MathF.Max( eOut, wOut ) ) - MathF.Min(
			MathF.Min( nOut, sOut ),
			MathF.Min( eOut, wOut ) );

		return lift > profile.MaxCenterShelfLift && spread < profile.MaxRegionalDelta * 1.1f;
	}

	static void SampleRing(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float radius,
		int count,
		List<float> into )
	{
		for ( var i = 0; i < count; i++ )
		{
			var ang = i * ( MathF.PI * 2f / count );
			if ( Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + MathF.Cos( ang ) * radius, ly + MathF.Sin( ang ) * radius, out var h ) )
				into.Add( h );
		}
	}

	static float AverageRing(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float radius )
	{
		var sum = 0f;
		var count = 0;
		const int n = 16;
		for ( var i = 0; i < n; i++ )
		{
			var ang = i * ( MathF.PI * 2f / n );
			if ( Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + MathF.Cos( ang ) * radius, ly + MathF.Sin( ang ) * radius, out var h ) )
			{
				sum += h;
				count++;
			}
		}

		return count > 0 ? sum / count : 0f;
	}

	static float Average( float[] values )
	{
		if ( values is null || values.Length == 0 )
			return 0f;

		var sum = 0f;
		var count = 0;
		for ( var i = 0; i < values.Length; i++ )
		{
			if ( values[i] == 0f )
				continue;
			sum += values[i];
			count++;
		}

		return count > 0 ? sum / count : 0f;
	}

	static bool Sample(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		out float hz )
	{
		hz = ThornsTerrainGeometry.SampleHeightLocalZUp( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );
		return !float.IsNaN( hz ) && !float.IsInfinity( hz );
	}

	struct SiteProfile
	{
		public float LocalFlatRadius;
		public float LocalMaxDelta;
		public float MacroInner;
		public float MacroMid;
		public float MacroOuter;
		public float[] HorizonDistances;
		public float MaxRegionalDelta;
		public float MaxMacroDelta;
		public float MaxSectorOpposingDelta;
		public float MaxOneSidedDrop;
		public float MaxCenterShelfLift;
		public float MinRetainability;
		public float MaxCliffFraction;
		public float MaxRidgeScore;
		public float MinContinuity;
		public float MinHorizonBalance;
	}

	struct MacroRegionResult
	{
		public float MaxDelta;
		public float Variance01;
		public float OuterMean;
		public float MidMean;
		public float CliffFraction;
	}

	struct HorizonResult
	{
		public ThornsWorldSettlementSiteRejectReason Reject;
		public float[] SectorMeans;
		public float Balance01;
		public float MaxDrop;
		public float Asymmetry01;
	}

	struct ContinuityResult
	{
		public ThornsWorldSettlementSiteRejectReason Reject;
		public float Score01;
		public float Ridge01;
		public float Basin01;
	}

	struct RetainabilityResult
	{
		public ThornsWorldSettlementSiteRejectReason Reject;
		public float Score01;
		public float WorkUnits;
	}
}

public enum ThornsWorldSettlementSiteRejectReason
{
	None,
	LocalNotFlat,
	InvalidSample,
	RegionalDiscontinuity,
	RegionalCliffBand,
	HorizonAsymmetry,
	OneSidedCliff,
	TerrainShelf,
	RidgeOrMountainEdge,
	LowContinuity,
	LowRetainability,
	ShelfOrSidehill
}

public struct ThornsWorldSettlementSiteEvaluation
{
	public float LocalX { get; init; }
	public float LocalY { get; init; }
	public bool Acceptable { get; set; }
	public float CompositeScore { get; set; }
	public ThornsWorldSettlementSiteRejectReason RejectReason { get; set; }
	public float LocalFlatness { get; set; }
	public float CenterHeight { get; set; }
	public float MacroVariance { get; set; }
	public float MacroDelta { get; set; }
	public float HorizonBalance { get; set; }
	public float HorizonAsymmetry { get; set; }
	public float MaxSectorDrop { get; set; }
	public float Continuity { get; set; }
	public float BasinQuality { get; set; }
	public float RidgeScore { get; set; }
	public float Retainability { get; set; }
	public float EstimatedShapingWork { get; set; }
	public float[] SectorMeans { get; set; }
}

public struct ThornsWorldSettlementSiteCandidateDebug
{
	public float LocalX { get; init; }
	public float LocalY { get; init; }
	public bool Accepted { get; init; }
	public bool Selected { get; init; }
	public ThornsWorldSettlementSiteRejectReason RejectReason { get; init; }
	public float CompositeScore { get; init; }
	public float HorizonBalance { get; init; }
	public float Continuity { get; init; }
	public float BasinQuality { get; init; }
	public float Retainability { get; init; }
	public float[] SectorMeans { get; init; }
}
