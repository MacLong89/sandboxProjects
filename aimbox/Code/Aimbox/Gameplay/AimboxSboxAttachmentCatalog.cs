namespace Sandbox;

/// <summary>Maps Aimbox attachment IDs to facepunch/sboxweapons FP visuals (bodygroups + child models).</summary>
public static class AimboxSboxAttachmentCatalog
{
	public readonly record struct BodyGroupSpec( string[] NameCandidates, int Choice );

	public readonly record struct VisualSpec(
		string ModelPath,
		string[] MountPointCandidates,
		string[] BodyGroupNameCandidates,
		int BodyGroupChoice,
		bool RequiresWeaponPose,
		string ReferenceMountPoint,
		Vector3 ReferenceLocalPosition,
		Rotation ReferenceLocalRotation,
		string FallbackMountBone,
		Vector3 FallbackLocalPosition,
		Rotation FallbackLocalRotation,
		BodyGroupSpec[] ExtraBodyGroups = null,
		float LocalScale = 1f );

	public const string RedDotRmrPath = "models/weapons/sbox_attachments/reddot_rmr/reddot_rmr.vmdl";
	public const string RedDotRmrRaisedPath = "models/weapons/sbox_attachments/reddot_rmr/reddot_rmr_raised.vmdl";
	public const string HolographicSightPath = "models/weapons/sbox_attachments/sight_holographic/sight_holographic.vmdl";
	public const string RangedSightPath = "models/weapons/sbox_attachments/sight_ranged/sight_ranged.vmdl";
	public const string ForegripStraightPath = "models/weapons/sbox_attachments/foregrip_straight/foregrip_straight.vmdl";
	public const string ForegripAngledPath = "models/weapons/sbox_attachments/foregrip_angled/foregrip_angled.vmdl";
	public const string FlashlightPath = "models/weapons/sbox_attachments/flashlight/flashlight.vmdl";
	public const string Suppressor9mmPath = "models/weapons/sbox_attachments/silencer_9mm/suppressor_9mm.vmdl";

	static readonly string[] OpticMountCandidates = ["top_rail", "rail", "sight", "optic", "scope", "reddot"];
	static readonly string[] GripMountCandidates = ["handguard", "attach_grip", "foregrip", "grip"];
	static readonly string[] FlashlightMountCandidates = ["side_rail", "flashlight", "laser", "underbarrel", "rail"];
	static readonly string[] MuzzleMountCandidates = ["muzzle", "attach_muzzle", "muzzle_attach", "barrel"];
	static readonly BodyGroupSpec[] M4OpticFrontIronSuppression = [new( ["gasblock"], 1 )];
	static readonly BodyGroupSpec[] M700IronSightBodyGroups =
	[
		new( ["top_rail"], 0 ),
		new( ["top_rail_mount"], 0 )
	];
	static readonly string[] M700DefaultBodyGroupNames = ["top_rail", "top_rail_mount"];
	static Dictionary<string, int> _m700SpawnBodyGroups;
	static readonly string[] HandguardCoverBodyGroups = ["handguard_covers", "handguard", "grip", "cover"];

	public static bool TryGetVisual( AimboxWeaponId weapon, AimboxAttachmentId attachment, out VisualSpec spec )
	{
		spec = default;
		if ( !AimboxAttachmentCatalog.IsCompatible( weapon, attachment ) )
			return false;

		return attachment switch
		{
			AimboxAttachmentId.HoloSight => AssignHoloSight( weapon, out spec ),
			AimboxAttachmentId.RaisedRedDot => AssignRaisedRedDot( weapon, out spec ),
			AimboxAttachmentId.RangedSight => AssignRangedSight( weapon, out spec ),
			AimboxAttachmentId.ForegripStraight => AssignForegrip( weapon, ForegripStraightPath, out spec ),
			AimboxAttachmentId.ForegripAngled => AssignForegrip( weapon, ForegripAngledPath, out spec ),
			AimboxAttachmentId.Flashlight => AssignFlashlight( weapon, out spec ),
			AimboxAttachmentId.ExtendedMag => AssignExtendedMag( weapon, out spec ),
			AimboxAttachmentId.Suppressor => AssignSuppressor( weapon, out spec ),
			_ => false
		};
	}

