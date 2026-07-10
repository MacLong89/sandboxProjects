namespace Terraingen.Player;

using Sandbox.Citizen;
using Terraingen.AI;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Rendering;

/// <summary>Third-person w_* weapon mesh for remote player pawns (local owner uses FP viewmodel).</summary>
[Title( "Thorns Third Person Weapon Visual" )]
[Category( "Thorns/Player" )]
[Order( 202 )]
public sealed class ThornsThirdPersonWeaponVisual : Component
{
	[Sync( SyncFlags.FromHost )] public string EquippedCombatWeaponId { get; private set; } = "";

	GameObject _weaponWorld;
	SkinnedModelRenderer _renderer;
	string _lastCombatId;
	string _lastModelPath;

	protected override void OnStart()
	{
		EnsureWeaponObject();
	}

	protected override void OnUpdate()
	{
		if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
		{
			if ( _renderer.IsValid() )
				_renderer.Enabled = false;
			return;
		}

		HostSyncEquippedWeapon();
		EnsureWeaponObject();
		UpdateModel();

		var show = ShouldShowWeapon();
		if ( _renderer.IsValid() )
			_renderer.Enabled = show;
	}

	protected override void OnPreRender()
	{
		if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) || !_weaponWorld.IsValid() || !ShouldShowWeapon() )
			return;

		_weaponWorld.LocalScale = ThornsBanditUtil.WorldWeaponLocalScale;
		var velocity = ResolvePresentationVelocity();
		var bob = ThornsCitizenRig.ComputeMovementBobOffsetLocal( velocity, Time.Now );
		var duckLevel = ResolveDuckLevel();

		if ( ThornsCitizenRig.TryAlignWeaponToCitizenRightHand( GameObject, _weaponWorld, bob ) )
		{
			ThornsCitizenRig.WireCitizenHandIk( GameObject, _weaponWorld, handAttached: true );
			return;
		}

		ThornsCitizenRig.ParentWorldWeaponToBodyFallback( GameObject, _weaponWorld, duckLevel, bob );
		ThornsCitizenRig.WireCitizenHandIk( GameObject, _weaponWorld, handAttached: false );
	}

	void HostSyncEquippedWeapon()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
		{
			EquippedCombatWeaponId = "";
			return;
		}

		if ( !gameplay.TryGetActiveHotbarItemId( out var itemId )
		     || !ThornsItemRegistry.TryGet( itemId, out var def )
		     || def.ItemType != ThornsItemType.Weapon )
		{
			EquippedCombatWeaponId = "";
			return;
		}

		var combatId = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) ? itemId : def.CombatWeaponDefinitionId.Trim();
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( wdef, combatId ) || wdef.ClipSize <= 0 )
		{
			EquippedCombatWeaponId = "";
			return;
		}

		EquippedCombatWeaponId = combatId;
	}

	bool ShouldShowWeapon()
	{
		if ( string.IsNullOrWhiteSpace( EquippedCombatWeaponId ) )
			return false;

		var hp = Components.Get<ThornsPlayerHealth>();
		return !hp.IsValid() || hp.IsAlive && !hp.IsDeadState;
	}

	Vector3 ResolvePresentationVelocity()
	{
		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		return controller.IsValid() ? controller.Velocity : Vector3.Zero;
	}

	float ResolveDuckLevel()
	{
		var cc = Components.Get<CharacterController>( FindMode.EverythingInSelf );
		if ( cc.IsValid() && cc.Height < ThornsPlayerFirstPersonRig.DefaultBodyHeight - 8f )
			return 1f;

		return 0f;
	}

	void EnsureWeaponObject()
	{
		if ( _weaponWorld.IsValid() )
			return;

		_weaponWorld = ThornsBanditUtil.FindChild( GameObject, ThornsBanditUtil.WorldWeaponChildName );
		if ( !_weaponWorld.IsValid() )
		{
			_weaponWorld = new GameObject( true, ThornsBanditUtil.WorldWeaponChildName );
			_weaponWorld.SetParent( GameObject );
		}

		_weaponWorld.LocalScale = ThornsBanditUtil.WorldWeaponLocalScale;
		_renderer = ThornsBanditUtil.GetOrCreateWorldSkinnedModelRenderer( _weaponWorld );
		if ( _renderer.IsValid() )
			_renderer.UseAnimGraph = false;
	}

	void UpdateModel()
	{
		var combatId = EquippedCombatWeaponId?.Trim() ?? "";
		if ( !TryResolveWorldModelPath( combatId, out var path ) )
		{
			if ( string.Equals( _lastCombatId, combatId, StringComparison.Ordinal ) && string.IsNullOrEmpty( path ) )
				return;

			_lastCombatId = combatId;
			_lastModelPath = "";
			if ( _renderer.IsValid() )
				_renderer.Model = default;
			return;
		}

		if ( string.Equals( _lastCombatId, combatId, StringComparison.Ordinal )
		     && string.Equals( _lastModelPath, path, StringComparison.Ordinal ) )
			return;

		_lastCombatId = combatId;
		_lastModelPath = path;

		if ( ThornsWeaponResourceLoad.TryLoadWeaponWorldModel( path, $"third-person {combatId}", out var model ) )
		{
			_renderer.Model = model;
			_renderer.Tint = Color.White;
		}
		else
		{
			_renderer.Model = default;
		}
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
