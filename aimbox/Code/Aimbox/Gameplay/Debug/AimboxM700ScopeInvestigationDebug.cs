namespace Sandbox;

/// <summary>
/// One-time M700 draw-call survey at full ADS + per-shot PiP vs lens alignment logging.
/// Filter console for <c>[Aimbox M700 Investigate]</c>. Toggle via Aimbox Game → Enable M700 Scope Investigation.
/// </summary>
public static class AimboxM700ScopeInvestigationDebug
{
	public static bool Enabled { get; set; }

	static readonly string[] GlassMaterialHints =
	[
		"glass", "lens", "scope_glass", "scope_lens", "optic_glass", "rmr_glass"
	];

	static readonly string[] ScopeMaterialHints =
	[
		"v_ranged_sight", "ranged_sight", "sight_ranged", "scope", "telescopic"
	];

	static readonly string[] HousingMaterialHints =
	[
		"body", "housing", "metal", "mount", "rail", "vmat"
	];

	static bool _loggedDrawCalls;
	static bool _recordingShot;
	static bool _wasAttackDown;
	static int _shotIndex;
	static TimeSince _shotRecordElapsed;
	static TimeSince _shotLogThrottle;
	static float _peakLensDeltaPx;
	static float _sumLensDeltaPx;
	static int _sampleCount;
	static float _peakScreenDeltaPx;
	static float _peakCenterLock;
	static float _peakViewKickMag;
	static float _peakAttackHold;

	public static void ResetForNewViewModel()
	{
		_loggedDrawCalls = false;
		_recordingShot = false;
		_wasAttackDown = false;
	}

	public static void NotifyPipLayout(
		Vector2 lensProjectedPx,
		Vector2 pipCenterPx,
		Vector2 screenCenterPx,
		float centerLock01,
		float adsBlend,
		Vector3 viewKickDegrees,
		float attackHold01 )
	{
		if ( !Enabled || !_recordingShot )
			return;

		var lensDelta = (pipCenterPx - lensProjectedPx).Length;
		var screenDelta = (pipCenterPx - screenCenterPx).Length;
		_peakLensDeltaPx = MathF.Max( _peakLensDeltaPx, lensDelta );
		_peakScreenDeltaPx = MathF.Max( _peakScreenDeltaPx, screenDelta );
		_peakCenterLock = MathF.Max( _peakCenterLock, centerLock01 );
		_peakViewKickMag = MathF.Max( _peakViewKickMag, viewKickDegrees.Length );
		_peakAttackHold = MathF.Max( _peakAttackHold, attackHold01 );
		_sumLensDeltaPx += lensDelta;
		_sampleCount++;

		if ( _shotLogThrottle < 0.05f )
			return;

		_shotLogThrottle = 0f;
		Log.Info(
			$"[Aimbox M700 Investigate] shot#{_shotIndex} t={_shotRecordElapsed:F2}s " +
			$"lensProj=({lensProjectedPx.x:F1},{lensProjectedPx.y:F1}) pip=({pipCenterPx.x:F1},{pipCenterPx.y:F1}) " +
			$"screen=({screenCenterPx.x:F1},{screenCenterPx.y:F1}) dLens={lensDelta:F1}px dScreen={screenDelta:F1}px " +
			$"lock={centerLock01:F2} kick=({viewKickDegrees.x:F2},{viewKickDegrees.y:F2},{viewKickDegrees.z:F2}) " +
			$"attackHold={attackHold01:F2} ads={adsBlend:F2}" );
	}

	public static void Tick(
		AimboxPlayerController player,
		AimboxViewModelController viewModel,
		AimboxAdsSightMode sightMode,
		float presentationBlend )
	{
		if ( !Enabled || player is null || player.IsProxy )
			return;

		if ( player.ActiveWeapon != AimboxWeaponId.M700
		     || player.CurrentWeapon?.Attachments.Contains( AimboxAttachmentId.RangedSight ) != true )
		{
			_wasAttackDown = false;
			_recordingShot = false;
			return;
		}

		if ( sightMode == AimboxAdsSightMode.SniperScope
		     && presentationBlend >= 0.98f
		     && viewModel?.WeaponSkin.IsValid() == true
		     && !_loggedDrawCalls )
		{
			LogDrawCallsOnce( viewModel.WeaponSkin );
			_loggedDrawCalls = true;
		}

		var attackDown = Input.Down( "Attack1" );
		if ( attackDown && !_wasAttackDown
		     && player.ShowScopePip
		     && sightMode == AimboxAdsSightMode.SniperScope
		     && presentationBlend >= 0.95f )
		{
			BeginShotRecording();
		}

		_wasAttackDown = attackDown;

		if ( _recordingShot && _shotRecordElapsed > 0.45f )
			FinishShotRecording();
	}