	/// <summary>Doc: M4 <c>weapon_pose = 1</c> when handguard covers bodygroup is active.</summary>
	public static bool RequiresWeaponPose( AimboxWeaponId weapon ) => weapon == AimboxWeaponId.M4A1;

	public static bool HasVisual( AimboxWeaponId weapon, AimboxAttachmentId attachment ) =>
		TryGetVisual( weapon, attachment, out _ );

	public static float GetRedDotLocalScale( AimboxAttachmentId attachment ) => attachment switch
	{
		AimboxAttachmentId.HoloSight => 0.68f,
		_ => 1f
	};

	public static Vector3 GetRedDotAdsEyeAttachmentOffset( AimboxAttachmentId attachment ) => attachment switch
	{
		AimboxAttachmentId.HoloSight => AimboxAdsSightTuning.HolographicRedDotEyeAttachmentOffset,
		_ => Vector3.Zero
	};

	public static Vector3 GetRangedSightAdsEyeAttachmentOffset() =>
		AimboxAdsSightTuning.RangedSightAdsEyeAttachmentOffset;

	/// <summary>Ocular lens center on sight_ranged in attachment-local space (+X = toward barrel / objective).</summary>
	public static Vector3 ResolveRangedSightLensLocalOffset( Model model )
	{
		if ( !model.IsValid() || model.IsError )
			return GetRangedSightAdsEyeAttachmentOffset();

		var bounds = model.RenderBounds;
		if ( bounds.Size.Length < 0.001f )
			return GetRangedSightAdsEyeAttachmentOffset();

		var center = bounds.Center;
		var half = bounds.Size * 0.5f;
		// Player looks through the ocular bell at the rear of the mesh (Min X), not the objective lens.
		return new Vector3( center.x - half.x, center.y, center.z )
		       + AimboxAdsSightTuning.RangedSightLensBoundsFineTune;
	}

	public static string GetRaisedRedDotAdsModelPath( AimboxWeaponId weapon, bool useRaised )
	{
		if ( !useRaised )
			return ResolveRedDotModelPath( weapon );

		return weapon == AimboxWeaponId.Usp ? RedDotRmrPath : RedDotRmrRaisedPath;
	}

	static string ResolveRedDotModelPath( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.Usp => RedDotRmrPath,
		_ => RedDotRmrRaisedPath
	};

	static bool AssignHoloSight( AimboxWeaponId weapon, out VisualSpec spec )
	{
		var fallback = GetOpticFallback( weapon );
		spec = BuildOpticSpec(
			weapon,
			HolographicSightPath,
			0.68f,
			OpticExtraBodyGroupsFor( weapon ),
			fallback );
		return true;
	}

	static bool AssignRaisedRedDot( AimboxWeaponId weapon, out VisualSpec spec )
	{
		var fallback = GetOpticFallback( weapon );
		spec = BuildOpticSpec(
			weapon,
			ResolveRedDotModelPath( weapon ),
			1f,
			OpticExtraBodyGroupsFor( weapon ),
			fallback );
		return true;
	}

	static bool AssignRangedSight( AimboxWeaponId weapon, out VisualSpec spec )
	{
		// M700 ranged sight = stock integrated scope on v_m700 (bodygroups applied in viewmodel presentation).
		if ( weapon == AimboxWeaponId.M700 )
		{
			spec = default;
			return false;
		}

		var fallback = GetOpticFallback( weapon );
		spec = BuildOpticSpec(
			weapon,
			RangedSightPath,
			1f,
			OpticExtraBodyGroupsFor( weapon ),
			fallback );
		return true;
	}

