namespace Sandbox;

/// <summary>Localized foundation aprons — density-aware, cooperative overlap, block-surface first.</summary>
public static class ThornsBuildingFoundationTerrain
{
	public const float DefaultEmbedCity = 12f;
	public const float DefaultEmbedTown = 10f;
	public const float DefaultEmbedIsolated = 8f;
	public const float DownhillFillBoost = 1.38f;
	public const float CornerWeightBoost = 1.28f;
	public const float DoorWeightBoost = 1.42f;
	public const float MaxCooperativeBlend = 0.68f;
	public const float SparseMaxCooperativeBlend = 0.68f;

	/// <summary>Max height delta (inches) between adjacent heightmap samples after local pad sculpting.</summary>
	public const float PadRimMaxNeighborStepInches = 28f;

	public const int PadRimSoftenPasses = 3;

	const int MaxContributionsPerCell = 8;
	const float ReliefApronScaleInches = 140f;
	const float MaxReliefApronMul = 5.5f;

	public static bool IsLocalFoundationPad( in ThornsTerrainProcBuildingPad pad ) =>
		pad.Kind == ThornsSettlementTerrainPadKind.LocalBuilding
		&& !IsMacroLike( in pad );

	static bool IsMacroLike( in ThornsTerrainProcBuildingPad pad ) =>
		pad.Kind == ThornsSettlementTerrainPadKind.MacroSettlement
		|| pad.Kind == ThornsSettlementTerrainPadKind.HubPlateau
		|| ( pad.YawRadians == 0f && pad.HalfW == pad.HalfD && pad.HalfW >= 400f && pad.PeakBlend < 0.99f );

	public static float ComputeApronStrengthMul( int blockBuildingCount ) =>
		ThornsSettlementDensityRestraint.Compute( blockBuildingCount ).ApronStrengthMul;

	public static Vector2 DoorOutwardWorld( int doorSide, float yawRadians )
	{
		var (lx, ly) = doorSide switch
		{
			0 => (0f, -1f),
			2 => (0f, 1f),
			3 => (-1f, 0f),
			1 => (1f, 0f),
			_ => (0f, -1f)
		};
		var c = MathF.Cos( yawRadians );
		var s = MathF.Sin( yawRadians );
		return new Vector2( lx * c - ly * s, lx * s + ly * c );
	}

	/// <summary>Apply all local foundation pads at a heightmap cell with overlap cooperation.</summary>
	public static bool TryApplyCell(
		List<ThornsTerrainProcBuildingPad> pads,
		in ThornsTerrainNetSpec spec,
		float wx,
		float wy,
		float hNatural,
		out float hOut )
	{
		hOut = hNatural;
		if ( pads is null || pads.Count == 0 )
			return false;

		var denseBlockInterior = false;
		var blockCount = 0;
		var restraint = ThornsSettlementDensityRestraint.Compute( 0 );
		if ( ThornsWorldSettlementBlockTerrain.TrySampleBlockSurfaceBlend(
			     spec.SettlementBlockTerrain,
			     wx,
			     wy,
			     out _,
			     out var blockW ) )
		{
			for ( var b = 0; b < spec.SettlementBlockTerrain.Count; b++ )
			{
				var blk = spec.SettlementBlockTerrain[b];
				if ( TryBlockContains( blk, wx, wy ) )
				{
					blockCount = Math.Max( blockCount, blk.BuildingCount );
					break;
				}
			}

			restraint = ThornsSettlementDensityRestraint.Compute( blockCount );
			denseBlockInterior = blockW > 0.28f && restraint.PreferBlockSurfaceOnly;
		}

		Span<Contribution> contribs = stackalloc Contribution[MaxContributionsPerCell];
		var n = 0;
		for ( var p = 0; p < pads.Count && n < MaxContributionsPerCell; p++ )
		{
			var pad = pads[p];
			if ( !pad.SculptHeightmap || !IsLocalFoundationPad( in pad ) )
				continue;

			if ( !TryEvaluate( in pad, wx, wy, hNatural, out var supportZ, out var w, out var isInterior, out var isDoorZone ) )
				continue;

			if ( denseBlockInterior && !isDoorZone )
				continue;

			contribs[n++] = new Contribution( supportZ, w, isInterior );
		}

		if ( n == 0 )
			return false;

		MergeContributions(
			contribs,
			n,
			restraint.MaxCooperativeBlend,
			out var mergedZ,
			out var mergedW,
			out var mergedInterior );
		hOut = ApplyHeight( hNatural, mergedZ, mergedW, mergedInterior );
		return true;
	}

