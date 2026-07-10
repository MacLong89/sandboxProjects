namespace Terraingen.UI.Hud;

using Terraingen.Buildings;
using Terraingen;
using Terraingen.Combat;
using Terraingen.Core;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Resolves the centered interaction prompt for the gameplay HUD.</summary>
public static class ThornsInteractionPrompt
{
	static ThornsHudPromptState _cached;
	static TimeSince _lastResolve;
	static int _aimSignature;

	public static ThornsHudPromptState Resolve()
	{
		var sig = ComputeAimSignature();
		if ( _lastResolve < Terraingen.Core.ThornsHudTickRates.InteractionPromptSeconds && sig == _aimSignature )
			return _cached;

		_aimSignature = sig;
		_lastResolve = 0;
		_cached = ResolveImmediate();
		return _cached;
	}

	public static void Invalidate() => _lastResolve = 999f;

	internal static void NotifyChanged()
		=> UiRevisionBus.Publish( UiRevisionChannel.Interaction );

	static int ComputeAimSignature()
	{
		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() )
			return 0;

		var root = player.GameObject;
		if ( !root.IsValid() )
			return 0;

		var pos = root.WorldPosition;
		if ( !TryResolveAimRay( root, out _, out var forward ) )
			return HashCode.Combine( pos.x.GetHashCode(), pos.y.GetHashCode(), pos.z.GetHashCode() );

		return HashCode.Combine( pos.x.GetHashCode(), pos.y.GetHashCode(), pos.z.GetHashCode(), forward.x.GetHashCode(), forward.y.GetHashCode() );
	}

	static ThornsHudPromptState ResolveImmediate()
	{
		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() )
			return default;

		var root = player.GameObject;
		if ( !root.IsValid() )
			return default;

		var building = ThornsPlayerBuildingController.Local;
		if ( building?.IsHotbarPlaceModeActive == true
		     && ThornsPlayerBuildingDefinitions.TryGet( building.HotbarPlaceStructureId, out var placeDef ) )
		{
			var suffix = building.CurrentPreview.Valid ? "Q/R rotate" : "blocked";
			return ThornsHudPromptState.AttackAction( $"Place {placeDef.DisplayName} ({suffix})" );
		}

		var taming = root.Components.Get<ThornsPlayerAnimalTaming>();
		if ( taming.IsValid() && taming.TryGetPrompt( out var tameVerb ) )
			return ThornsHudPromptState.HoldAction( tameVerb, holdFraction: taming.TameHoldFraction );

		var mountUse = root.Components.Get<ThornsPlayerMountUse>();
		if ( mountUse is not null && mountUse.HasMountTargetInFront() )
			return ThornsHudPromptState.HoldAction( "Mount", holdFraction: mountUse.MountHoldFraction );

		var guildCore = root.Components.Get<ThornsPlayerNpcGuildCoreUse>();
		if ( guildCore.IsValid() && guildCore.TryGetClaimPrompt( out var claimVerb, out var claimHold ) )
			return ThornsHudPromptState.HoldAction( claimVerb, holdFraction: claimHold );

		if ( ThornsPlayerContainerUse.HasOpenableTargetInFront( root ) )
		{
			if ( root.Components.Get<ThornsPlayerDeathCrateUse>()?.HasLootTargetInFront() == true )
				return ThornsHudPromptState.PressAction( "Open Death Crate" );

			if ( root.Components.Get<ThornsPlayerAirdropUse>()?.HasLootTargetInFront() == true )
				return ThornsHudPromptState.PressAction( "Open Supply Drop" );

			return ThornsHudPromptState.PressAction( "Open Container" );
		}

		var water = root.Components.Get<ThornsPlayerWaterDrinkUse>();
		if ( water.IsValid() && ThornsNaturalWaterDrink.CanDrinkAt( root.Scene, root ) )
			return ThornsHudPromptState.HoldAction( "Drink", holdFraction: water.DrinkHoldFraction );

		var bloomSeed = root.Components.Get<ThornsPlayerBloomSeedUse>();
		if ( bloomSeed.IsValid() && bloomSeed.TryGetPurifyPrompt( out var purifyVerb, out var purifyHold ) )
			return ThornsHudPromptState.HoldAction( purifyVerb, holdFraction: purifyHold );

		if ( ThornsPlayerRadioShopUse.HasRadioShopTargetInFront( root ) )
			return ThornsHudPromptState.PressAction( "Open Radio Shop" );

		if ( ThornsPlayerCampfireUse.TryGetPrompt( root, out var campfireVerb ) )
			return ThornsHudPromptState.PressAction( campfireVerb );

		if ( ThornsPlayerWorkbenchUse.TryGetPrompt( root, out var workbenchVerb ) )
			return ThornsHudPromptState.PressAction( workbenchVerb );

		if ( ThornsPlayerCraftStationUse.TryGetCraftStationPrompt( root, out var craftVerb ) )
			return ThornsHudPromptState.PressAction( craftVerb );

		if ( ThornsPlayerDoorUse.TryGetPrompt( root, out var doorVerb ) )
			return ThornsHudPromptState.PressAction( doorVerb );

		if ( ThornsPlayerResearchStationUse.TryGetPrompt( root, out var researchVerb ) )
			return ThornsHudPromptState.PressAction( researchVerb );

		if ( ThornsPlayerResourceSalvageUse.TryGetPromptVerb( root, out var salvageVerb ) )
			return ThornsHudPromptState.AttackAction( salvageVerb );

		var hotbarConsume = root.Components.Get<ThornsPlayerHotbarConsumeUse>();
		if ( hotbarConsume.IsValid()
		     && hotbarConsume.TryGetActiveConsumablePrompt( out _ ) )
		{
			return ThornsHudPromptState.HoldAction(
				"Consume",
				keyHint: "RMB",
				holdFraction: hotbarConsume.ConsumeHoldFraction );
		}

		if ( !TryResolveAimRay( root, out var origin, out var forward ) )
			return default;

		if ( ThornsAxeTools.PlayerHasAxeEquipped( root )
		     && ThornsTreeHitUtil.TryPickTreeAlongRay( root.Scene, origin, forward, ThornsGatheringRange.Inches, root, out _ ) )
			return ThornsHudPromptState.AttackAction( "Chop Tree" );

		if ( ThornsPickaxeTools.PlayerHasPickaxeEquipped( root )
		     && ThornsMineralHitUtil.TryPickNodeAlongRay( root.Scene, origin, forward, ThornsGatheringRange.Inches, root, out var nodeId )
		     && ThornsMineralWorldService.ResolveInstance()?.TryGetLiveNodeKind( nodeId, out var kind ) == true )
		{
			var verb = kind == MineralKind.Ore ? "Mine Ore" : "Mine Stone";
			return ThornsHudPromptState.AttackAction( verb );
		}

		return default;
	}

	static bool TryResolveAimRay( GameObject root, out Vector3 origin, out Vector3 forward ) =>
		ThornsInteractAimPick.TryResolveCrosshairAimRay( root, out origin, out forward );
}
