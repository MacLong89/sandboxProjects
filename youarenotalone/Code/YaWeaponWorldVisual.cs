using System;

namespace Sandbox;

/// <summary>
/// Third-person weapon mesh on the pawn (networked hierarchy). THORNS_EVERYTHING_DOCUMENT: world model is what others see;
/// viewmodel is separate and never networked.
/// </summary>
[Title( "Thorns — Weapon World Visual" )]
[Category( "Thorns" )]
[Icon( "sports_martial_arts" )]
[Order( 200 )]
public sealed class YaWeaponWorldVisual : Component
{
	[Property] public bool HideForOwningPlayer { get; set; } = true;

	/// <summary>
	/// When true, follows Citizen's animated right-hand bone via <see cref="SkinnedModelRenderer.TryGetBoneTransform"/> (matches grip motion).
	/// When false or bones missing, uses manual offsets below (parent = Citizen <c>Body</c>).
	/// </summary>
	[Property] public bool AttachWorldWeaponToCitizenRightHandBone { get; set; }

	/// <summary>
	/// WeaponWorld local position under Citizen <c>Body</c> (main knob for “move TP gun”). Z-up map axes.
	/// Defaults match <see cref="YaWeapon.WorldMeshLocalPositionRelBody"/> — edit here in the inspector per prefab/scene.
	/// </summary>
	[Property, Group( "Third-person manual (vs Body)" )]
	public Vector3 TpWeaponManualLocalPositionRelBody { get; set; } = YaWeapon.WorldMeshLocalPositionRelBody;

	[Property, Group( "Third-person manual (vs Body)" )]
	public Rotation TpWeaponManualLocalRotationRelBody { get; set; } = Rotation.Identity;

	/// <summary>Used only if <c>Body</c> is missing from the pawn hierarchy (should be rare).</summary>
	[Property, Group( "Third-person manual (vs Body)" )]
	public Vector3 TpWeaponManualLocalPositionIfNoBody { get; set; } = YaWeapon.WorldMeshLocalPositionIfNoBody;

	/// <summary>Added after bone orientation each frame, in hand-rig space (tune muzzle direction vs barrel).</summary>
	[Property] public Rotation TpWeaponGripRotationAfterHand { get; set; } = Rotation.Identity;

	/// <summary>Extra translation in hand-rig space before mapping to world (fine-tune vs palm).</summary>
	[Property] public Vector3 TpWeaponGripOffsetInHandSpace { get; set; }

	YaPawn _pawn;
	YaHotbarEquipment _hotbar;
	SkinnedModelRenderer _renderer;
	string _lastAppliedCombatId = "__init__";
	string _lastLoggedTpApplySignature = "";
	bool _lastApplyUsedLoadFailedFallbackGeometry;

	protected override void OnStart()
	{
		_pawn = GameObject.Components.GetInAncestorsOrSelf<YaPawn>( true );
		_hotbar = _pawn.IsValid() ? _pawn.Components.Get<YaHotbarEquipment>() : default;
		_renderer = YaWeapon.GetOrCreateWorldSkinnedModelRenderer( GameObject );
		AlignWorldWeaponToBody();
		ApplyWorldPresentation( force: true );
		_ = ApplyVisibilityWithRetriesAsync();
	}

	/// <summary>Runs on every client so the mesh follows the Citizen torso (host-only <see cref="YaWeapon.HostApplyEquippedWorldPresentation"/> cannot reparent for remotes).</summary>
	void AlignWorldWeaponToBody()
	{
		if ( !_pawn.IsValid() )
			return;

		if ( AttachWorldWeaponToCitizenRightHandBone
		     && YaWeapon.TryAlignThirdPersonWeaponToCitizenRightHand(
			     _pawn.GameObject,
			     GameObject,
			     TpWeaponGripOffsetInHandSpace,
			     TpWeaponGripRotationAfterHand ) )
			return;

		YaWeapon.ParentWorldWeaponToCitizenRig(
			_pawn.GameObject,
			GameObject,
			TpWeaponManualLocalPositionRelBody,
			TpWeaponManualLocalRotationRelBody,
			TpWeaponManualLocalPositionIfNoBody );
	}

