namespace Terraingen.Multiplayer;

using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.Progression;

/// <summary>Serializes player progression into world save blobs.</summary>
public static class ThornsPlayerProgressPersistence
{
	public static ThornsPersistentPlayerProgressDto Capture( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !gameplay.IsValid() )
			return new ThornsPersistentPlayerProgressDto();

		var bundle = gameplay.HostBuildSnapshotBundle();
		return new ThornsPersistentPlayerProgressDto
		{
			InventoryJson = Json.Serialize( bundle.Inventory ?? new ThornsInventorySnapshotDto() ),
			CraftJson = Json.Serialize( bundle.Craft ?? new ThornsCraftSnapshotDto() ),
			JournalJson = Json.Serialize( bundle.Journal ?? new ThornsJournalSnapshotDto() ),
			SkillsJson = Json.Serialize( bundle.Skills ?? new ThornsSkillsSnapshotDto() ),
			VitalsJson = Json.Serialize( bundle.Vitals ?? new ThornsVitalsSnapshotDto() ),
			ResearchJson = Json.Serialize( bundle.Research ?? new ThornsResearchSnapshotDto() ),
			TotalXp = bundle.Skills?.TotalXp ?? gameplay.HostGetTotalXp(),
			PlayerLevel = bundle.Skills?.PlayerLevel ?? gameplay.HostGetPlayerLevel(),
			ActiveHotbarIndex = gameplay.HostGetActiveHotbarIndex(),
			CraftCategory = gameplay.HostGetCraftCategory(),
			SelectedRecipeId = gameplay.HostGetSelectedRecipeId(),
			CraftPanelExpanded = gameplay.HostGetCraftPanelExpanded(),
			ContractsJson = Json.Serialize( gameplay.HostPeekSurvivorContracts() ?? new ThornsSurvivorContractsSnapshotDto() )
		};
	}

	public static bool TryRestoreFromWorld( ThornsPersistentWorldDto world, ThornsPlayerGameplay gameplay )
	{
		if ( world?.PlayerProgressByAccountKey is null || gameplay is null || string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			return false;

		if ( !world.PlayerProgressByAccountKey.TryGetValue( gameplay.AccountKey, out var blob ) || blob is null )
			return false;

		if ( !HasSavedProgress( blob ) )
			return false;

		Restore( gameplay, blob );
		return true;
	}

	public static bool HasSavedProgress( ThornsPersistentPlayerProgressDto blob )
	{
		if ( blob is null )
			return false;

		if ( !string.IsNullOrWhiteSpace( blob.JournalJson ) )
			return true;

		if ( !string.IsNullOrWhiteSpace( blob.InventoryJson ) )
			return true;

		if ( !string.IsNullOrWhiteSpace( blob.SkillsJson ) )
			return true;

		if ( !string.IsNullOrWhiteSpace( blob.VitalsJson ) )
			return true;

		if ( !string.IsNullOrWhiteSpace( blob.ResearchJson ) )
			return true;

		return blob.TotalXp > 0 || blob.PlayerLevel > 1;
	}

	public static void Restore( ThornsPlayerGameplay gameplay, ThornsPersistentPlayerProgressDto blob )
	{
		if ( gameplay is null || !gameplay.IsValid() || blob is null )
			return;

		gameplay.HostSetProgressionMeta(
			blob.TotalXp,
			blob.PlayerLevel,
			blob.ActiveHotbarIndex,
			blob.CraftCategory,
			blob.SelectedRecipeId,
			blob.CraftPanelExpanded );

		try
		{
			if ( !string.IsNullOrWhiteSpace( blob.InventoryJson ) )
			{
				var inv = Json.Deserialize<ThornsInventorySnapshotDto>( blob.InventoryJson );
				if ( inv is not null )
					gameplay.Inventory.ApplySnapshot( inv );
			}

			if ( !string.IsNullOrWhiteSpace( blob.JournalJson ) )
			{
				var journal = Json.Deserialize<ThornsJournalSnapshotDto>( blob.JournalJson );
				if ( journal is not null )
					gameplay.HostApplyJournalSnapshot( journal );
			}

			if ( !string.IsNullOrWhiteSpace( blob.CraftJson ) )
			{
				var craft = Json.Deserialize<ThornsCraftSnapshotDto>( blob.CraftJson );
				if ( craft is not null )
					gameplay.HostApplyCraftSnapshot( craft );
			}

			if ( !string.IsNullOrWhiteSpace( blob.SkillsJson ) )
			{
				var skills = Json.Deserialize<ThornsSkillsSnapshotDto>( blob.SkillsJson );
				if ( skills is not null )
					gameplay.HostApplySkillsSnapshot( skills );
			}

			if ( !string.IsNullOrWhiteSpace( blob.VitalsJson ) )
			{
				var vitals = Json.Deserialize<ThornsVitalsSnapshotDto>( blob.VitalsJson );
				if ( vitals is not null )
					gameplay.HostApplyVitalsSnapshot( vitals );
			}

			if ( !string.IsNullOrWhiteSpace( blob.ResearchJson ) )
			{
				var research = Json.Deserialize<ThornsResearchSnapshotDto>( blob.ResearchJson );
				if ( research is not null )
					gameplay.HostApplyResearchSnapshot( research );
			}

			if ( !string.IsNullOrWhiteSpace( blob.ContractsJson ) )
			{
				var contracts = Json.Deserialize<ThornsSurvivorContractsSnapshotDto>( blob.ContractsJson );
				if ( contracts is not null )
					gameplay.HostApplySurvivorContractsSnapshot( contracts );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Persistence] Player progression restore partial failure." );
		}

		if ( gameplay.Inventory.IsEmpty() )
		{
			Log.Warning( "[Thorns Persistence] Inventory empty after restore — applying starter loadout." );
			gameplay.Inventory.SeedStarterItems();
		}

		gameplay.HostFinalizeProgressionRestore();
	}
}
