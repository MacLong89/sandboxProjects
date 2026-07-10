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
public sealed class ThornsWeaponWorldVisual : Component, Component.INetworkSpawn
{
	[Property] public bool HideForOwningPlayer { get; set; } = true;

	// TP grip: Citizen right-hand bone vs manual offsets under Body — inspector uses class summary / Property codegen (avoid XML-on-property SB2000).
	[Property] public bool AttachWorldWeaponToCitizenRightHandBone { get; set; }

	// WeaponWorld local position under Citizen Body — tune TP gun here per prefab/scene (defaults match ThornsWeapon.WorldMeshLocalPositionRelBody).
	[Property, Group( "Third-person manual (vs Body)" )]
	public Vector3 TpWeaponManualLocalPositionRelBody { get; set; } = ThornsWeapon.WorldMeshLocalPositionRelBody;

	[Property, Group( "Third-person manual (vs Body)" )]
	public Rotation TpWeaponManualLocalRotationRelBody { get; set; } = Rotation.Identity;

	// Manual TP offset when Body is missing from the pawn hierarchy (rare).
	[Property, Group( "Third-person manual (vs Body)" )]
	public Vector3 TpWeaponManualLocalPositionIfNoBody { get; set; } = ThornsWeapon.WorldMeshLocalPositionIfNoBody;

	// After bone orientation each frame, in hand-rig space (muzzle vs barrel).
	[Property] public Rotation TpWeaponGripRotationAfterHand { get; set; } = Rotation.Identity;

	// Extra translation in hand-rig space before world mapping (fine-tune vs palm).
	[Property] public Vector3 TpWeaponGripOffsetInHandSpace { get; set; }

