using System.Collections.Generic;



namespace Sandbox;



/// <summary>

/// Building identity pipeline: tile blueprint → compile → strict validate → facade, with safe settlement fallbacks.

/// </summary>

public static class ThornsProcBuildingIdentityGenerator

{

	const int SettlementBlueprintAttempts = 8;

	static readonly HashSet<int> SettlementRejectionLogKeys = new();



	/// <summary>Call at the start of each world settlement scatter pass.</summary>

	public static void ClearSettlementPlacementDiagnostics()

	{

		SettlementRejectionLogKeys.Clear();

		ThornsProcBuildingSettlementDiagnostics.Reset();

		ThornsWorldSettlementTerrainDiagnostics.Reset();

		ThornsWorldSettlementPlacementDiagnostics.Reset();

		ThornsWorldGenerationQaMetrics.Reset();

		LastSettlementGenerationUsedFallback = false;

	}

	/// <summary>Set during <see cref="TryGenerateForSettlement"/> — read immediately after for QA.</summary>
	public static bool LastSettlementGenerationUsedFallback { get; private set; }



	public static ThornsProcBuildingLayout Generate(

		ThornsProcBuildingType type,

		ThornsProcBuildingDistrict district,

		Random rnd )

	{

		var blueprint = ThornsProcTileBlueprintLibrary.Get( type );

		ThornsProcBuildingLayout best = null;

		var bestScore = -1;



		for ( var attempt = 0; attempt < ThornsProcBuildingRules.MaxGenerationAttempts; attempt++ )

		{

			var damaged = ShouldCompileDamagedVariant( type, blueprint, rnd );

			if ( !ThornsProcTileBlueprintCompiler.TryCompile(

				     blueprint,

				     district,

				     rnd,

				     ruinVariant: type == ThornsProcBuildingType.Ruin || damaged,

				     out var layout,

				     ThornsProcBuildingCompilePolicy.Strict ) )

				continue;



			var report = ThornsProcBuildingValidation.Validate( layout, layout.InteriorWalls );

			if ( !report.Passed )

				continue;



			if ( report.QualityScore > bestScore )

			{

				bestScore = report.QualityScore;

				best = layout;

				if ( bestScore >= ThornsProcBuildingQuality.ExcellentThreshold )

					break;

			}

		}



		if ( best is not null && bestScore >= ThornsProcBuildingRules.MinQualityScoreToAccept )

			return best;



		Log.Warning( $"[Thorns ProcBuilding] Blueprint fallback for {type} in {district} (bestScore={bestScore})." );

		return GenerateFallbackLayout( type, settlementArchetype: false );

	}



	/// <summary>World-gen: strict blueprint validation with validated archetype fallback; rejects if both fail.</summary>

	public static bool TryGenerateForSettlement(

		ThornsProcBuildingType type,

		ThornsProcBuildingDistrict district,

		Random rnd,

		out ThornsProcBuildingLayout layout,

		bool organicCompactFootprint = false )

