using Dynasty.Core.Enums;

using Dynasty.Core.Identifiers;

using Dynasty.Core.Interfaces;

using Dynasty.Domain.Simulation;



namespace Dynasty.Systems.Simulation;



/// <summary>

/// Statistical football simulation. Produces believable scores and an event stream for visualization replay.

/// Outcomes are fully determined here before any viewer consumes events.

/// </summary>

public sealed class GameSimulationEngine

{

	public GameSimulationOutput Simulate( GameSimulationInput input, ILeagueRandom random )

	{

		var output = new GameSimulationOutput();

		var homeStrength = ComputeStrength( input.Home );

		var awayStrength = ComputeStrength( input.Away );



		var eventIndex = 0;

		void AddEvent( SimEventType type, int quarter, int clock, TeamId possession, string description, int yardLine = 50 )

		{

			output.Events.Add( new SimEventRecord

			{

				Index = eventIndex++,

				Type = type,

				Quarter = quarter,

				ClockSeconds = Math.Max( 0, clock ),

				HomeScore = output.HomeScore,

				AwayScore = output.AwayScore,

				YardLine = yardLine,

				Down = 0,

				YardsToGo = 0,

				PossessionTeamId = possession,

				Description = description

			} );

		}



		var quarter = 1;

		var clock = 900;

		var possession = random.Chance( 0.5f ) ? input.HomeTeamId : input.AwayTeamId;

		AddEvent( SimEventType.Kickoff, 1, clock, possession, "Kickoff" );



		var drivesRemaining = random.NextInt( 18, 26 );



		for ( var d = 0; d < drivesRemaining; d++ )

		{

			var isHome = possession.Value == input.HomeTeamId.Value;

			var offense = isHome ? homeStrength : awayStrength;

			var defense = isHome ? awayStrength : homeStrength;

			var matchup = offense - defense * 0.85f;



			AddEvent( SimEventType.DriveStart, quarter, clock, possession, "Drive starts", yardLine: random.NextInt( 15, 35 ) );



			var driveOutcome = random.NextFloat();

			if ( driveOutcome < 0.42f + matchup * 0.001f )

			{

				var points = random.Chance( 0.12f ) ? 7 : 3;

				if ( isHome ) output.HomeScore += points;

				else output.AwayScore += points;



				AddEvent( points == 7 ? SimEventType.Score : SimEventType.FieldGoalAttempt, quarter, clock - random.NextInt( 30, 180 ), possession,

					points == 7 ? "Touchdown" : "Field goal good" );

				possession = isHome ? input.AwayTeamId : input.HomeTeamId;

			}

			else if ( driveOutcome < 0.52f )

			{

				possession = isHome ? input.AwayTeamId : input.HomeTeamId;

				AddEvent( SimEventType.Turnover, quarter, clock - random.NextInt( 20, 120 ), possession, "Turnover" );

			}

			else

			{

				possession = isHome ? input.AwayTeamId : input.HomeTeamId;

				AddEvent( SimEventType.Punt, quarter, clock - random.NextInt( 40, 200 ), possession, "Punt" );

			}



			clock -= random.NextInt( 60, 200 );

			if ( clock <= 0 )

			{

				quarter = Math.Min( 4, quarter + 1 );

				clock = 900;

			}

		}



		ResolveTie( output, input, random, input.IsPlayoff );



		AddEvent( SimEventType.GameEnd, 4, 0, input.HomeTeamId,

			$"Final: {output.HomeScore}-{output.AwayScore}" );



		return output;

	}



	static void ResolveTie( GameSimulationOutput output, GameSimulationInput input, ILeagueRandom random, bool isPlayoff )

	{

		if ( output.HomeScore != output.AwayScore )

			return;



		if ( !isPlayoff )

			return;



		var homeRating = TeamProfileBuilder.ComputeOverallRating( input.Home );

		var awayRating = TeamProfileBuilder.ComputeOverallRating( input.Away );



		if ( homeRating == awayRating )

		{

			if ( random.Chance( 0.5f ) )

				output.HomeScore += 3;

			else

				output.AwayScore += 3;

		}

		else if ( homeRating > awayRating )

			output.HomeScore += 3;

		else

			output.AwayScore += 3;

	}



	static float ComputeStrength( TeamSimulationProfile profile )

		=> TeamProfileBuilder.ComputeOverallRating( profile );

}

