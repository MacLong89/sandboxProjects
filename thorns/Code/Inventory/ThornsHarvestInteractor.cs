namespace Sandbox;

/// <summary>
/// Owner intent: harvest nearest resource node on <b>primary attack only</b> (<c>Attack1</c> / mouse1 — not <c>Use</c> / E).
/// Host validates range, node state, inventory.
/// </summary>
[Title( "Thorns — Harvest Interactor" )]
[Category( "Thorns" )]
[Icon( "eco" )]
[Order( 78 )]
public sealed class ThornsHarvestInteractor : Component
{
	/// <summary>Must cover host wood reach (see <see cref="ThornsResourceNode.HostIsCallerInHarvestRange"/> canopy slack for foliage tree1–7).</summary>
	const float ClientHarvestSearchRadius = 380f;


	/// <summary>Shared with <see cref="ThornsWeapon"/> so TAB shell / build mode block harvest and melee the same way.</summary>
	public static bool LocalOwnerGameplayInputBlocked( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return true;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return true;

		var shell = pawnRoot.Components.Get<ThornsGameShell>();
		var shellHud = shell.IsValid() && shell.Enabled;
		if ( shellHud && shell.BlocksGameplayShellOverlay )
			return true;

		var hud = pawnRoot.Components.Get<ThornsDebugHudHost>();
		var hp = pawnRoot.Components.Get<ThornsHealth>();
		var build = pawnRoot.Components.Get<ThornsBuildingController>();

		if ( hud.IsValid() && hud.ShowDebugOverlay )
			return true;

		if ( !shellHud && hud.IsValid() && (hud.ShowFullInventory || hud.ShowRadioShop) )
			return true;

		if ( hp.IsValid() && hp.IsDeadState )
			return true;

		if ( build.IsValid() && build.BuildModeActive )
			return true;

		return false;
	}

	bool UiBlocksHarvest() => LocalOwnerGameplayInputBlocked( GameObject );

	/// <summary>Shared by <see cref="ThornsToolMeleeCombat"/> so melee world-contact audio matches harvest strike (primitive pick vs axe).</summary>
	public static bool ClientMirrorHarvestToolMatchesNode( ThornsHotbarEquipment hb, ThornsInventory inv, ThornsResourceNode node )
	{
		if ( !hb.IsValid() || !node.IsValid() )
			return false;

		if ( !ClientTryResolveHarvestToolDefinition( hb, inv, out var def ) )
			return false;

		return ThornsItemRegistry.HarvestToolMatchesResourceKind( def.HarvestToolKind, node.ResourceKind );
	}

	/// <summary>False when inventory/hotbar mirrors are not ready — caller should not swallow Attack1.</summary>
	public static bool ClientMirrorCanValidateHarvestTool( ThornsHotbarEquipment hb, ThornsInventory inv ) =>
		ClientTryResolveHarvestToolDefinition( hb, inv, out _ );

	static bool ClientTryResolveHarvestToolDefinition(
		ThornsHotbarEquipment hb,
		ThornsInventory inv,
		out ThornsItemRegistry.ThornsItemDefinition def )
	{
		def = default;
		if ( !hb.IsValid() )
			return false;

		if ( !ThornsToolMeleeCombat.ClientTryGetEquippedHotbarItemId( hb, inv, out var itemId ) )
			return false;

		if ( !string.IsNullOrWhiteSpace( itemId ) )
		{
			if ( ThornsItemRegistry.TryGet( itemId, out def ) )
				return def.ItemType == ThornsItemType.Tool;

			if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
			{
				def = ThornsItemRegistry.PrimitiveToolDefinition;
				return true;
			}

			return false;
		}

		if ( !ThornsToolMeleeCombat.ClientActsAsPrimitiveToolCombat( hb, inv ) )
			return false;

		def = ThornsItemRegistry.PrimitiveToolDefinition;
		return true;
	}

	void ClientPushHarvestBlockedToast( string toastMessage, float toastDurationSeconds )
	{
		if ( string.IsNullOrWhiteSpace( toastMessage ) )
			return;

		var shell = Components.Get<ThornsGameShell>();
		if ( shell.IsValid() )
			shell.PushGameplayToast( toastMessage.Trim(), toastDurationSeconds, ThornsGameplayToastKind.Hint );
	}

