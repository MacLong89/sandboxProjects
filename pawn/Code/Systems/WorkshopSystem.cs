namespace PawnShop;

/// <summary>Cleaning, repairing, researching, and scrapping owned items.</summary>
public sealed class WorkshopSystem
{
	private readonly SaveData _save;
	private readonly EconomySystem _economy;

	public WorkshopSystem( SaveData save, EconomySystem economy )
	{
		_save = save;
		_economy = economy;
	}

	public bool RepairsUnlocked => _save.OwnsUpgrade( UpgradeId.RepairBench );
	public bool AdvancedRepairs => _save.OwnsUpgrade( UpgradeId.AdvancedRepair );

	// ------------------------------------------------------------- Cleaning
	public const int CleanCost = 10;

	public bool CanClean( ItemInstance item ) =>
		item is not null && !item.Cleaned && item.Dirtiness > 0.05f
		&& item.Location is ItemLocation.Backroom or ItemLocation.OnDisplay;

	public bool Clean( ItemInstance item )
	{
		if ( !CanClean( item ) || !_economy.CanAfford( CleanCost ) ) return false;

		_economy.Spend( CleanCost );
		_economy.Ledger.RepairCosts += CleanCost;
		item.Dirtiness = 0f;
		item.Cleaned = true;

		// Cleaning sometimes reveals a visible defect that grime was hiding.
		var hidden = item.Defects.FirstOrDefault( d =>
			!item.DiscoveredDefects.Contains( d ) && DefectCatalog.Get( d )?.RevealTool == InspectTool.Eyes );
		if ( hidden is not null )
			item.DiscoverDefect( hidden );

		Sfx.Play( Sfx.Cleaning, 0.7f );
		return true;
	}

	// ------------------------------------------------------------- Repair
	/// <summary>Cost to repair scales with item value and how broken it is.</summary>
	public int RepairCost( ItemInstance item, GameManager game )
	{
		var value = ItemValue.KnownValue( item, game );
		var conditionGap = Math.Max( 0, (int)Condition.Good - (int)item.Condition );
		var defectCount = item.DiscoveredDefects.Count( d => DefectCatalog.Get( d )?.Repairable == true );
		return Math.Max( 15, (int)(value * 0.12f) + conditionGap * 20 + defectCount * 15 );
	}

	/// <summary>Whether this item needs the advanced bench.</summary>
	public bool NeedsAdvanced( ItemInstance item )
	{
		var def = item.Def;
		if ( def is null ) return false;
		if ( item.Condition == Condition.Broken ) return true;
		return def.Category is ItemCategory.Antiques or ItemCategory.Art or ItemCategory.Jewelry or ItemCategory.Instruments
			&& item.Defects.Any( d => DefectCatalog.Get( d )?.Repairable == true );
	}

	public bool CanRepair( ItemInstance item, GameManager game, out string reason )
	{
		reason = null;
		if ( item is null ) { reason = "No item."; return false; }
		if ( !RepairsUnlocked ) { reason = "Buy the Repair Bench upgrade first."; return false; }
		if ( item.Location is not (ItemLocation.Backroom or ItemLocation.OnDisplay) ) { reason = "Item isn't in your stock."; return false; }

		var hasRepairableDefect = item.DiscoveredDefects.Any( d => DefectCatalog.Get( d )?.Repairable == true );
		var badCondition = item.Condition < Condition.Good;
		if ( !hasRepairableDefect && !badCondition ) { reason = "Nothing to repair."; return false; }

		if ( NeedsAdvanced( item ) && !AdvancedRepairs ) { reason = "Needs Specialist Tooling."; return false; }
		if ( !_economy.CanAfford( RepairCost( item, game ) ) ) { reason = "Not enough cash."; return false; }
		return true;
	}

