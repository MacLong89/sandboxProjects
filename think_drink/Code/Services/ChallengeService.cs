namespace ThinkDrink.Services;

public sealed class ChallengeService : IChallengeGenerator, IChallengeTracker
{
	public IReadOnlyList<ChallengeProgress> GenerateDaily( DateTime utcDate )
	{
		var seed = HashDate( utcDate.Date );
		var rng = new Random( seed );

		var templates = new (ChallengeMetric metric, int target, string desc, int xp)[]
		{
			( ChallengeMetric.AnswerQuestions, 10, "Answer 10 questions today", 80 ),
			( ChallengeMetric.WinMatches, 1, "Win 1 match today", 120 ),
			( ChallengeMetric.AnswerCategory, 5, $"Answer 5 {PickCategory( rng )} questions", 90 ),
			( ChallengeMetric.BuzzFirst, 3, "Buzz in first 3 times", 75 ),
		};

		var pick = templates[rng.Next( templates.Length )];
		var id = $"daily_{utcDate:yyyyMMdd}_{pick.metric}";

		return new List<ChallengeProgress>
		{
			new()
			{
				ChallengeId = id,
				Target = pick.target,
				Current = 0,
				Description = pick.desc,
				XpReward = pick.xp
			}
		};
	}

	public IReadOnlyList<ChallengeProgress> GenerateWeekly( DateTime utcDate )
	{
		var week = GetIsoWeek( utcDate );
		var seed = week * 7919;
		var rng = new Random( seed );

		var templates = new (ChallengeMetric metric, int target, string desc, int xp)[]
		{
			( ChallengeMetric.WinMatches, 10, "Win 10 matches this week", 400 ),
			( ChallengeMetric.CorrectAnswers, 100, "Get 100 correct answers", 350 ),
			( ChallengeMetric.MaintainAccuracy, 80, "Maintain 80% accuracy (min 5 games)", 300 ),
		};

		var pick = templates[rng.Next( templates.Length )];
		var id = $"weekly_{utcDate.Year}W{week}_{pick.metric}";

		return new List<ChallengeProgress>
		{
			new()
			{
				ChallengeId = id,
				Target = pick.target,
				Current = 0,
				Description = pick.desc,
				XpReward = pick.xp
			}
		};
	}

	public void EnsureChallenges( PlayerProfile profile, DateTime utcNow )
	{
		var dailyId = $"daily_{utcNow:yyyyMMdd}";
		var weeklyId = $"weekly_{utcNow.Year}W{GetIsoWeek( utcNow )}";

		if ( !profile.DailyChallenges.Keys.Any( k => k.StartsWith( dailyId ) ) )
		{
			profile.DailyChallenges.Clear();
			foreach ( var c in GenerateDaily( utcNow ) )
				profile.DailyChallenges[c.ChallengeId] = c;
		}

		if ( !profile.WeeklyChallenges.Keys.Any( k => k.StartsWith( weeklyId ) ) )
		{
			profile.WeeklyChallenges.Clear();
			foreach ( var c in GenerateWeekly( utcNow ) )
				profile.WeeklyChallenges[c.ChallengeId] = c;
		}
	}

	public void OnCorrectAnswer( PlayerProfile profile, TriviaQuestion question, bool buzzedFirst )
	{
		if ( profile is null ) return;

		foreach ( var kv in profile.DailyChallenges )
			TickChallenge( kv.Value, ChallengeMetric.AnswerQuestions, 1 );

		foreach ( var kv in profile.WeeklyChallenges )
			TickChallenge( kv.Value, ChallengeMetric.CorrectAnswers, 1 );

		if ( question is not null )
		{
			foreach ( var kv in profile.DailyChallenges )
			{
				if ( kv.Value.Description.Contains( question.Category, StringComparison.OrdinalIgnoreCase ) )
					TickChallenge( kv.Value, ChallengeMetric.AnswerCategory, 1 );
			}
		}

		if ( buzzedFirst )
		{
			foreach ( var kv in profile.DailyChallenges )
				TickChallenge( kv.Value, ChallengeMetric.BuzzFirst, 1 );
		}
	}

	public void OnBuzzWin( PlayerProfile profile )
	{
		profile.BuzzWins++;
	}

	public void OnMatchEnd( PlayerProfile profile, MatchResult result )
	{
		if ( profile is null || result is null ) return;

		var player = result.Players.FirstOrDefault( p => p.SteamId == profile.SteamId );
		if ( player is null ) return;

		if ( player.IsWinner )
		{
			foreach ( var kv in profile.DailyChallenges )
				TickChallenge( kv.Value, ChallengeMetric.WinMatches, 1 );
			foreach ( var kv in profile.WeeklyChallenges )
				TickChallenge( kv.Value, ChallengeMetric.WinMatches, 1 );
		}

		foreach ( var kv in profile.WeeklyChallenges )
		{
			var c = kv.Value;
			if ( !c.Description.Contains( "accuracy", StringComparison.OrdinalIgnoreCase ) ) continue;
			if ( profile.GamesPlayed < 5 ) continue;

			var pct = (int)(profile.Accuracy * 100f);
			c.Current = Math.Max( c.Current, pct );
			if ( c.Current >= c.Target ) c.Completed = true;
		}
	}

	public List<string> GetCompletedUnclaimed( PlayerProfile profile )
	{
		var list = new List<string>();

		foreach ( var kv in profile.DailyChallenges )
		{
			if ( kv.Value.Completed && !kv.Value.Claimed )
				list.Add( kv.Value.ChallengeId );
		}

		foreach ( var kv in profile.WeeklyChallenges )
		{
			if ( kv.Value.Completed && !kv.Value.Claimed )
				list.Add( kv.Value.ChallengeId );
		}

		return list;
	}

	public void ClaimRewards( PlayerProfile profile )
	{
		foreach ( var kv in profile.DailyChallenges )
		{
			if ( kv.Value.Completed && !kv.Value.Claimed )
			{
				XpService.ApplyXp( profile, kv.Value.XpReward );
				kv.Value.Claimed = true;
			}
		}

		foreach ( var kv in profile.WeeklyChallenges )
		{
			if ( kv.Value.Completed && !kv.Value.Claimed )
			{
				XpService.ApplyXp( profile, kv.Value.XpReward );
				kv.Value.Claimed = true;
			}
		}
	}

	private static void TickChallenge( ChallengeProgress c, ChallengeMetric metric, int amount )
	{
		if ( c.Completed ) return;
		if ( !c.ChallengeId.Contains( metric.ToString(), StringComparison.OrdinalIgnoreCase ) &&
		     !c.Description.Contains( MetricKeyword( metric ), StringComparison.OrdinalIgnoreCase ) )
			return;

		c.Current = Math.Min( c.Target, c.Current + amount );
		if ( c.Current >= c.Target ) c.Completed = true;
	}

	private static string MetricKeyword( ChallengeMetric metric ) => metric switch
	{
		ChallengeMetric.AnswerQuestions => "Answer",
		ChallengeMetric.WinMatches => "Win",
		ChallengeMetric.AnswerCategory => "Answer",
		ChallengeMetric.BuzzFirst => "Buzz",
		ChallengeMetric.CorrectAnswers => "correct",
		ChallengeMetric.MaintainAccuracy => "accuracy",
		_ => ""
	};

	private static string PickCategory( Random rng ) =>
		GameConstants.Categories[rng.Next( GameConstants.Categories.Length )];

	private static int HashDate( DateTime date ) => date.Year * 10000 + date.Month * 100 + date.Day;

	private static int GetIsoWeek( DateTime date )
	{
		var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
		return cal.GetWeekOfYear( date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday );
	}
}
