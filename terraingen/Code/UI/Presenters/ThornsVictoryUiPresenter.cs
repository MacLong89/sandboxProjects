namespace Terraingen.UI.Presenters;

using Terraingen.GameData;
using Terraingen.Victory;

/// <summary>Maps victory snapshots to display strings without hardcoding path ids in screens.</summary>
public static class ThornsVictoryUiPresenter
{
	public static string FormatPercent( float percent ) => $"{percent:0.#}%";

	public static string FormatRank( int rank ) => rank <= 0 ? "—" : $"#{rank}";

	public static string FormatScore( long score ) => $"{score:N0}";

	public static string FormatProgress( long current, long total ) => $"{current:N0} / {total:N0}";

	public static string FormatLeaderLine( ThornsVictoryPathCardDto card )
	{
		if ( card is null || string.IsNullOrWhiteSpace( card.CurrentLeaderName ) || card.CurrentLeaderName == "—" )
			return "No leader yet";
		return $"Led by {card.CurrentLeaderName}";
	}

	public static string FormatMilestoneLine( ThornsVictoryPathCardDto card )
	{
		if ( card is null )
			return "";
		if ( string.IsNullOrWhiteSpace( card.NextMilestoneTitle ) )
			return "All milestones reached";
		return $"Next: {card.NextMilestoneTitle} ({card.PlayerProgress:N0}/{card.NextMilestoneThreshold:N0})";
	}

	public static float GuildPercentComplete( ThornsVictoryPathCardDto card )
	{
		if ( card is null || card.TargetProgress <= 0 )
			return 0f;
		return Math.Clamp( (float)card.GuildProgress / card.TargetProgress * 100f, 0f, 100f );
	}

	public static string FormatGuildMilestoneLine( ThornsVictoryPathCardDto card )
	{
		if ( card is null || !ThornsVictoryPathCatalog.TryGet( card.PathId, out var def ) )
			return "";

		var next = def.Milestones.FirstOrDefault( m => card.GuildProgress < m.Threshold );
		if ( next is null )
			return "All milestones reached";

		return $"Next Milestone: {next.Title}";
	}

	public static string FormatGuildMilestoneReward( ThornsVictoryPathCardDto card )
	{
		if ( card is null || !ThornsVictoryPathCatalog.TryGet( card.PathId, out var def ) )
			return "";

		var next = def.Milestones.FirstOrDefault( m => card.GuildProgress < m.Threshold );
		return next?.RewardPreview ?? "Server prestige";
	}

	public static string PathCssClass( string pathId )
	{
		if ( string.IsNullOrWhiteSpace( pathId ) )
			return "path-unknown";

		return pathId.ToLowerInvariant() switch
		{
			ThornsVictoryPathIds.Dominion => "path-dominion",
			ThornsVictoryPathIds.Ascension => "path-ascension",
			ThornsVictoryPathIds.Purification => "path-purification",
			ThornsVictoryPathIds.Apex => "path-apex",
			_ => "path-unknown"
		};
	}

	public static Color PathAccentColor( string pathId )
	{
		return pathId?.ToLowerInvariant() switch
		{
			ThornsVictoryPathIds.Dominion => new Color( 0.82f, 0.28f, 0.24f ),
			ThornsVictoryPathIds.Ascension => new Color( 0.28f, 0.52f, 0.92f ),
			ThornsVictoryPathIds.Purification => new Color( 0.35f, 0.78f, 0.42f ),
			ThornsVictoryPathIds.Apex => new Color( 0.92f, 0.72f, 0.22f ),
			_ => new Color( 0.79f, 0.64f, 0.36f )
		};
	}

	public static float PathPercentForGuild( ThornsVictoryGuildComparisonRowDto row, string pathId )
	{
		if ( row?.PathRows is null )
			return 0f;

		var match = row.PathRows.FirstOrDefault( p => string.Equals( p.PathId, pathId, StringComparison.OrdinalIgnoreCase ) );
		return match?.PercentComplete ?? 0f;
	}

	public static string FormatLeadershipChange( ThornsVictoryLeadershipChangeDto change )
	{
		if ( change is null )
			return "";
		var scope = change.Scope == ThornsVictoryScope.Guild ? "Guild" : "Player";
		return $"{change.PathDisplayName} ({scope}): {change.PreviousLeaderName} → {change.NewLeaderName}";
	}

	public static ThornsVictoryPathCardDto FindCard( ThornsVictorySnapshot snap, string pathId )
	{
		if ( snap?.PathCards is null )
			return null;
		return snap.PathCards.FirstOrDefault( c => string.Equals( c.PathId, pathId, StringComparison.OrdinalIgnoreCase ) );
	}

	public static ThornsVictoryGuildPathRowDto FindGuildPathRow( ThornsVictorySnapshot snap, string pathId )
	{
		return snap?.GuildSummary?.PathRows?.FirstOrDefault( p =>
			string.Equals( p.PathId, pathId, StringComparison.OrdinalIgnoreCase ) );
	}
}
