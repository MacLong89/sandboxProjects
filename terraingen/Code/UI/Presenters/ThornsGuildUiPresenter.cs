namespace Terraingen.UI.Presenters;

using Terraingen.GameData;
using Terraingen.Victory;

/// <summary>Display-only formatting for guild command center UI.</summary>
public static class ThornsGuildUiPresenter
{
	public static string FormatStat( long value ) => $"{value:N0}";

	public static string FormatTerritories( int count )
		=> count <= 0 ? "—" : $"{count} Settlement{(count == 1 ? "" : "s")}";

	public static string FormatMemberCount( int count, int max = 60 )
		=> $"{count} / {max}";

	public static string FormatMemberLine( int count, int max = 60 )
		=> $"{FormatMemberCount( count, max )} Members";

	public static string FormatFocusPath( string pathId, float percent )
	{
		if ( string.IsNullOrWhiteSpace( pathId ) )
			return "—";

		if ( !ThornsVictoryPathCatalog.TryGet( pathId, out var def ) )
			return pathId;

		return percent > 0f
			? $"{def.DisplayName} · {percent:0.#}%"
			: def.DisplayName;
	}

	public static string FormatRelativeTime( string timestampUtc )
	{
		if ( string.IsNullOrWhiteSpace( timestampUtc ) )
			return "";

		if ( !DateTime.TryParse( timestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc ) )
			return timestampUtc;

		if ( utc.Kind == DateTimeKind.Unspecified )
			utc = DateTime.SpecifyKind( utc, DateTimeKind.Utc );

		var delta = DateTime.UtcNow - utc.ToUniversalTime();
		if ( delta.TotalMinutes < 1 )
			return "Just now";
		if ( delta.TotalHours < 1 )
			return $"{(int)delta.TotalMinutes}m ago";
		if ( delta.TotalDays < 1 )
			return $"{(int)delta.TotalHours}h ago";
		if ( delta.TotalDays < 2 )
			return "Yesterday";
		return $"{(int)delta.TotalDays}d ago";
	}

	public static string FormatNoticeAuthorLine( ThornsGuildNoticeDto notice )
	{
		if ( notice is null || string.IsNullOrWhiteSpace( notice.Message ) )
			return "";

		var author = string.IsNullOrWhiteSpace( notice.AuthorName ) ? "Leader" : notice.AuthorName;
		var when = FormatRelativeTime( notice.TimestampUtc );
		return string.IsNullOrWhiteSpace( when ) ? $"Posted by {author}" : $"Posted by {author} · {when}";
	}

	public static string FormatPathProgressDetail( ThornsGuildVictoryPathEntryDto path )
	{
		if ( path is null )
			return "—";

		if ( path.TargetProgress > 0 )
			return $"{path.GuildProgress:N0} / {path.TargetProgress:N0}";

		return FormatPercent( path.PercentComplete );
	}

	public static string FormatPercent( float percent ) => $"{percent:0.#}%";

	public static string FormatGuildXp( float current, float toNext )
	{
		if ( toNext <= 0f )
			return $"{current:N0} XP";

		return $"{current:N0} / {toNext:N0} XP";
	}

	public static string FormatAllianceOrRival( ThornsGuildSnapshotDto snap )
	{
		var rival = snap.RivalNpcGuilds?.FirstOrDefault( r => r is not null && r.HasRival && !r.IsEliminated );
		if ( rival is not null && !string.IsNullOrWhiteSpace( rival.GuildName ) )
			return rival.GuildName;

		return "—";
	}

	public static string DefaultMotto( bool isNpcGuild )
		=> isNpcGuild ? "Hold the line. Claim the wasteland." : "We survive. We gather. We endure.";

	public static IEnumerable<string> SplitRewardPreview( string rewardPreview )
	{
		if ( string.IsNullOrWhiteSpace( rewardPreview ) )
			yield break;

		if ( rewardPreview.Contains( '&', StringComparison.Ordinal ) )
		{
			foreach ( var part in rewardPreview.Split( '&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
				yield return part;
			yield break;
		}

		yield return rewardPreview;
	}
}
