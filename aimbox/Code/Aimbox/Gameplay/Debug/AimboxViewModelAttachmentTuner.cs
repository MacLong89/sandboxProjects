using System.Text;

namespace Sandbox;

/// <summary>
/// Press Play, equip a weapon (1–4), select Attachment Tuner, edit that weapon's groups below.
/// All weapon groups stay visible in the inspector — switch guns in-game to preview each one.
/// </summary>
[Title( "Aimbox Viewmodel Attachment Tuner" )]
[Category( "Aimbox/Debug" )]
[Icon( "build" )]
public sealed class AimboxViewModelAttachmentTuner : Component
{
	public static AimboxViewModelAttachmentTuner Instance { get; private set; }

	[Property] public bool OverrideGameplayMounts { get; set; } = false;

	[Property, ReadOnly, Group( "Status" )] public string PreviewWeapon { get; private set; } = "Waiting for player…";

	[Property, Group( "M4A1 — Red Dot" )] public Vector3 M4RedDotPosition { get; set; }
	[Property, Group( "M4A1 — Red Dot" )] public Angles M4RedDotRotation { get; set; }
	[Property, Group( "M4A1 — Red Dot" )] public string M4RedDotMountBone { get; set; } = "weapon_root";

	[Property, Group( "M4A1 — Recoil Grip" )] public Vector3 M4RecoilGripPosition { get; set; }
	[Property, Group( "M4A1 — Recoil Grip" )] public Angles M4RecoilGripRotation { get; set; }
	[Property, Group( "M4A1 — Recoil Grip" )] public string M4RecoilGripMountBone { get; set; } = "weapon_root";

	[Property, Group( "M4A1 — Suppressor" )] public Vector3 M4SuppressorPosition { get; set; }
	[Property, Group( "M4A1 — Suppressor" )] public Angles M4SuppressorRotation { get; set; }
	[Property, Group( "M4A1 — Suppressor" )] public string M4SuppressorMountBone { get; set; } = "muzzle";

	[Property, Group( "Shotgun — Suppressor" )] public Vector3 ShotgunSuppressorPosition { get; set; }
	[Property, Group( "Shotgun — Suppressor" )] public Angles ShotgunSuppressorRotation { get; set; }
	[Property, Group( "Shotgun — Suppressor" )] public string ShotgunSuppressorMountBone { get; set; } = "muzzle";

	[Property, Group( "M700 — Suppressor" )] public Vector3 M700SuppressorPosition { get; set; }
	[Property, Group( "M700 — Suppressor" )] public Angles M700SuppressorRotation { get; set; }
	[Property, Group( "M700 — Suppressor" )] public string M700SuppressorMountBone { get; set; } = "muzzle";

	[Property, Group( "M700 — Scope ADS" )]
	[Title( "Use tuned scope eye (inspector live while holding RMB)" )]
	public bool UseTunedM700ScopeEye { get; set; } = false;

	[Property, Group( "M700 — Scope ADS" )] public string M700ScopeEyeMountBone { get; set; } = "weapon_root";

	[Property, Group( "M700 — Scope ADS" )]
	[Title( "Scope eye on mount bone (+X forward, +Y left, +Z up)" )]
	public Vector3 M700ScopeEyePosition { get; set; }

	[Property, Group( "M700 — Scope ADS" )]
	[Title( "Extra view nudge at full ADS (+X forward, +Z up)" )]
	public Vector3 M700ScopeAdsFineTune { get; set; }

	[Property, Group( "M700 — Scope PiP" )]
	[Title( "Use tuned PiP screen layout (inspector or O tuner)" )]
	public bool UseTunedM700ScopePipLayout { get; set; } = false;

	[Property, Group( "M700 — Scope PiP" )]
	[Title( "Panel offset X (right +)" )]
	public float M700ScopePipOffsetX { get; set; }

	[Property, Group( "M700 — Scope PiP" )]
	[Title( "Panel offset Y (down +)" )]
	public float M700ScopePipOffsetY { get; set; }

