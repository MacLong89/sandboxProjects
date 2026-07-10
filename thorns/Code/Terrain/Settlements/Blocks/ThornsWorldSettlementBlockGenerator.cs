using System.Collections.Generic;

namespace Sandbox;

/// <summary>Procedural districts → blocks → lots with road corridors and frontage.</summary>
public static class ThornsWorldSettlementBlockGenerator
{
	public static ThornsWorldSettlementBlockPlan Generate(
		ThornsWorldSettlementPlan plan,
		ThornsWorldRoadNetwork roadNetwork,
		int seed )
	{
		if ( plan is null )
			return ThornsWorldSettlementBlockPlan.Empty;

		_lotCounter = 0;
		var rnd = new Random( unchecked( seed ^ (int)0x8c41b0c4 ) );
		var areas = new List<ThornsWorldSettlementAreaBlockPlan>();

		areas.Add( GenerateCity( plan.MainCity, rnd ) );
		for ( var t = 0; t < plan.Towns.Count; t++ )
			areas.Add( GenerateTown( plan.Towns[t], t, rnd ) );

		return new ThornsWorldSettlementBlockPlan
		{
			IsPopulated = true,
			Seed = seed,
			Areas = areas,
			InterSettlementCorridors = BuildTrailCorridors( roadNetwork )
		};
	}

	static List<ThornsWorldRoadCorridor> BuildTrailCorridors( ThornsWorldRoadNetwork roadNetwork )
	{
		var list = new List<ThornsWorldRoadCorridor>();
		if ( roadNetwork?.Segments is null )
			return list;

		foreach ( var seg in roadNetwork.Segments )
		{
			list.Add( new ThornsWorldRoadCorridor
			{
				A = seg.FromLocal,
				B = seg.ToLocal,
				HalfWidth = seg.Kind == ThornsWorldTrailKind.DirtRoad ? 96f : 64f,
				Kind = ThornsWorldRoadCorridorKind.Trail
			} );
		}

		return list;
	}

	static ThornsWorldSettlementAreaBlockPlan GenerateCity(
		ThornsWorldSettlementZone city,
		Random rnd )
	{
		var cell = ThornsBuildingModule.Cell;
		var center = city.CenterLocal;
		var coreR = cell * 4.5f;
		var midR = cell * 6.2f;
		var outerR = MathF.Min( city.Radius * 0.88f, cell * 8.4f );

		var area = new ThornsWorldSettlementAreaBlockPlan
		{
			SettlementKind = ThornsWorldSettlementKind.MainCity,
			TownIndex = -1,
			Label = city.Label,
			CenterLocal = center
		};

		area.Corridors.AddRange( BuildCityCorridors( center, coreR, midR, outerR, cell ) );

		var coreDistrict = new ThornsWorldSettlementDistrictPlan
		{
			Kind = ThornsWorldSettlementDistrictKind.Core,
			CityRing = ThornsWorldCityRing.Core,
			InnerRadius = 0f,
			OuterRadius = coreR
		};
		BuildCityCoreBlock( coreDistrict, area, center, cell, coreR );
		area.Districts.Add( coreDistrict );

		var midDistrict = new ThornsWorldSettlementDistrictPlan
		{
			Kind = ThornsWorldSettlementDistrictKind.MidCommercialResidential,
			CityRing = ThornsWorldCityRing.MidRing,
			InnerRadius = coreR + cell * 0.8f,
			OuterRadius = midR
		};
		BuildCityMidBlocks( midDistrict, area, center, cell, coreR, midR, rnd );
		area.Districts.Add( midDistrict );

		var outerDistrict = new ThornsWorldSettlementDistrictPlan
		{
			Kind = ThornsWorldSettlementDistrictKind.OuterIndustrial,
			CityRing = ThornsWorldCityRing.OuterRing,
			InnerRadius = midR + cell * 0.6f,
			OuterRadius = outerR
		};
		BuildCityOuterBlocks( outerDistrict, area, center, cell, midR, outerR, rnd );
		area.Districts.Add( outerDistrict );

		AssignCitySlotsToLots( area, city.BuildingSlots );
		return area;
	}