	{

		layout = null;
		LastSettlementGenerationUsedFallback = false;

		var ruinShell = type == ThornsProcBuildingType.Ruin;
		if ( ThornsInteriorFurnitureFloorplanAscii.TryCreateSettlementLayout(
			     type,
			     district,
			     rnd,
			     ruinShell,
			     out var asciiLayout,
			     out _ )
		     && ThornsProcBuildingStrictValidation.TryValidate( asciiLayout, null, out _, out _ ) )
		{
			asciiLayout.Identity ??= new ThornsProcBuildingIdentityMeta { Type = type, District = district };
			asciiLayout.Identity.District = district;
			layout = asciiLayout;
			ThornsProcBuildingSettlementDiagnostics.RecordBlueprintCompileSuccess();
			ThornsProcBuildingSettlementDiagnostics.RecordBlueprintStrictValidationPassed();
			return true;
		}

		var blueprint = organicCompactFootprint
			? ThornsProcTileBlueprintLibrary.GetForOrganicPlacement( type )
			: ThornsProcTileBlueprintLibrary.Get( type );

		var blueprintId = blueprint?.Type.ToString() ?? type.ToString();



		for ( var attempt = 0; attempt < SettlementBlueprintAttempts; attempt++ )

		{

			var damaged = ShouldCompileDamagedVariant( type, blueprint, rnd );

			if ( !ThornsProcTileBlueprintCompiler.TryCompile(

				     blueprint,

				     district,

				     rnd,

				     ruinVariant: type == ThornsProcBuildingType.Ruin || damaged,

				     out var candidate,

				     ThornsProcBuildingCompilePolicy.FallbackAllowed ) )

			{

				ThornsProcBuildingSettlementDiagnostics.RecordBlueprintCompileFailed();

				continue;

			}



			ThornsProcBuildingSettlementDiagnostics.RecordBlueprintCompileSuccess();



			if ( ThornsProcBuildingStrictValidation.TryValidate(

				     candidate,

				     blueprint,

				     out var report,

				     out var failure ) )

			{

				ThornsProcBuildingSettlementDiagnostics.RecordBlueprintStrictValidationPassed();

				layout = candidate;
				LastSettlementGenerationUsedFallback = false;

				return true;

			}



			ThornsProcBuildingSettlementDiagnostics.RecordBlueprintStrictValidationFailed();

			LogBlueprintRejectedOnce(

				type,

				district,

				blueprintId,

				candidate,

				failure,

				report,

				fallbackUsed: false );

		}



		ThornsProcBuildingSettlementDiagnostics.RecordFallbackUsed();

		var fallback = CreateValidatedSettlementFallback( type, district, blueprintId, out var fallbackFailure );

		if ( fallback is not null )

		{

			layout = fallback;
			LastSettlementGenerationUsedFallback = true;

			return true;

		}



		ThornsProcBuildingSettlementDiagnostics.RecordPlacementRejected();

		LogPlacementRejectedOnce( type, district, blueprintId, fallbackFailure );

		return false;

	}



	/// <summary>Legacy wrapper — prefer <see cref="TryGenerateForSettlement"/>.</summary>

	public static ThornsProcBuildingLayout GenerateForSettlement(

		ThornsProcBuildingType type,

		ThornsProcBuildingDistrict district,

		Random rnd )

	{

		if ( TryGenerateForSettlement( type, district, rnd, out var layout ) )

			return layout;



		Log.Warning(

			$"[Thorns ProcBuilding] GenerateForSettlement failed for {type} — returning minimal 1-story emergency fallback." );

		return ThornsProcBuildingLayout.CreateGuaranteedFallback( 1 );

	}



	static ThornsProcBuildingLayout CreateValidatedSettlementFallback(

		ThornsProcBuildingType type,

		ThornsProcBuildingDistrict district,

		string blueprintId,

		out ThornsProcBuildingValidationFailureSummary failure )

