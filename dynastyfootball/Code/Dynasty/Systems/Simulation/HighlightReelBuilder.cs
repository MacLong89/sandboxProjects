using Dynasty.Core.Enums;
using Dynasty.Domain.Simulation;

namespace Dynasty.Systems.Simulation;

/// <summary>
/// Builds a short highlight reel from precomputed simulation events.
/// </summary>
public static class HighlightReelBuilder
{
	public static IReadOnlyList<HighlightClip> Build( IEnumerable<SimEventRecord> events, int maxClips = 5 )
	{
		if ( events == null )
			return Array.Empty<HighlightClip>();

		var scored = events
			.Select( ( e, i ) => ( Event: e, Score: ScoreEvent( e ), Index: i ) )
			.Where( x => x.Score > 0 )
			.OrderByDescending( x => x.Score )
			.ThenBy( x => x.Index )
			.Take( maxClips )
			.OrderBy( x => x.Index )
			.Select( x => HighlightClip.From( x.Event ) )
			.ToList();

		return scored;
	}

	static int ScoreEvent( SimEventRecord e )
	{
		var score = 0;
		if ( e.Type is SimEventType.Score )
			score += 100;
		if ( e.Type is SimEventType.FieldGoalAttempt && e.Description.Contains( "GOOD", StringComparison.OrdinalIgnoreCase ) )
			score += 60;
		if ( e.Type is SimEventType.Turnover )
			score += 70;
		if ( e.Description.Contains( "touchdown", StringComparison.OrdinalIgnoreCase ) )
			score += 40;
		if ( e.Description.Contains( "interception", StringComparison.OrdinalIgnoreCase )
			|| e.Description.Contains( "fumble", StringComparison.OrdinalIgnoreCase ) )
			score += 30;
		if ( e.Description.Contains( "sack", StringComparison.OrdinalIgnoreCase ) )
			score += 15;

		return score;
	}
}

public sealed class HighlightClip
{
	public string Quarter { get; init; } = "";
	public string Clock { get; init; } = "";
	public string Description { get; init; } = "";
	public string Score { get; init; } = "";

	public static HighlightClip From( SimEventRecord e )
	{
		var mins = e.ClockSeconds / 60;
		var secs = e.ClockSeconds % 60;
		return new HighlightClip
		{
			Quarter = $"Q{e.Quarter}",
			Clock = $"{mins}:{secs:D2}",
			Description = e.Description,
			Score = $"{e.AwayScore}-{e.HomeScore}"
		};
	}
}