	static List<ThornsWorldRoadCorridor> BuildCityCorridors(
		Vector2 center,
		float coreR,
		float midR,
		float outerR,
		float cell )
	{
		var radialW = cell * 2.05f;
		var ringW = cell * 1.72f;
		var list = new List<ThornsWorldRoadCorridor>( 12 );
		var extent = outerR + cell * 2f;

		for ( var i = 0; i < 4; i++ )
		{
			var ang = i * MathF.PI * 0.5f;
			var dir = new Vector2( MathF.Cos( ang ), MathF.Sin( ang ) );
			list.Add( new ThornsWorldRoadCorridor
			{
				A = center,
				B = center + dir * extent,
				HalfWidth = radialW,
				Kind = ThornsWorldRoadCorridorKind.Radial
			} );
		}

		AddRingCorridor( list, center, coreR + cell * 0.35f, ringW );
		AddRingCorridor( list, center, midR - cell * 0.15f, ringW );
		AddRingCorridor( list, center, outerR - cell * 0.25f, ringW * 0.9f );
		return list;
	}

	static void AddRingCorridor( List<ThornsWorldRoadCorridor> list, Vector2 center, float radius, float halfWidth )
	{
		const int segments = 4;
		for ( var i = 0; i < segments; i++ )
		{
			var a0 = i * MathF.PI * 0.5f;
			var a1 = ( i + 1 ) * MathF.PI * 0.5f;
			list.Add( new ThornsWorldRoadCorridor
			{
				A = center + new Vector2( MathF.Cos( a0 ), MathF.Sin( a0 ) ) * radius,
				B = center + new Vector2( MathF.Cos( a1 ), MathF.Sin( a1 ) ) * radius,
				HalfWidth = halfWidth,
				Kind = ThornsWorldRoadCorridorKind.Ring
			} );
		}
	}

	static void BuildCityCoreBlock(
		ThornsWorldSettlementDistrictPlan district,
		ThornsWorldSettlementAreaBlockPlan area,
		Vector2 center,
		float cell,
		float coreR )
	{
		var block = new ThornsWorldSettlementBlock
		{
			Index = area.Districts.Count * 10,
			District = ThornsWorldSettlementDistrictKind.Core,
			CityRing = ThornsWorldCityRing.Core,
			CenterLocal = center,
			HalfW = coreR,
			HalfD = coreR,
			YawRadians = 0f
		};

		var plazaR = cell * 1.85f;
		AddVacantLot( block, area, district, center, plazaR * 0.5f, plazaR * 0.5f, 0f, ThornsWorldCityRing.Core );

		// Ring spacing must exceed combined lot half-extents (~3.75 cell each → need center dist > 7.5 cell).
		var lotR = cell * 5.85f;
		var coreSlotIndices = new[] { 11, 10, 9 };
		for ( var i = 0; i < 3; i++ )
		{
			var ang = -MathF.PI * 0.5f + i * ( MathF.PI * 2f / 3f );
			var pos = center + new Vector2( MathF.Cos( ang ), MathF.Sin( ang ) ) * lotR;
			var frontage = ThornsWorldSettlementRoadCorridors.NearestCorridorFrontage( pos, area.Corridors );
			var yaw = MathF.Atan2( frontage.y, frontage.x ) - MathF.PI * 0.5f;
			AddLot( block, area, district, pos, cell * 3.35f, cell * 3.15f, yaw, frontage, ThornsWorldCityRing.Core, coreSlotIndices[i] );
		}

		district.Blocks.Add( block );
	}

	static void BuildCityMidBlocks(
		ThornsWorldSettlementDistrictPlan district,
		ThornsWorldSettlementAreaBlockPlan area,
		Vector2 center,
		float cell,
		float coreR,
		float midR,
		Random rnd )
	{
		var blockR = ( coreR + midR ) * 0.5f;
		var lotsPerQuad = new[] { 2, 1, 1, 1 };
		for ( var q = 0; q < 4; q++ )
		{
			var angCenter = q * MathF.PI * 0.5f + MathF.PI * 0.25f;
			var blockCenter = center + new Vector2( MathF.Cos( angCenter ), MathF.Sin( angCenter ) ) * blockR;
			var block = new ThornsWorldSettlementBlock
			{
				Index = district.Blocks.Count + q,
				District = ThornsWorldSettlementDistrictKind.MidCommercialResidential,
				CityRing = ThornsWorldCityRing.MidRing,
				CenterLocal = blockCenter,
				HalfW = cell * 2.85f,
				HalfD = cell * 2.85f,
				YawRadians = angCenter
			};

			AddVacantLot( block, area, district, blockCenter, cell * 1.45f, cell * 1.45f, angCenter, ThornsWorldCityRing.MidRing );

			for ( var l = 0; l < lotsPerQuad[q]; l++ )
			{
				// Perpendicular spacing must exceed combined lot half-widths (~3.25 cell each → ≥6.5 cell center distance).
				var spreadMul = lotsPerQuad[q] > 1 ? 4.65f : 1.05f;
				var spread = ( l - ( lotsPerQuad[q] - 1 ) * 0.5f ) * cell * spreadMul;
				var pos = blockCenter
				          + new Vector2( -MathF.Sin( angCenter ), MathF.Cos( angCenter ) ) * spread;
				if ( ThornsWorldSettlementRoadCorridors.PointInCorridor( pos, area.Corridors, cell * 0.5f ) )
					pos += new Vector2( MathF.Cos( angCenter ), MathF.Sin( angCenter ) ) * cell * 0.4f;

				var frontage = ThornsWorldSettlementRoadCorridors.NearestCorridorFrontage( pos, area.Corridors );
				var yaw = MathF.Atan2( frontage.y, frontage.x ) - MathF.PI * 0.5f;
				yaw += (float)( rnd.NextDouble() - 0.5 ) * 0.08f;
				AddLot( block, area, district, pos, cell * 3.25f, cell * 3.0f, yaw, frontage, ThornsWorldCityRing.MidRing );
			}

			district.Blocks.Add( block );
		}
	}