	{

		failure = default;

		var layout = ThornsProcBuildingLayout.CreateSettlementArchetypeFallback( type );

		layout.Identity ??= new ThornsProcBuildingIdentityMeta

		{

			Type = type,

			District = district,

			IsRuinVariant = false

		};



		layout.GetFootprintHalfExtents( out var halfW, out var halfD );

		var footprintCellsW = (int)MathF.Round( halfW * 2f / ThornsBuildingModule.Cell );

		var footprintCellsD = (int)MathF.Round( halfD * 2f / ThornsBuildingModule.Cell );



		if ( ThornsProcBuildingStrictValidation.TryValidate( layout, blueprint: null, out _, out failure ) )
		{
			ThornsProcBuildingSettlementDiagnostics.RecordFallbackStrictValidationPassed();
			Log.Info(
				$"[Thorns ProcBuilding] Using strict fallback: Type={type} Blueprint={blueprintId} "
				+ $"Stories={layout.Stories} Size={footprintCellsW}x{footprintCellsD} District={district}" );
			return layout;
		}

		var compact = ThornsProcBuildingLayout.CreateGuaranteedFallback( 1 );
		compact.Identity ??= new ThornsProcBuildingIdentityMeta
		{
			Type = type,
			District = district,
			IsRuinVariant = false
		};

		if ( ThornsProcBuildingStrictValidation.TryValidate( compact, blueprint: null, out _, out failure ) )
		{
			ThornsProcBuildingSettlementDiagnostics.RecordFallbackStrictValidationPassed();
			compact.GetFootprintHalfExtents( out var chw, out var chd );
			var cw = (int)MathF.Round( chw * 2f / ThornsBuildingModule.Cell );
			var cd = (int)MathF.Round( chd * 2f / ThornsBuildingModule.Cell );
			Log.Warning(
				$"[Thorns ProcBuilding] Using emergency 1-story fallback: Type={type} Blueprint={blueprintId} "
				+ $"Stories={compact.Stories} Size={cw}x{cd} District={district}" );
			return compact;
		}

		ThornsProcBuildingSettlementDiagnostics.RecordFallbackStrictValidationFailed();
		Log.Warning(
			$"[Thorns ProcBuilding] Fallback validation failed: Type={type} Blueprint={blueprintId} "
			+ $"Stories={layout.Stories} Size={footprintCellsW}x{footprintCellsD} {failure.FormatForLog( type )}" );

		return null;

	}



	static void LogBlueprintRejectedOnce(

		ThornsProcBuildingType type,

		ThornsProcBuildingDistrict district,

		string blueprintId,

		ThornsProcBuildingLayout layout,

		ThornsProcBuildingValidationFailureSummary failure,

		ThornsProcBuildingValidationReport report,

		bool fallbackUsed )

	{

		var key = unchecked( (int)type * 31 + (int)district + (int)failure.Rule * 17 + 0x5100 );

		if ( !SettlementRejectionLogKeys.Add( key ) )

			return;



		layout.GetFootprintHalfExtents( out var halfW, out var halfD );

		var w = (int)MathF.Round( halfW * 2f / ThornsBuildingModule.Cell );

		var d = (int)MathF.Round( halfD * 2f / ThornsBuildingModule.Cell );



		Log.Warning(

			$"[Thorns ProcBuilding] Blueprint rejected: {failure.FormatForLog( type )} "

			+ $"Blueprint={blueprintId} Stories={layout.Stories} Size={w}x{d} District={district} "

			+ $"Category={failure.Category} FallbackUsed={fallbackUsed} Detail={report?.Summary ?? ""}" );

	}



	static void LogPlacementRejectedOnce(

		ThornsProcBuildingType type,

		ThornsProcBuildingDistrict district,

		string blueprintId,

		ThornsProcBuildingValidationFailureSummary failure )

	{

		var key = unchecked( (int)type * 31 + (int)district + 0x9200 );

		if ( !SettlementRejectionLogKeys.Add( key ) )

			return;



		Log.Warning(

			$"[Thorns ProcBuilding] Placement rejected: Type={type} Blueprint={blueprintId} District={district} "

			+ $"Reason={failure.Reason} Rule={failure.Rule} Category={failure.Category}" );

	}



	static ThornsProcBuildingLayout GenerateFallbackLayout( ThornsProcBuildingType type, bool settlementArchetype )

	{

		if ( settlementArchetype )

			return ThornsProcBuildingLayout.CreateSettlementArchetypeFallback( type );



		var blueprint = ThornsProcTileBlueprintLibrary.Get( type );
		var stories = ThornsProcBuildingPoc.RollStoriesForBlueprint( blueprint, Random.Shared );

		return ThornsProcBuildingLayout.CreateGuaranteedFallback( stories );

	}



	static bool ShouldCompileDamagedVariant( ThornsProcBuildingType type, ThornsProcTileBlueprint blueprint, Random rnd )

	{

		if ( type == ThornsProcBuildingType.Ruin )

			return true;



		return blueprint.AllowDamagedVariant && rnd.NextDouble() < 0.14f;

	}

}