	ThornsPawn _pawn;
	ThornsHotbarEquipment _hotbar;
	SkinnedModelRenderer _renderer;
	string _lastTpVisualKey = "__init__";
	string _lastLoggedTpApplySignature = "";
	bool _lastApplyUsedLoadFailedFallbackGeometry;

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !_pawn.IsValid() )
			_pawn = GameObject.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		if ( !_hotbar.IsValid() && _pawn.IsValid() )
			_hotbar = _pawn.Components.Get<ThornsHotbarEquipment>();
		// Joiners: host Sync props can arrive after this component's first frames — force a TP mesh resolve once the pawn exists.
		ApplyWorldPresentation( force: true );
	}

	protected override void OnStart()
	{
		_pawn = GameObject.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		_hotbar = _pawn.IsValid() ? _pawn.Components.Get<ThornsHotbarEquipment>() : default;
		ThornsWeapon.ResetThirdPersonWeaponWorldVisual( GameObject );
		_renderer = ThornsWeapon.GetOrCreateWorldSkinnedModelRenderer( GameObject );
		AlignWorldWeaponToBody();
		ApplyWorldPresentation( force: true );
		_ = ApplyVisibilityWithRetriesAsync();
	}

	/// <summary>Runs on every client so the mesh follows the Citizen torso (host-only <see cref="ThornsWeapon.HostApplyEquippedWorldPresentation"/> cannot reparent for remotes).</summary>
	void AlignWorldWeaponToBody()
	{
		if ( !_pawn.IsValid() )
			return;

		if ( AttachWorldWeaponToCitizenRightHandBone
		     && ThornsWeapon.TryAlignThirdPersonWeaponToCitizenRightHand(
			     _pawn.GameObject,
			     GameObject,
			     TpWeaponGripOffsetInHandSpace,
			     TpWeaponGripRotationAfterHand ) )
			return;

		ThornsWeapon.ParentWorldWeaponToCitizenRig(
			_pawn.GameObject,
			GameObject,
			TpWeaponManualLocalPositionRelBody,
			TpWeaponManualLocalRotationRelBody,
			TpWeaponManualLocalPositionIfNoBody );
	}

	async Task ApplyVisibilityWithRetriesAsync()
	{
		for ( var attempt = 0; attempt < 8; attempt++ )
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

	static bool TryResolveThirdPersonItemDefinition(
		string equippedHotbarItemId,
		string combatWeaponDefinitionId,
		out ThornsItemRegistry.ThornsItemDefinition def )
	{
		def = default;
		var item = (equippedHotbarItemId ?? "").Trim();
		var combat = (combatWeaponDefinitionId ?? "").Trim();

		if ( !string.IsNullOrEmpty( item )
		     && ThornsItemRegistry.TryGet( item, out def )
		     && !string.IsNullOrWhiteSpace( def.WorldModelAsset ) )
			return true;

		if ( !string.IsNullOrEmpty( combat )
		     && ThornsItemRegistry.TryGet( combat, out def )
		     && !string.IsNullOrWhiteSpace( def.WorldModelAsset ) )
			return true;

		def = default;
		return false;
	}

	void ApplyWorldPresentation( bool force )
	{
		if ( !_renderer.IsValid() )
			_renderer = ThornsWeapon.GetOrCreateWorldSkinnedModelRenderer( GameObject );
		if ( !_renderer.IsValid() )
			return;

		if ( !_hotbar.IsValid() && _pawn.IsValid() )
			_hotbar = _pawn.Components.Get<ThornsHotbarEquipment>();

		var combatId = _hotbar.IsValid() ? _hotbar.ObserversCombatWeaponDefinitionId : "";
		var itemId = _hotbar.IsValid() ? _hotbar.ObserversEquippedHotbarItemId : "";
		var tpKey = $"{combatId}|{itemId}";

		var ownerId = _pawn.IsValid() ? _pawn.GameObject.Network.OwnerId : GameObject.Network.OwnerId;
		var local = Connection.Local;
		var isOwningPlayer = local is not null && ownerId == local.Id;
		var hideTpMeshFromOwner = HideForOwningPlayer && isOwningPlayer;

		if ( string.IsNullOrWhiteSpace( combatId ) && string.IsNullOrWhiteSpace( itemId ) )
		{
			if ( _renderer.Enabled )
				_renderer.Enabled = false;
			_renderer.Model = default;
			_lastTpVisualKey = "";
			_lastApplyUsedLoadFailedFallbackGeometry = false;
			return;
		}

		if ( !force && string.Equals( tpKey, _lastTpVisualKey, StringComparison.Ordinal ) )
		{
			if ( !_lastApplyUsedLoadFailedFallbackGeometry )
			{
				var desiredScale = ThornsWeapon.WorldMeshLocalScaleWeapon;
				if ( (GameObject.LocalScale - desiredScale).LengthSquared > 1e-6f )
					GameObject.LocalScale = desiredScale;
				ApplyThirdPersonWeaponVisibility( hideTpMeshFromOwner );
				return;
			}
		}

		if ( !TryResolveThirdPersonItemDefinition( itemId, combatId, out var def )
		     || string.IsNullOrWhiteSpace( def.WorldModelAsset ) )
		{
			_renderer.Enabled = false;
			_renderer.Model = default;
			_lastTpVisualKey = tpKey;
			_lastApplyUsedLoadFailedFallbackGeometry = false;
			ApplyThirdPersonWeaponVisibility( hideTpMeshFromOwner );
			return;
		}

		if ( !ThornsWeaponResourceLoad.TryLoadWeaponWorldModel( def.WorldModelAsset, "TP world weapon (client apply)", out var worldModel ) )
		{
			_renderer.Enabled = false;
			_renderer.Model = default;
			_lastApplyUsedLoadFailedFallbackGeometry = true;
			_lastTpVisualKey = tpKey;
			ApplyThirdPersonWeaponVisibility( hideTpMeshFromOwner );
			return;
		}

		_lastApplyUsedLoadFailedFallbackGeometry = false;
		_renderer.Model = worldModel;
		_renderer.UseAnimGraph = false;
		_renderer.Tint = Color.White;
		GameObject.LocalScale = ThornsWeapon.WorldMeshLocalScaleWeapon;
		ThornsModelMaterialUvScale.ApplyScaledSkinnedPresentation(
			_renderer,
			GameObject,
			worldModel,
			def.WorldModelAsset );
		_lastTpVisualKey = tpKey;
		var sig = $"{tpKey}|{def.WorldModelAsset}|ok";
		if ( !string.Equals( sig, _lastLoggedTpApplySignature, StringComparison.Ordinal ) )
			_lastLoggedTpApplySignature = sig;

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