	static void BeginShotRecording()
	{
		_shotIndex++;
		_recordingShot = true;
		_shotRecordElapsed = 0f;
		_shotLogThrottle = 0f;
		_peakLensDeltaPx = 0f;
		_sumLensDeltaPx = 0f;
		_sampleCount = 0;
		_peakScreenDeltaPx = 0f;
		_peakCenterLock = 0f;
		_peakViewKickMag = 0f;
		_peakAttackHold = 0f;
		Log.Info( $"[Aimbox M700 Investigate] shot#{_shotIndex} — recording PiP vs lens for 450ms (fire while scoped)." );
	}

	static void FinishShotRecording()
	{
		_recordingShot = false;
		var avgLensDelta = _sampleCount > 0 ? _sumLensDeltaPx / _sampleCount : 0f;
		var dominant = ClassifyMisalignment(
			_peakLensDeltaPx,
			avgLensDelta,
			_peakScreenDeltaPx,
			_peakCenterLock,
			_peakViewKickMag,
			_peakAttackHold );

		Log.Info(
			$"[Aimbox M700 Investigate] shot#{_shotIndex} summary samples={_sampleCount} " +
			$"peak dLens={_peakLensDeltaPx:F1}px avg dLens={avgLensDelta:F1}px peak dScreen={_peakScreenDeltaPx:F1}px " +
			$"peak lock={_peakCenterLock:F2} peak |kick|={_peakViewKickMag:F2} peak attackHold={_peakAttackHold:F2} " +
			$"→ dominant cause: {dominant}" );
	}

	static string ClassifyMisalignment(
		float peakLensDelta,
		float avgLensDelta,
		float peakScreenDelta,
		float peakCenterLock,
		float peakViewKick,
		float peakAttackHold )
	{
		if ( peakCenterLock > 0.85f && peakScreenDelta < 4f && peakLensDelta > 8f && peakAttackHold < 0.15f )
			return "screen-center lock (PiP decoupled from moving lens mesh)";

		if ( peakCenterLock > 0.85f && peakScreenDelta > 8f && peakLensDelta < 4f )
			return "lens anchor offset (PiP tracks anchor but anchor ≠ screen center)";

		if ( peakAttackHold > 0.25f && peakLensDelta > 6f )
			return "fire animation (attack_hold + moving scope bone/viewmodel)";

		if ( peakViewKick > 0.35f && peakLensDelta > 4f )
			return "view kick (additive VM rotation)";

		if ( peakLensDelta < 4f )
			return "aligned (misalignment below noise threshold)";

		return "mixed / tune panel offset or lens anchor";
	}

	static void LogDrawCallsOnce( SkinnedModelRenderer skin )
	{
		if ( !skin.IsValid() || !skin.Model.IsValid() || skin.Model.IsError )
			return;

		var modelName = skin.Model.ResourceName ?? skin.Model.Name ?? "unknown";
		Log.Info( $"[Aimbox M700 Investigate] === v_m700 draw-call survey @ full ADS (model={modelName}) ===" );

		var glassCandidates = 0;
		var scopeCombined = 0;
		var housingOnly = 0;
		var other = 0;

		for ( var drawCall = 0; drawCall < skin.Model.MeshCount; drawCall++ )
		{
			var material = skin.Model.Materials.ElementAtOrDefault( drawCall );
			var materialName = material.IsValid() ? material.ResourceName ?? "" : "(null)";
			var glass = MatchesAnyHint( materialName, GlassMaterialHints );
			var scope = MatchesAnyHint( materialName, ScopeMaterialHints );
			var housing = MatchesAnyHint( materialName, HousingMaterialHints );
			var m700ScopeRule = AimboxM700ScopeLensPresentation.IsScopeMaterialForDebug( materialName );
			var holoLensRule = AimboxOpticLensPresentation.IsGenericLensMaterialForDebug( materialName );
			var separableGlass = glass && !scope;
			var isolatable = separableGlass || (glass && housing && !scope);

			if ( isolatable )
				glassCandidates++;
			else if ( scope || m700ScopeRule )
				scopeCombined++;
			else if ( housing )
				housingOnly++;
			else
				other++;

			Log.Info(
				$"[Aimbox M700 Investigate] drawCall={drawCall} mat='{materialName}' " +
				$"glassHint={glass} scopeHint={scope || m700ScopeRule} housingHint={housing} " +
				$"holoLensRule={holoLensRule} separableGlass={isolatable} " +
				$"override={skin.Materials.HasOverride( drawCall )}" );
		}

		var holoStylePossible = glassCandidates > 0;
		Log.Info(
			$"[Aimbox M700 Investigate] material summary: drawCalls={skin.Model.MeshCount} " +
			$"isolatableGlass={glassCandidates} scopeCombined={scopeCombined} housingOnly={housingOnly} other={other}" );
		Log.Info(
			holoStylePossible
				? "[Aimbox M700 Investigate] holo-style see-through: MAYBE — at least one glass-like draw call separate from scope housing hints."
				: "[Aimbox M700 Investigate] holo-style see-through: NO — no isolatable glass pass found; PiP compositing is the practical path." );
	}

	static bool MatchesAnyHint( string materialName, string[] hints )
	{
		if ( string.IsNullOrWhiteSpace( materialName ) )
			return false;

		foreach ( var hint in hints )
		{
			if ( materialName.Contains( hint, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}
