namespace Terraingen.UI;

using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI.Core;

/// <summary>Authoritative snapshot mirror for local player UI. Updated only via RPC/snapshot apply.</summary>
public static class ThornsUiClientState
{
	public static ThornsPlayerSnapshotBundle Snapshot { get; private set; } = new();
	public static bool HasSnapshot { get; private set; }

	/// <summary>Clears owner snapshot gate when a new gameplay menu host boots (editor re-entry).</summary>
	public static void ResetForGameplaySession()
	{
		Snapshot = new ThornsPlayerSnapshotBundle();
		HasSnapshot = false;
	}

	static void EnsureSnapshotCoherent()
	{
		Snapshot ??= new ThornsPlayerSnapshotBundle();
		Snapshot.Inventory ??= new();
		Snapshot.Inventory.Slots ??= new();
		Snapshot.Craft ??= new();
		Snapshot.Journal ??= new();
		Snapshot.Journal.Goals ??= new();
		Snapshot.Skills ??= new();
		Snapshot.Tames ??= new();
		Snapshot.Tames.Tames ??= new();
		Snapshot.Guild ??= new();
		Snapshot.Map ??= new();
		Snapshot.Map.Markers ??= new();
		Snapshot.ExternalContainer ??= new();
		Snapshot.ExternalContainer.Slots ??= new();
		Snapshot.RadioShop ??= new();
		Snapshot.Research ??= new();
		Snapshot.Campfire ??= new();
		Snapshot.Workbench ??= new();
		Snapshot.Victory ??= new();
		Snapshot.Contracts ??= new();
		Snapshot.Vitals ??= new();
	}

	public static void EnsureSnapshotCoherentForRefresh()
	{
		if ( !HasSnapshot )
			return;

		EnsureSnapshotCoherent();
	}

	public static void ApplySnapshot( ThornsPlayerSnapshotBundle bundle )
	{
		var previousTames = HasSnapshot ? Snapshot?.Tames : null;
		Snapshot = bundle ?? new ThornsPlayerSnapshotBundle();
		EnsureSnapshotCoherent();
		if ( previousTames is not null )
			PreserveTameUiState( previousTames, Snapshot.Tames );
		if ( Snapshot.Vitals is not null )
			Snapshot.Vitals = CloneVitals( Snapshot.Vitals );
		else
			Snapshot.Vitals = new();
		HasSnapshot = true;
		ThornsGameplayUiDiagnostics.Event( "UiClientState snapshot applied (inventory/vitals/hotbar data available)." );
		UiRevisionBus.Publish( UiRevisionChannel.Inventory );
		UiRevisionBus.Publish( UiRevisionChannel.Craft );
		UiRevisionBus.Publish( UiRevisionChannel.Journal );
		UiRevisionBus.Publish( UiRevisionChannel.Skills );
		UiRevisionBus.Publish( UiRevisionChannel.Tames );
		UiRevisionBus.Publish( UiRevisionChannel.Guild );
		UiRevisionBus.Publish( UiRevisionChannel.Map );
		UiRevisionBus.Publish( UiRevisionChannel.Vitals );
		UiRevisionBus.Publish( UiRevisionChannel.Hotbar );
		UiRevisionBus.Publish( UiRevisionChannel.Victory );
		UiRevisionBus.Publish( UiRevisionChannel.Research );
		UiRevisionBus.Publish( UiRevisionChannel.Campfire );
		UiRevisionBus.Publish( UiRevisionChannel.Workbench );
	}

	public static void ApplyPartialVictory( ThornsVictorySnapshot victory )
	{
		if ( victory is not null )
			Snapshot.Victory = victory;
		UiRevisionBus.Publish( UiRevisionChannel.Victory );
		UiRevisionBus.Publish( UiRevisionChannel.Guild );
	}

	public static void ApplyPartialInventory( ThornsInventorySnapshotDto inv, ThornsCraftSnapshotDto craft )
	{
		if ( inv is not null )
			Snapshot.Inventory = inv;
		if ( craft is not null )
			Snapshot.Craft = craft;
		EnsureSnapshotCoherent();
		UiRevisionBus.Publish( UiRevisionChannel.Inventory );
		UiRevisionBus.Publish( UiRevisionChannel.Craft );
		UiRevisionBus.Publish( UiRevisionChannel.Hotbar );
		UiRevisionBus.Publish( UiRevisionChannel.BuildMenu );
	}

	/// <summary>Lightweight hotbar ammo/durability refresh without serializing the full inventory.</summary>
	public static void PatchHotbarSlotWeaponState(
		int hotbarIndex,
		int loadedAmmo,
		float durability,
		bool hasDurability,
		bool weaponBroken,
		int weaponClipSize = -1 )
	{
		if ( !HasSnapshot || Snapshot?.Inventory?.Slots is null )
			return;

		EnsureSnapshotCoherent();
		hotbarIndex = Math.Clamp( hotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );

		for ( var i = 0; i < Snapshot.Inventory.Slots.Count; i++ )
		{
			var slot = Snapshot.Inventory.Slots[i];
			if ( slot.Container != ThornsContainerKind.Hotbar || slot.Index != hotbarIndex )
				continue;

			slot.WeaponLoadedAmmo = loadedAmmo;
			slot.HasDurability = hasDurability;
			slot.Durability = durability;
			slot.WeaponBroken = weaponBroken;
			if ( weaponClipSize >= 0 )
				slot.WeaponClipSize = weaponClipSize;

			UiRevisionBus.Publish( UiRevisionChannel.Hotbar );
			return;
		}
	}

