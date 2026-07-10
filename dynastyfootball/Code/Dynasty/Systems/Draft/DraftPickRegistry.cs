using Dynasty.Core.Identifiers;

using Dynasty.Domain.League;

using Dynasty.Domain.Teams;



namespace Dynasty.Systems.Draft;



/// <summary>

/// Single source of truth for draft pick ownership and valuation.

/// </summary>

public static class DraftPickRegistry

{

	public const int FutureSeasonsToTrack = 3;



	public static void EnsureFuturePickInventory( LeagueState state )

	{

		for ( var offset = 1; offset <= FutureSeasonsToTrack; offset++ )

			EnsureSeasonPicks( state, state.CurrentSeason + offset );

	}



	public static void EnsureSeasonPicks( LeagueState state, int draftSeason )

	{

		var orderedTeams = DraftOrderHelper.GetPickOrder( state );

		var rounds = state.Settings.RookieDraftRounds;



		var existingPickNumbers = new HashSet<int>();

		foreach ( var team in state.Teams.Values )

		{

			foreach ( var pick in team.DraftPicks.Where( p => p.Season == draftSeason ) )

				existingPickNumbers.Add( pick.PickNumber );

		}



		var pickNum = 1;

		for ( var round = 1; round <= rounds; round++ )

		{

			var roundTeams = round % 2 == 0 ? orderedTeams.AsEnumerable().Reverse() : orderedTeams;

			foreach ( var teamId in roundTeams )

			{

				if ( !existingPickNumbers.Contains( pickNum ) )

				{

					if ( !state.Teams.TryGetValue( teamId, out var team ) )

					{

						pickNum++;

						continue;

					}



					team.DraftPicks.Add( new DraftPickAsset

					{

						Id = DraftPickId.New(),

						Season = draftSeason,

						Round = round,

						PickNumber = pickNum,

						OriginalOwnerId = teamId,

						CurrentOwnerId = teamId

					} );

				}



				pickNum++;

			}

		}

	}



	public static DraftPickAsset FindPick( LeagueState state, DraftPickId pickId )

	{

		foreach ( var team in state.Teams.Values )

		{

			var pick = team.DraftPicks.FirstOrDefault( p => p.Id.Value == pickId.Value );

			if ( pick != null )

				return pick;

		}



		return null;

	}



	public static TeamId GetPickOwner( LeagueState state, DraftPickId pickId )

	{

		var pick = FindPick( state, pickId );

		return pick?.CurrentOwnerId ?? TeamId.Empty;

	}



	public static bool TryTransferPick( LeagueState state, DraftPickId pickId, TeamId from, TeamId to )

	{

		if ( !state.Teams.TryGetValue( from, out var fromTeam ) || !state.Teams.TryGetValue( to, out var toTeam ) )

			return false;



		var pick = fromTeam.DraftPicks.FirstOrDefault( p => p.Id.Value == pickId.Value );

		if ( pick == null || pick.CurrentOwnerId.Value != from.Value )

			return false;



		fromTeam.DraftPicks.Remove( pick );

		pick.CurrentOwnerId = to;

		toTeam.DraftPicks.Add( pick );

		return true;

	}



	public static void ConsumePick( LeagueState state, DraftPickId pickId )

	{

		foreach ( var team in state.Teams.Values )

			team.DraftPicks.RemoveAll( p => p.Id.Value == pickId.Value );

	}



	public static float GetPickValue( DraftPickAsset pick )

	{

		if ( pick == null )

			return 0f;



		var roundBase = pick.Round switch

		{

			1 => 82f,

			2 => 62f,

			3 => 48f,

			4 => 38f,

			5 => 30f,

			6 => 24f,

			_ => 18f

		};



		var slotBonus = Math.Clamp( ( 33 - pick.PickNumber ) * 0.15f, -4f, 6f );

		return roundBase + slotBonus;

	}



	public static float GetAssetValue( LeagueState state, DraftPickId pickId )

	{

		var pick = FindPick( state, pickId );

		return GetPickValue( pick );

	}

}

