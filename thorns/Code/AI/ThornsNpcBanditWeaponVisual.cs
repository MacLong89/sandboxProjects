namespace Sandbox;

/// <summary>Keeps third-person M4 aligned to the Citizen rig for bandit NPCs (no <see cref="ThornsWeaponWorldVisual"/> / hotbar).</summary>
[Title( "Thorns — NPC bandit weapon (TP M4)" )]
[Category( "Thorns/AI" )]
[Icon( "sports_martial_arts" )]
public sealed class ThornsNpcBanditWeaponVisual : Component
{
	bool _weaponPresentationReady;

	protected override void OnStart()
	{
		if ( Game.IsPlaying )
			TryEnsureWeaponPresentation();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return;

		if ( !_weaponPresentationReady )
			TryEnsureWeaponPresentation();

		GameObject pawnRoot = default;
		for ( var p = GameObject; p.IsValid(); p = p.Parent )
		{
			if ( p.Components.Get<ThornsBanditBrain>( FindMode.EnabledInSelf ).IsValid() )
			{
				pawnRoot = p;
				break;
			}
		}

		if ( !pawnRoot.IsValid() )
			return;

		if ( ThornsWeapon.TryAlignThirdPersonWeaponToCitizenRightHand(
			     pawnRoot,
			     GameObject,
			     Vector3.Zero,
			     Rotation.Identity ) )
			return;

		ThornsWeapon.ParentWorldWeaponToCitizenRig( pawnRoot, GameObject );
	}

	void TryEnsureWeaponPresentation()
	{
		var smr = ThornsWeapon.GetOrCreateWorldSkinnedModelRenderer( GameObject );
		if ( !smr.IsValid() )
			return;

		// Scaled-mesh UV helper may have bound a bogus inferred _basecolor.vmat on the host at spawn.
		if ( smr.MaterialOverride.IsValid() )
			smr.MaterialOverride = default;

		if ( smr.Model.IsValid() && !smr.Model.IsError )
		{
			_weaponPresentationReady = true;
			return;
		}

		if ( !ThornsItemRegistry.TryGet( "m4", out var m4def )
		     || string.IsNullOrWhiteSpace( m4def.WorldModelAsset ) )
			return;

		if ( !ThornsWeaponResourceLoad.TryLoadWeaponWorldModel( m4def.WorldModelAsset, "npc bandit m4 (client)", out var worldModel ) )
			return;

		smr.Model = worldModel;
		smr.UseAnimGraph = false;
		smr.Tint = Color.White;
		smr.Enabled = true;
		GameObject.LocalScale = ThornsWeapon.WorldMeshLocalScaleWeapon;
		_weaponPresentationReady = true;
	}
}