	static void BuildCityOuterBlocks(
		ThornsWorldSettlementDistrictPlan district,
		ThornsWorldSettlementAreaBlockPlan area,
		Vector2 center,
		float cell,
		float midR,
		float outerR,
		Random rnd )
	{
		var blockR = ( midR + outerR ) * 0.5f;
		for ( var q = 0; q < 4; q++ )
		{
			var angCenter = q * MathF.PI * 0.5f;
			var blockCenter = center + new Vector2( MathF.Cos( angCenter ), MathF.Sin( angCenter ) ) * blockR;
			var block = new ThornsWorldSettlementBlock
			{
				Index = district.Blocks.Count + q,
				District = ThornsWorldSettlementDistrictKind.OuterIndustrial,
				CityRing = ThornsWorldCityRing.OuterRing,
				CenterLocal = blockCenter,
				HalfW = cell * 3.05f,
				HalfD = cell * 3.05f,
				YawRadians = angCenter
			};

			AddVacantLot( block, area, district, blockCenter + new Vector2( cell * 0.6f, cell * 0.4f ), cell, cell, angCenter, ThornsWorldCityRing.OuterRing );

			var pos = blockCenter;
			if ( ThornsWorldSettlementRoadCorridors.PointInCorridor( pos, area.Corridors, cell * 0.45f ) )
				pos += new Vector2( MathF.Cos( angCenter ), MathF.Sin( angCenter ) ) * cell * 0.55f;

			var frontage = ThornsWorldSettlementRoadCorridors.NearestCorridorFrontage( pos, area.Corridors );
			var yaw = MathF.Atan2( frontage.y, frontage.x ) - MathF.PI * 0.5f;
			yaw += (float)( rnd.NextDouble() - 0.5 ) * 0.06f;
			AddLot( block, area, district, pos, cell * 3.1f, cell * 2.85f, yaw, frontage, ThornsWorldCityRing.OuterRing, preferredSlotIndex: q );
			district.Blocks.Add( block );
		}
	}

	static void AssignCitySlotsToLots(
		ThornsWorldSettlementAreaBlockPlan area,
		IReadOnlyList<ThornsWorldBuildingSlot> slots )
	{
		if ( slots is null )
			return;

		var orderedSlots = new List<ThornsWorldBuildingSlot>( slots );
		orderedSlots.Sort( ThornsWorldSettlementPlacementPriority.CompareSlots );

		foreach ( var slot in orderedSlots )
		{
			ThornsWorldSettlementLot best = null;
			var bestArea = 0f;
			foreach ( var lot in area.Lots )
			{
				if ( lot.State != ThornsWorldSettlementLotState.Vacant )
					continue;
				if ( lot.CityRing != slot.CityRing )
					continue;
				if ( lot.PreferredSlotIndex >= 0 && lot.PreferredSlotIndex != slot.Index )
					continue;
				if ( !ThornsProcBuildingFootprintOverlap.TypeFitsLot( slot.Type, lot.HalfW, lot.HalfD ) )
					continue;

				var areaScore = lot.HalfW * lot.HalfD;
				var preferLargest = slot.CityRing == ThornsWorldCityRing.Core
				                    || ThornsProcBuildingIdentityRegistry.Get( slot.Type ).PreferLargeFootprint;
				if ( best is null
				     || ( preferLargest ? areaScore > bestArea : areaScore < bestArea ) )
				{
					best = lot;
					bestArea = areaScore;
				}
			}

			if ( best is null )
			{
				foreach ( var lot in area.Lots )
				{
					if ( lot.State != ThornsWorldSettlementLotState.Vacant )
						continue;
					if ( lot.CityRing != slot.CityRing )
						continue;

					var areaScore = lot.HalfW * lot.HalfD;
					if ( best is null || areaScore > bestArea )
					{
						best = lot;
						bestArea = areaScore;
					}
				}
			}

			if ( best is null )
				continue;

			best.AssignedType = slot.Type;
			best.SlotIndex = slot.Index;
			best.State = ThornsWorldSettlementLotState.Assigned;
		}
	}