	[Property, Group( "M700 — Scope PiP" )]
	[Title( "Radius scale multiplier" )]
	public float M700ScopePipRadiusScale { get; set; } = 1f;

	[Property, ReadOnly, Group( "M700 — Scope ADS" )] public string M700ScopeDebug { get; private set; } = "";

	[Property, Group( "USP — Suppressor" )] public Vector3 UspSuppressorPosition { get; set; }
	[Property, Group( "USP — Suppressor" )] public Angles UspSuppressorRotation { get; set; }
	[Property, Group( "USP — Suppressor" )] public string UspSuppressorMountBone { get; set; } = "muzzle";

	[Property, ReadOnly, Group( "When Done" ), Title( "Paste into AimboxSboxAttachmentCatalog.cs" )]
	public string CatalogCopyPaste { get; private set; } = "";

	readonly List<MountedTuneEntry> _mounted = [];
	SkinnedModelRenderer _targetSkin;
	AimboxWeaponId _targetWeapon;
	int _revision = -1;
	float _boneWait;

	struct MountedTuneEntry
	{
		public AimboxAttachmentId Attachment;
		public GameObject Object;
		public string MountBoneName;
	}

	protected override void OnStart()
	{
		Instance = this;
		PullAllDefaultsFromCatalog();
	}

	protected override void OnEnabled()
	{
		Instance = this;
		if ( OverrideGameplayMounts )
			AimboxViewModelAttachmentDebug.Enabled = true;
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;

		ClearMountedAttachments();
	}

	protected override void OnUpdate()
	{
		if ( !TryResolveTarget( out var skin, out var weaponId, out var attachments ) )
		{
			PreviewWeapon = "Equip a weapon (keys 1–4) while playing.";
			ClearMountedAttachments();
			_targetSkin = default;
			return;
		}

		PreviewWeapon = $"{weaponId} — edit the matching groups below while this weapon is equipped.";

		if ( skin != _targetSkin || weaponId != _targetWeapon )
		{
			_targetSkin = skin;
			_targetWeapon = weaponId;
			_revision = -1;
			_boneWait = 0f;
		}

		var revision = ComputeRevision( weaponId );
		if ( revision != _revision )
		{
			_revision = revision;
			RemountAll( skin, weaponId, attachments );
		}

		ApplyLiveTransforms( weaponId, attachments );
		UpdateCatalogCopyPaste( weaponId, attachments );
		UpdateScopeDebug( skin, weaponId );
		SyncScopePipLayoutFromInspector();
	}

	public bool TryGetTunedM700ScopeEyeSkinLocal( SkinnedModelRenderer skin, out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !UseTunedM700ScopeEye || !skin.IsValid() )
			return false;