	/// <summary>
	/// Owner <c>Attack1</c> harvest path. Returns true when this swing was consumed (harvest RPC, wrong-tool feedback, etc.).
	/// <see cref="ThornsWeapon"/> calls this before bare-hands / tool melee so punches are not swallowed by stale defer logic.
	/// </summary>
	public bool ClientTryHandlePrimaryHarvestSwing()
	{
		if ( !Game.IsPlaying || !ThornsPawn.IsLocalConnectionOwner( this ) )
			return false;

		if ( UiBlocksHarvest() )
			return false;

		var node = ThornsResourceNode.FindNearestHarvestable( GameObject.Scene, GameObject.WorldPosition, ClientHarvestSearchRadius );
		if ( !node.IsValid() )
			return false;

		var hb = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		if ( !ClientMirrorHarvestToolMatchesNode( hb, inv, node ) )
		{
			if ( !ClientMirrorCanValidateHarvestTool( hb, inv ) )
				return false;

			TryPushWrongToolOrBlockedHarvestToast( hb, inv, node );
			return true;
		}

		if ( ThornsToolMeleeCombat.ClientTryGetEquippedHotbarItemId( hb, inv, out var swingItemId )
		     && !string.IsNullOrWhiteSpace( swingItemId )
		     && ThornsToolMeleeCombat.ClientSwingWouldPreferMeleeOverHarvest( GameObject, swingItemId ) )
			return false;

		if ( !ThornsToolMeleeCombat.ClientTryConsumePrimaryStrikeCadence() )
			return false;

		ThornsToolMeleeCombat.ClientPlayPrimaryToolStrikePresentation( GameObject, (int)node.ResourceKind );
		ThornsToolMeleeCombat.ClientMarkPrimaryStrikePresentationPlayed();

		RequestHarvestNode( node.NodeId );
		return true;
	}

	/// <summary>Consumable routing only — do not use to block melee (see <see cref="ClientTryHandlePrimaryHarvestSwing"/>).</summary>
	public bool ShouldSuppressConsumableBecauseHarvestableInRange()
	{
		if ( UiBlocksHarvest() )
			return false;

		var node = ThornsResourceNode.FindNearestHarvestable( GameObject.Scene, GameObject.WorldPosition, ClientHarvestSearchRadius );
		if ( !node.IsValid() )
			return false;

		var hb = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		if ( !ClientMirrorHarvestToolMatchesNode( hb, inv, node ) )
			return false;

		if ( ThornsToolMeleeCombat.ClientTryGetEquippedHotbarItemId( hb, inv, out var itemId )
		     && !string.IsNullOrWhiteSpace( itemId )
		     && ThornsToolMeleeCombat.ClientSwingWouldPreferMeleeOverHarvest( GameObject, itemId ) )
			return false;

		return true;
	}

	[Rpc.Host]
	public void RequestHarvestNode( Guid nodeId )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
		{
			Log.Warning( "[Thorns] Harvest rejected: caller does not own this pawn" );
			RpcHarvestFeedback( "rejected", "", 0, "not_owner", false, -1 );
			return;
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Warning( "[Thorns] Harvest rejected: dead" );
			RpcHarvestFeedback( "rejected", "", 0, "dead", false, -1 );
			return;
		}

		if ( !ThornsResourceNode.ActiveById.TryGetValue( nodeId, out var node ) || !node.IsValid() )
		{
			Log.Warning( "[Thorns] Harvest rejected: invalid node id" );
			RpcHarvestFeedback( "rejected", "", 0, "invalid_node", false, -1 );
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			Log.Warning( "[Thorns] Harvest rejected: no inventory" );
			RpcHarvestFeedback( "rejected", "", 0, "no_inventory", false, -1 );
			return;
		}

		if ( !node.HostIsCallerInHarvestRange( GameObject ) )
		{
			Log.Warning( $"[Thorns] Harvest rejected: distance node={nodeId}" );
			RpcHarvestFeedback( "rejected", "", 0, "distance", false, -1 );
			return;
		}

		var weaponCd = GameObject.Components.Get<ThornsWeapon>( FindMode.EnabledInSelf );
		if ( weaponCd.IsValid() && weaponCd.HostIsPrimaryMeleeCooldownActive() )
		{
			RpcHarvestFeedback( "rejected", "", 0, "rate_limited", false, -1 );
			return;
		}

		var hotbar = Components.Get<ThornsHotbarEquipment>();
		var grantItemId = node.HostResolveYieldItemId();

		var yieldMul = 1f;
		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( ups.IsValid() )
			yieldMul = ups.GetHarvestYieldMultiplier( node.ResourceKind );

		if ( !node.HostTryHarvestStrike( inv, GameObject, yieldMul, out var grantedQty, out var reason, out var depletedNode ) )
		{
			Log.Warning( $"[Thorns] Harvest rejected: {reason}" );
			RpcHarvestFeedback( "rejected", "", 0, reason, false, -1 );
			return;
		}

		if ( weaponCd.IsValid() )
			weaponCd.HostApplyPrimaryMeleeCooldownSeconds( ThornsToolMeleeCombat.ToolMeleeLightSwingCooldownSeconds );

