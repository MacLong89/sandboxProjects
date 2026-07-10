using System.Text.Json;
using Dynasty.Core;
using Dynasty.Domain.League;

namespace Dynasty.Persistence;

public sealed class LeaderboardEntry
{
	public string SaveSlot { get; set; } = "";
	public string LeagueName { get; set; } = "";
	public string TeamAbbreviation { get; set; } = "";
	public string TeamDisplayName { get; set; } = "";
	public int DynastyScore { get; set; }
	public int Season { get; set; }
	public int OwnerJobSecurity { get; set; }
	public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Local dynasty score leaderboard — scaffolding for async / competitive comparison.
/// </summary>
public static class DynastyLeaderboardService
{
	static string LeaderboardPath => $"{GameSaveStorage.SaveRoot}/leaderboard.json";

	public static IReadOnlyList<LeaderboardEntry> GetTopEntries( int limit = 25 )
	{
		var entries = LoadAll();
		return entries
			.OrderByDescending( e => e.DynastyScore )
			.ThenByDescending( e => e.UpdatedUtc )
			.Take( limit )
			.ToList();
	}

	public static void SubmitFromLeague( LeagueState state, string saveSlot )
	{
		if ( state == null || string.IsNullOrWhiteSpace( saveSlot ) )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		var progress = state.FranchiseProgress;
		var entry = new LeaderboardEntry
		{
			SaveSlot = saveSlot,
			LeagueName = state.Settings.LeagueName,
			TeamAbbreviation = team.Identity.Abbreviation,
			TeamDisplayName = $"{team.Identity.City} {team.Identity.Name}",
			DynastyScore = progress?.DynastyScore ?? 0,
			Season = state.CurrentSeason,
			OwnerJobSecurity = progress?.OwnerJobSecurity ?? 0,
			UpdatedUtc = DateTime.UtcNow
		};

		var all = LoadAll();
		all.RemoveAll( e => e.SaveSlot.Equals( saveSlot, StringComparison.OrdinalIgnoreCase ) );
		all.Add( entry );
		SaveAll( all );
	}

	static List<LeaderboardEntry> LoadAll()
	{
		try
		{
			var json = GameSaveStorage.ReadText( LeaderboardPath );
			if ( string.IsNullOrEmpty( json ) )
				return new List<LeaderboardEntry>();

			return JsonSerializer.Deserialize<List<LeaderboardEntry>>( json ) ?? new List<LeaderboardEntry>();
		}
		catch
		{
			return new List<LeaderboardEntry>();
		}
	}

	static void SaveAll( List<LeaderboardEntry> entries )
	{
		var json = JsonSerializer.Serialize( entries, new JsonSerializerOptions { WriteIndented = true } );
		GameSaveStorage.WriteText( LeaderboardPath, json );
	}
}