	async Task ApplyVisibilityWithRetriesAsync()
	{
		for ( var attempt = 0; attempt < 4; attempt++ )
		{
			if ( !GameObject.IsValid() || !Game.IsPlaying )
				return;

			AlignWorldWeaponToBody();
			ApplyWorldPresentation( force: attempt == 0 );

			await Task.DelayRealtimeSeconds( 0.06f );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return;

		AlignWorldWeaponToBody();
		ApplyWorldPresentation( force: false );
	}

	void ApplyWorldPresentation( bool force )
	{
		if ( !_renderer.IsValid() )
			_renderer = YaWeapon.GetOrCreateWorldSkinnedModelRenderer( GameObject );
		if ( !_renderer.IsValid() )
			return;

		if ( !_hotbar.IsValid() && _pawn.IsValid() )
			_hotbar = _pawn.Components.Get<YaHotbarEquipment>();

		var combatId = _hotbar.IsValid() ? _hotbar.ObserversCombatWeaponDefinitionId : "";

		var ownerId = _pawn.IsValid() ? _pawn.GameObject.Network.OwnerId : GameObject.Network.OwnerId;
		var local = Connection.Local;
		var isOwningPlayer = local is not null && ownerId == local.Id;

		var aloneMech = _pawn.IsValid() ? _pawn.Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf ) : default;
		if ( !isOwningPlayer
		     && aloneMech.IsValid()
		     && aloneMech.MimicPresentationActive
		     && !string.IsNullOrWhiteSpace( aloneMech.MimicMirrorCombatId ) )
			combatId = aloneMech.MimicMirrorCombatId;
		var hideTpMeshFromOwner = HideForOwningPlayer && isOwningPlayer;

		if ( string.IsNullOrWhiteSpace( combatId ) )
		{
			if ( _renderer.Enabled )
				_renderer.Enabled = false;
			_lastAppliedCombatId = "";
			_lastApplyUsedLoadFailedFallbackGeometry = false;
			return;
		}

		if ( !force && string.Equals( combatId, _lastAppliedCombatId, StringComparison.OrdinalIgnoreCase ) )
		{
			var desiredScale = _lastApplyUsedLoadFailedFallbackGeometry
				? YaWeapon.WorldMeshLocalScaleWeaponLoadFailed
				: YaWeapon.WorldMeshLocalScaleWeapon;
			if ( (GameObject.LocalScale - desiredScale).LengthSquared > 1e-6f )
				GameObject.LocalScale = desiredScale;
			ApplyThirdPersonWeaponVisibility( hideTpMeshFromOwner );
			return;
		}

		if ( !YaWeaponItemCatalog.TryGet( combatId, out var def ) || string.IsNullOrWhiteSpace( def.WorldModelAsset ) )
		{
			Log.Warning( $"[YA] Weapon world visual: no world model mapping for combat id '{combatId}'." );
			_renderer.Enabled = false;
			_lastAppliedCombatId = combatId;
			return;
		}

		var model = YaWeaponResourceLoad.LoadWeaponModelOrFallback( def.WorldModelAsset, "TP world weapon (client apply)", out var usedFallbackGeometry );
		_lastApplyUsedLoadFailedFallbackGeometry = usedFallbackGeometry;
		_renderer.Model = model;
		_renderer.UseAnimGraph = false;
		_renderer.Tint = usedFallbackGeometry ? new Color( 0.85f, 0.45f, 0.12f, 1f ) : Color.White;
		GameObject.LocalScale = usedFallbackGeometry ? YaWeapon.WorldMeshLocalScaleWeaponLoadFailed : YaWeapon.WorldMeshLocalScaleWeapon;
		_lastAppliedCombatId = combatId;
		var sig = $"{combatId}|{def.WorldModelAsset}|{usedFallbackGeometry}";
		if ( !string.Equals( sig, _lastLoggedTpApplySignature, StringComparison.Ordinal ) )
		{
			_lastLoggedTpApplySignature = sig;
			Log.Info(
				$"[YA] TP world weapon apply: combatId={combatId} model='{def.WorldModelAsset}' loadFailedFallback={usedFallbackGeometry} scale={GameObject.LocalScale}" );
		}

		ApplyThirdPersonWeaponVisibility( hideTpMeshFromOwner );
	}

	/// <summary>
	/// Owning connection still drives authoritative mesh + scale so remote clients see the correct TP weapon;
	/// locally we only hide the renderer for FP (first-person replaces it).
	/// </summary>
	void ApplyThirdPersonWeaponVisibility( bool hideRendererFromOwner )
	{
		if ( hideRendererFromOwner )
		{
			if ( _renderer.Enabled )
				_renderer.Enabled = false;
			return;
		}

		if ( !_renderer.Enabled )
			_renderer.Enabled = true;
	}

}
