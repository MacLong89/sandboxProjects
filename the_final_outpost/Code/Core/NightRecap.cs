namespace FinalOutpost;

public sealed class NightRecapLine
{
	public string Icon { get; init; } = "info";
	public string Text { get; init; }
	public string Detail { get; init; }
	public bool Warn { get; init; }
}

/// <summary>Summary shown after surviving a night — casualties, damage, earnings, unlocks.</summary>
public sealed class NightRecap
{
	public int ClearedNight { get; init; }
	public int NextNight { get; init; }
	public double ClearBonus { get; init; }
	public double FirstNightBonus { get; init; }
	public double PlotExpansionBonus { get; init; }
	public int Kills { get; init; }
	public int RecruitsLost { get; init; }
	public double RepairAllCost { get; init; }
	public string UnlockMsg { get; init; }
	public IReadOnlyList<NightRecapLine> StatusLines { get; init; } = Array.Empty<NightRecapLine>();

	public static NightRecap Build(
		GameCore core,
		int clearedNight,
		double clearBonus,
		double firstNightBonus,
		double plotExpansionBonus,
		string unlockMsg,
		int recruitsAtStart,
		long killsAtStart )
	{
		var save = core?.Save;
		var build = core?.Build;
		var outpost = core?.Outpost;

		var recruitsLost = Math.Max( 0, recruitsAtStart - (save?.Recruits.Count ?? 0) );
		var kills = (int)Math.Max( 0, (save?.TotalKills ?? 0) - killsAtStart );
		var repairCost = build?.RepairAllCost() ?? 0;

		var lines = new List<NightRecapLine>();

		if ( recruitsLost > 0 )
		{
			lines.Add( new NightRecapLine
			{
				Icon = "military_tech",
				Text = recruitsLost == 1 ? "1 recruit killed in action" : $"{recruitsLost} recruits killed in action",
				Warn = true
			} );
		}
		else if ( recruitsAtStart > 0 )
		{
			lines.Add( new NightRecapLine
			{
				Icon = "verified",
				Text = "All recruits survived"
			} );
		}

		if ( outpost is not null )
		{
			var corePct = outpost.CoreMaxHealth > 0f
				? (int)MathF.Round( outpost.CoreHealth / outpost.CoreMaxHealth * 100f )
				: 100;
			var wallPct = (int)MathF.Round( outpost.WallIntegrityFraction * 100f );

			if ( corePct < 100 || wallPct < 100 )
			{
				lines.Add( new NightRecapLine
				{
					Icon = "home",
					Text = $"Command Post {corePct}% · Walls {wallPct}%",
					Warn = corePct < 85 || wallPct < 85
				} );
			}
			else
			{
				lines.Add( new NightRecapLine
				{
					Icon = "home",
					Text = "Command Post and walls intact"
				} );
			}
		}

		var damaged = 0;
		var destroyed = 0;
		if ( build is not null )
		{
			foreach ( var b in build.Buildings )
			{
				if ( b.IsDestroyed )
				{
					destroyed++;
					continue;
				}

				if ( b.MaxHealth > 0f && b.Health < b.MaxHealth - 0.5f )
					damaged++;
			}
		}

		if ( destroyed > 0 )
		{
			lines.Add( new NightRecapLine
			{
				Icon = "warning",
				Text = destroyed == 1 ? "1 structure destroyed" : $"{destroyed} structures destroyed",
				Warn = true
			} );
		}

		if ( damaged > 0 )
		{
			lines.Add( new NightRecapLine
			{
				Icon = "build",
				Text = damaged == 1 ? "1 structure needs repair" : $"{damaged} structures need repair",
				Detail = repairCost > 0 ? GameConstants.FormatScrap( repairCost ) : null,
				Warn = true
			} );
		}
		else if ( destroyed == 0 && repairCost <= 0 )
		{
			lines.Add( new NightRecapLine
			{
				Icon = "build",
				Text = "All structures intact"
			} );
		}

		if ( kills > 0 )
		{
			lines.Add( new NightRecapLine
			{
				Icon = "pest_control",
				Text = kills == 1 ? "1 zombie eliminated" : $"{kills} zombies eliminated"
			} );
		}

		return new NightRecap
		{
			ClearedNight = clearedNight,
			NextNight = save?.CurrentNight ?? clearedNight + 1,
			ClearBonus = clearBonus,
			FirstNightBonus = firstNightBonus,
			PlotExpansionBonus = plotExpansionBonus,
			Kills = kills,
			RecruitsLost = recruitsLost,
			RepairAllCost = repairCost,
			UnlockMsg = unlockMsg,
			StatusLines = lines
		};
	}
}
