namespace Sandbox;

/// <summary>Runtime viewmodel fine-tune for optic ADS alignment (keyboard tuner + defaults).</summary>
public static class AimboxOpticAdsLayout
{
	public static Vector3 HoloFineTune { get; set; } = AimboxAdsSightTuning.HoloSightAdsViewmodelFineTune;
	public static Vector3 RaisedRedDotFineTune { get; set; } = AimboxAdsSightTuning.RaisedRedDotAdsViewmodelFineTune;
	public static Vector3 M4RangedSightFineTune { get; set; } = AimboxAdsSightTuning.M4RangedSightAdsViewmodelFineTune;
	public static Vector3 M700RangedSightFineTune { get; set; } = AimboxAdsSightTuning.M700ScopeAdsViewmodelFineTune;

	/// <summary>Legacy alias.</summary>
	public static Vector3 M700FineTune
	{
		get => M700RangedSightFineTune;
		set => M700RangedSightFineTune = value;
	}

	/// <summary>Legacy alias.</summary>
	public static Vector3 RedDotFineTune
	{
		get => HoloFineTune;
		set => HoloFineTune = value;
	}

	public static void ResetHoloToDefaults() =>
		HoloFineTune = AimboxAdsSightTuning.HoloSightAdsViewmodelFineTune;

	public static void ResetRaisedRedDotToDefaults() =>
		RaisedRedDotFineTune = AimboxAdsSightTuning.RaisedRedDotAdsViewmodelFineTune;

	public static void ResetM4RangedSightToDefaults() =>
		M4RangedSightFineTune = AimboxAdsSightTuning.M4RangedSightAdsViewmodelFineTune;

	public static void ResetM700RangedSightToDefaults() =>
		M700RangedSightFineTune = AimboxAdsSightTuning.M700ScopeAdsViewmodelFineTune;

	public static void ResetM700ToDefaults() => ResetM700RangedSightToDefaults();

	public static void ResetRedDotToDefaults() => ResetHoloToDefaults();

	public static void ResetActiveTuning( AimboxPlayerController player )
	{
		if ( player is null )
			return;

		switch ( ResolveTuningTarget( player ) )
		{
			case OpticAdsTuningTarget.M700RangedSight:
				ResetM700RangedSightToDefaults();
				break;
			case OpticAdsTuningTarget.M4RangedSight:
				ResetM4RangedSightToDefaults();
				break;
			case OpticAdsTuningTarget.M4RaisedRedDot:
				ResetRaisedRedDotToDefaults();
				break;
			case OpticAdsTuningTarget.M4Holo:
				ResetHoloToDefaults();
				break;
		}
	}

	public static void ResetActiveWeapon( AimboxWeaponId weapon )
	{
		switch ( weapon )
		{
			case AimboxWeaponId.M700:
				ResetM700RangedSightToDefaults();
				break;
			case AimboxWeaponId.M4A1:
				ResetHoloToDefaults();
				ResetRaisedRedDotToDefaults();
				ResetM4RangedSightToDefaults();
				break;
		}
	}

	public static OpticAdsTuningTarget ResolveTuningTarget( AimboxPlayerController player )
	{
		if ( player is null )
			return OpticAdsTuningTarget.None;

		var attachments = player.CurrentWeapon?.Attachments;
		if ( player.ActiveWeapon == AimboxWeaponId.M700
		     && attachments?.Contains( AimboxAttachmentId.RangedSight ) == true )
			return OpticAdsTuningTarget.M700RangedSight;

		if ( player.ActiveWeapon != AimboxWeaponId.M4A1 || attachments is null )
			return OpticAdsTuningTarget.None;

		if ( attachments.Contains( AimboxAttachmentId.RangedSight ) )
			return OpticAdsTuningTarget.M4RangedSight;

		if ( attachments.Contains( AimboxAttachmentId.RaisedRedDot ) )
			return OpticAdsTuningTarget.M4RaisedRedDot;

		if ( attachments.Contains( AimboxAttachmentId.HoloSight ) )
			return OpticAdsTuningTarget.M4Holo;

		return OpticAdsTuningTarget.None;
	}

