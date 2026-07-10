using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sandbox;

using Terraingen.Combat.Attachments;

/// <summary>Parents sboxweapons attachment meshes onto a FP weapon viewmodel.</summary>
[Title( "Thorns — ViewModel Attachment Mount" )]
[Category( "Thorns" )]
public sealed class ThornsViewModelAttachmentMount : Component
{
	readonly List<SpawnedAttachmentEntry> _spawned = [];
	HashSet<ThornsAttachmentId> _equipped = [];
	readonly Dictionary<string, int> _bodyGroupSnapshot = [];

	SkinnedModelRenderer _weaponSkin;
	string _combatWeaponId = "";
	bool _overlayPass = true;
	bool _requiresWeaponPose;
	bool _redDotUsingRaised;

	public bool RequiresWeaponPose => _requiresWeaponPose;
	public ModelRenderer SuppressorRenderer { get; private set; }

	public bool HasAttachment( ThornsAttachmentId attachment ) => _equipped.Contains( attachment );

	public bool HasSpawnedAttachmentMesh( ThornsAttachmentId attachment )
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
		public ThornsAttachmentId Attachment;
	}

	public void SetMeshesVisibleForAttachment( ThornsAttachmentId attachment, bool visible )
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
		foreach ( var attachment in ThornsAttachmentCatalog.RedDotStyleAttachments )
			SetMeshesVisibleForAttachment( attachment, visible );
	}

	public bool HasRedDotStyleAttachment()
	{
		foreach ( var attachment in ThornsAttachmentCatalog.RedDotStyleAttachments )
		{
			if ( HasAttachment( attachment ) )
				return true;
		}

		return false;
	}

	public bool TryGetEquippedRedDotStyleAttachment( out ThornsAttachmentId attachment )
	{
		foreach ( var candidate in ThornsAttachmentCatalog.RedDotStyleAttachments )
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

	static bool IsRedDotStyleAttachment( ThornsAttachmentId attachment ) =>
		ThornsAttachmentCatalog.IsRedDotStyle( attachment );

	public void ApplyRedDotLensPresentation( bool useClearLens )
	{
		if ( !TryGetEquippedRedDotStyleAttachment( out var equippedSight ) )
			return;

		var profile = ThornsOpticLensPresentation.GetRedDotLensProfile( equippedSight );
		var hideLens = useClearLens || ThornsOpticLensPresentation.ShouldAlwaysHideM4RedDotLens( profile );

		if ( profile == ThornsOpticLensProfile.HolographicAttachment && _weaponSkin.IsValid() )
			ThornsOpticLensPresentation.Apply( _weaponSkin, hideLens, profile );

		foreach ( var entry in _spawned )
		{
			if ( !IsRedDotStyleAttachment( entry.Attachment ) || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( renderer.IsValid() )
				ThornsOpticLensPresentation.Apply( renderer, hideLens, profile );
		}
	}

	public bool TryGetAttachmentOriginSkinLocal(
		ThornsAttachmentId attachment,
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
				var eyeOffset = ThornsSboxAttachmentCatalog.GetRedDotAdsEyeAttachmentOffset( attachment );
				if ( eyeOffset != Vector3.Zero )
					originWorld = entry.Object.WorldTransform.PointToWorld( eyeOffset );
			}
			else if ( attachment == ThornsAttachmentId.RangedSight )
			{
				var renderer = entry.Object.Components.Get<ModelRenderer>();
				var lensLocal = renderer.IsValid() && renderer.Model.IsValid()
					? ThornsSboxAttachmentCatalog.ResolveRangedSightLensLocalOffset( renderer.Model )
					: ThornsSboxAttachmentCatalog.GetRangedSightAdsEyeAttachmentOffset();
				originWorld = entry.Object.WorldTransform.PointToWorld( lensLocal );
			}

			skinLocal = skin.WorldTransform.PointToLocal( originWorld );
			return true;
		}

		return false;
	}

	public bool TryGetRangedSightLensSkinLocal( SkinnedModelRenderer skin, out Vector3 skinLocal ) =>
		TryGetAttachmentOriginSkinLocal( ThornsAttachmentId.RangedSight, skin, out skinLocal );

	public void UpdateRedDotAdsMesh( bool useRaised )
	{
		if ( !HasAttachment( ThornsAttachmentId.RaisedRedDot ) )
			return;

		if ( _redDotUsingRaised == useRaised )
			return;

		_redDotUsingRaised = useRaised;

		foreach ( var entry in _spawned )
		{
			if ( entry.Attachment != ThornsAttachmentId.RaisedRedDot || !entry.Object.IsValid() )
				continue;

			var renderer = entry.Object.Components.Get<ModelRenderer>();
			if ( !renderer.IsValid() )
				continue;

			var path = ThornsSboxAttachmentCatalog.GetRaisedRedDotAdsModelPath( _combatWeaponId, useRaised );
			var model = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback( path, "FP red dot ADS", out _ );
			if ( !model.IsValid() || model.IsError )
				continue;

			renderer.Model = model;
			if ( renderer.SceneObject.IsValid() )
				renderer.RenderOptions.Apply( renderer.SceneObject );
		}
	}

	public void Apply(
		string combatWeaponId,
		SkinnedModelRenderer weaponSkin,
		IEnumerable<ThornsAttachmentId> attachments,
		bool overlayPass )
	{
		if ( !weaponSkin.IsValid() )
			return;

		combatWeaponId = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId );
		var next = new HashSet<ThornsAttachmentId>( attachments ?? [] );
		if ( _combatWeaponId == combatWeaponId && _weaponSkin == weaponSkin && next.SetEquals( _equipped ) && overlayPass == _overlayPass )
			return;

		Clear();
		_combatWeaponId = combatWeaponId;
		_weaponSkin = weaponSkin;
		_equipped = next;
		_overlayPass = overlayPass;
		_requiresWeaponPose = false;
		SuppressorRenderer = default;

		weaponSkin.CreateBoneObjects = true;
		weaponSkin.CreateAttachments = true;

		foreach ( var attachment in next )
		{
			if ( !ThornsSboxAttachmentCatalog.TryGetVisual( combatWeaponId, attachment, out var visual ) )
				continue;

			TrySetBodyGroup( weaponSkin, attachment, visual.BodyGroupNameCandidates, visual.BodyGroupChoice );
			ApplyExtraBodyGroups( weaponSkin, attachment, visual.ExtraBodyGroups );

			if ( visual.RequiresWeaponPose && ThornsSboxAttachmentCatalog.RequiresWeaponPose( combatWeaponId ) )
				_requiresWeaponPose = true;

			if ( string.IsNullOrWhiteSpace( visual.ModelPath ) )
				continue;

			var model = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback(
				visual.ModelPath,
				$"FP attachment {attachment}",
				out _ );
			if ( !model.IsValid() || model.IsError )
				continue;

			var child = SpawnAttachmentRenderer( model, attachment );
			_spawned.Add( new SpawnedAttachmentEntry { Object = child, Attachment = attachment } );
			_ = ParentToMountAsync( child, weaponSkin, visual, attachment );

			if ( attachment == ThornsAttachmentId.Suppressor && child.Components.Get<ModelRenderer>() is { } suppressorRenderer )
				SuppressorRenderer = suppressorRenderer;
		}
	}

	public void Clear()
	{
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
		_combatWeaponId = "";
		_bodyGroupSnapshot.Clear();
		_redDotUsingRaised = false;
	}

	GameObject SpawnAttachmentRenderer( Model model, ThornsAttachmentId attachment )
	{
		var go = new GameObject( true, $"Attachment_{attachment}" );
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( GameObject );

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
		ThornsSboxAttachmentCatalog.VisualSpec visual,
		ThornsAttachmentId attachment )
	{
		await Task.DelayRealtimeSeconds( 0.05f );

		for ( var attempt = 0; attempt < 20; attempt++ )
		{
			if ( !child.IsValid() || !weaponSkin.IsValid() )
				return;

			if ( TryResolveMountParent( weaponSkin, visual, out var parent, out var localPosition, out var localRotation ) )
			{
				MountChild( child, parent, localPosition, localRotation, visual.LocalScale );
				await FinishAttachmentRendererAsync( child, attachment );
				return;
			}

			await Task.DelayRealtimeSeconds( 0.033f );
		}

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

	async Task FinishAttachmentRendererAsync( GameObject child, ThornsAttachmentId attachment )
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
		     && ThornsOpticLensPresentation.ShouldAlwaysHideM4RedDotLens(
			     ThornsOpticLensPresentation.GetRedDotLensProfile( equippedSight ) ) )
		{
			ThornsOpticLensPresentation.Apply(
				renderer,
				hideLens: true,
				ThornsOpticLensPresentation.GetRedDotLensProfile( equippedSight ) );
		}
	}

	static bool TryResolveMountParent(
		SkinnedModelRenderer weaponSkin,
		ThornsSboxAttachmentCatalog.VisualSpec visual,
		out GameObject parent,
		out Vector3 localPosition,
		out Rotation localRotation )
	{
		parent = default;
		localPosition = Vector3.Zero;
		localRotation = Rotation.Identity;
		if ( !weaponSkin.IsValid() )
			return false;

		foreach ( var name in visual.MountPointCandidates )
		{
			if ( !TryResolveMountByName( weaponSkin, name, out parent ) )
				continue;

			return true;
		}

		if ( TryRailAlignedMount( weaponSkin, visual, out parent, out localPosition, out localRotation ) )
			return true;

		if ( string.IsNullOrWhiteSpace( visual.FallbackMountBone ) )
			return false;

		if ( !TryResolveMountByName( weaponSkin, visual.FallbackMountBone, out parent ) )
			return false;

		localPosition = visual.FallbackLocalPosition;
		localRotation = visual.FallbackLocalRotation;
		return true;
	}

	static bool TryRailAlignedMount(
		SkinnedModelRenderer weaponSkin,
		ThornsSboxAttachmentCatalog.VisualSpec visual,
		out GameObject parent,
		out Vector3 localPosition,
		out Rotation localRotation )
	{
		parent = default;
		localPosition = Vector3.Zero;
		localRotation = Rotation.Identity;

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
			return true;
		}

		localPosition = visual.FallbackLocalPosition;
		return true;
	}

	public static bool TryResolveMountBone( SkinnedModelRenderer weaponSkin, string name, out GameObject parent ) =>
		TryResolveMountByName( weaponSkin, name, out parent );

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
		ThornsAttachmentId attachment,
		string[] bodyGroupCandidates,
		int choice )
	{
		if ( !skin.IsValid() || bodyGroupCandidates is null || bodyGroupCandidates.Length <= 0 )
			return;

		if ( TryApplyBodyGroupChoice( skin, attachment, bodyGroupCandidates, choice, _bodyGroupSnapshot ) )
			return;

		foreach ( var part in DiscoverBodyParts( skin.Model, attachment ) )
		{
			if ( TryApplyBodyGroupChoice( skin, attachment, [part.Name], choice, _bodyGroupSnapshot ) )
				return;
		}
	}

	static void ApplyExtraBodyGroups(
		SkinnedModelRenderer skin,
		ThornsAttachmentId attachment,
		ThornsSboxAttachmentCatalog.BodyGroupSpec[] extraBodyGroups )
	{
		if ( !skin.IsValid() || extraBodyGroups is null || extraBodyGroups.Length <= 0 )
			return;

		foreach ( var extra in extraBodyGroups )
		{
			if ( extra.NameCandidates is null || extra.NameCandidates.Length <= 0 )
				continue;

			if ( TryApplyBodyGroupChoice( skin, attachment, extra.NameCandidates, extra.Choice, null ) )
				continue;

			foreach ( var part in skin.Model.Parts.All )
			{
				if ( part.Choices.Count <= 1 )
					continue;

				if ( !NameMatchesKeywords( part.Name, extra.NameCandidates ) )
					continue;

				if ( TryApplyBodyGroupChoice( skin, attachment, [part.Name], extra.Choice, null ) )
					break;
			}
		}
	}

	static bool TryApplyBodyGroupChoice(
		SkinnedModelRenderer skin,
		ThornsAttachmentId attachment,
		string[] bodyGroupCandidates,
		int choice,
		Dictionary<string, int> snapshot )
	{
		if ( !skin.IsValid() || bodyGroupCandidates is null )
			return false;

		foreach ( var candidate in bodyGroupCandidates )
		{
			if ( !TryGetBodyPart( skin.Model, candidate, out var part ) )
				continue;

			if ( !SafeSetBodyGroup( skin, part, choice, snapshot ) )
				continue;

			return true;
		}

		return false;
	}

	static IEnumerable<Model.BodyPart> DiscoverBodyParts( Model model, ThornsAttachmentId attachment )
	{
		if ( !model.IsValid() || model.IsError )
			yield break;

		var keywords = attachment switch
		{
			ThornsAttachmentId.HoloSight or ThornsAttachmentId.RaisedRedDot or ThornsAttachmentId.RangedSight
				=> new[] { "top_rail", "rail", "sight", "optic", "scope", "iron" },
			ThornsAttachmentId.ForegripStraight or ThornsAttachmentId.ForegripAngled
				=> new[] { "handguard", "grip", "cover" },
			ThornsAttachmentId.ExtendedMag => new[] { "mag", "clip", "shell" },
			ThornsAttachmentId.Suppressor => new[] { "muzzle", "silencer", "suppressor" },
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

	protected override void OnDestroy()
	{
		Clear();
		base.OnDestroy();
	}
}
