using System;

namespace Sandbox;

/// <summary>Snapshot for one evaluator pass (owner client).</summary>
public readonly struct ThornsHotTipContext
{
	public GameObject PawnRoot { get; init; }
	public Scene Scene { get; init; }
	public ThornsGameShell Shell { get; init; }
	public ThornsVitals Vitals { get; init; }
	public ThornsInventory Inventory { get; init; }
	public ThornsHotbarEquipment Hotbar { get; init; }
	public ThornsHealth Health { get; init; }
	public ThornsBuildingController Building { get; init; }
	public ThornsPlayerMilestones Milestones { get; init; }
	public ThornsResourceNode LookResourceNode { get; init; }
	public ThornsLootCrate LookLootCrate { get; init; }
	public ThornsWildlifeIdentity LookTameWildlife { get; init; }
	public float LookResourceSeconds { get; init; }
	public float LookLootCrateSeconds { get; init; }
	public float LookTameWildlifeSeconds { get; init; }
	public float SessionSeconds { get; init; }
	public float Hunger01 { get; init; }
	public float Thirst01 { get; init; }
	public float Health01 { get; init; }
	public float InventoryFill01 { get; init; }
	public int WoodCount { get; init; }
	public int StoneCount { get; init; }
	public int MetalOreCount { get; init; }
	public int BandageCount { get; init; }
	public bool IsNight { get; init; }
	public bool HasStoneHatchet { get; init; }
	public bool HasStonePick { get; init; }
	public bool HasAnyGunHotbar { get; init; }
	public bool HasBuildingMaterials { get; init; }
	public bool FoundationPlaced { get; init; }
	public bool InBuildMode { get; init; }
	public bool SuppressCombatTips { get; init; }
	public bool NearAirdropCrate { get; init; }
	public bool NearMilitaryCrate { get; init; }
	public bool CarryingRareLoot { get; init; }
	public bool HasCampfirePlaced { get; init; }

	public bool NewPlayerWindow => SessionSeconds < 30f * 60f;

	public static ThornsHotTipContext Build(
		GameObject pawnRoot,
		float sessionSeconds,
		float lookWoodSec,
		float lookStoneSec,
		float lookOreSec,
		float lookLootSec,
		float lookTameSec,
		ThornsResourceNode lookNode,
		ThornsLootCrate lookCrate,
		ThornsWildlifeIdentity lookTame )
	{
		var scene = pawnRoot.Scene;
		var shell = pawnRoot.Components.Get<ThornsGameShell>();
		var vitals = pawnRoot.Components.Get<ThornsVitals>();
		var inv = pawnRoot.Components.Get<ThornsInventory>();
		var hb = pawnRoot.Components.Get<ThornsHotbarEquipment>();
		var hp = pawnRoot.Components.Get<ThornsHealth>();
		var build = pawnRoot.Components.Get<ThornsBuildingController>();
		var ms = pawnRoot.Components.Get<ThornsPlayerMilestones>();

		var hunger01 = 1f;
		var thirst01 = 1f;
		if ( vitals.IsValid() && vitals.MaxHunger > 0.01f )
			hunger01 = vitals.Hunger / vitals.MaxHunger;
		if ( vitals.IsValid() && vitals.MaxThirst > 0.01f )
			thirst01 = vitals.Thirst / vitals.MaxThirst;

		var health01 = 1f;
		if ( hp.IsValid() && hp.MaxHealth > 0.01f )
			health01 = hp.CurrentHealth / hp.MaxHealth;

		var wood = inv.IsValid() ? inv.ClientMirrorCountItemId( "wood" ) : 0;
		var stone = inv.IsValid() ? inv.ClientMirrorCountItemId( "stone" ) : 0;
		var ore = inv.IsValid() ? inv.ClientMirrorCountItemId( "metal_ore" ) : 0;
		var bandage = inv.IsValid() ? inv.ClientMirrorCountItemId( "bandage" ) : 0;

		var fill = 0f;
		if ( inv.IsValid() )
		{
			var used = 0;
			for ( var i = 0; i < ThornsInventory.TotalSlots; i++ )
			{
				if ( inv.TryGetClientMirrorSlot( i, out var s ) && ClientMirrorSlotOccupied( s ) )
					used++;
			}

			fill = used / (float)ThornsInventory.TotalSlots;
		}

		var hasHatchet = inv.IsValid() && inv.ClientMirrorCountItemId( "stone_hatchet" ) > 0;
		var hasPick = inv.IsValid() && inv.ClientMirrorCountItemId( "stone_pick" ) > 0;
		var hasGun = ClientMirrorHasGunOnHotbar( inv, hb );

		var foundationDone = ms.IsValid() && ms.ClientIsGoalComplete( FindGoalIndex( "goal_first_foundation" ) );

		var isNight = TryResolveNight( scene );
		var nearDrop = false;
		var nearMil = false;
		foreach ( var c in ThornsLootCrate.ActiveById.Values )
		{
			if ( !c.IsValid() )
				continue;

			var d = (c.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > 2200f )
				continue;

			if ( c.CrateKindSync == ThornsLootCrateKind.AirdropPremium )
				nearDrop = true;
			if ( c.CrateKindSync == ThornsLootCrateKind.MilitaryMixed )
				nearMil = true;
		}

		var rareLoot = inv.IsValid()
		               && ( inv.ClientMirrorCountItemId( "metal" ) > 0
		                    || inv.ClientMirrorCountItemId( "kevlar_chest" ) > 0
		                    || ClientMirrorHasGunOnHotbar( inv, hb ) );

		var lookKind = lookNode.IsValid() ? lookNode.ResourceKind : (ThornsResourceKind)(-1);
		var lookSec = lookKind switch
		{
			ThornsResourceKind.Wood => lookWoodSec,
			ThornsResourceKind.Stone => lookStoneSec,
			ThornsResourceKind.MetalOre => lookOreSec,
			_ => 0f
		};

		var suppressCombat = shell.IsValid() && shell.BlocksGameplayShellOverlay
		                   || ( build.IsValid() && build.BuildModeActive )
		                   || ( Input.Down( "attack1" ) && hasGun );

		return new ThornsHotTipContext
		{
			PawnRoot = pawnRoot,
			Scene = scene,
			Shell = shell,
			Vitals = vitals,
			Inventory = inv,
			Hotbar = hb,
			Health = hp,
			Building = build,
			Milestones = ms,
			LookResourceNode = lookNode,
			LookLootCrate = lookCrate,
			LookTameWildlife = lookTame,
			LookResourceSeconds = lookSec,
			LookLootCrateSeconds = lookLootSec,
			LookTameWildlifeSeconds = lookTameSec,
			SessionSeconds = sessionSeconds,
			Hunger01 = hunger01,
			Thirst01 = thirst01,
			Health01 = health01,
			InventoryFill01 = fill,
			WoodCount = wood,
			StoneCount = stone,
			MetalOreCount = ore,
			BandageCount = bandage,
			IsNight = isNight,
			HasStoneHatchet = hasHatchet,
			HasStonePick = hasPick,
			HasAnyGunHotbar = hasGun,
			HasBuildingMaterials = wood >= 12 && stone >= 8,
			FoundationPlaced = foundationDone,
			InBuildMode = build.IsValid() && build.BuildModeActive,
			SuppressCombatTips = suppressCombat,
			NearAirdropCrate = nearDrop,
			NearMilitaryCrate = nearMil,
			CarryingRareLoot = rareLoot,
			HasCampfirePlaced = ms.IsValid() && ms.ClientIsGoalComplete( FindGoalIndex( "goal_place_campfire" ) )
		};
	}

	static int FindGoalIndex( string id )
	{
		if ( !ThornsMilestoneDefinitions.TryGetById( id, out var idx, out _ ) )
			return -1;

		return idx;
	}

	static bool TryResolveNight( Scene scene )
	{
		return ThornsCelestialSystem.TryGetTimeOfDay( scene, out _, out var night ) && night;
	}

	public static bool ClientMirrorSlotOccupied( ThornsInventorySlotNet s ) =>
		s.Quantity > 0 && !string.IsNullOrWhiteSpace( s.ItemId );

	static bool ClientMirrorHasGunOnHotbar( ThornsInventory inv, ThornsHotbarEquipment hb )
	{
		if ( !inv.IsValid() || !hb.IsValid() )
			return false;

		for ( var i = 0; i < ThornsInventory.HotbarSlotCount; i++ )
		{
			if ( !inv.TryGetClientMirrorSlot( i, out var s ) || !ClientMirrorSlotOccupied( s ) )
				continue;

			if ( !ThornsItemRegistry.TryGet( s.ItemId, out var def ) )
				continue;

			if ( def.ItemType == ThornsItemType.Weapon )
				return true;
		}

		var cid = hb.ObserversCombatWeaponDefinitionId?.Trim() ?? "";
		return !string.IsNullOrEmpty( cid )
		       && !ThornsWeaponDefinitions.IsMeleeWeapon( ThornsWeaponDefinitions.Get( cid ) );
	}
}