	static bool TryBlockContains( ThornsSettlementBlockTerrainNet block, float wx, float wy )
	{
		var dx = wx - block.CenterX;
		var dy = wy - block.CenterY;
		var c = MathF.Cos( -block.YawRadians );
		var s = MathF.Sin( -block.YawRadians );
		var bx = dx * c - dy * s;
		var by = dx * s + dy * c;
		return MathF.Abs( bx ) <= block.HalfW * 1.05f && MathF.Abs( by ) <= block.HalfD * 1.05f;
	}

	public static bool TryEvaluate(
		in ThornsTerrainProcBuildingPad pad,
		float wx,
		float wy,
		float hNatural,
		out float supportZ,
		out float blendWeight,
		out bool isInterior,
		out bool isDoorZone )
	{
		supportZ = pad.TargetZ;
		blendWeight = 0f;
		isInterior = false;
		isDoorZone = false;

		if ( !IsLocalFoundationPad( in pad ) )
			return false;

		var densityMul = Math.Clamp( pad.ApronStrengthMul, 0.05f, 1f );
		var peakCap = Math.Clamp( pad.PeakBlend, 0.12f, 1f );
		var fw = pad.FoundationHalfW > 1f ? pad.FoundationHalfW : pad.HalfW * 0.82f;
		var fd = pad.FoundationHalfD > 1f ? pad.FoundationHalfD : pad.HalfD * 0.82f;
		var embed = MathF.Max( 0f, pad.FoundationEmbed ) * (densityMul < 0.35f ? 0.2f : densityMul < 0.55f ? 0.45f : 1f);
		var floorZ = pad.TargetZ;
		var relief = MathF.Abs( hNatural - floorZ );
		var reliefMul = 1f + Math.Clamp( relief / ReliefApronScaleInches, 0f, MaxReliefApronMul - 1f );
		var wallApron = MathF.Max( 12f, pad.WallApron ) * (0.35f + densityMul * 0.45f ) * reliefMul;
		var outerApron = MathF.Max( wallApron + 8f, pad.Apron ) * (0.22f + densityMul * 0.48f ) * reliefMul;

		var dx = wx - pad.CenterX;
		var dy = wy - pad.CenterY;
		var c = MathF.Cos( -pad.YawRadians );
		var s = MathF.Sin( -pad.YawRadians );
		var bx = dx * c - dy * s;
		var by = dx * s + dy * c;

		var ox = MathF.Max( MathF.Abs( bx ) - fw, 0f );
		var oy = MathF.Max( MathF.Abs( by ) - fd, 0f );
		var dist = MathF.Sqrt( ox * ox + oy * oy );

		if ( ox <= 0f && oy <= 0f )
		{
			isInterior = true;
			supportZ = pad.TargetZ - embed;
			// Dense settlement blocks use weak interior sculpting; dedicated organic plots always scrape flat.
			if ( relief > 220f && peakCap < 0.99f )
				blendWeight = Math.Clamp( 0.42f + 220f / relief * 0.35f, 0.42f, 0.82f );
			else
				blendWeight = densityMul < 0.35f
					? Math.Clamp( peakCap * densityMul, 0.08f, 0.42f )
					: 1f;
			return true;
		}

		if ( dist >= outerApron )
			return false;

		var w = 0f;
		if ( dist <= wallApron )
		{
			var t = dist / MathF.Max( wallApron, 1f );
			w = (1f - SmootherStep( t )) * peakCap * densityMul;
			supportZ = floorZ - embed * (1f - t * 0.5f);
		}
		else
		{
			var t = (dist - wallApron) / MathF.Max( outerApron - wallApron, 1f );
			w = (1f - SmootherStep( t )) * peakCap * densityMul * 0.28f;
			var uplift = densityMul < 0.35f ? 0.08f : 0.22f;
			supportZ = floorZ + (hNatural - floorZ) * SmootherStep( t ) * uplift;
		}

		if ( ox > 0.5f && oy > 0.5f && densityMul > 0.35f )
			w *= 1f + (CornerWeightBoost - 1f) * densityMul;

		if ( pad.DoorOutwardX * pad.DoorOutwardX + pad.DoorOutwardY * pad.DoorOutwardY > 0.01f )
		{
			var toX = wx - pad.CenterX;
			var toY = wy - pad.CenterY;
			var len = MathF.Sqrt( toX * toX + toY * toY );
			if ( len > 1f )
			{
				toX /= len;
				toY /= len;
				var doorAlign = toX * pad.DoorOutwardX + toY * pad.DoorOutwardY;
				if ( doorAlign > 0.55f && dist < wallApron * 1.2f )
				{
					w *= 1f + (DoorWeightBoost - 1f) * MathF.Max( densityMul, 0.35f );
					isDoorZone = true;
				}
			}
		}

		if ( hNatural < floorZ - 2f && densityMul > 0.35f )
		{
			w *= 1f + (DownhillFillBoost - 1f) * densityMul;
			supportZ = MathF.Max( supportZ, floorZ - embed * 0.18f );
		}

		blendWeight = Math.Clamp( w, 0f, 1f );
		return blendWeight > 0.001f;
	}

