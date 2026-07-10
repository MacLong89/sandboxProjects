using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>Parents sboxweapons attachment meshes onto a FP weapon viewmodel.</summary>
[Title( "Aimbox Viewmodel Attachment Mount" )]
[Category( "Aimbox" )]
public sealed class AimboxViewModelAttachmentMount : Component
{
	readonly List<SpawnedAttachmentEntry> _spawned = [];
	HashSet<AimboxAttachmentId> _equipped = [];
	readonly Dictionary<string, int> _bodyGroupSnapshot = [];

	SkinnedModelRenderer _weaponSkin;
	AimboxWeaponId _weaponId;
	bool _overlayPass = true;
	bool _requiresWeaponPose;
	bool _redDotUsingRaised;

	public bool RequiresWeaponPose => _requiresWeaponPose;
	public ModelRenderer SuppressorRenderer { get; private set; }

	public bool HasAttachment( AimboxAttachmentId attachment ) => _equipped.Contains( attachment );

	public bool HasSpawnedAttachmentMesh( AimboxAttachmentId attachment )
	{
		foreach ( var entry in _spawned )
		{
			if ( entry.Attachment == attachment && entry.Object.IsValid() )
				return true;
		}

		return false;
	}

	struct SpawnedAttachmentEntry
	{
		public GameObject Object;
		public AimboxAttachmentId Attachment;
	}