		return AimboxViewModelSightResolve.TryGetWeaponRootOffsetSkinLocal(
			skin,
			M700ScopeEyeMountBone,
			M700ScopeEyePosition,
			out skinLocal );
	}

	void UpdateScopeDebug( SkinnedModelRenderer skin, AimboxWeaponId weaponId )
	{
		if ( weaponId != AimboxWeaponId.M700 || !skin.IsValid() )
		{
			M700ScopeDebug = "";
			return;
		}

		if ( !TryGetTunedM700ScopeEyeSkinLocal( skin, out var skinLocal ) )
		{
			M700ScopeDebug = "Scope eye unresolved — check mount bone name.";
			return;
		}

		var applied = Vector3.Zero;
		foreach ( var viewModel in Scene.GetAllComponents<AimboxViewModelController>() )
		{
			if ( !viewModel.IsValid() || viewModel.WeaponSkin != skin )
				continue;

			applied = viewModel.DebugSightEyeViewmodelOffset;
			break;
		}

		M700ScopeDebug = $"bone={M700ScopeEyeMountBone} pos={M700ScopeEyePosition} skinLocal={skinLocal} fine={M700ScopeAdsFineTune} appliedVmOffset={applied}";
	}

	void SyncScopePipLayoutFromInspector()
	{
		if ( !UseTunedM700ScopePipLayout || AimboxM700ScopePipTuner.IsActive )
			return;

		AimboxM700ScopePipLayout.PanelOffset = new Vector2( M700ScopePipOffsetX, M700ScopePipOffsetY );
		AimboxM700ScopePipLayout.RadiusScale = M700ScopePipRadiusScale;
	}

	void PullAllDefaultsFromCatalog()
	{
		PullCatalogDefaults( AimboxWeaponId.M4A1, AimboxAttachmentId.RaisedRedDot,
			v => M4RedDotMountBone = v, v => M4RedDotPosition = v, v => M4RedDotRotation = v );
		PullCatalogDefaults( AimboxWeaponId.M4A1, AimboxAttachmentId.ForegripStraight,
			v => M4RecoilGripMountBone = v, v => M4RecoilGripPosition = v, v => M4RecoilGripRotation = v );
		PullCatalogDefaults( AimboxWeaponId.M4A1, AimboxAttachmentId.Suppressor,
			v => M4SuppressorMountBone = v, v => M4SuppressorPosition = v, v => M4SuppressorRotation = v );
		PullCatalogDefaults( AimboxWeaponId.SpaghelliM4, AimboxAttachmentId.Suppressor,
			v => ShotgunSuppressorMountBone = v, v => ShotgunSuppressorPosition = v, v => ShotgunSuppressorRotation = v );
		PullCatalogDefaults( AimboxWeaponId.M700, AimboxAttachmentId.Suppressor,
			v => M700SuppressorMountBone = v, v => M700SuppressorPosition = v, v => M700SuppressorRotation = v );
		M700ScopeEyeMountBone = "weapon_root";
		M700ScopeEyePosition = AimboxAdsSightTuning.M700ScopeEyeWeaponRootOffset;
		M700ScopePipOffsetX = AimboxAdsSightTuning.M700ScopePipCenterXOffsetPixels;
		M700ScopePipOffsetY = AimboxAdsSightTuning.M700ScopePipCenterYOffsetPixels;
		M700ScopePipRadiusScale = AimboxAdsSightTuning.M700ScopePipRadiusScale;
		PullCatalogDefaults( AimboxWeaponId.Usp, AimboxAttachmentId.Suppressor,
			v => UspSuppressorMountBone = v, v => UspSuppressorPosition = v, v => UspSuppressorRotation = v );
	}

	static void PullCatalogDefaults(
		AimboxWeaponId weaponId,
		AimboxAttachmentId attachmentId,
		Action<string> setMountBone,
		Action<Vector3> setPosition,
		Action<Angles> setRotation )
	{
		if ( !AimboxSboxAttachmentCatalog.TryGetVisual( weaponId, attachmentId, out var visual ) )
			return;

		setMountBone( string.IsNullOrWhiteSpace( visual.FallbackMountBone ) ? "weapon_root" : visual.FallbackMountBone );
		setPosition( visual.FallbackLocalPosition );
		setRotation( visual.FallbackLocalRotation.Angles() );
	}

	bool TryResolveTarget(
		out SkinnedModelRenderer skin,
		out AimboxWeaponId weaponId,
		out IReadOnlyList<AimboxAttachmentId> attachments )
	{
		skin = default;
		weaponId = default;
		attachments = [];

		if ( !TryFindLocalPlayer( out var player ) )
			return false;

		weaponId = player.ActiveWeapon;
		attachments = player.CurrentWeapon?.Attachments?.ToList() ?? [];

		foreach ( var viewModel in Scene.GetAllComponents<AimboxViewModelController>() )
		{
			if ( !viewModel.IsValid() || !viewModel.WeaponSkin.IsValid() )
				continue;

			if ( !IsDescendantOf( viewModel.GameObject, player.GameObject ) )
				continue;

			skin = viewModel.WeaponSkin;
			return skin.Model.IsValid() && !skin.Model.IsError;
		}

		return false;
	}

	bool TryFindLocalPlayer( out AimboxPlayerController player )
	{
		player = default;
		foreach ( var candidate in Scene.GetAllComponents<AimboxPlayerController>() )
		{
			if ( candidate.IsProxy )
				continue;

			player = candidate;
			return true;
		}

		return false;
	}

	static bool IsDescendantOf( GameObject child, GameObject ancestor )
	{
		for ( var walk = child; walk.IsValid(); walk = walk.Parent )
		{
			if ( walk == ancestor )
				return true;
		}

		return false;
	}

	bool TryGetTuneSlot(
		AimboxWeaponId weaponId,
		AimboxAttachmentId attachmentId,
		out string mountBone,
		out Vector3 position,
		out Angles rotation )
	{
		mountBone = "weapon_root";
		position = default;
		rotation = default;

		switch ( weaponId, attachmentId )
		{
			case (AimboxWeaponId.M4A1, AimboxAttachmentId.RaisedRedDot):
				mountBone = M4RedDotMountBone;
				position = M4RedDotPosition;
				rotation = M4RedDotRotation;
				return true;
			case (AimboxWeaponId.M4A1, AimboxAttachmentId.ForegripStraight):
				mountBone = M4RecoilGripMountBone;
				position = M4RecoilGripPosition;
				rotation = M4RecoilGripRotation;
				return true;
			case (AimboxWeaponId.M4A1, AimboxAttachmentId.Suppressor):
				mountBone = M4SuppressorMountBone;
				position = M4SuppressorPosition;
				rotation = M4SuppressorRotation;
				return true;
			case (AimboxWeaponId.SpaghelliM4, AimboxAttachmentId.Suppressor):
				mountBone = ShotgunSuppressorMountBone;
				position = ShotgunSuppressorPosition;
				rotation = ShotgunSuppressorRotation;
				return true;
			case (AimboxWeaponId.M700, AimboxAttachmentId.Suppressor):
				mountBone = M700SuppressorMountBone;
				position = M700SuppressorPosition;
				rotation = M700SuppressorRotation;
				return true;
			case (AimboxWeaponId.Usp, AimboxAttachmentId.Suppressor):
				mountBone = UspSuppressorMountBone;
				position = UspSuppressorPosition;
				rotation = UspSuppressorRotation;
				return true;
			default:
				return false;
		}
	}

	IEnumerable<(AimboxAttachmentId Attachment, string MountBone, Vector3 Position, Angles Rotation)> MeshTuneSlots(
		AimboxWeaponId weaponId,
		IReadOnlyList<AimboxAttachmentId> equipped )
	{
		foreach ( var attachment in equipped )
		{
			if ( !TryGetTuneSlot( weaponId, attachment, out var mountBone, out var position, out var rotation ) )
				continue;

			if ( !AimboxSboxAttachmentCatalog.TryGetVisual( weaponId, attachment, out var visual ) )
				continue;

			if ( string.IsNullOrWhiteSpace( visual.ModelPath ) )
				continue;

			yield return (
				attachment,
				ResolveMountBoneName( mountBone, visual ),
				position,
				rotation );
		}
	}

	static string ResolveMountBoneName( string mountBone, AimboxSboxAttachmentCatalog.VisualSpec visual ) =>
		string.IsNullOrWhiteSpace( mountBone )
			? string.IsNullOrWhiteSpace( visual.FallbackMountBone ) ? "weapon_root" : visual.FallbackMountBone
			: mountBone;

	void RemountAll(
		SkinnedModelRenderer skin,
		AimboxWeaponId weaponId,
		IReadOnlyList<AimboxAttachmentId> equipped )
	{
		ClearMountedAttachments();

		AimboxViewModelAttachmentMount.ApplyAttachmentBodyGroups( skin, weaponId, equipped );

		_boneWait += Time.Delta;
		if ( _boneWait < 0.05f )
		{
			_revision = -1;
			return;
		}

		foreach ( var slot in MeshTuneSlots( weaponId, equipped ) )
			TryMountSlot( skin, weaponId, slot.Attachment, slot.MountBone, slot.Position, slot.Rotation );

		if ( _mounted.Count <= 0 && _boneWait < 2f && MeshTuneSlots( weaponId, equipped ).Any() )
			_revision = -1;

		skin.GameObject.Components.Get<AimboxViewModelController>()?.Animator?.SetWeaponPose(
			AimboxSboxAttachmentCatalog.RequiresWeaponPose( weaponId ) ? 1 : 0 );
	}

	void TryMountSlot(
		SkinnedModelRenderer skin,
		AimboxWeaponId weaponId,
		AimboxAttachmentId attachmentId,
		string mountBoneName,
		Vector3 localPosition,
		Angles localRotation )
	{
		if ( !AimboxSboxAttachmentCatalog.TryGetVisual( weaponId, attachmentId, out var visual ) )
			return;

		if ( string.IsNullOrWhiteSpace( visual.ModelPath ) )
			return;

		var model = AimboxWeaponResourceLoad.LoadWeaponModelOrFallback(
			visual.ModelPath,
			$"Tuner {attachmentId}",
			out _ );
		if ( !model.IsValid() || model.IsError )
			return;

		if ( !AimboxViewModelAttachmentMount.TryResolveMountBone( skin, mountBoneName, out var mountBone ) )
			return;

		var go = new GameObject( true, $"Tuner_{weaponId}_{attachmentId}" );
		go.SetParent( mountBone );
		go.LocalPosition = localPosition;
		go.LocalRotation = Rotation.From( localRotation );
		go.LocalScale = Vector3.One;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		renderer.RenderOptions.Game = true;
		renderer.RenderOptions.Overlay = skin.RenderOptions.Overlay;
		renderer.Enabled = true;

		_mounted.Add( new MountedTuneEntry
		{
			Attachment = attachmentId,
			Object = go,
			MountBoneName = mountBoneName
		} );
	}

	void ApplyLiveTransforms( AimboxWeaponId weaponId, IReadOnlyList<AimboxAttachmentId> equipped )
	{
		if ( !_targetSkin.IsValid() )
			return;

		var remount = false;
		foreach ( var slot in MeshTuneSlots( weaponId, equipped ) )
		{
			var mounted = _mounted.FirstOrDefault( x => x.Attachment == slot.Attachment );
			if ( !mounted.Object.IsValid() )
				continue;

			if ( !string.Equals( mounted.MountBoneName, slot.MountBone, StringComparison.OrdinalIgnoreCase ) )
			{
				remount = true;
				break;
			}

			mounted.Object.LocalPosition = slot.Position;
			mounted.Object.LocalRotation = Rotation.From( slot.Rotation );
		}

		if ( remount )
			_revision = -1;
	}

	int ComputeRevision( AimboxWeaponId weaponId )
	{
		var hash = new HashCode();
		hash.Add( weaponId );
		hash.Add( M4RedDotMountBone );
		hash.Add( M4RedDotPosition );
		hash.Add( M4RedDotRotation );
		hash.Add( M4RecoilGripMountBone );
		hash.Add( M4RecoilGripPosition );
		hash.Add( M4RecoilGripRotation );
		hash.Add( M4SuppressorMountBone );
		hash.Add( M4SuppressorPosition );
		hash.Add( M4SuppressorRotation );
		hash.Add( ShotgunSuppressorMountBone );
		hash.Add( ShotgunSuppressorPosition );
		hash.Add( ShotgunSuppressorRotation );
		hash.Add( M700SuppressorMountBone );
		hash.Add( M700SuppressorPosition );
		hash.Add( M700SuppressorRotation );
		hash.Add( UspSuppressorMountBone );
		hash.Add( UspSuppressorPosition );
		hash.Add( UspSuppressorRotation );
		return hash.ToHashCode();
	}

	void UpdateCatalogCopyPaste( AimboxWeaponId weaponId, IReadOnlyList<AimboxAttachmentId> equipped )
	{
		var sb = new StringBuilder();
		sb.AppendLine( $"// {weaponId} — paste into AimboxSboxAttachmentCatalog.cs" );
		foreach ( var slot in MeshTuneSlots( weaponId, equipped ) )
		{
			sb.AppendLine();
			sb.AppendLine( $"// {slot.Attachment}" );
			sb.AppendLine( $"FallbackMountBone = \"{slot.MountBone}\"" );
			sb.AppendLine( $"new Vector3( {slot.Position.x:F2}f, {slot.Position.y:F2}f, {slot.Position.z:F2}f )," );
			sb.AppendLine( $"Rotation.From( new Angles( {slot.Rotation.pitch:F2}f, {slot.Rotation.yaw:F2}f, {slot.Rotation.roll:F2}f ) )," );
		}

		if ( weaponId == AimboxWeaponId.M700 )
		{
			sb.AppendLine();
			sb.AppendLine( "// Scope ADS eye — paste into AimboxAdsSightTuning.cs" );
			sb.AppendLine( $"M700ScopeEyeWeaponRootOffset = new( {M700ScopeEyePosition.x:F2}f, {M700ScopeEyePosition.y:F2}f, {M700ScopeEyePosition.z:F2}f );" );
			if ( M700ScopeAdsFineTune != Vector3.Zero )
				sb.AppendLine( $"// Fine tune (view-local): new( {M700ScopeAdsFineTune.x:F2}f, {M700ScopeAdsFineTune.y:F2}f, {M700ScopeAdsFineTune.z:F2}f )" );
		}

		CatalogCopyPaste = sb.ToString();
	}

	void ClearMountedAttachments()
	{
		foreach ( var entry in _mounted )
		{
			if ( entry.Object.IsValid() )
				entry.Object.Destroy();
		}

		_mounted.Clear();
	}

	public void SetRedDotMeshVisible( bool visible )
	{
		foreach ( var entry in _mounted )
		{
			if ( entry.Attachment != AimboxAttachmentId.RaisedRedDot || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( renderer.IsValid() )
				renderer.Enabled = visible;
		}
	}

	public void ApplyRedDotLensPresentation( bool useClearLens )
	{
		var profile = AimboxOpticLensPresentation.GetRedDotLensProfile( AimboxAttachmentId.RaisedRedDot );
		var hideLens = useClearLens || AimboxOpticLensPresentation.ShouldAlwaysHideM4RedDotLens( profile );

		foreach ( var entry in _mounted )
		{
			if ( entry.Attachment != AimboxAttachmentId.RaisedRedDot || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( renderer.IsValid() )
				AimboxOpticLensPresentation.Apply( renderer, hideLens, profile );
		}
	}

	public bool TryGetTunedAttachmentOriginSkinLocal(
		AimboxAttachmentId attachment,
		SkinnedModelRenderer skin,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.IsValid() )
			return false;

		foreach ( var entry in _mounted )
		{
			if ( entry.Attachment != attachment || !entry.Object.IsValid() )
				continue;

			var originWorld = entry.Object.WorldPosition;
			if ( attachment == AimboxAttachmentId.RaisedRedDot )
			{
				var eyeOffset = AimboxSboxAttachmentCatalog.GetRedDotAdsEyeAttachmentOffset( attachment );
				if ( eyeOffset != Vector3.Zero )
					originWorld = entry.Object.WorldTransform.PointToWorld( eyeOffset );
			}

			skinLocal = skin.WorldTransform.PointToLocal( originWorld );
			return true;
		}

		return false;
	}

	public void UpdateRedDotAdsMesh( bool useRaised )
	{
		if ( _redDotUsingRaised == useRaised )
			return;

		_redDotUsingRaised = useRaised;

		foreach ( var entry in _mounted )
		{
			if ( entry.Attachment != AimboxAttachmentId.RaisedRedDot || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( !renderer.IsValid() )
				continue;

			var path = AimboxSboxAttachmentCatalog.GetRaisedRedDotAdsModelPath( _targetWeapon, useRaised );
			var model = AimboxWeaponResourceLoad.LoadWeaponModelOrFallback( path, "FP red dot ADS", out _ );
			if ( !model.IsValid() || model.IsError )
				continue;

			renderer.Model = model;
			if ( renderer.SceneObject.IsValid() )
				renderer.RenderOptions.Apply( renderer.SceneObject );
		}
	}

	bool _redDotUsingRaised;
}
