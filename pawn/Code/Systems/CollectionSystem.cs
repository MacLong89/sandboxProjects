namespace PawnShop;

/// <summary>
/// The Collector's Ledger: every item definition you've ever flipped (bought AND sold)
/// gets a stamp. Completing a whole category pays a one-time bounty and reputation.
/// </summary>
public sealed class CollectionSystem
{
	private readonly SaveData _save;

	public CollectionSystem( SaveData save )
	{
		_save = save;
	}

	public bool IsFlipped( string defId ) => _save.FlippedDefs.Contains( defId );
	public bool CategoryRewarded( ItemCategory cat ) => _save.RewardedCategories.Contains( cat.ToString() );

	public int FlippedIn( ItemCategory cat ) =>
		ItemCatalog.All.Count( d => d.Category == cat && IsFlipped( d.Id ) );

	public int TotalIn( ItemCategory cat ) =>
		ItemCatalog.All.Count( d => d.Category == cat );

	public int FlippedTotal => _save.FlippedDefs.Count;
	public int CatalogTotal => ItemCatalog.All.Count;

	public static int CategoryReward( ItemCategory cat ) =>
		100 + ItemCatalog.All.Where( d => d.Category == cat ).Sum( d => d.BaseValue ) / 20;

	/// <summary>Record a completed flip (an item you owned was sold). Pays category bounties.</summary>
	public void RecordFlip( ItemInstance item, GameManager game )
	{
		var def = item?.Def;
		if ( def is null || _save.FlippedDefs.Contains( def.Id ) ) return;

		_save.FlippedDefs.Add( def.Id );

		var cat = def.Category;
		if ( !CategoryRewarded( cat ) && FlippedIn( cat ) >= TotalIn( cat ) )
		{
			_save.RewardedCategories.Add( cat.ToString() );
			var reward = CategoryReward( cat );
			game.Economy.Earn( reward );
			game.Economy.Ledger.GoalBonuses += reward;
			game.Reputation.Add( 3f );
			Sfx.Play( Sfx.BigFind, 0.8f );
			game.Toast( $"Ledger complete: {cat.Label()}! Collectors pay tribute — +{GameConstants.FormatCash( reward )} and reputation.", "collections_bookmark" );
		}

		UiState.Bump();
	}
}
