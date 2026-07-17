namespace FinalOutpost;

/// <summary>Passive bonuses from rare plots the player has cleared and claimed.</summary>
public static class PlotBoosts
{
	public static bool IsClaimed( SaveData save, int x, int y ) =>
		save?.ClaimedPlotBoosts?.ContainsKey( PlotGrid.Key( x, y ) ) == true;

	public static void Claim( GameCore core, int x, int y )
	{
		if ( core?.Save is null || !core.IsCure ) return;

		var kind = PlotWorldRolls.BoostAt( x, y );
		if ( kind == PlotBoostKind.None ) return;

		core.Save.ClaimedPlotBoosts ??= new Dictionary<string, string>();
		var key = PlotGrid.Key( x, y );
		if ( core.Save.ClaimedPlotBoosts.ContainsKey( key ) ) return;

		core.Save.ClaimedPlotBoosts[key] = kind.ToString();
		var def = PlotWorldRolls.GetBoost( kind );
		core.ShowToast( $"{def.Name} secured — {def.Description}" );
	}

	public static IEnumerable<PlotBoostDef> Active( SaveData save )
	{
		if ( save?.ClaimedPlotBoosts is null ) yield break;
		foreach ( var id in save.ClaimedPlotBoosts.Values )
		{
			if ( Enum.TryParse<PlotBoostKind>( id, out var kind ) && kind != PlotBoostKind.None )
				yield return PlotWorldRolls.GetBoost( kind );
		}
	}

	public static float FoodPerSec( SaveData save ) =>
		Active( save ).Sum( b => b.FoodPerSec );

	public static float ScrapPerSec( SaveData save ) =>
		Active( save ).Sum( b => b.ScrapPerSec );

	public static float KnowledgePerSec( SaveData save ) =>
		Active( save ).Sum( b => b.KnowledgePerSec );

	public static float ForagerMult( SaveData save )
	{
		var bonus = Active( save ).Sum( b => b.ForagerMult );
		return bonus <= 0f ? 1f : 1f + bonus;
	}

	public static float SicknessHealPerSec( SaveData save ) =>
		Active( save ).Sum( b => b.SicknessHealPerSec );
}
