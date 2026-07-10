using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Coaches;
using Dynasty.Domain.Contracts;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Schedule;
using Dynasty.Domain.Teams;

namespace Dynasty.Domain.Factories;

/// <summary>
/// Creates new leagues from data definitions. Used by singleplayer and dedicated server bootstrap.
/// </summary>
public static class LeagueFactory
{
	public static LeagueState CreateNewLeague( LeagueSettings settings, ILeagueDataDefinitions definitions, ILeagueRandom random )
	{
		var startsInDraft = settings.StartMode is DynastyStartMode.RookieDraft or DynastyStartMode.ExpansionDraft;

		var league = new LeagueState
		{
			Id = LeagueId.New(),
			Settings = settings,
			Phase = startsInDraft ? LeaguePhase.Draft : LeaguePhase.Preseason,
			CurrentSeason = 1,
			CurrentWeek = 1,
			RandomSeed = random.NextInt( 1, int.MaxValue ),
			StateRevision = 1
		};

		var teamDefs = definitions.Teams.Take( settings.TeamCount ).ToList();
		var populateRosters = settings.StartMode != DynastyStartMode.ExpansionDraft;

		foreach ( var def in teamDefs )
		{
			var team = CreateTeam( def, random );
			league.Teams[team.Id] = team;

			if ( populateRosters )
				LeaguePlayerGenerator.PopulateTeamRoster( league, team, definitions, random, settings.RosterSize, settings.MinPlayersPerPositionPerTeam );

			PopulateTeamCoaches( league, team, random );
		}

		if ( settings.StartMode == DynastyStartMode.ExpansionDraft )
		{
			LeaguePlayerGenerator.GenerateLeaguePool(
				league,
				definitions,
				random,
				settings.LeaguePlayerPoolSize,
				settings.MinPlayersPerPositionPerTeam );
		}

		league.Schedule = ScheduleGenerator.GenerateRegularSeason( league, settings.RegularSeasonWeeks, random );
		ApplyChallengeMode( league, settings );
		league.FranchiseProgress ??= new Domain.Franchise.FranchiseProgressState();
		return league;
	}

	static void ApplyChallengeMode( LeagueState league, LeagueSettings settings )
	{
		if ( settings.ChallengeMode == ChallengeMode.Standard )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( league );
		if ( human.IsEmpty || !league.Teams.TryGetValue( human, out var team ) )
			return;

		switch ( settings.ChallengeMode )
		{
			case ChallengeMode.Rebuild:
				team.Finances.Budget = 250_000_000;
				team.BuildingWindow = TeamBuildingWindow.Rebuilding;
				break;
			case ChallengeMode.WinNow:
				team.BuildingWindow = TeamBuildingWindow.WinNow;
				team.Prestige.Prestige = Math.Min( 100, team.Prestige.Prestige + 5 );
				break;
			case ChallengeMode.DraftGenius:
				team.Facilities.Levels[FacilityType.ScoutingDepartment] = 3;
				break;
		}
	}

	static TeamState CreateTeam( TeamDefinition def, ILeagueRandom random )
	{
		return new TeamState
		{
			Id = TeamId.New(),
			Identity = new TeamIdentity
			{
				City = def.City,
				Name = def.Name,
				Abbreviation = def.Abbreviation,
				PrimaryColor = def.PrimaryColor,
				SecondaryColor = def.SecondaryColor,
				Stadium = def.Stadium
			},
			Finances = new TeamFinances
			{
				Budget = 500_000_000,
				SalaryCapSpace = 50_000_000
			},
			Prestige = new TeamPrestigeState
			{
				Prestige = random.NextInt( 35, 85 ),
				FanSupport = random.NextInt( 40, 90 )
			},
			ControlType = GmControlType.AI
		};
	}

	static void PopulateTeamCoaches( LeagueState league, TeamState team, ILeagueRandom random )
	{
		foreach ( var role in new[] { CoachRole.HeadCoach, CoachRole.OffensiveCoordinator, CoachRole.DefensiveCoordinator } )
		{
			var coach = new CoachState
			{
				Id = CoachId.New(),
				FirstName = "Coach",
				LastName = $"{team.Identity.Abbreviation}-{role}",
				Age = random.NextInt( 38, 65 ),
				Role = role,
				TeamId = team.Id,
				Ratings = new CoachRatings
				{
					Overall = random.NextInt( 55, 92 ),
					Development = random.NextInt( 50, 95 ),
					GamePlanning = random.NextInt( 50, 95 ),
					Motivation = random.NextInt( 50, 95 ),
					Scouting = random.NextInt( 45, 90 )
				},
				Contract = new CoachContract { YearsRemaining = random.NextInt( 1, 5 ), AnnualSalary = random.NextInt( 1_000_000, 12_000_000 ) }
			};

			league.Coaches[coach.Id] = coach;
			team.CoachIds.Add( coach.Id );
		}

		for ( var s = 0; s < 3; s++ )
		{
			var scout = new CoachState
			{
				Id = CoachId.New(),
				FirstName = "Scout",
				LastName = $"{team.Identity.Abbreviation}-{s + 1}",
				Age = random.NextInt( 28, 58 ),
				Role = CoachRole.Scout,
				TeamId = team.Id,
				Ratings = new CoachRatings { Scouting = random.NextInt( 50, 95 ), Overall = random.NextInt( 50, 80 ) }
			};
			league.Coaches[scout.Id] = scout;
			team.CoachIds.Add( scout.Id );
		}
	}
}