	static ThornsWorldSettlementAreaBlockPlan GenerateTown(
		ThornsWorldSettlementZone town,
		int townIndex,
		Random rnd )
	{
		var cell = ThornsBuildingModule.Cell;
		var center = town.CenterLocal;
		var streetAngle = (float)( rnd.NextDouble() * Math.PI * 2.0 );
		var streetDir = new Vector2( MathF.Cos( streetAngle ), MathF.Sin( streetAngle ) );
		var streetPerp = new Vector2( -streetDir.y, streetDir.x );
		var streetLen = MathF.Max( 280f, town.Radius * 1.1f );

		var area = new ThornsWorldSettlementAreaBlockPlan
		{
			SettlementKind = ThornsWorldSettlementKind.Town,
			TownIndex = townIndex,
			Label = town.Label,
			CenterLocal = center
		};

		area.Corridors.Add( new ThornsWorldRoadCorridor
		{
			A = center - streetDir * streetLen,
			B = center + streetDir * streetLen,
			HalfWidth = cell * 1.45f,
			Kind = ThornsWorldRoadCorridorKind.MainStreet
		} );

		var centerDistrict = new ThornsWorldSettlementDistrictPlan
		{
			Kind = ThornsWorldSettlementDistrictKind.TownCenter,
			InnerRadius = 0f,
			OuterRadius = cell * 3.5f
		};
		var resDistrict = new ThornsWorldSettlementDistrictPlan
		{
			Kind = ThornsWorldSettlementDistrictKind.TownResidential,
			InnerRadius = cell * 2f,
			OuterRadius = town.Radius * 0.72f
		};

		var mainBlock = new ThornsWorldSettlementBlock
		{
			Index = 0,
			District = ThornsWorldSettlementDistrictKind.TownCenter,
			CenterLocal = center,
			HalfW = cell * 3f,
			HalfD = cell * 2f,
			YawRadians = streetAngle
		};
		AddVacantLot( mainBlock, area, centerDistrict, center, cell * 1.2f, cell * 0.9f, streetAngle, null );

		var storePos = center + streetDir * cell * 2.2f;
		var storeFront = ThornsWorldSettlementRoadCorridors.NearestCorridorFrontage( storePos, area.Corridors );
		AddLot( mainBlock, area, centerDistrict, storePos, cell * 2f, cell * 1.8f,
			MathF.Atan2( storeFront.y, storeFront.x ) - MathF.PI * 0.5f, storeFront, null );
		centerDistrict.Blocks.Add( mainBlock );

		var resBlock = new ThornsWorldSettlementBlock
		{
			Index = 1,
			District = ThornsWorldSettlementDistrictKind.TownResidential,
			CenterLocal = center,
			HalfW = town.Radius * 0.5f,
			HalfD = town.Radius * 0.5f,
			YawRadians = streetAngle
		};

		var offsets = new[]
		{
			streetPerp * cell * 3.75f + streetDir * cell * 0.55f,
			-streetPerp * cell * 3.75f + streetDir * cell * 0.55f,
			streetPerp * cell * 4.1f - streetDir * cell * 2.65f,
			-streetPerp * cell * 4.1f - streetDir * cell * 2.65f
		};

		for ( var i = 0; i < offsets.Length; i++ )
		{
			var pos = center + offsets[i];
			pos += new Vector2( (float)( rnd.NextDouble() - 0.5 ) * cell * 0.35f, (float)( rnd.NextDouble() - 0.5 ) * cell * 0.35f );
			if ( ThornsWorldSettlementRoadCorridors.PointInCorridor( pos, area.Corridors, cell * 0.35f ) )
				pos += streetPerp * cell * 0.65f;

			var frontage = ThornsWorldSettlementRoadCorridors.NearestCorridorFrontage( pos, area.Corridors );
			var yaw = MathF.Atan2( frontage.y, frontage.x ) - MathF.PI * 0.5f;
			AddLot( resBlock, area, resDistrict, pos, cell * 2.75f, cell * 2.5f, yaw, frontage, null );
		}

		AddVacantLot( resBlock, area, resDistrict, center - streetDir * cell * 3.8f, cell, cell, streetAngle, null );
		resDistrict.Blocks.Add( resBlock );

		area.Districts.Add( centerDistrict );
		area.Districts.Add( resDistrict );

		AssignTownSlotsToLots( area, town.BuildingSlots, rnd );
		return area;
	}