	/// <summary>
	/// Attempt a repair. Success improves condition and clears repairable discovered
	/// defects; failure on tricky jobs can worsen condition.
	/// </summary>
	public (bool Success, string Message) Repair( ItemInstance item, GameManager game )
	{
		if ( !CanRepair( item, game, out var reason ) )
			return (false, reason);

		var cost = RepairCost( item, game );
		_economy.Spend( cost );
		_economy.Ledger.RepairCosts += cost;
		item.RepairInvested += cost;
		item.RepairsDone++;

		var successChance = AdvancedRepairs ? 0.92f : 0.8f;
		if ( item.Condition == Condition.Broken ) successChance -= 0.15f;

		if ( Game.Random.Float() > successChance )
		{
			// Botched: risk further damage.
			if ( item.Condition > Condition.Broken && Game.Random.Float() < 0.5f )
			{
				item.Condition--;
				Sfx.Play( Sfx.UiError );
				return (false, "The repair went badly — it's in worse shape now.");
			}
			Sfx.Play( Sfx.UiError );
			return (false, "The repair didn't take. Cost you parts and time.");
		}

		// Success: remove repairable discovered defects, bump condition.
		var fixedDefects = item.DiscoveredDefects.Where( d => DefectCatalog.Get( d )?.Repairable == true ).ToList();
		foreach ( var d in fixedDefects )
		{
			item.Defects.Remove( d );
			item.DiscoveredDefects.Remove( d );
		}

		if ( item.Condition < Condition.Good )
			item.Condition++;
		else if ( item.Condition < Condition.Excellent && Game.Random.Float() < 0.4f )
			item.Condition++;

		Sfx.Play( Sfx.Repairing, 0.7f );
		return (true, fixedDefects.Count > 0 ? $"Fixed {fixedDefects.Count} issue(s) and improved condition." : "Condition improved.");
	}

	// ------------------------------------------------------------- Research
	public int ResearchCost( ItemInstance item, GameManager game )
	{
		var value = ItemValue.KnownValue( item, game );
		var cost = Math.Max( 20, (int)(value * 0.15f) );
		if ( _save.OwnsUpgrade( UpgradeId.ReferenceComputer ) ) cost /= 2;
		return cost;
	}

	public bool CanResearch( ItemInstance item ) =>
		item is not null && !item.Researched
		&& item.Location is ItemLocation.Backroom or ItemLocation.OnDisplay or ItemLocation.PawnStorage;

	/// <summary>
	/// Research reveals authenticity, rarity, legal status, and all database-tier defects.
	/// Rare items can jump dramatically in value when their rarity is confirmed.
	/// </summary>
	public (bool Success, string Message) Research( ItemInstance item, GameManager game )
	{
		if ( !CanResearch( item ) ) return (false, "Can't research that.");

		var cost = ResearchCost( item, game );
		if ( !_economy.CanAfford( cost ) ) return (false, "Not enough cash.");

		_economy.Spend( cost );
		_economy.Ledger.RepairCosts += cost;

		item.Researched = true;
		item.AuthenticityKnown = true;
		item.RarityKnown = true;
		item.LegalStatusKnown = true;

		foreach ( var d in item.Defects.Where( d => DefectCatalog.Get( d )?.RevealTool == InspectTool.Database ).ToList() )
			item.DiscoverDefect( d );

		Sfx.Play( Sfx.ResearchDone, 0.7f );

		var message = item.TrueAuthenticity switch
		{
			Authenticity.Counterfeit => "Bad news: it's a counterfeit.",
			Authenticity.Replica => "It's a licensed replica — worth a fraction of the original.",
			Authenticity.Altered => "It's been altered from factory spec.",
			_ when item.Rarity >= Rarity.Rare => $"Confirmed {item.Rarity.Label()}! This is a real find.",
			_ => "Verified genuine. Standard production run.",
		};
		if ( item.LegalStatus == LegalStatus.Stolen )
			message += " WARNING: it's flagged on the stolen goods register.";

		if ( item.Rarity >= Rarity.VeryRare )
			Sfx.Play( Sfx.BigFind, 0.8f );

		return (true, message);
	}

	// ------------------------------------------------------------- Scrap
	public int ScrapValue( ItemInstance item, GameManager game ) =>
		Math.Max( 5, ItemValue.TrueValue( item, game ) / 6 );

	public bool Scrap( ItemInstance item, GameManager game )
	{
		if ( item is null || item.Location is not (ItemLocation.Backroom or ItemLocation.OnDisplay) )
			return false;

		_economy.Earn( ScrapValue( item, game ) );
		item.Location = ItemLocation.Scrapped;
		item.DisplaySlot = -1;
		GameManager.Instance?.Shop?.RefreshDisplays();
		Sfx.Play( Sfx.Scrap, 0.6f );
		return true;
	}
}