		var vitals = Components.Get<ThornsVitals>();
		if ( vitals.IsValid() )
			vitals.AddXp( ThornsXpBalance.HarvestStrikeActivity );

		var selHarvest = hotbar.IsValid() ? hotbar.ServerGetSelectedHotbarIndex() : -1;
		if ( selHarvest >= 0 && inv.TryGetHostSlot( selHarvest, out var harvestToolSlot )
		     && ThornsItemRegistry.TryGet( harvestToolSlot.ItemId, out var harvestToolDef )
		     && harvestToolDef.ItemType == ThornsItemType.Tool
		     && harvestToolDef.ToolMaxDurability > 0.001f )
		{
			ThornsInventory.HostApplyToolDurabilityLoss(
				ref harvestToolSlot,
				harvestToolDef,
				harvestToolDef.ToolDurabilityLossPerStrike,
				ups );
			inv.ServerWriteSlot( selHarvest, harvestToolSlot );
			var weapon = GameObject.Components.Get<ThornsWeapon>( FindMode.EnabledInSelf );
			if ( weapon.IsValid() )
				weapon.HostPushWeaponHudFromInventory();
		}

		RpcHarvestFeedback( "ok", grantItemId, grantedQty, "", depletedNode, (int)node.ResourceKind );
	}

	[Rpc.Owner]
	void RpcHarvestFeedback( string status, string itemId, int qty, string reason, bool resourceFullyDepleted, int harvestResourceKindOrdinal )
	{
		if ( status != "ok" )
		{
			var msg = FormatHarvestRejectToast( reason );
			if ( !string.IsNullOrWhiteSpace( msg ) )
				ClientPushHarvestBlockedToast( msg, 2.9f );

			if ( !string.IsNullOrEmpty( reason ) )
				Log.Warning( $"[Thorns Harvest] rejected: {reason}" );
			return;
		}

		if ( qty <= 0 )
			return;

		ThornsToolMeleeCombat.ClientSyncPrimaryStrikeCadenceFromAuthoritative();

		if ( !ThornsToolMeleeCombat.ClientPrimaryStrikePresentationAlreadyPlayed() )
		{
			ThornsToolMeleeCombat.ClientPlayPrimaryToolStrikePresentation( GameObject, harvestResourceKindOrdinal );
			ThornsToolMeleeCombat.ClientMarkPrimaryStrikePresentationPlayed();
		}

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		if ( ThornsItemRegistry.TryGet( itemId, out var def ) )
		{
			var line1 = $"+{qty} {def.DisplayName}";
			var line2 = resourceFullyDepleted ? "Resource cleared!" : "Nice hit — keep harvesting.";
			shell.PushGameplayToast( $"{line1}\n{line2}", 2.35f, ThornsGameplayToastKind.Positive );
		}
		else
			shell.PushGameplayToast( $"+{qty} {itemId}", 2.2f, ThornsGameplayToastKind.Positive );
	}

	/// <summary>Primary swing at a node while a tool is equipped that cannot harvest this resource (e.g. primitive on ore).</summary>
	void TryPushWrongToolOrBlockedHarvestToast(
		ThornsHotbarEquipment hb,
		ThornsInventory inv,
		ThornsResourceNode node )
	{
		if ( !hb.IsValid() || !inv.IsValid() || !node.IsValid() )
			return;

		if ( !ClientTryResolveHarvestToolDefinition( hb, inv, out var def ) )
			return;

		if ( def.ItemType != ThornsItemType.Tool )
			return;

		if ( ThornsItemRegistry.HarvestToolMatchesResourceKind( def.HarvestToolKind, node.ResourceKind ) )
			return;

		if ( !ThornsToolMeleeCombat.ClientTryConsumePrimaryStrikeCadence() )
			return;

		ThornsToolMeleeCombat.ClientPlayPrimaryToolStrikePresentation( GameObject, useMissSound: true );
		ThornsToolMeleeCombat.ClientMarkPrimaryStrikePresentationPlayed();
		ClientPushHarvestBlockedToast( "You need a better tool to harvest this!", 2.85f );
	}

	static string FormatHarvestRejectToast( string reason )
	{
		if ( string.IsNullOrWhiteSpace( reason ) )
			return "";

		return reason.Trim().ToLowerInvariant() switch
		{
			"wrong_tool" => "You need a better tool to harvest this!",
			"depleted" => "Nothing left to gather here.",
			"distance" => "Move closer to harvest.",
			"inventory_full" => "Inventory full — make space before harvesting.",
			"no_tool_selected" => "Select a harvest tool on your hotbar.",
			"unknown_tool" => "That equipped item can't harvest this.",
			"not_host" or "no_caller" or "not_owner" or "invalid_node" or "bad_inventory" or "no_inventory"
				or "no_equipment" => "",
			_ => "Can't harvest that right now."
		};
	}
}