	static VisualSpec BuildOpticSpec(
		AimboxWeaponId weapon,
		string modelPath,
		float localScale,
		BodyGroupSpec[] extraBodyGroups,
		(string MountBone, Vector3 LocalPosition) fallback ) =>
		new(
			modelPath,
			OpticMountCandidates,
			TopRailBodyGroupCandidatesFor( weapon ),
			1,
			false,
			"",
			Vector3.Zero,
			Rotation.Identity,
			fallback.MountBone,
			fallback.LocalPosition,
			Rotation.Identity,
			extraBodyGroups,
			localScale );

	static string[] TopRailBodyGroupCandidatesFor( AimboxWeaponId weapon ) => TopRailBodyGroups;

	static readonly string[] TopRailBodyGroups = ["top_rail", "rail", "sight", "optic", "scope", "iron"];

	static BodyGroupSpec[] OpticExtraBodyGroupsFor( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.M4A1 => M4OpticFrontIronSuppression,
		_ => null
	};

	/// <summary>Snapshot v_m700 bodygroups before iron-sight overrides — used to restore stock scope.</summary>
	public static void CaptureM700DefaultBodyGroups( SkinnedModelRenderer skin )
	{
		_m700SpawnBodyGroups = null;
		if ( !skin.IsValid() || !skin.Model.IsValid() || skin.Model.IsError )
			return;

		var captured = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var name in M700DefaultBodyGroupNames )
		{
			var part = skin.Model.Parts.Get( name );
			if ( part is null || part.Choices.Count <= 1 )
				continue;

			captured[name] = skin.GetBodyGroup( name );
		}

