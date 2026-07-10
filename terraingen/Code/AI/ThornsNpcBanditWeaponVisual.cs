namespace Terraingen.AI;

using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Keeps third-person weapon aligned to the Citizen rig for bandit NPCs.</summary>
[Title( "Thorns NPC Bandit Weapon Visual" )]
[Category( "Thorns/AI" )]
[Order( 201 )]
public sealed class ThornsNpcBanditWeaponVisual : Component
{
	ThornsBanditCombat _combat;
	string _lastCombatId;
	string _lastModelPath;

	protected override void OnStart()
	{
		ResolveCombat();
		TryEnsureWeaponModel();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return;

		ResolveCombat();
		TryEnsureWeaponModel();

		if ( !TryResolvePawnRoot( out var pawnRoot ) )
			return;

		GameObject.LocalScale = ThornsBanditUtil.WorldWeaponLocalScale;
		_ = pawnRoot;
	}

	protected override void OnPreRender()
	{
		if ( !Game.IsPlaying || !TryResolvePawnRoot( out var pawnRoot ) )
			return;

		GameObject.LocalScale = ThornsBanditUtil.WorldWeaponLocalScale;
		var bob = ThornsCitizenRig.ComputeMovementBobOffsetLocal( ResolveBanditVelocity( pawnRoot ), Time.Now );
		if ( ThornsCitizenRig.TryAlignWeaponToCitizenRightHand( pawnRoot, GameObject, bob ) )
		{
			ThornsCitizenRig.WireCitizenHandIk( pawnRoot, GameObject, handAttached: true );
			return;
		}

		ThornsCitizenRig.ParentWorldWeaponToBodyFallback( pawnRoot, GameObject, 0f, bob );
		ThornsCitizenRig.WireCitizenHandIk( pawnRoot, GameObject, handAttached: false );
	}

	void ResolveCombat()
	{
		if ( _combat.IsValid() )
			return;

		for ( var p = GameObject; p.IsValid(); p = p.Parent )
		{
			_combat = p.Components.Get<ThornsBanditCombat>( FindMode.EnabledInSelf );
			if ( _combat.IsValid() )
				return;
		}
	}

	bool TryResolvePawnRoot( out GameObject pawnRoot )
	{
		pawnRoot = default;
		for ( var p = GameObject; p.IsValid(); p = p.Parent )
		{
			if ( p.Components.Get<ThornsBanditBrain>( FindMode.EnabledInSelf ).IsValid() )
			{
				pawnRoot = p;
				return true;
			}
		}

		return false;
	}

	static Vector3 ResolveBanditVelocity( GameObject pawnRoot )
	{
		var cc = pawnRoot.Components.Get<CharacterController>();
		return cc.IsValid() ? cc.Velocity : Vector3.Zero;
	}

	void TryEnsureWeaponModel()
	{
		var smr = ThornsBanditUtil.GetOrCreateWorldSkinnedModelRenderer( GameObject );
		if ( !smr.IsValid() )
			return;

		if ( smr.MaterialOverride.IsValid() )
			smr.MaterialOverride = default;

		var combatId = _combat.IsValid() ? _combat.CombatWeaponDefinitionId?.Trim() ?? "m4" : "m4";
		if ( !TryResolveWorldModelPath( combatId, out var path ) )
		{
			if ( string.Equals( _lastCombatId, combatId, StringComparison.Ordinal ) )
				return;

			_lastCombatId = combatId;
			_lastModelPath = "";
			smr.Model = default;
			return;
		}

		if ( string.Equals( _lastCombatId, combatId, StringComparison.Ordinal )
		     && string.Equals( _lastModelPath, path, StringComparison.Ordinal )
		     && smr.Model.IsValid() && !smr.Model.IsError )
			return;

		_lastCombatId = combatId;
		_lastModelPath = path;

		if ( !ThornsWeaponResourceLoad.TryLoadWeaponWorldModel( path, $"npc bandit {combatId}", out var worldModel ) )
		{
			smr.Model = default;
			return;
		}

		smr.Model = worldModel;
		smr.UseAnimGraph = false;
		smr.Tint = Color.White;
		smr.Enabled = true;
	}

	static bool TryResolveWorldModelPath( string combatWeaponId, out string path )
	{
		path = "";
		if ( string.IsNullOrWhiteSpace( combatWeaponId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( combatWeaponId, out var def ) || string.IsNullOrWhiteSpace( def.WorldModelAsset ) )
			return false;

		path = def.WorldModelAsset;
		return true;
	}
}
