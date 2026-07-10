namespace Terraingen.Player;

using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Progression;

/// <summary>Progression and snapshot restore API used by world save (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public int HostGetTotalXp() => _totalXp;
	public int HostGetPlayerLevel() => _playerLevel;
	public int HostGetActiveHotbarIndex() => _activeHotbarIndex;
	public string HostGetCraftCategory() => _craftCategory;
	public string HostGetSelectedRecipeId() => _selectedRecipeId;
	public bool HostGetCraftPanelExpanded() => _craftPanelExpanded;

	public void HostSetProgressionMeta( int totalXp, int level, int hotbar, string craftCategory, string recipeId, bool craftExpanded )
	{
		_totalXp = Math.Max( 0, totalXp );
		_playerLevel = Math.Max( 1, level );
		_activeHotbarIndex = Math.Clamp( hotbar, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		_craftCategory = ThornsCraftCatalog.NormalizeCraftCategoryId( craftCategory );
		_selectedRecipeId = ThornsItemIdAliases.CanonicalizeRecipeId(
			string.IsNullOrWhiteSpace( recipeId ) ? "recipe_stone_pickaxe" : recipeId );
		_craftPanelExpanded = craftExpanded;
	}

	public void HostApplyJournalSnapshot( ThornsJournalSnapshotDto journal )
	{
		if ( journal is null )
			return;

		if ( journal.Goals is null || journal.Goals.Count == 0 )
		{
			Log.Warning( "[Thorns Persistence] Empty journal snapshot — rebuilding from progression state." );
			HostRebuildJournal();
			return;
		}

		_journal = journal;
		ThornsJourneyProgression.HostMigrateJournalSnapshot( _journal );
	}

	public void HostApplyCraftSnapshot( ThornsCraftSnapshotDto craft )
	{
		if ( craft is null )
			return;

		_craftQueue.ApplySnapshot( craft );
		_nearestStation = craft.NearestStation;
	}

	public void HostPersistPlayerState() => ThornsWorldPersistence.RequestSave();

	public void HostApplySkillsSnapshot( ThornsSkillsSnapshotDto skills )
	{
		if ( skills is null )
			return;

		skills.Ranks ??= new List<ThornsSkillRankDto>();
		_skills = skills;
		_playerLevel = Math.Max( 1, skills.PlayerLevel );
		_totalXp = Math.Max( 0, skills.TotalXp );
		HostRecalculateUpgradePoints();
	}

	public void HostApplyVitalsSnapshot( ThornsVitalsSnapshotDto vitals )
	{
		if ( vitals is null )
			return;

		_vitals = vitals;
	}

	public void HostApplyResearchSnapshot( ThornsResearchSnapshotDto research )
	{
		if ( research is null )
			return;

		_research = research;
		_research.IsOpen = false;
		_research.StationInstanceKey = "";
		_research.CompletedLevel = Math.Clamp( _research.CompletedLevel, 0, ThornsResearchCatalog.MaxLevel );
		_research.ActiveLevel = Math.Clamp( _research.ActiveLevel, 0, ThornsResearchCatalog.MaxLevel );
		if ( _research.ActiveLevel <= _research.CompletedLevel )
			_research.ActiveLevel = 0;
		_research.ActiveSecondsRemaining = Math.Max( 0f, _research.ActiveSecondsRemaining );
		_research.Levels ??= new List<ThornsResearchLevelDto>();
		_research.Levels.Clear();
		_research.PercentComplete = ResearchPercent( _research.CompletedLevel );
	}

	public void HostFinalizeProgressionRestore()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var derivedLevel = ThornsXpBalance.LevelFromTotalXp( _totalXp );
		if ( derivedLevel != _playerLevel )
		{
			Log.Warning(
				$"[Thorns Persistence] Player level {_playerLevel} drifted from total XP ({_totalXp}) — reconciling to {derivedLevel}." );
			_playerLevel = derivedLevel;
		}

		HostNormalizeWeaponRows();
		HostRecalculateUpgradePoints();
		HostSyncDiscoveriesFromInventory();
		MarkInventorySyncDirty();
	}
}
