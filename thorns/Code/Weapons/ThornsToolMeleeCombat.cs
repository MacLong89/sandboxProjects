using System;

namespace Sandbox;

/// <summary>
/// Harvest tools double as short-range melee vs players, wildlife, and bandits (<see cref="ThornsSharedHostHitscan.IsResolvedDamageTarget"/>).
/// </summary>
public static class ThornsToolMeleeCombat
{
	public const string CombatIdPrimitive = "tool_melee_primitive";
	public const string CombatIdStone = "tool_melee_stone";
	public const string CombatIdMetal = "tool_melee_metal";

	/// <summary>Primary tool melee / bare-hands harvest cadence — host <see cref="ThornsWeapon"/> and client harvest-block UX.</summary>
	public const float ToolMeleeLightSwingCooldownSeconds = 0.5f;

	static double _clientPrimaryStrikeCadenceUntil;
	static ulong _clientPrimaryStrikeGeneration;
	static ulong _clientPrimaryStrikePresentationGeneration;

	/// <summary>Owner client: primary tool / hands swing still on cooldown (0.5s fire rate).</summary>
	public static bool ClientIsPrimaryStrikeOnCooldown()
		=> Game.IsPlaying && Time.Now < _clientPrimaryStrikeCadenceUntil;

	/// <summary>Owner client: consume one primary strike slot; false when faster than <see cref="ToolMeleeLightSwingCooldownSeconds"/>.</summary>
	public static bool ClientTryConsumePrimaryStrikeCadence()
	{
		if ( !Game.IsPlaying )
			return false;

		if ( ClientIsPrimaryStrikeOnCooldown() )
			return false;

		_clientPrimaryStrikeCadenceUntil = Time.Now + ToolMeleeLightSwingCooldownSeconds;
		_clientPrimaryStrikeGeneration++;
		return true;
	}

	/// <summary>Owner client: authoritative swing landed — keep client cadence aligned with host.</summary>
	public static void ClientSyncPrimaryStrikeCadenceFromAuthoritative()
	{
		if ( !Game.IsPlaying )
			return;

		var until = Time.Now + ToolMeleeLightSwingCooldownSeconds;
		_clientPrimaryStrikeCadenceUntil = Math.Max( _clientPrimaryStrikeCadenceUntil, until );
	}

	/// <summary>Owner client: paired strike FX already played for the current cadence generation (click prediction).</summary>
	public static bool ClientPrimaryStrikePresentationAlreadyPlayed()
		=> _clientPrimaryStrikePresentationGeneration == _clientPrimaryStrikeGeneration;

	public static void ClientMarkPrimaryStrikePresentationPlayed()
		=> _clientPrimaryStrikePresentationGeneration = _clientPrimaryStrikeGeneration;

	/// <summary>Owner client: clear strike pacing after join / respawn so the first M1 is not stuck behind a stale cooldown.</summary>
	public static void ClientResetPrimaryStrikeCadence()
	{
		_clientPrimaryStrikeCadenceUntil = 0;
		_clientPrimaryStrikeGeneration = 0;
		_clientPrimaryStrikePresentationGeneration = 0;
	}

