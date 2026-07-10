namespace Sandbox;

/// <summary>Console diagnostics for M700 ADS — does not affect gameplay.</summary>
public static class AimboxM700ScopeDebug
{
	public const bool Enabled = false;
	public const float ScopedLogIntervalSeconds = 0.45f;

	static bool _wasWantsAds;
	static bool _wasEquipped;
	static TimeSince _scopedLogTimer;
	static float _lastLoggedBlend = -1f;

	public static void NotifyWeaponEquipped( AimboxWeaponId weaponId, string modelPath )
	{
		if ( !Enabled || weaponId != AimboxWeaponId.M700 )
			return;

		Log.Info( $"[Aimbox M700 Scope] Equipped viewmodel path='{modelPath}'." );
	}

	public static void Tick(
		AimboxPlayerController player,
		AimboxViewModelController viewModel,
		AimboxAdsSightMode sightMode,
		float presentationBlend,
		float animBlend,
		float fallbackBlend,
		float targetFov,
		float smoothedFov )
	{
		if ( !Enabled || player is null || player.IsProxy )
			return;

		var isM700 = player.ActiveWeapon == AimboxWeaponId.M700;
		if ( isM700 && !_wasEquipped )
			Log.Info( "[Aimbox M700 Scope] Active weapon is M700." );

		_wasEquipped = isM700;
		if ( !isM700 )
		{
			_wasWantsAds = false;
			_lastLoggedBlend = -1f;
			return;
		}

		var wantsAds = player.WantsAdsPresentationForDebug;
		if ( wantsAds && !_wasWantsAds )
			Log.Info( "[Aimbox M700 Scope] ADS pressed — scope-in starting." );

		if ( !wantsAds && _wasWantsAds )
			Log.Info( "[Aimbox M700 Scope] ADS released." );

		_wasWantsAds = wantsAds;

		if ( sightMode != AimboxAdsSightMode.SniperScope || presentationBlend <= 0.001f )
			return;

		var blendChanged = MathF.Abs( presentationBlend - _lastLoggedBlend ) > 0.08f;
		if ( _scopedLogTimer < ScopedLogIntervalSeconds && !blendChanged )
			return;

		_scopedLogTimer = 0f;
		_lastLoggedBlend = presentationBlend;

		var snap = viewModel?.LastM700ScopeSnapshot;
		var anim = viewModel?.Animator?.GetAdsDebugState() ?? default;

		Log.Info(
			$"[Aimbox M700 Scope] blend={presentationBlend:F2} anim={animBlend:F2} fallback={fallbackBlend:F2} " +
			$"fov={smoothedFov:F1}->{targetFov:F1} mode={sightMode}" );

		if ( viewModel is null || !viewModel.HasActiveViewModel )
		{
			Log.Warning( "[Aimbox M700 Scope] No active viewmodel on camera." );
			return;
		}

		Log.Info(
			$"[Aimbox M700 Scope] vm path='{viewModel.ActiveModelPath}' localPos={viewModel.DebugViewModelLocalPosition} " +
			$"adsFwd={viewModel.DebugAdsOffsetCurrent} sightOff={viewModel.DebugSightEyeViewmodelOffset} scale={viewModel.DebugViewModelScale} " +
			$"enabled={viewModel.DebugViewModelEnabled} overlay={snap?.OverlayPass} skinEnabled={viewModel.DebugWeaponSkinEnabled}" );

		if ( viewModel.DebugSightEyeViewmodelOffset.Length > 35f )
			Log.Warning( "[Aimbox M700 Scope] sightOff magnitude is very large — gun may be off-screen." );

		if ( snap is null )
		{
			Log.Warning( "[Aimbox M700 Scope] No sniper sight snapshot (ComputeSniperViewmodelOffset did not run?)." );
		}
		else
		{
			var s = snap.Value;
			Log.Info(
				$"[Aimbox M700 Scope] bone={(s.HasCameraBone ? s.CameraBoneSkinLocal.ToString() : "missing")} " +
				$"optic={(s.HasOpticAnchor ? s.OpticSkinLocal.ToString() : "missing")} " +
				$"eyeOff={s.EyeLocalOffset} vmSightOff={s.ViewmodelSightOffset} vmScaleMul={s.VmScale}" );
		}

		Log.Info(
			$"[Aimbox M700 Scope] anim ironsights={anim.IronsightsBlend01:F2} graphParam={anim.GraphHasIronsights} " +
			$"useGraph={anim.UseGraphIronsights} desiredAds={anim.DesiredAdsPose} appliedAds={anim.AppliedAdsPose} " +
			$"equipDone={anim.EquipPlaybackDone} useAnimGraph={anim.SkinUsesAnimGraph}" );
	}
}

public readonly record struct AimboxM700ScopeSightSnapshot(
	bool HasCameraBone,
	Vector3 CameraBoneSkinLocal,
	bool HasOpticAnchor,
	Vector3 OpticSkinLocal,
	Vector3 EyeLocalOffset,
	Vector3 ViewmodelSightOffset,
	float VmScale,
	bool OverlayPass,
	bool ViewmodelEnabled );
