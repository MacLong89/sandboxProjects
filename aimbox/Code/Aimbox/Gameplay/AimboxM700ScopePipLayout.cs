namespace Sandbox;

public enum AimboxScopePipTuningTarget
{
	M700RangedSight,
	BoltOnRangedSight
}

/// <summary>Runtime screen-space layout for scope PiP circles (panel coordinates).</summary>
public static class AimboxM700ScopePipLayout
{
	public static AimboxScopePipTuningTarget ActiveTarget { get; private set; }

	public static Vector2 PanelOffset { get; set; }
	public static float RadiusScale { get; set; }

	static AimboxM700ScopePipLayout()
	{
		ActiveTarget = AimboxScopePipTuningTarget.M700RangedSight;
		ResetToDefaults();
	}

	public static bool SupportsPlayer( AimboxPlayerController player )
	{
		if ( player is null || player.IsProxy )
			return false;

		return player.CurrentWeapon?.Attachments.Contains( AimboxAttachmentId.RangedSight ) == true;
	}

	public static AimboxScopePipTuningTarget ResolveTarget( AimboxPlayerController player ) =>
		player?.ActiveWeapon == AimboxWeaponId.M700
			? AimboxScopePipTuningTarget.M700RangedSight
			: AimboxScopePipTuningTarget.BoltOnRangedSight;

	public static void SetActiveTarget( AimboxScopePipTuningTarget target )
	{
		if ( ActiveTarget == target )
			return;

		ActiveTarget = target;
		ResetToDefaults();
	}

	public static void ResetToDefaults()
	{
		PanelOffset = DefaultPanelOffset( ActiveTarget );
		RadiusScale = DefaultRadiusScale( ActiveTarget );
	}

	public static string DescribeActiveTarget() => ActiveTarget switch
	{
		AimboxScopePipTuningTarget.M700RangedSight => "M700 ranged sight",
		AimboxScopePipTuningTarget.BoltOnRangedSight => "bolt-on ranged sight",
		_ => "scope PiP"
	};

	public static string FormatForCopyPaste()
	{
		return ActiveTarget switch
		{
			AimboxScopePipTuningTarget.M700RangedSight =>
				$"M700ScopePipCenterXOffsetPixels = {PanelOffset.x:F1}f;\n" +
				$"M700ScopePipCenterYOffsetPixels = {PanelOffset.y:F1}f;\n" +
				$"M700ScopePipRadiusScale = {AimboxAdsSightTuning.M700ScopePipRadiusScale * RadiusScale:F3}f;",
			_ =>
				$"RangedSightScopePipCenterXOffsetPixels = {PanelOffset.x:F1}f;\n" +
				$"RangedSightScopePipCenterYOffsetPixels = {PanelOffset.y:F1}f;\n" +
				$"RangedSightScopePipRadiusScale = {AimboxAdsSightTuning.RangedSightScopePipRadiusScale * RadiusScale:F3}f;",
		};
	}

	static Vector2 DefaultPanelOffset( AimboxScopePipTuningTarget target ) => target switch
	{
		AimboxScopePipTuningTarget.M700RangedSight => new(
			AimboxAdsSightTuning.M700ScopePipCenterXOffsetPixels,
			AimboxAdsSightTuning.M700ScopePipCenterYOffsetPixels ),
		_ => new(
			AimboxAdsSightTuning.RangedSightScopePipCenterXOffsetPixels,
			AimboxAdsSightTuning.RangedSightScopePipCenterYOffsetPixels )
	};

	static float DefaultRadiusScale( AimboxScopePipTuningTarget target ) => 1f;
}
