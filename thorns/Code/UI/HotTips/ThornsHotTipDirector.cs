using System;

namespace Sandbox;

/// <summary>Owner client: contextual hot tips → <see cref="ThornsGameShell"/> center-left rail.</summary>
[Title( "Thorns — Hot Tips" )]
[Category( "Thorns/UI" )]
[Icon( "tips_and_updates" )]
[Order( 79 )]
public sealed class ThornsHotTipDirector : Component
{
	const float EvalIntervalSeconds = 0.22f;
	const float LootCrateLookRange = 130f;

	readonly ThornsHotTipMemory _memory = new();

	double _sessionStart = -1;
	double _nextEval;
	double _globalCooldownUntil;
	float _lookWoodSec;
	float _lookStoneSec;
	float _lookOreSec;
	float _lookLootSec;
	float _lookTameSec;
	bool _pendingReloadTip;
	bool _pendingFirstShotTip;
	bool _pendingEquipWeaponTip;
	bool _hadGunHotbar;

	protected override void OnStart()
	{
		if ( _sessionStart < 0 )
			_sessionStart = Time.Now;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		TryInitMemoryAccount();

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() || !shell.Enabled || !shell.IsLocalHudReady )
			return;

		try
		{
			shell.TickHotTips();

			FlushPendingInstantTips( shell );
			TryQueueEquipWeaponTip( shell );

			if ( Time.Now < _nextEval )
				return;

			_nextEval = Time.Now + EvalIntervalSeconds;

			if ( shell.MenuOpen || shell.BlocksGameplayShellOverlay )
				return;

			if ( shell.HotTipSlotCount >= 2 )
				return;

			if ( Time.Now < _globalCooldownUntil )
				return;

			TickLookTargets( out var lookNode, out var lookCrate, out var lookTame );

			var sessionSec = (float)Math.Max( 0, Time.Now - _sessionStart );
			var ctx = ThornsHotTipContext.Build(
				GameObject,
				sessionSec,
				_lookWoodSec,
				_lookStoneSec,
				_lookOreSec,
				_lookLootSec,
				_lookTameSec,
				lookNode,
				lookCrate,
				lookTame );

			if ( !ThornsHotTipEvaluator.TryPick( ctx, _memory, Time.Now, out var def ) )
				return;

			if ( shell.EnqueueHotTip( def ) )
				_globalCooldownUntil = Time.Now + ThornsHotTipRegistry.DefaultGlobalCooldownSeconds;

			_memory.FlushIfDirty();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Hot tips: Update failed — skipping frame." );
		}
	}

	void FlushPendingInstantTips( ThornsGameShell shell )
	{
		if ( _pendingFirstShotTip && ThornsHotTipRegistry.TryGet( ThornsHotTipIds.ReloadR, out var reloadDef ) )
		{
			_pendingFirstShotTip = false;
			if ( _memory.TryBeginShow( reloadDef, Time.Now ) && shell.EnqueueHotTip( reloadDef ) )
				_globalCooldownUntil = Time.Now + 8f;
		}

		if ( _pendingReloadTip && ThornsHotTipRegistry.TryGet( ThornsHotTipIds.ReloadR, out var rDef ) )
		{
			_pendingReloadTip = false;
			if ( _memory.TryBeginShow( rDef, Time.Now ) && shell.EnqueueHotTip( rDef ) )
				_globalCooldownUntil = Time.Now + 8f;
		}
	}

	void TryQueueEquipWeaponTip( ThornsGameShell shell )
	{
		var hb = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		var hasGun = false;
		if ( inv.IsValid() && hb.IsValid() )
		{
			for ( var i = 0; i < ThornsInventory.HotbarSlotCount; i++ )
			{
				if ( !inv.TryGetClientMirrorSlot( i, out var s ) || !ThornsHotTipContext.ClientMirrorSlotOccupied( s ) )
					continue;

				if ( ThornsItemRegistry.TryGet( s.ItemId, out var def ) && def.ItemType == ThornsItemType.Weapon )
				{
					hasGun = true;
					break;
				}
			}
		}

		if ( hasGun && !_hadGunHotbar )
			_pendingEquipWeaponTip = true;

		_hadGunHotbar = hasGun;

		if ( !_pendingEquipWeaponTip )
			return;

		_pendingEquipWeaponTip = false;
		if ( !ThornsHotTipRegistry.TryGet( ThornsHotTipIds.EquipWeaponHotbar, out var tipDef ) )
			return;

		if ( !_memory.TryBeginShow( tipDef, Time.Now ) )
			return;

		if ( shell.EnqueueHotTip( tipDef ) )
			_globalCooldownUntil = Time.Now + 8f;
	}

	void TryInitMemoryAccount()
	{
		var key = Connection.Local is { } lc
			? ThornsPersistenceIdentity.GetStableAccountKey( lc )
			: "local";

		_memory.BindAccountKey( key );
	}

	void TickLookTargets(
		out ThornsResourceNode lookNode,
		out ThornsLootCrate lookCrate,
		out ThornsWildlifeIdentity lookTame )
	{
		var dt = EvalIntervalSeconds;
		lookNode = default;
		lookCrate = default;
		lookTame = default;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
		{
			DecayLookTimers( dt, false, false, false, false, false );
			return;
		}

		var node = ThornsResourceNode.FindNearestHarvestable( scene, GameObject.WorldPosition, 380f );
		if ( node.IsValid()
		     && ThornsWorldUseAim.PawnLooksAtInteractableRoot( GameObject, node.GameObject, 380f ) )
		{
			lookNode = node;
			switch ( node.ResourceKind )
			{
				case ThornsResourceKind.Wood:
					_lookWoodSec += dt;
					_lookStoneSec = 0f;
					_lookOreSec = 0f;
					break;
				case ThornsResourceKind.Stone:
					_lookStoneSec += dt;
					_lookWoodSec = 0f;
					_lookOreSec = 0f;
					break;
				case ThornsResourceKind.MetalOre:
					_lookOreSec += dt;
					_lookWoodSec = 0f;
					_lookStoneSec = 0f;
					break;
				default:
					DecayLookTimers( dt, true, true, true, false, false );
					break;
			}
		}
		else
		{
			_lookWoodSec = 0f;
			_lookStoneSec = 0f;
			_lookOreSec = 0f;
		}

		lookCrate = FindLookLootCrate();
		if ( lookCrate.IsValid() )
			_lookLootSec += dt;
		else
			_lookLootSec = 0f;

		if ( ThornsWildlifeTamingRules.TryGetRayTameCandidate( GameObject, out var wid, out _ ) && wid.IsValid() )
		{
			lookTame = wid;
			_lookTameSec += dt;
		}
		else
		{
			_lookTameSec = 0f;
		}
	}

	void DecayLookTimers( float dt, bool wood, bool stone, bool ore, bool loot, bool tame )
	{
		if ( !wood ) _lookWoodSec = 0f;
		if ( !stone ) _lookStoneSec = 0f;
		if ( !ore ) _lookOreSec = 0f;
		if ( !loot ) _lookLootSec = 0f;
		if ( !tame ) _lookTameSec = 0f;
	}

	ThornsLootCrate FindLookLootCrate()
	{
		ThornsLootCrate pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var c in ThornsLootCrate.ActiveById.Values )
		{
			if ( !c.IsValid() )
				continue;

			var d = (c.GameObject.WorldPosition - GameObject.WorldPosition).Length;
			if ( d > LootCrateLookRange || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( GameObject, c.GameObject, LootCrateLookRange ) )
				continue;

			bestD = d;
			pick = c;
		}

		return pick;
	}

	/// <summary>Called from gameplay code for one-shot tips (first shot, reload, etc.).</summary>
	public static void NotifyLocal( GameObject pawnRoot, string tipId )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var dir = pawnRoot.Components.Get<ThornsHotTipDirector>();
		if ( !dir.IsValid() )
			return;

		if ( string.Equals( tipId, ThornsHotTipIds.ReloadR, StringComparison.OrdinalIgnoreCase ) )
			dir._pendingReloadTip = true;
	}

	public void NotifyFirstGunshot()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		_pendingFirstShotTip = true;
	}

	/// <summary>Owner called reload intent successfully (first time).</summary>
	public void NotifyReloadIntent()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		_pendingReloadTip = true;
	}

	protected override void OnDestroy()
	{
		_memory.FlushIfDirty();
		base.OnDestroy();
	}
}