		if ( captured.Count > 0 )
			_m700SpawnBodyGroups = captured;
	}

	/// <summary>M700 without ranged sight — iron sights, integrated scope mesh hidden.</summary>
	public static void ApplyM700IronSightBodyGroups( SkinnedModelRenderer skin )
	{
		if ( !skin.IsValid() || !skin.Model.IsValid() )
			return;

		foreach ( var extra in M700IronSightBodyGroups )
			ApplyBodyGroupSpec( skin, extra );
	}

	/// <summary>M700 with ranged sight — restore spawn-time stock bodygroups (integrated scope).</summary>
	public static void ApplyM700StockScopeBodyGroups( SkinnedModelRenderer skin )
	{
		if ( !skin.IsValid() || !skin.Model.IsValid() || _m700SpawnBodyGroups is null )
			return;

		foreach ( var (name, choice) in _m700SpawnBodyGroups )
			TrySetBodyGroup( skin, name, choice );
	}

	static void TrySetBodyGroup( SkinnedModelRenderer skin, string name, int choice )
	{
		var part = skin.Model.Parts.Get( name );
		if ( part is null || part.Choices.Count <= 1 )
			return;

		skin.SetBodyGroup( name, Math.Clamp( choice, 0, part.Choices.Count - 1 ) );
	}

	static void ApplyBodyGroupSpec( SkinnedModelRenderer skin, BodyGroupSpec spec )
	{
		if ( spec.NameCandidates is null || spec.NameCandidates.Length <= 0 )
			return;

		foreach ( var candidate in spec.NameCandidates )
		{
			var part = skin.Model.Parts.Get( candidate );
			if ( part is null || part.Choices.Count <= 1 )
				continue;

			skin.SetBodyGroup(
				candidate,
				Math.Clamp( spec.Choice, 0, part.Choices.Count - 1 ) );
			break;
		}
	}

	static bool AssignForegrip( AimboxWeaponId weapon, string modelPath, out VisualSpec spec )
	{
		var fallback = GetGripFallback( weapon );
		spec = new VisualSpec(
			modelPath,
			GripMountCandidates,
			HandguardCoverBodyGroups,
			1,
			RequiresWeaponPose( weapon ),
			"",
			Vector3.Zero,
			Rotation.Identity,
			fallback.MountBone,
			fallback.LocalPosition,
			Rotation.Identity );
		return true;
	}

	static bool AssignFlashlight( AimboxWeaponId weapon, out VisualSpec spec )
	{
		var fallback = GetFlashlightFallback( weapon );
		spec = new VisualSpec(
			FlashlightPath,
			FlashlightMountCandidates,
			[],
			0,
			false,
			"",
			Vector3.Zero,
			Rotation.Identity,
			fallback.MountBone,
			fallback.LocalPosition,
			Rotation.Identity );
		return true;
	}

	static bool AssignExtendedMag( AimboxWeaponId weapon, out VisualSpec spec )
	{
		var candidates = weapon switch
		{
			AimboxWeaponId.SpaghelliM4 => new[] { "magazine", "mag", "shell" },
			AimboxWeaponId.Usp => new[] { "magazine", "mag", "clip" },
			_ => new[] { "magazine", "mag" }
		};

		spec = new VisualSpec(
			"",
			[],
			candidates,
			1,
			false,
			"",
			Vector3.Zero,
			Rotation.Identity,
			"",
			Vector3.Zero,
			Rotation.Identity );
		return true;
	}

	static bool AssignSuppressor( AimboxWeaponId weapon, out VisualSpec spec )
	{
		var fallback = GetSuppressorFallback( weapon );
		spec = new VisualSpec(
			Suppressor9mmPath,
			MuzzleMountCandidates,
			["muzzle"],
			1,
			false,
			"",
			Vector3.Zero,
			Rotation.Identity,
			fallback.MountBone,
			fallback.LocalPosition,
			Rotation.Identity );
		return true;
	}

	static (string MountBone, Vector3 LocalPosition) GetOpticFallback( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.M4A1 => ("weapon_root", new Vector3( 2.35f, 0f, 2f )),
		AimboxWeaponId.M700 => ("weapon_root", new Vector3( 5.25f, 0f, 4.35f )),
		AimboxWeaponId.Mp5 => ("weapon_root", new Vector3( 1.85f, 0f, 1.55f )),
		AimboxWeaponId.Usp => ("weapon_root", new Vector3( 0.45f, 0f, 0.75f )),
		_ => ("weapon_root", new Vector3( 2f, 0f, 2f ))
	};

	static (string MountBone, Vector3 LocalPosition) GetGripFallback( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.M4A1 => ("weapon_root", new Vector3( 7f, 0f, -2f )),
		_ => ("weapon_root", new Vector3( 7f, 0f, -2f ))
	};

	static (string MountBone, Vector3 LocalPosition) GetFlashlightFallback( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.M4A1 => ("weapon_root", new Vector3( 5.5f, 0.55f, -1.2f )),
		AimboxWeaponId.Mp5 => ("weapon_root", new Vector3( 4.5f, 0.45f, -1f )),
		_ => ("weapon_root", new Vector3( 5f, 0.5f, -1f ))
	};

	static (string MountBone, Vector3 LocalPosition) GetSuppressorFallback( AimboxWeaponId weapon ) => weapon switch
	{
		AimboxWeaponId.M4A1 => ("weapon_root", new Vector3( 3.4f, 0.14f, 0.06f )),
		AimboxWeaponId.Mp5 => ("weapon_root", new Vector3( 2.4f, 0.11f, 0.05f )),
		AimboxWeaponId.Usp => ("weapon_root", new Vector3( 0.85f, 0f, 0.02f )),
		AimboxWeaponId.M700 => ("weapon_root", new Vector3( 4.6f, 0.1f, 0.05f )),
		AimboxWeaponId.SpaghelliM4 => ("weapon_root", new Vector3( 2.2f, 0.12f, 0.04f )),
		_ => ("weapon_root", new Vector3( 3f, 0.1f, 0.05f ))
	};
}