	static void AssignTownSlotsToLots(
		ThornsWorldSettlementAreaBlockPlan area,
		IReadOnlyList<ThornsWorldBuildingSlot> slots,
		Random rnd )
	{
		if ( slots is null )
			return;

		var orderedSlots = new List<ThornsWorldBuildingSlot>( slots );
		orderedSlots.Sort( ThornsWorldSettlementPlacementPriority.CompareSlots );

		foreach ( var slot in orderedSlots )
		{
			ThornsWorldSettlementLot best = null;
			var bestScore = float.MaxValue;
			foreach ( var lot in area.Lots )
			{
				if ( lot.State != ThornsWorldSettlementLotState.Vacant )
					continue;
				if ( !ThornsProcBuildingFootprintOverlap.TypeFitsLot( slot.Type, lot.HalfW, lot.HalfD ) )
					continue;

				var preferCenter = slot.Type == ThornsProcBuildingType.Store
				                   || slot.Type == ThornsProcBuildingType.Warehouse;
				var dist = ( lot.CenterLocal - area.CenterLocal ).Length;
				var score = dist + ( preferCenter ? 0f : 120f );
				if ( slot.Type == ThornsProcBuildingType.Barn || slot.Type == ThornsProcBuildingType.Warehouse )
					score -= dist * 0.15f;

				if ( score < bestScore )
				{
					bestScore = score;
					best = lot;
				}
			}

			if ( best is null )
			{
				var bestArea = 0f;
				foreach ( var lot in area.Lots )
				{
					if ( lot.State != ThornsWorldSettlementLotState.Vacant )
						continue;

					var areaScore = lot.HalfW * lot.HalfD;
					if ( best is null || areaScore > bestArea )
					{
						bestArea = areaScore;
						best = lot;
					}
				}
			}

			if ( best is null )
				continue;

			best.AssignedType = slot.Type;
			best.SlotIndex = slot.Index;
			best.State = ThornsWorldSettlementLotState.Assigned;
			_ = rnd;
		}
	}

	static int _lotCounter;

	static void AddLot(
		ThornsWorldSettlementBlock block,
		ThornsWorldSettlementAreaBlockPlan area,
		ThornsWorldSettlementDistrictPlan district,
		Vector2 pos,
		float halfW,
		float halfD,
		float yaw,
		Vector2 frontage,
		ThornsWorldCityRing? ring,
		int preferredSlotIndex = -1 )
	{
		var lot = new ThornsWorldSettlementLot
		{
			LotIndex = _lotCounter++,
			BlockIndex = block.Index,
			District = district.Kind,
			CityRing = ring,
			CenterLocal = pos,
			HalfW = halfW,
			HalfD = halfD,
			YawRadians = yaw,
			FrontageDirection = frontage,
			PreferredSlotIndex = preferredSlotIndex,
			State = ThornsWorldSettlementLotState.Vacant
		};
		block.Lots.Add( lot );
		area.Lots.Add( lot );
	}

	static void AddVacantLot(
		ThornsWorldSettlementBlock block,
		ThornsWorldSettlementAreaBlockPlan area,
		ThornsWorldSettlementDistrictPlan district,
		Vector2 pos,
		float halfW,
		float halfD,
		float yaw,
		ThornsWorldCityRing? ring )
	{
		var lot = new ThornsWorldSettlementLot
		{
			LotIndex = _lotCounter++,
			BlockIndex = block.Index,
			District = district.Kind,
			CityRing = ring,
			CenterLocal = pos,
			HalfW = halfW,
			HalfD = halfD,
			YawRadians = yaw,
			FrontageDirection = new Vector2( 1f, 0f ),
			State = ThornsWorldSettlementLotState.Vacant
		};
		block.Lots.Add( lot );
		area.Lots.Add( lot );
	}
}