	static void MergeContributions(
		ReadOnlySpan<Contribution> contribs,
		int count,
		float maxCooperativeBlend,
		out float supportZ,
		out float blendWeight,
		out bool isInterior )
	{
		if ( count == 1 )
		{
			supportZ = contribs[0].SupportZ;
			blendWeight = contribs[0].Weight;
			isInterior = contribs[0].IsInterior;
			return;
		}

		var overlapDamp = 1f / MathF.Sqrt( count );
		blendWeight = 0f;
		var zSum = 0f;
		var wSum = 0f;
		isInterior = true;
		for ( var i = 0; i < count; i++ )
		{
			var wi = contribs[i].Weight * overlapDamp;
			blendWeight = 1f - (1f - blendWeight) * (1f - wi);
			zSum += contribs[i].SupportZ * wi;
			wSum += wi;
			if ( !contribs[i].IsInterior )
				isInterior = false;
		}

		blendWeight = MathF.Min( blendWeight, MathF.Max( 0.12f, maxCooperativeBlend ) );
		supportZ = wSum > 0.001f ? zSum / wSum : contribs[0].SupportZ;
	}

	public static float ApplyHeight( float hNatural, float supportZ, float blendWeight, bool isInterior )
	{
		if ( blendWeight <= 0f )
			return hNatural;

		if ( isInterior )
			return supportZ;

		if ( blendWeight >= 0.98f )
			return supportZ;

		var blended = hNatural + (supportZ - hNatural) * blendWeight;
		var relief = MathF.Abs( supportZ - hNatural );
		var maxDelta = 36f + relief * 0.35f;
		var delta = blended - hNatural;
		if ( MathF.Abs( delta ) > maxDelta )
			blended = hNatural + MathF.Sign( delta ) * maxDelta;

		return blended;
	}

