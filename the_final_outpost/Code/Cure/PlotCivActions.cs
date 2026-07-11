namespace FinalOutpost;

/// <summary>Trade, ally, and raid actions for neighboring colony plots.</summary>
public static class PlotCivActions
{
	public static bool IsNeutralCiv( int x, int y ) =>
		GameCore.Instance?.IsCure == true && PlotFeatureGrid.KindAt( x, y ) == PlotKind.NeutralCiv;

	public static bool IsAllied( SaveData save, int x, int y ) =>
		save?.AlliedCivPlots?.Contains( PlotGrid.Key( x, y ) ) == true;

	public static bool IsRaided( SaveData save, int x, int y ) =>
		save?.RaidedCivPlots?.Contains( PlotGrid.Key( x, y ) ) == true;

	public static bool CanInteract( SaveData save, int x, int y ) =>
		IsNeutralCiv( x, y ) && !IsRaided( save, x, y );

	public static bool TryTrade( GameCore core, int x, int y )
	{
		if ( core?.Save is null || !CanInteract( core.Save, x, y ) ) return false;
		if ( !core.Resources.TrySpend( ResourceKind.Food, 20 ) ) return false;

		var scrap = TechTreeCatalog.IsUnlocked( core.Save, "diplomacy" ) ? 90.0 : 60.0;
		core.Wallet.Earn( scrap );
		core.ShowToast( $"Trade complete — +{scrap:0} Scrap" );
		core.SaveManagerTouch();
		Sfx.Play( Sfx.Purchase );
		return true;
	}

	public static bool TryAlly( GameCore core, int x, int y )
	{
		if ( core?.Save is null || !CanInteract( core.Save, x, y ) ) return false;
		if ( IsAllied( core.Save, x, y ) ) return false;

		core.Save.AlliedCivPlots ??= new List<string>();
		var key = PlotGrid.Key( x, y );
		if ( !core.Save.AlliedCivPlots.Contains( key ) )
			core.Save.AlliedCivPlots.Add( key );

		core.Resources.Add( ResourceKind.Knowledge, 25 );
		core.Resources.Add( ResourceKind.Food, 40 );
		core.ShowToast( "Alliance formed — +25 Knowledge, +40 Food" );
		core.SaveManagerTouch();
		Sfx.Play( Sfx.Purchase );
		return true;
	}

	public static bool TryRaid( GameCore core, int x, int y )
	{
		if ( core?.Save is null || !CanInteract( core.Save, x, y ) ) return false;

		core.Save.RaidedCivPlots ??= new List<string>();
		var key = PlotGrid.Key( x, y );
		if ( !core.Save.RaidedCivPlots.Contains( key ) )
			core.Save.RaidedCivPlots.Add( key );

		core.Resources.Add( ResourceKind.Supplies, 50 );
		core.Wallet.Earn( 80 );
		core.Save.ColonySickness = MathF.Min( CureConstants.MaxSickness,
			core.Save.ColonySickness + 8f );
		core.ShowToast( "Raid successful — +50 Supplies, +80 Scrap. Sickness increased." );
		core.SaveManagerTouch();
		Sfx.Play( Sfx.Purchase );
		return true;
	}
}
