namespace FinalOutpost;

/// <summary>Rival colonies with command posts that expand outward each season.</summary>
public static class RivalCivManager
{
	public static bool IsSeedPlot( int x, int y ) =>
		GameCore.Instance?.Save?.RivalSeeds?.Contains( PlotGrid.Key( x, y ) ) == true;

	public static bool IsRivalOwned( SaveData save, int x, int y ) =>
		save?.RivalOwnedPlots?.Contains( PlotGrid.Key( x, y ) ) == true;

	public static void EnsureSeeded( SaveData save )
	{
		if ( save is null || GameCore.Instance?.IsCure != true ) return;
		if ( save.RivalSeeds is { Count: > 0 } ) return;

		save.RivalSeeds = new List<string>();
		save.RivalOwnedPlots = new List<string>();

		for ( var x = -PlotGrid.Radius; x <= PlotGrid.Radius; x++ )
		for ( var y = -PlotGrid.Radius; y <= PlotGrid.Radius; y++ )
		{
			if ( !PlotWorldRolls.IsRivalSeedCandidate( x, y ) ) continue;
			if ( !IsFarEnoughFromSeeds( save, x, y ) ) continue;
			if ( PlotGrid.Ring( x, y ) < 3 ) continue;

			var key = PlotGrid.Key( x, y );
			save.RivalSeeds.Add( key );
			save.RivalOwnedPlots.Add( key );
		}
	}

	public static void ExpandSeason( GameCore core )
	{
		if ( core?.Save is null || !core.IsCure ) return;

		core.Save.RivalOwnedPlots ??= new List<string>();
		var owned = new HashSet<string>( core.Save.RivalOwnedPlots );
		var added = 0;

		foreach ( var key in owned.ToList() )
		{
			if ( !PlotGrid.ParseKey( key, out var x, out var y ) ) continue;

			TryClaim( x + 1, y );
			TryClaim( x - 1, y );
			TryClaim( x, y + 1 );
			TryClaim( x, y - 1 );
		}

		core.Save.RivalOwnedPlots = owned.ToList();
		if ( added > 0 )
			core.ShowToast( $"Rival colonies expanded — {added} plot{(added == 1 ? "" : "s")} seized" );

		PlotManager.Instance?.RebuildVisuals();
		core.SaveManagerTouch();

		void TryClaim( int nx, int ny )
		{
			if ( !PlotGrid.InGrid( nx, ny ) || PlotGrid.IsHome( nx, ny ) ) return;
			var nKey = PlotGrid.Key( nx, ny );
			if ( owned.Contains( nKey ) ) return;
			if ( core.Save.OwnedPlots.Contains( nKey ) ) return;
			owned.Add( nKey );
			added++;
		}
	}

	public static double InvasionCostMult( SaveData save, int x, int y ) =>
		IsRivalOwned( save, x, y ) ? 2.0 : 1.0;

	private static bool IsFarEnoughFromSeeds( SaveData save, int x, int y )
	{
		if ( PlotGrid.Ring( x, y ) < 3 ) return false;

		foreach ( var key in save.RivalSeeds )
		{
			if ( !PlotGrid.ParseKey( key, out var sx, out var sy ) ) continue;
			if ( Math.Max( Math.Abs( x - sx ), Math.Abs( y - sy ) ) < 4 ) return false;
		}

		return true;
	}
}