	public void SetMeshesVisibleForAttachment( AimboxAttachmentId attachment, bool visible )
	{
		foreach ( var entry in _spawned )
		{
			if ( entry.Attachment != attachment || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( renderer.IsValid() )
				renderer.Enabled = visible;
		}
	}

	public void SetRedDotStyleMeshesVisible( bool visible )
	{
		foreach ( var attachment in AimboxAttachmentCatalog.RedDotStyleAttachments )
			SetMeshesVisibleForAttachment( attachment, visible );
	}

	public bool HasRedDotStyleAttachment()
	{
		foreach ( var attachment in AimboxAttachmentCatalog.RedDotStyleAttachments )
		{
			if ( HasAttachment( attachment ) )
				return true;
		}

		return false;
	}

	public bool TryGetEquippedRedDotStyleAttachment( out AimboxAttachmentId attachment )
	{
		foreach ( var candidate in AimboxAttachmentCatalog.RedDotStyleAttachments )
		{
			if ( HasAttachment( candidate ) )
			{
				attachment = candidate;
				return true;
			}
		}

		attachment = default;
		return false;
	}

	static bool IsRedDotStyleAttachment( AimboxAttachmentId attachment ) =>
		AimboxAttachmentCatalog.IsRedDotStyle( attachment );

	/// <summary>Swap holo / RMR lens to transparent material during world-pass ADS.</summary>
	public void ApplyRedDotLensPresentation( bool useClearLens )
	{
		if ( !TryGetEquippedRedDotStyleAttachment( out var equippedSight ) )
			return;

		var profile = AimboxOpticLensPresentation.GetRedDotLensProfile( equippedSight );
		var hideLens = useClearLens || AimboxOpticLensPresentation.ShouldAlwaysHideM4RedDotLens( profile );

		if ( profile == AimboxOpticLensProfile.HolographicAttachment && _weaponSkin.IsValid() )
			AimboxOpticLensPresentation.Apply( _weaponSkin, hideLens, profile );

		foreach ( var entry in _spawned )
		{
			if ( !IsRedDotStyleAttachment( entry.Attachment ) || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( renderer.IsValid() )
				AimboxOpticLensPresentation.Apply( renderer, hideLens, profile );
		}
	}

	public bool TryGetAttachmentOriginSkinLocal(
		AimboxAttachmentId attachment,
		SkinnedModelRenderer skin,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.IsValid() )
			return false;

		foreach ( var entry in _spawned )
		{
			if ( entry.Attachment != attachment || !entry.Object.IsValid() )
				continue;

			var originWorld = entry.Object.WorldPosition;
			if ( IsRedDotStyleAttachment( attachment ) )
			{
				var eyeOffset = AimboxSboxAttachmentCatalog.GetRedDotAdsEyeAttachmentOffset( attachment );
				if ( eyeOffset != Vector3.Zero )
					originWorld = entry.Object.WorldTransform.PointToWorld( eyeOffset );
			}
			else if ( attachment == AimboxAttachmentId.RangedSight )
			{
				var renderer = entry.Object.Components.Get<ModelRenderer>();
				var lensLocal = renderer.IsValid() && renderer.Model.IsValid()
					? AimboxSboxAttachmentCatalog.ResolveRangedSightLensLocalOffset( renderer.Model )
					: AimboxSboxAttachmentCatalog.GetRangedSightAdsEyeAttachmentOffset();
				originWorld = entry.Object.WorldTransform.PointToWorld( lensLocal );
			}

			skinLocal = skin.WorldTransform.PointToLocal( originWorld );
			return true;
		}

		return false;
	}

	public bool TryGetRangedSightLensSkinLocal( SkinnedModelRenderer skin, out Vector3 skinLocal ) =>
		TryGetAttachmentOriginSkinLocal( AimboxAttachmentId.RangedSight, skin, out skinLocal );

	public void UpdateRedDotAdsMesh( bool useRaised )
	{
		if ( !HasAttachment( AimboxAttachmentId.RaisedRedDot ) )
			return;

		if ( _redDotUsingRaised == useRaised )
			return;

		_redDotUsingRaised = useRaised;

		foreach ( var entry in _spawned )
		{
			if ( entry.Attachment != AimboxAttachmentId.RaisedRedDot || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( !renderer.IsValid() )
				continue;

			var path = AimboxSboxAttachmentCatalog.GetRaisedRedDotAdsModelPath( _weaponId, useRaised );
			var model = AimboxWeaponResourceLoad.LoadWeaponModelOrFallback( path, "FP red dot ADS", out _ );
			if ( !model.IsValid() || model.IsError )
				continue;

			renderer.Model = model;
			if ( renderer.SceneObject.IsValid() )
				renderer.RenderOptions.Apply( renderer.SceneObject );
		}
	}

	public void Apply(
		AimboxWeaponId weaponId,
		SkinnedModelRenderer weaponSkin,
		IEnumerable<AimboxAttachmentId> attachments,
		bool overlayPass )
	{
		if ( !weaponSkin.IsValid() )
		{
			AimboxViewModelAttachmentDebug.Warn( "Apply aborted — weapon renderer invalid." );
			return;
		}

		var next = new HashSet<AimboxAttachmentId>( attachments ?? [] );
		if ( _weaponId == weaponId && _weaponSkin == weaponSkin && next.SetEquals( _equipped ) && overlayPass == _overlayPass )
			return;

		if ( AimboxViewModelAttachmentTuner.Instance is { OverrideGameplayMounts: true } )
		{
			AimboxViewModelAttachmentMount.ApplyAttachmentBodyGroups( weaponSkin, weaponId, next );
			foreach ( var entry in _spawned )
			{
				if ( entry.Object.IsValid() )
					entry.Object.Destroy();
			}

			_spawned.Clear();
			_equipped = next;
			_weaponId = weaponId;
			_weaponSkin = weaponSkin;
			_overlayPass = overlayPass;
			_requiresWeaponPose = false;
			SuppressorRenderer = default;
			return;
		}

		AimboxViewModelAttachmentDebug.Info(
			$"Apply weapon={weaponId} overlay={overlayPass} attachments=[{FormatAttachmentList( next )}] weaponModel={weaponSkin.Model?.ResourceName}" );

		Clear();
		_weaponId = weaponId;
		_weaponSkin = weaponSkin;
		_equipped = next;
		_overlayPass = overlayPass;
		_requiresWeaponPose = false;
		SuppressorRenderer = default;

		weaponSkin.CreateBoneObjects = true;
		weaponSkin.CreateAttachments = true;

		foreach ( var attachment in next )
		{
			if ( !AimboxSboxAttachmentCatalog.TryGetVisual( weaponId, attachment, out var visual ) )
			{
				AimboxViewModelAttachmentDebug.Info( $"Skip {attachment} — no visual spec for {weaponId}." );
				continue;
			}

			TrySetBodyGroup( weaponSkin, attachment, visual.BodyGroupNameCandidates, visual.BodyGroupChoice );
			ApplyExtraBodyGroups( weaponSkin, attachment, visual.ExtraBodyGroups );

			if ( visual.RequiresWeaponPose && AimboxSboxAttachmentCatalog.RequiresWeaponPose( weaponId ) )
				_requiresWeaponPose = true;

			if ( string.IsNullOrWhiteSpace( visual.ModelPath ) )
			{
				AimboxViewModelAttachmentDebug.Info( $"Skip {attachment} mesh — bodygroup-only visual." );
				continue;
			}

			var model = AimboxWeaponResourceLoad.LoadWeaponModelOrFallback(
				visual.ModelPath,
				$"FP attachment {attachment}",
				out var usedFallbackGeometry );
			if ( !model.IsValid() || model.IsError )
			{
				AimboxViewModelAttachmentDebug.Warn(
					$"Model load failed for {attachment}: path='{visual.ModelPath}' fallback={usedFallbackGeometry} error={model.IsError}" );
				continue;
			}

			AimboxViewModelAttachmentDebug.Info(
				$"Loaded {attachment} model='{model.ResourceName}' path='{visual.ModelPath}' usedFallback={usedFallbackGeometry}" );

			var child = SpawnAttachmentRenderer( model, attachment );
			_spawned.Add( new SpawnedAttachmentEntry { Object = child, Attachment = attachment } );
			_ = ParentToMountAsync( child, weaponSkin, visual, attachment );

			if ( attachment == AimboxAttachmentId.Suppressor && child.Components.Get<ModelRenderer>() is { } suppressorRenderer )
				SuppressorRenderer = suppressorRenderer;
		}
	}

	public void Clear()
	{
		if ( _spawned.Count > 0 )
			AimboxViewModelAttachmentDebug.Info( $"Clear {_spawned.Count} spawned attachment object(s)." );

		foreach ( var entry in _spawned )
		{
			if ( entry.Object.IsValid() )
				entry.Object.Destroy();
		}

		_spawned.Clear();
		_equipped.Clear();
		_requiresWeaponPose = false;
		SuppressorRenderer = default;

		if ( _weaponSkin.IsValid() )
			RestoreBodyGroups( _weaponSkin );

		_weaponSkin = default;
		_weaponId = default;
		_bodyGroupSnapshot.Clear();
		_redDotUsingRaised = false;
	}

	GameObject SpawnAttachmentRenderer( Model model, AimboxAttachmentId attachment )
	{
		var go = new GameObject( true, $"Attachment_{attachment}" );
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( GameObject );

		// sboxweapons attachment VMDLs are static meshes (0 bones) — ModelRenderer, not SkinnedModelRenderer.
		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		renderer.RenderOptions.Game = true;
		renderer.RenderOptions.Overlay = _overlayPass;
		renderer.Enabled = true;
		renderer.Tint = new Color( 0.94f, 0.94f, 0.94f, 1f );
		return go;
	}

	async Task ParentToMountAsync(
		GameObject child,
		SkinnedModelRenderer weaponSkin,
		AimboxSboxAttachmentCatalog.VisualSpec visual,
		AimboxAttachmentId attachment )
	{
		await Task.DelayRealtimeSeconds( 0.05f );

		for ( var attempt = 0; attempt < 20; attempt++ )
		{
			if ( !child.IsValid() )
			{
				AimboxViewModelAttachmentDebug.Warn( $"Parent aborted for {attachment} — child destroyed before attempt {attempt}." );
				return;
			}

			if ( !weaponSkin.IsValid() )
			{
				AimboxViewModelAttachmentDebug.Warn( $"Parent aborted for {attachment} — weapon renderer invalid before attempt {attempt}." );
				return;
			}

			if ( TryResolveMountParent( weaponSkin, visual, attachment, out var parent, out var localPosition, out var localRotation, out var mountMethod ) )
			{
				MountChild( child, parent, localPosition, localRotation, visual.LocalScale );
				await FinishAttachmentRendererAsync( child, attachment );
				LogMountSuccess( child, weaponSkin, attachment, mountMethod, localPosition, localRotation );
				return;
			}

			await Task.DelayRealtimeSeconds( 0.033f );
		}

		LogMountFailure( weaponSkin, attachment );
		if ( child.IsValid() )
			child.Destroy();
	}

	static void MountChild( GameObject child, GameObject parent, Vector3 localPosition, Rotation localRotation, float localScale )
	{
		child.SetParent( parent );
		child.LocalPosition = localPosition;
		child.LocalRotation = localRotation;
		child.LocalScale = Vector3.One * Math.Max( 0.01f, localScale );
		child.Transform.ClearInterpolation();
	}

	async Task FinishAttachmentRendererAsync( GameObject child, AimboxAttachmentId attachment )
	{
		await Task.DelayRealtimeSeconds( 0.02f );
		if ( !child.IsValid() )
			return;

		var renderer = child.Components.Get<ModelRenderer>();
		if ( !renderer.IsValid() )
			return;

		renderer.RenderOptions.Game = true;
		renderer.RenderOptions.Overlay = _overlayPass;
		if ( renderer.SceneObject.IsValid() )
			renderer.RenderOptions.Apply( renderer.SceneObject );

		if ( IsRedDotStyleAttachment( attachment )
		     && TryGetEquippedRedDotStyleAttachment( out var equippedSight )
		     && AimboxOpticLensPresentation.ShouldAlwaysHideM4RedDotLens(
			     AimboxOpticLensPresentation.GetRedDotLensProfile( equippedSight ) ) )
		{
			AimboxOpticLensPresentation.Apply(
				renderer,
				hideLens: true,
				AimboxOpticLensPresentation.GetRedDotLensProfile( equippedSight ) );
		}
	}

	static bool TryResolveMountParent(
		SkinnedModelRenderer weaponSkin,
		AimboxSboxAttachmentCatalog.VisualSpec visual,
		AimboxAttachmentId attachment,
		out GameObject parent,
		out Vector3 localPosition,
		out Rotation localRotation,
		out string mountMethod )
	{
		parent = default;
		localPosition = Vector3.Zero;
		localRotation = Rotation.Identity;
		mountMethod = "";
		if ( !weaponSkin.IsValid() )
			return false;

		foreach ( var name in visual.MountPointCandidates )
		{
			if ( !TryResolveMountByName( weaponSkin, name, out parent ) )
				continue;

			mountMethod = $"direct:{name}";
			return true;
		}

		foreach ( var name in DiscoverMountPointNames( weaponSkin.Model, attachment ) )
		{
			if ( Array.IndexOf( visual.MountPointCandidates, name ) >= 0 )
				continue;

			if ( !TryResolveMountByName( weaponSkin, name, out parent ) )
				continue;

			mountMethod = $"discovered:{name}";
			return true;
		}

		if ( TryRailAlignedMount( weaponSkin, visual, out parent, out localPosition, out localRotation, out mountMethod ) )
			return true;

		if ( string.IsNullOrWhiteSpace( visual.FallbackMountBone ) )
			return false;

		if ( !TryResolveMountByName( weaponSkin, visual.FallbackMountBone, out parent ) )
			return false;

		localPosition = visual.FallbackLocalPosition;
		localRotation = visual.FallbackLocalRotation;
		mountMethod = $"fallback:{visual.FallbackMountBone}+offset";
		return true;
	}

	/// <summary>
	/// Parent on the weapon bone (same axes as the rail bodygroup mesh). Optional reference point
	/// only supplies a world position hint — never used as the rotation parent.
	/// </summary>
	static bool TryRailAlignedMount(
		SkinnedModelRenderer weaponSkin,
		AimboxSboxAttachmentCatalog.VisualSpec visual,
		out GameObject parent,
		out Vector3 localPosition,
		out Rotation localRotation,
		out string mountMethod )
	{
		parent = default;
		localPosition = Vector3.Zero;
		localRotation = Rotation.Identity;
		mountMethod = "";

		if ( string.IsNullOrWhiteSpace( visual.FallbackMountBone ) )
			return false;

		if ( !TryResolveMountByName( weaponSkin, visual.FallbackMountBone, out parent ) )
			return false;

		localRotation = visual.FallbackLocalRotation;

		if ( !string.IsNullOrWhiteSpace( visual.ReferenceMountPoint )
		     && TryResolveMountByName( weaponSkin, visual.ReferenceMountPoint, out var referenceObject ) )
		{
			var worldPos = referenceObject.WorldTransform.PointToWorld( visual.ReferenceLocalPosition );
			localPosition = parent.WorldTransform.PointToLocal( worldPos );
			mountMethod = $"rail:{visual.ReferenceMountPoint}->{visual.FallbackMountBone}";
			return true;
		}

		localPosition = visual.FallbackLocalPosition;
		mountMethod = $"rail:{visual.FallbackMountBone}+offset";
		return true;
	}

	public static bool TryResolveMountBone( SkinnedModelRenderer weaponSkin, string name, out GameObject parent ) =>
		TryResolveMountByName( weaponSkin, name, out parent );

	public static void ApplyAttachmentBodyGroups(
		SkinnedModelRenderer skin,
		AimboxWeaponId weaponId,
		IEnumerable<AimboxAttachmentId> attachments )
	{
		if ( !skin.IsValid() || attachments is null )
			return;

		foreach ( var attachment in attachments )
		{
			if ( !AimboxSboxAttachmentCatalog.TryGetVisual( weaponId, attachment, out var visual ) )
				continue;

			if ( visual.BodyGroupNameCandidates is null || visual.BodyGroupNameCandidates.Length <= 0 )
				continue;

			if ( TryApplyBodyGroupChoice( skin, attachment, visual.BodyGroupNameCandidates, visual.BodyGroupChoice, null, "tuner" ) )
			{
				ApplyExtraBodyGroups( skin, attachment, visual.ExtraBodyGroups, null );
				continue;
			}

			foreach ( var part in DiscoverBodyParts( skin.Model, attachment ) )
			{
				if ( TryApplyBodyGroupChoice( skin, attachment, [part.Name], visual.BodyGroupChoice, null, "tuner-discovered" ) )
				{
					ApplyExtraBodyGroups( skin, attachment, visual.ExtraBodyGroups, null );
					break;
				}
			}
		}
	}

	static void ApplyExtraBodyGroups(
		SkinnedModelRenderer skin,
		AimboxAttachmentId attachment,
		AimboxSboxAttachmentCatalog.BodyGroupSpec[] extraBodyGroups,
		Dictionary<string, int> snapshot = null )
	{
		if ( !skin.IsValid() || extraBodyGroups is null || extraBodyGroups.Length <= 0 )
			return;

		foreach ( var extra in extraBodyGroups )
		{
			if ( extra.NameCandidates is null || extra.NameCandidates.Length <= 0 )
				continue;

			if ( TryApplyBodyGroupChoice( skin, attachment, extra.NameCandidates, extra.Choice, snapshot, "extra" ) )
				continue;

			foreach ( var part in skin.Model.Parts.All )
			{
				if ( part.Choices.Count <= 1 )
					continue;

				if ( !NameMatchesKeywords( part.Name, extra.NameCandidates ) )
					continue;

				if ( TryApplyBodyGroupChoice( skin, attachment, [part.Name], extra.Choice, snapshot, "extra-discovered" ) )
					break;
			}
		}
	}

	void ApplyExtraBodyGroups(
		SkinnedModelRenderer skin,
		AimboxAttachmentId attachment,
		AimboxSboxAttachmentCatalog.BodyGroupSpec[] extraBodyGroups ) =>
		ApplyExtraBodyGroups( skin, attachment, extraBodyGroups, _bodyGroupSnapshot );

	static bool TryResolveMountByName( SkinnedModelRenderer weaponSkin, string name, out GameObject parent )
	{
		parent = default;
		if ( string.IsNullOrWhiteSpace( name ) || !weaponSkin.IsValid() || !weaponSkin.Model.IsValid() )
			return false;

		var attachmentObject = weaponSkin.GetAttachmentObject( name );
		if ( IsSceneReady( attachmentObject ) )
		{
			parent = attachmentObject;
			return true;
		}

		foreach ( var modelAttachment in weaponSkin.Model.Attachments.All )
		{
			if ( !string.Equals( modelAttachment.Name, name, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var mountObject = weaponSkin.GetAttachmentObject( modelAttachment );
			if ( IsSceneReady( mountObject ) )
			{
				parent = mountObject;
				return true;
			}

			if ( modelAttachment.Bone is not null && TryResolveBoneObject( weaponSkin, modelAttachment.Bone.Name, out parent ) )
				return true;
		}

		return TryResolveBoneObject( weaponSkin, name, out parent );
	}

	static bool TryResolveBoneObject( SkinnedModelRenderer weaponSkin, string boneName, out GameObject parent )
	{
		parent = default;
		if ( string.IsNullOrWhiteSpace( boneName ) || !weaponSkin.IsValid() || !weaponSkin.Model.IsValid() )
			return false;

		if ( weaponSkin.Model.Bones.HasBone( boneName ) )
		{
			var bone = weaponSkin.Model.Bones.GetBone( boneName );
			var boneObject = weaponSkin.GetBoneObject( bone );
			if ( IsSceneReady( boneObject ) )
			{
				parent = boneObject;
				return true;
			}
		}

		var directBoneObject = weaponSkin.GetBoneObject( boneName );
		if ( IsSceneReady( directBoneObject ) )
		{
			parent = directBoneObject;
			return true;
		}

		return false;
	}

	static bool IsSceneReady( GameObject go ) => go.IsValid() && go.Scene is not null;

	static IEnumerable<string> DiscoverMountPointNames( Model model, AimboxAttachmentId attachment )
	{
		if ( !model.IsValid() || model.IsError )
			yield break;

		var keywords = attachment switch
		{
			AimboxAttachmentId.HoloSight or AimboxAttachmentId.RaisedRedDot or AimboxAttachmentId.RangedSight
				=> new[] { "top_rail", "rail", "sight", "optic", "scope", "reddot" },
			AimboxAttachmentId.ForegripStraight or AimboxAttachmentId.ForegripAngled
				=> new[] { "grip", "foregrip", "handguard" },
			AimboxAttachmentId.Flashlight
				=> new[] { "side_rail", "flashlight", "laser", "underbarrel", "rail" },
			AimboxAttachmentId.ExtendedMag
				=> new[] { "mag", "clip", "shell" },
			AimboxAttachmentId.Suppressor
				=> new[] { "muzzle", "silencer", "suppressor", "barrel" },
			_ => Array.Empty<string>()
		};

		foreach ( var mount in model.Attachments.All )
		{
			if ( NameMatchesKeywords( mount.Name, keywords ) )
				yield return mount.Name;

			if ( mount.Bone is not null && NameMatchesKeywords( mount.Bone.Name, keywords ) )
				yield return mount.Bone.Name;
		}

		foreach ( var bone in model.Bones.AllBones )
		{
			if ( NameMatchesKeywords( bone.Name, keywords ) )
				yield return bone.Name;
		}
	}

	static bool NameMatchesKeywords( string name, string[] keywords )
	{
		if ( string.IsNullOrWhiteSpace( name ) || keywords is null || keywords.Length <= 0 )
			return false;

		foreach ( var keyword in keywords )
		{
			if ( name.Contains( keyword, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	void RestoreBodyGroups( SkinnedModelRenderer skin )
	{
		foreach ( var (name, value) in _bodyGroupSnapshot )
		{
			if ( TryGetBodyPart( skin.Model, name, out var part ) )
				SafeSetBodyGroup( skin, part, value );
		}

		_bodyGroupSnapshot.Clear();
	}

	void TrySetBodyGroup(
		SkinnedModelRenderer skin,
		AimboxAttachmentId attachment,
		string[] bodyGroupCandidates,
		int choice )
	{
		if ( !skin.IsValid() || bodyGroupCandidates is null || bodyGroupCandidates.Length <= 0 )
			return;

		if ( TryApplyBodyGroupChoice( skin, attachment, bodyGroupCandidates, choice, _bodyGroupSnapshot, "explicit" ) )
			return;

		foreach ( var part in DiscoverBodyParts( skin.Model, attachment ) )
		{
			if ( TryApplyBodyGroupChoice( skin, attachment, [part.Name], choice, _bodyGroupSnapshot, "discovered" ) )
				return;
		}

		AimboxViewModelAttachmentDebug.Warn(
			$"Bodygroup not applied for {attachment} on {skin.Model?.ResourceName}. candidates=[{string.Join( ", ", bodyGroupCandidates )}]" );
	}

	static bool TryApplyBodyGroupChoice(
		SkinnedModelRenderer skin,
		AimboxAttachmentId attachment,
		string[] bodyGroupCandidates,
		int choice,
		Dictionary<string, int> snapshot,
		string source )
	{
		if ( !skin.IsValid() || bodyGroupCandidates is null )
			return false;

		foreach ( var candidate in bodyGroupCandidates )
		{
			if ( !TryGetBodyPart( skin.Model, candidate, out var part ) )
				continue;

			if ( !SafeSetBodyGroup( skin, part, choice, snapshot ) )
				continue;

			AimboxViewModelAttachmentDebug.Info(
				$"Bodygroup {attachment}: {part.Name}={choice} ({source}) on {skin.Model?.ResourceName}" );
			return true;
		}

		return false;
	}

	static IEnumerable<Model.BodyPart> DiscoverBodyParts( Model model, AimboxAttachmentId attachment )
	{
		if ( !model.IsValid() || model.IsError )
			yield break;

		var keywords = attachment switch
		{
			AimboxAttachmentId.HoloSight or AimboxAttachmentId.RaisedRedDot or AimboxAttachmentId.RangedSight
				=> new[] { "top_rail", "rail", "sight", "optic", "scope", "iron" },
			AimboxAttachmentId.ForegripStraight or AimboxAttachmentId.ForegripAngled
				=> new[] { "handguard", "grip", "cover" },
			AimboxAttachmentId.ExtendedMag => new[] { "mag", "clip", "shell" },
			AimboxAttachmentId.Suppressor => new[] { "muzzle", "silencer", "suppressor" },
			_ => Array.Empty<string>()
		};

		foreach ( var part in model.Parts.All )
		{
			if ( part.Choices.Count <= 1 )
				continue;

			if ( NameMatchesKeywords( part.Name, keywords ) )
				yield return part;
		}
	}

	static bool TryGetBodyPart( Model model, string name, out Model.BodyPart part )
	{
		part = default;
		if ( !model.IsValid() || model.IsError || string.IsNullOrWhiteSpace( name ) )
			return false;

		part = model.Parts.Get( name );
		return part is not null && part.Choices.Count > 1;
	}

	static bool SafeSetBodyGroup(
		SkinnedModelRenderer skin,
		Model.BodyPart part,
		int choice,
		Dictionary<string, int> snapshot = null )
	{
		if ( !skin.IsValid() || part.Choices.Count <= 1 )
			return false;

		var clampedChoice = Math.Clamp( choice, 0, part.Choices.Count - 1 );
		if ( snapshot is not null && !snapshot.ContainsKey( part.Name ) )
			snapshot[part.Name] = skin.GetBodyGroup( part.Name );

		skin.SetBodyGroup( part.Name, clampedChoice );
		return true;
	}

	static void LogMountSuccess(
		GameObject child,
		SkinnedModelRenderer weaponSkin,
		AimboxAttachmentId attachment,
		string mountMethod,
		Vector3 localPosition,
		Rotation localRotation )
	{
		var renderer = child.Components.Get<ModelRenderer>();
		var parentName = child.Parent.IsValid() ? child.Parent.Name : "(none)";
		var sceneReady = renderer.IsValid() && renderer.SceneObject.IsValid();
		AimboxViewModelAttachmentDebug.Info(
			$"Mounted {attachment} via {mountMethod} parent='{parentName}' localPos={FormatVector( localPosition )} localRot={localRotation.Angles()} worldPos={FormatVector( child.WorldPosition )} worldScale={FormatVector( child.WorldScale )} enabled={renderer?.Enabled} overlay={renderer?.RenderOptions.Overlay} sceneReady={sceneReady} renderer=ModelRenderer model={renderer?.Model?.ResourceName}" );

		LogWeaponMountDiagnostics( weaponSkin );
	}

	static void LogWeaponMountDiagnostics( SkinnedModelRenderer weaponSkin )
	{
		if ( !weaponSkin.IsValid() || !weaponSkin.Model.IsValid() )
			return;

		var model = weaponSkin.Model;
		var sb = new StringBuilder();
		sb.Append( "Weapon mount diagnostics: " );
		sb.Append( $"attachments=[{string.Join( ", ", model.Attachments.All.Select( x => x.Bone is null ? x.Name : $"{x.Name}@{x.Bone.Name}" ) )}] " );
		sb.Append( $"bodygroups=[{string.Join( ", ", model.Parts.All.Where( x => x.Choices.Count > 1 ).Select( x => x.Name ) )}] " );
		sb.Append( $"bones=[{string.Join( ", ", model.Bones.AllBones.Select( x => x.Name ).Where( x =>
			x.Contains( "rail", StringComparison.OrdinalIgnoreCase )
			|| x.Contains( "sight", StringComparison.OrdinalIgnoreCase )
			|| x.Contains( "weapon_root", StringComparison.OrdinalIgnoreCase )
			|| x.Contains( "muzzle", StringComparison.OrdinalIgnoreCase ) ) )}]" );
		AimboxViewModelAttachmentDebug.Info( sb.ToString() );
	}

	static void LogMountFailure( SkinnedModelRenderer weaponSkin, AimboxAttachmentId attachment )
	{
		if ( !weaponSkin.IsValid() || !weaponSkin.Model.IsValid() )
		{
			AimboxViewModelAttachmentDebug.Warn( $"Could not parent {attachment} — weapon model invalid." );
			return;
		}

		LogWeaponMountDiagnostics( weaponSkin );
		AimboxViewModelAttachmentDebug.Warn( $"Could not parent {attachment} on {weaponSkin.Model.ResourceName} after retries." );
	}

	static string FormatAttachmentList( IEnumerable<AimboxAttachmentId> attachments ) =>
		string.Join( ", ", attachments );

	static string FormatVector( Vector3 value ) =>
		$"({value.x:F2}, {value.y:F2}, {value.z:F2})";

	protected override void OnDestroy()
	{
		Clear();
		base.OnDestroy();
	}
}
