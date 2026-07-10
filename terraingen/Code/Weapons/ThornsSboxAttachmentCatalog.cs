namespace Sandbox;

using Terraingen.Combat.Attachments;

/// <summary>Maps Thorns attachment IDs to facepunch/sboxweapons FP visuals (bodygroups + child models).</summary>
public static class ThornsSboxAttachmentCatalog
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
	static readonly string[] TopRailBodyGroups = ["top_rail", "rail", "sight", "optic", "scope", "iron"];

	public static bool TryGetVisual( string combatWeaponId, ThornsAttachmentId attachment, out VisualSpec spec )
	{
		spec = default;
		combatWeaponId = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId );
		if ( !ThornsAttachmentCatalog.IsCompatible( combatWeaponId, attachment ) )
			return false;

		return attachment switch
		{
			ThornsAttachmentId.HoloSight => AssignHoloSight( combatWeaponId, out spec ),
			ThornsAttachmentId.RaisedRedDot => AssignRaisedRedDot( combatWeaponId, out spec ),
			ThornsAttachmentId.RangedSight => AssignRangedSight( combatWeaponId, out spec ),
			ThornsAttachmentId.ForegripStraight => AssignForegrip( combatWeaponId, ForegripStraightPath, out spec ),
			ThornsAttachmentId.ForegripAngled => AssignForegrip( combatWeaponId, ForegripAngledPath, out spec ),
			ThornsAttachmentId.Flashlight => AssignFlashlight( combatWeaponId, out spec ),
			ThornsAttachmentId.ExtendedMag => AssignExtendedMag( combatWeaponId, out spec ),
			ThornsAttachmentId.Suppressor => AssignSuppressor( combatWeaponId, out spec ),
			_ => false
		};
	}

	public static bool RequiresWeaponPose( string combatWeaponId ) =>
		ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) == "m4";

	public static float GetRedDotLocalScale( ThornsAttachmentId attachment ) => attachment switch
	{
		ThornsAttachmentId.HoloSight => 0.68f,
		_ => 1f
	};

	public static Vector3 GetRedDotAdsEyeAttachmentOffset( ThornsAttachmentId attachment ) => attachment switch
	{
		ThornsAttachmentId.HoloSight => ThornsAdsSightTuning.HolographicRedDotEyeAttachmentOffset,
		_ => Vector3.Zero
	};

	public static Vector3 GetRangedSightAdsEyeAttachmentOffset() =>
		ThornsAdsSightTuning.RangedSightAdsEyeAttachmentOffset;

	public static Vector3 ResolveRangedSightLensLocalOffset( Model model )
	{
		if ( !model.IsValid() || model.IsError )
			return GetRangedSightAdsEyeAttachmentOffset();

		var bounds = model.RenderBounds;
		if ( bounds.Size.Length < 0.001f )
			return GetRangedSightAdsEyeAttachmentOffset();

		var center = bounds.Center;
		var half = bounds.Size * 0.5f;
		return new Vector3( center.x - half.x, center.y, center.z )
		       + ThornsAdsSightTuning.RangedSightLensBoundsFineTune;
	}

	public static string GetRaisedRedDotAdsModelPath( string combatWeaponId, bool useRaised )
	{
		if ( !useRaised )
			return ResolveRedDotModelPath( combatWeaponId );

		return ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) == "usp"
			? RedDotRmrPath
			: RedDotRmrRaisedPath;
	}

	static string ResolveRedDotModelPath( string combatWeaponId ) =>
		ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) == "usp"
			? RedDotRmrPath
			: RedDotRmrRaisedPath;

	static bool AssignHoloSight( string combatWeaponId, out VisualSpec spec )
	{
		spec = BuildOpticSpec(
			combatWeaponId,
			HolographicSightPath,
			0.68f,
			OpticExtraBodyGroupsFor( combatWeaponId ),
			GetOpticFallback( combatWeaponId ) );
		return true;
	}

	static bool AssignRaisedRedDot( string combatWeaponId, out VisualSpec spec )
	{
		spec = BuildOpticSpec(
			combatWeaponId,
			ResolveRedDotModelPath( combatWeaponId ),
			1f,
			OpticExtraBodyGroupsFor( combatWeaponId ),
			GetOpticFallback( combatWeaponId ) );
		return true;
	}

	static bool AssignRangedSight( string combatWeaponId, out VisualSpec spec )
	{
		if ( ThornsAttachmentCatalog.UsesIntegratedSniperScope( combatWeaponId ) )
		{
			spec = default;
			return false;
		}

		spec = BuildOpticSpec(
			combatWeaponId,
			RangedSightPath,
			1f,
			OpticExtraBodyGroupsFor( combatWeaponId ),
			GetOpticFallback( combatWeaponId ) );
		return true;
	}

	static VisualSpec BuildOpticSpec(
		string combatWeaponId,
		string modelPath,
		float localScale,
		BodyGroupSpec[] extraBodyGroups,
		(string MountBone, Vector3 LocalPosition) fallback ) =>
		new(
			modelPath,
			OpticMountCandidates,
			TopRailBodyGroups,
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

	static BodyGroupSpec[] OpticExtraBodyGroupsFor( string combatWeaponId ) =>
		ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) == "m4"
			? M4OpticFrontIronSuppression
			: null;

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

	public static void ApplyM700IronSightBodyGroups( SkinnedModelRenderer skin )
	{
		if ( !skin.IsValid() || !skin.Model.IsValid() )
			return;

		foreach ( var extra in M700IronSightBodyGroups )
			ApplyBodyGroupSpec( skin, extra );
	}

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

	static bool AssignForegrip( string combatWeaponId, string modelPath, out VisualSpec spec )
	{
		var fallback = GetGripFallback( combatWeaponId );
		spec = new VisualSpec(
			modelPath,
			GripMountCandidates,
			HandguardCoverBodyGroups,
			1,
			RequiresWeaponPose( combatWeaponId ),
			"",
			Vector3.Zero,
			Rotation.Identity,
			fallback.MountBone,
			fallback.LocalPosition,
			Rotation.Identity );
		return true;
	}

	static bool AssignFlashlight( string combatWeaponId, out VisualSpec spec )
	{
		var fallback = GetFlashlightFallback( combatWeaponId );
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

	static bool AssignExtendedMag( string combatWeaponId, out VisualSpec spec )
	{
		var candidates = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) switch
		{
			"shotgun" => new[] { "magazine", "mag", "shell" },
			"usp" => new[] { "magazine", "mag", "clip" },
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

	static bool AssignSuppressor( string combatWeaponId, out VisualSpec spec )
	{
		var fallback = GetSuppressorFallback( combatWeaponId );
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

	static (string MountBone, Vector3 LocalPosition) GetOpticFallback( string combatWeaponId ) =>
		ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) switch
		{
			"m4" => ("weapon_root", new Vector3( 2.35f, 0f, 2f )),
			"sniper" => ("weapon_root", new Vector3( 5.25f, 0f, 4.35f )),
			"mp5" => ("weapon_root", new Vector3( 1.85f, 0f, 1.55f )),
			"usp" => ("weapon_root", new Vector3( 0.45f, 0f, 0.75f )),
			_ => ("weapon_root", new Vector3( 2f, 0f, 2f ))
		};

	static (string MountBone, Vector3 LocalPosition) GetGripFallback( string combatWeaponId ) =>
		("weapon_root", new Vector3( 7f, 0f, -2f ));

	static (string MountBone, Vector3 LocalPosition) GetFlashlightFallback( string combatWeaponId ) =>
		ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) switch
		{
			"m4" => ("weapon_root", new Vector3( 5.5f, 0.55f, -1.2f )),
			"mp5" => ("weapon_root", new Vector3( 4.5f, 0.45f, -1f )),
			_ => ("weapon_root", new Vector3( 5f, 0.5f, -1f ))
		};

	static (string MountBone, Vector3 LocalPosition) GetSuppressorFallback( string combatWeaponId ) =>
		ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId ) switch
		{
			"m4" => ("weapon_root", new Vector3( 3.4f, 0.14f, 0.06f )),
			"mp5" => ("weapon_root", new Vector3( 2.4f, 0.11f, 0.05f )),
			"usp" => ("weapon_root", new Vector3( 0.85f, 0f, 0.02f )),
			"sniper" => ("weapon_root", new Vector3( 4.6f, 0.1f, 0.05f )),
			"shotgun" => ("weapon_root", new Vector3( 2.2f, 0.12f, 0.04f )),
			_ => ("weapon_root", new Vector3( 3f, 0.1f, 0.05f ))
		};
}