	public static void ApplyPartialJournal( ThornsJournalSnapshotDto journal )
	{
		if ( journal is not null )
		{
			ThornsDefinitionRegistry.EnsureInitialized();
			ThornsJourneyProgression.ClientMergeJournalSnapshot( journal );
			Snapshot.Journal = journal;
		}

		EnsureSnapshotCoherent();
		UiRevisionBus.Publish( UiRevisionChannel.Journal );
	}

	public static void ApplyPartialSkills( ThornsSkillsSnapshotDto skills )
	{
		if ( skills is not null )
			Snapshot.Skills = skills;
		EnsureSnapshotCoherent();
		UiRevisionBus.Publish( UiRevisionChannel.Skills );
	}

	public static void ApplyPartialTames( ThornsTamesSnapshotDto tames )
	{
		if ( tames is not null )
		{
			PreserveTameUiState( Snapshot.Tames, tames );
			Snapshot.Tames = tames;
		}
		UiRevisionBus.Publish( UiRevisionChannel.Tames );
	}

	static void PreserveTameUiState( ThornsTamesSnapshotDto previous, ThornsTamesSnapshotDto next )
	{
		if ( previous is null || next is null )
			return;

		if ( previous.SelectedEntityId != Guid.Empty
		     && next.Tames.Any( t => t.EntityId == previous.SelectedEntityId ) )
			next.SelectedEntityId = previous.SelectedEntityId;

		if ( !previous.BreedPanelOpen )
			return;

		next.BreedPanelOpen = true;
		if ( next.Tames.Any( t => t.EntityId == previous.BreedParentAId ) )
			next.BreedParentAId = previous.BreedParentAId;
		if ( next.Tames.Any( t => t.EntityId == previous.BreedParentBId ) )
			next.BreedParentBId = previous.BreedParentBId;

		if ( next.BreedParentAId == next.BreedParentBId )
			next.BreedParentBId = Guid.Empty;
	}

	public static void ApplyPartialGuild( ThornsGuildSnapshotDto guild )
	{
		if ( guild is not null )
			Snapshot.Guild = guild;
		UiRevisionBus.Publish( UiRevisionChannel.Guild );
	}

	public static void ApplyPartialMap( ThornsMapSnapshotDto map )
	{
		if ( map is not null )
			Snapshot.Map = map;
		EnsureSnapshotCoherent();
		UiRevisionBus.Publish( UiRevisionChannel.Map );
	}

	public static void ApplyPartialVitals( ThornsVitalsSnapshotDto vitals )
	{
		if ( vitals is null )
			return;

		var next = CloneVitals( vitals );
		if ( ThornsPlayerVitalsNetwork.VitalsEqual( Snapshot.Vitals, next ) )
			return;

		Snapshot.Vitals = next;
		EnsureSnapshotCoherent();
		UiRevisionBus.Publish( UiRevisionChannel.Vitals );
	}

	static ThornsVitalsSnapshotDto CloneVitals( ThornsVitalsSnapshotDto vitals ) => new()
	{
		Health = vitals.Health,
		MaxHealth = vitals.MaxHealth,
		Stamina = vitals.Stamina,
		MaxStamina = vitals.MaxStamina,
		Food = vitals.Food,
		MaxFood = vitals.MaxFood,
		Water = vitals.Water,
		MaxWater = vitals.MaxWater,
		TemperatureC = vitals.TemperatureC,
		ShowHealth = vitals.ShowHealth,
		ShowStamina = vitals.ShowStamina,
		ShowFood = vitals.ShowFood,
		ShowWater = vitals.ShowWater,
		ShowTemperature = vitals.ShowTemperature,
		HasCampfireWarmth = vitals.HasCampfireWarmth
	};

	public static void ApplyPartialExternalContainer( ThornsExternalContainerSnapshotDto container )
	{
		Snapshot.ExternalContainer = container ?? new ThornsExternalContainerSnapshotDto();
		UiRevisionBus.Publish( UiRevisionChannel.WorldContainer );
	}

	public static void ApplyPartialRadioShop( ThornsRadioShopSnapshotDto shop )
	{
		Snapshot.RadioShop = shop ?? new ThornsRadioShopSnapshotDto();
		UiRevisionBus.Publish( UiRevisionChannel.RadioShop );
	}

	public static void ApplyPartialResearch( ThornsResearchSnapshotDto research )
	{
		Snapshot.Research = research ?? new ThornsResearchSnapshotDto();
		UiRevisionBus.Publish( UiRevisionChannel.Research );
	}

	public static void ApplyPartialCampfire( ThornsCampfireSnapshotDto campfire )
	{
		Snapshot.Campfire = campfire ?? new ThornsCampfireSnapshotDto();
		UiRevisionBus.Publish( UiRevisionChannel.Campfire );
	}

	public static void ApplyPartialWorkbench( ThornsWorkbenchSnapshotDto workbench )
	{
		Snapshot.Workbench = workbench ?? new ThornsWorkbenchSnapshotDto();
		UiRevisionBus.Publish( UiRevisionChannel.Workbench );
	}
}