	/// <summary>Clamp sheer cliffs at proc-building pad rims back toward natural terrain.</summary>
	public static void SoftenHeightmapRimsAfterPads(
		in ThornsTerrainNetSpec spec,
		Span<float> heights,
		ReadOnlySpan<float> naturalHeights )
	{
		var pads = spec.ProcBuildingTerrainPads;
		if ( pads is null || pads.Count == 0 || heights.IsEmpty || naturalHeights.Length < heights.Length )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		if ( heights.Length < rx * rz )
			return;

		ThornsTerrainGeometry.GetExtents( spec, out var worldW, out var worldD );
		var cellX = worldW / (rx - 1f);
		var cellY = worldD / (rz - 1f);
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;
		var maxStep = MathF.Max( PadRimMaxNeighborStepInches, MathF.Max( cellX, cellY ) * 0.48f );
		var spikeStep = MathF.Max( cellX, cellY ) * ThornsTerrainHeightRepair.SheerSpikeGradeMultiplier;

		for ( var pass = 0; pass < PadRimSoftenPasses; pass++ )
		{
			for ( var gy = 1; gy < rz - 1; gy++ )
			{
				var wy = gy * cellY - halfD;
				var row = gy * rx;
				for ( var gx = 1; gx < rx - 1; gx++ )
				{
					var wx = gx * cellX - halfW;
					var i = row + gx;
					if ( !IsNearSculptedPad( pads, wx, wy ) )
						continue;

					var sculpted = heights[i];
					var natural = naturalHeights[i];
					var avg = (heights[i - 1] + heights[i + 1] + heights[i - rx] + heights[i + rx]) * 0.25f;
					var stepX = MathF.Abs( heights[i + 1] - heights[i - 1] );
					var stepY = MathF.Abs( heights[i + rx] - heights[i - rx] );
					var maxDiff = MathF.Max( stepX * 0.5f, stepY * 0.5f );
					var slope = MathF.Max( stepX / (cellX * 2f), stepY / (cellY * 2f) );

					if ( maxDiff < spikeStep
					     && MathF.Abs( sculpted - natural ) < 2f
					     && slope < 0.55f
					     && MathF.Abs( sculpted - avg ) < maxStep * 0.5f )
						continue;

					var h = sculpted + (avg - sculpted) * ( maxDiff > spikeStep ? 0.72f : 0.42f );
					h = ClampNeighborStep( h, heights[i - 1], maxStep );
					h = ClampNeighborStep( h, heights[i + 1], maxStep );
					h = ClampNeighborStep( h, heights[i - rx], maxStep );
					h = ClampNeighborStep( h, heights[i + rx], maxStep );
					if ( sculpted > natural + 48f )
						h = MathF.Min( h, natural + 48f + (sculpted - natural) * 0.15f );

					heights[i] = h;
				}
			}
		}
	}

	static bool IsNearSculptedPad( List<ThornsTerrainProcBuildingPad> pads, float wx, float wy )
	{
		for ( var p = 0; p < pads.Count; p++ )
		{
			var pad = pads[p];
			if ( IsMacroLike( in pad ) )
			{
				var reach = pad.HalfW + MathF.Max( 16f, pad.Apron ) + 48f;
				var dx = wx - pad.CenterX;
				var dy = wy - pad.CenterY;
				if ( dx * dx + dy * dy <= reach * reach )
					return true;

				continue;
			}

			if ( !IsLocalFoundationPad( in pad ) )
				continue;

			var fw = pad.FoundationHalfW > 1f ? pad.FoundationHalfW : pad.HalfW * 0.82f;
			var fd = pad.FoundationHalfD > 1f ? pad.FoundationHalfD : pad.HalfD * 0.82f;
			var outerApron = MathF.Max( pad.Apron, pad.WallApron + 8f ) * 1.35f;
			var reachObb = MathF.Sqrt( (fw + outerApron) * (fw + outerApron) + (fd + outerApron) * (fd + outerApron) );
			var dxp = wx - pad.CenterX;
			var dyp = wy - pad.CenterY;
			if ( dxp * dxp + dyp * dyp <= reachObb * reachObb )
				return true;
		}

		return false;
	}

	static float ClampNeighborStep( float h, float neighbor, float maxStep )
	{
		var d = h - neighbor;
		if ( d > maxStep )
			return neighbor + maxStep;
		if ( d < -maxStep )
			return neighbor - maxStep;
		return h;
	}

	static float SmootherStep( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	readonly struct Contribution
	{
		public readonly float SupportZ;
		public readonly float Weight;
		public readonly bool IsInterior;

		public Contribution( float supportZ, float weight, bool isInterior )
		{
			SupportZ = supportZ;
			Weight = weight;
			IsInterior = isInterior;
		}
	}
}