	/// <summary>Owner client: always pairs strike audio with FP arms swing and harvest-tool mesh swing when applicable.</summary>
	public static void ClientPlayPrimaryToolStrikePresentation(
		GameObject pawnRoot,
		int harvestResourceKindOrdinal = -1,
		bool useMissSound = false,
		bool playSound = true )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionPawnRoot( pawnRoot ) )
			return;

		TryResolveLocalOwnerSelectedToolItemId( pawnRoot, out var itemId );

		if ( playSound )
		{
			string soundPath;
			if ( useMissSound )
				soundPath = ThornsGameplaySfx.MeleeMiss;
			else if ( harvestResourceKindOrdinal >= 0 )
				soundPath = GetHarvestStrikeSoundPathForToolItemId( itemId, harvestResourceKindOrdinal );
			else
				soundPath = GetMeleeHitSoundPathForToolItemId( itemId );

			ThornsGameplaySfx.PlayAtPawnEar(
				pawnRoot,
				soundPath,
				ThornsGameplaySfx.VolumeMultiplierForToolStrikePath( soundPath ) );
		}

		ThornsViewModelController.TryTriggerHarvestToolSwingForLocalOwner( pawnRoot );
		ThornsWeapon.TryPlayToolPrimaryStrikeFpAnimationForLocalOwner( pawnRoot );
	}

	/// <summary>Owner client: selected hotbar row has no usable item (bare hands).</summary>
	public static bool ClientHotbarSelectionIsBareHands( ThornsHotbarEquipment hb, ThornsInventory inv )
	{
		if ( !hb.IsValid() || !inv.IsValid() )
			return false;

		if ( ClientTryGetEquippedHotbarItemId( hb, inv, out var itemId ) )
			return string.IsNullOrWhiteSpace( itemId );

		return string.Equals(
			hb.ObserversCombatWeaponDefinitionId?.Trim(),
			CombatIdPrimitive,
			StringComparison.OrdinalIgnoreCase )
		       && string.IsNullOrWhiteSpace( hb.ObserversEquippedHotbarItemId );
	}

	/// <summary>Best-known equipped hotbar item before inventory mirror catches up (equip RPC / synced observers).</summary>
	public static bool ClientTryGetEquippedHotbarItemId(
		ThornsHotbarEquipment hb,
		ThornsInventory inv,
		out string itemId )
	{
		itemId = "";
		if ( !hb.IsValid() )
			return false;

		var sel = hb.ClientMirrorSelectedHotbar;
		if ( sel >= 0 && inv.IsValid() && inv.TryGetClientMirrorSlot( sel, out var net ) )
		{
			if ( net.Quantity > 0 && !string.IsNullOrWhiteSpace( net.ItemId ) )
			{
				itemId = net.ItemId.Trim();
				return true;
			}

			return true;
		}

		var active = hb.ClientMirrorActiveItemId?.Trim() ?? "";
		if ( !string.IsNullOrWhiteSpace( active ) )
		{
			itemId = active;
			return true;
		}

		var observed = hb.ObserversEquippedHotbarItemId?.Trim() ?? "";
		if ( !string.IsNullOrWhiteSpace( observed ) )
		{
			itemId = observed;
			return true;
		}

		return sel >= 0;
	}

	/// <summary>
	/// Owner client: resolve combat id when equip RPC / owner mirror lag behind synced hotbar (common first ~seconds after spawn).
	/// </summary>
	public static string TryInferClientCombatDefinitionId( ThornsHotbarEquipment hb, ThornsInventory inv )
	{
		if ( !hb.IsValid() )
			return "";

		var observers = hb.ObserversCombatWeaponDefinitionId?.Trim() ?? "";
		if ( !string.IsNullOrWhiteSpace( observers ) )
			return observers;

		if ( inv.IsValid() && ClientActsAsPrimitiveToolCombat( hb, inv ) )
			return CombatIdPrimitive;

		if ( ClientTryGetEquippedHotbarItemId( hb, inv, out var itemId )
		     && !string.IsNullOrWhiteSpace( itemId ) )
		{
			var toolCombat = GetCombatDefinitionIdForToolItemId( itemId )?.Trim() ?? "";
			if ( !string.IsNullOrEmpty( toolCombat ) )
				return toolCombat;

			if ( ThornsItemRegistry.TryGet( itemId, out var def )
			     && def.ItemType == ThornsItemType.Weapon
			     && !string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) )
				return def.CombatWeaponDefinitionId.Trim();
		}

		if ( inv.IsValid() && ClientHotbarSelectionIsBareHands( hb, inv ) )
			return CombatIdPrimitive;

		if ( !string.IsNullOrWhiteSpace( hb.ClientMirrorActiveItemId ) )
		{
			var activeCombat = GetCombatDefinitionIdForToolItemId( hb.ClientMirrorActiveItemId )?.Trim() ?? "";
			if ( !string.IsNullOrEmpty( activeCombat ) )
				return activeCombat;
		}

		return "";
	}

	/// <summary>Owner client: best combat id for Attack1 routing before equip RPC / owner mirror catch up.</summary>
	public static string ResolveClientCombatDefinitionIdForInput( ThornsWeapon weapon )
	{
		if ( weapon is null || !weapon.IsValid() )
			return "";

		var cid = weapon.ClientMirrorCombatDefinitionId?.Trim() ?? "";
		if ( !string.IsNullOrEmpty( cid ) )
			return cid;

		var hb = weapon.Components.Get<ThornsHotbarEquipment>();
		var inv = weapon.Components.Get<ThornsInventory>();
		if ( !hb.IsValid() )
			return "";

		return TryInferClientCombatDefinitionId( hb, inv )?.Trim() ?? "";
	}

	/// <summary>Owner client: empty hotbar row or equipped primitive tool — harvest + melee primitive profile.</summary>
	public static bool ClientActsAsPrimitiveToolCombat( ThornsHotbarEquipment hb, ThornsInventory inv )
	{
		if ( !hb.IsValid() || !inv.IsValid() )
			return false;

		if ( ClientHotbarSelectionIsBareHands( hb, inv ) )
			return true;

		if ( !ClientTryGetEquippedHotbarItemId( hb, inv, out var itemId ) || string.IsNullOrWhiteSpace( itemId ) )
			return false;

		var fromItem = GetCombatDefinitionIdForToolItemId( itemId )?.Trim() ?? "";
		return string.Equals( fromItem, CombatIdPrimitive, StringComparison.OrdinalIgnoreCase );
	}

	public static bool TryResolveLocalOwnerSelectedToolItemId( GameObject pawnRoot, out string itemId )
	{
		itemId = "";
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var hb = pawnRoot.Components.Get<ThornsHotbarEquipment>();
		var inv = pawnRoot.Components.Get<ThornsInventory>();
		if ( !hb.IsValid() || !inv.IsValid() )
			return false;

		if ( ClientTryGetEquippedHotbarItemId( hb, inv, out var equipped ) && !string.IsNullOrWhiteSpace( equipped ) )
		{
			itemId = equipped;
			return true;
		}

		if ( ClientActsAsPrimitiveToolCombat( hb, inv ) )
		{
			itemId = "primitive_tool";
			return true;
		}

		return false;
	}

	public static bool IsToolMeleeCombatId( string combatId )
	{
		if ( string.IsNullOrWhiteSpace( combatId ) )
			return false;

		var t = combatId.Trim();
		return string.Equals( t, CombatIdPrimitive, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( t, CombatIdStone, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( t, CombatIdMetal, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Host: hotbar selection uses <see cref="CombatIdPrimitive"/> (empty hands or <c>primitive_tool</c> item).</summary>
	public static bool HostSelectedActsAsPrimitiveToolCombat( ThornsHotbarEquipment equip, ThornsInventory inv )
	{
		if ( equip is null || !equip.IsValid() || inv is null || !inv.IsValid() )
			return false;

		var sel = equip.ServerGetSelectedHotbarIndex();
		if ( sel < 0 || !inv.TryGetHostSlot( sel, out var slot ) )
			return false;

		if ( slot.IsEmpty )
		{
			var cid = equip.ServerGetActiveCombatWeaponDefinitionId()?.Trim() ?? "";
			if ( string.IsNullOrEmpty( cid ) )
				return true;
			return string.Equals( cid, CombatIdPrimitive, StringComparison.OrdinalIgnoreCase );
		}

		var fromItem = GetCombatDefinitionIdForToolItemId( slot.ItemId )?.Trim() ?? "";
		return string.Equals( fromItem, CombatIdPrimitive, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Maps hotbar tool item id to <see cref="ThornsWeaponDefinitions"/> row; empty if not a melee-capable tool.</summary>
	public static string GetCombatDefinitionIdForToolItemId( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "";

		if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
			return CombatIdPrimitive;

		if ( string.Equals( itemId, "stone_hatchet", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( itemId, "stone_pick", StringComparison.OrdinalIgnoreCase ) )
			return CombatIdStone;

		if ( string.Equals( itemId, "axe", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( itemId, "pickaxe", StringComparison.OrdinalIgnoreCase ) )
			return CombatIdMetal;

		return "";
	}

	/// <summary>Client-only: if true, primary swing should go to weapon melee instead of harvest RPC.</summary>
	/// <summary>Axe / primitive strike vs pickaxe strike — paths are <see cref="ThornsGameplaySfx"/>.</summary>
	public static string GetMeleeHitSoundPathForToolItemId( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return ThornsGameplaySfx.AxeHit;

		if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
			return ThornsGameplaySfx.AxeHit;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) || def.ItemType != ThornsItemType.Tool )
			return ThornsGameplaySfx.AxeHit;

		return def.HarvestToolKind == ThornsHarvestToolKind.Pickaxe
			? ThornsGameplaySfx.PickaxeHit
			: ThornsGameplaySfx.AxeHit;
	}

	/// <summary>Harvest success feedback — primitive uses node resource kind for pick vs axe strike; other tools follow item type.</summary>
	public static string GetHarvestStrikeSoundPathForToolItemId( string itemId, int harvestResourceKindOrdinal )
	{
		if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase )
		     && Enum.IsDefined( typeof(ThornsResourceKind ), harvestResourceKindOrdinal ) )
		{
			var k = (ThornsResourceKind)harvestResourceKindOrdinal;
			return k == ThornsResourceKind.Stone ? ThornsGameplaySfx.PickaxeHit : ThornsGameplaySfx.AxeHit;
		}

		return GetMeleeHitSoundPathForToolItemId( itemId );
	}

	/// <summary>
	/// Owner melee swing that hit world geometry but dealt no damage — must match harvest strike for <c>primitive_tool</c>
	/// (stone → pickaxe, wood/fiber → axe). <see cref="GetMeleeHitSoundPathForToolItemId"/> alone always used axe for primitive.
	/// </summary>
	public static string GetMeleeWorldContactStrikeSoundPathForTool( GameObject pawnRoot, string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) || pawnRoot is null || !pawnRoot.IsValid() )
			return ThornsGameplaySfx.AxeHit;

		if ( !string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
			return GetMeleeHitSoundPathForToolItemId( itemId );

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return ThornsGameplaySfx.AxeHit;

		// Same search radius as <see cref="ThornsHarvestInteractor"/> harvest intent.
		const float radius = 380f;
		var node = ThornsResourceNode.FindNearestHarvestable( scene, pawnRoot.WorldPosition, radius );
		if ( !node.IsValid() )
			return ThornsGameplaySfx.AxeHit;

		var hb = pawnRoot.Components.Get<ThornsHotbarEquipment>( FindMode.EnabledInSelf );
		var inv = pawnRoot.Components.Get<ThornsInventory>( FindMode.EnabledInSelf );
		if ( !ThornsHarvestInteractor.ClientMirrorHarvestToolMatchesNode( hb, inv, node ) )
			return ThornsGameplaySfx.AxeHit;

		return GetHarvestStrikeSoundPathForToolItemId( itemId, (int)node.ResourceKind );
	}

	public static bool ClientSwingWouldPreferMeleeOverHarvest( GameObject pawnRoot, string toolItemId )
	{
		var cid = GetCombatDefinitionIdForToolItemId( toolItemId );
		if ( string.IsNullOrEmpty( cid ) )
			return false;

		var def = ThornsWeaponDefinitions.Get( cid );
		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		var dir = rot.Forward.Normal;
		return ThornsSharedHostHitscan.TryResolveHitscanDamageTarget(
			pawnRoot,
			eye,
			dir,
			def.MaxRange,
			ThornsSharedHostHitscan.MeleeMaxAbsVerticalSeparationFeetDefault,
			out _,
			out _,
			out _,
			out var vh,
			out _,
			out _ )
		       && vh.IsValid();
	}
}