	public static Vector3 GetActiveFineTune( OpticAdsTuningTarget target ) => target switch
	{
		OpticAdsTuningTarget.M4Holo => HoloFineTune,
		OpticAdsTuningTarget.M4RaisedRedDot => RaisedRedDotFineTune,
		OpticAdsTuningTarget.M4RangedSight => M4RangedSightFineTune,
		OpticAdsTuningTarget.M700RangedSight => M700RangedSightFineTune,
		_ => Vector3.Zero
	};

	public static void SetActiveFineTune( OpticAdsTuningTarget target, Vector3 value )
	{
		switch ( target )
		{
			case OpticAdsTuningTarget.M4Holo:
				HoloFineTune = value;
				break;
			case OpticAdsTuningTarget.M4RaisedRedDot:
				RaisedRedDotFineTune = value;
				break;
			case OpticAdsTuningTarget.M4RangedSight:
				M4RangedSightFineTune = value;
				break;
			case OpticAdsTuningTarget.M700RangedSight:
				M700RangedSightFineTune = value;
				break;
		}
	}

	public static string DescribeTarget( OpticAdsTuningTarget target ) => target switch
	{
		OpticAdsTuningTarget.M4Holo => "M4 holo window",
		OpticAdsTuningTarget.M4RaisedRedDot => "M4 raised red dot",
		OpticAdsTuningTarget.M4RangedSight => "M4 ranged sight",
		OpticAdsTuningTarget.M700RangedSight => "M700 ranged sight",
		_ => "none"
	};

	public static string FormatForCopyPaste( AimboxPlayerController player )
	{
		return ResolveTuningTarget( player ) switch
		{
			OpticAdsTuningTarget.M700RangedSight =>
				$"M700RangedSightAdsViewmodelFineTune = new( {M700RangedSightFineTune.x:F2}f, {M700RangedSightFineTune.y:F2}f, {M700RangedSightFineTune.z:F2}f );",
			OpticAdsTuningTarget.M4RangedSight =>
				$"M4RangedSightAdsViewmodelFineTune = new( {M4RangedSightFineTune.x:F2}f, {M4RangedSightFineTune.y:F2}f, {M4RangedSightFineTune.z:F2}f );",
			OpticAdsTuningTarget.M4RaisedRedDot =>
				$"RaisedRedDotAdsViewmodelFineTune = new( {RaisedRedDotFineTune.x:F2}f, {RaisedRedDotFineTune.y:F2}f, {RaisedRedDotFineTune.z:F2}f );",
			OpticAdsTuningTarget.M4Holo =>
				$"HoloSightAdsViewmodelFineTune = new( {HoloFineTune.x:F2}f, {HoloFineTune.y:F2}f, {HoloFineTune.z:F2}f );",
			_ => "// Equip M4A1 holo / RMR / ranged sight, or M700 ranged sight."
		};
	}

	public static string FormatForCopyPaste( AimboxWeaponId weapon ) =>
		weapon switch
		{
			AimboxWeaponId.M700 =>
				$"M700RangedSightAdsViewmodelFineTune = new( {M700RangedSightFineTune.x:F2}f, {M700RangedSightFineTune.y:F2}f, {M700RangedSightFineTune.z:F2}f );",
			AimboxWeaponId.M4A1 =>
				$"HoloSightAdsViewmodelFineTune = new( {HoloFineTune.x:F2}f, {HoloFineTune.y:F2}f, {HoloFineTune.z:F2}f );",
			_ => "// Equip M4A1 holo / RMR / ranged sight, or M700 ranged sight."
		};
}

public enum OpticAdsTuningTarget
{
	None,
	M4Holo,
	M4RaisedRedDot,
	M4RangedSight,
	M700RangedSight
}
