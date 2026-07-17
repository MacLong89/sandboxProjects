using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace HeightsHotel;

public sealed class SaveService
{
	public const string SaveFile = "heights_hotel_save.json";
	public const string BackupFile = "heights_hotel_save.bak.json";

	public void Save( HotelState state )
	{
		state.SaveVersion = GameBalance.SaveVersion;
		state.LastRealWorldUtc = DateTimeOffset.UtcNow;

		if ( FileSystem.Data.FileExists( SaveFile ) )
		{
			var existing = FileSystem.Data.ReadAllText( SaveFile );
			FileSystem.Data.WriteAllText( BackupFile, existing );
		}

		FileSystem.Data.WriteJson( SaveFile, state );
	}

	public (HotelState State, string Warning) LoadOrNew()
	{
		var (state, warning) = TryLoad( SaveFile );
		if ( state is not null )
			return (state, warning);

		(state, warning) = TryLoad( BackupFile );
		if ( state is not null )
			return (state, warning ?? "Restored from backup save.");

		return (HotelSimulation.CreateNewGame(), warning ?? "Starting a new hotel.");
	}

	(HotelState State, string Warning) TryLoad( string path )
	{
		try
		{
			if ( !FileSystem.Data.FileExists( path ) )
				return (null, null);

			var state = FileSystem.Data.ReadJson<HotelState>( path );
			if ( state is null )
				return (null, "Save was empty.");

			if ( state.SaveVersion < 1 || state.SaveVersion > GameBalance.SaveVersion )
				return (null, $"Save version {state.SaveVersion} unsupported.");

			if ( state.Cells is null || state.Cells.Count == 0 )
				return (null, "Save had no hotel layout.");

			state.Guests ??= new();
			state.Employees ??= new();
			state.Ledger ??= new();
			state.CompletedTutorials ??= new List<string>();
			state.DailyGoals ??= new List<DailyGoal>();
			Normalize( state );
			var warning = state.SaveVersion < GameBalance.SaveVersion
				? $"Migrated save from v{state.SaveVersion} to v{GameBalance.SaveVersion}."
				: null;
			state.SaveVersion = GameBalance.SaveVersion;
			return (state, warning);
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to load {path}: {e.Message}" );
			return (null, $"Corrupt save ({path}).");
		}
	}

	static void Normalize( HotelState state )
	{
		var employeeIds = new HashSet<int>();
		foreach ( var employee in state.Employees )
			employeeIds.Add( employee.Id );

		foreach ( var cell in state.Cells )
		{
			cell.Level = Math.Clamp( cell.Level, 1, GameBalance.MaxRoomLevel );
			cell.Cleanliness = Math.Clamp( cell.Cleanliness, 0f, 1f );
			cell.OccupantGuestIds ??= new List<int>();
			if ( cell.AssignedEmployeeId is int id && !employeeIds.Contains( id ) )
				cell.AssignedEmployeeId = null;
		}

		foreach ( var employee in state.Employees )
		{
			if ( employee.AssignedRoomX is not int x || employee.AssignedRoomY is not int y
				|| !state.Cells.Exists( c => c.X == x && c.Y == y ) )
			{
				employee.AssignedRoomX = null;
				employee.AssignedRoomY = null;
			}
		}

		foreach ( var guest in state.Guests )
		{
			guest.PreferredTier = Math.Clamp( guest.PreferredTier <= 0 ? 1 : guest.PreferredTier, 1, 3 );
		}

		state.PeakReputationLevel = Math.Max( state.PeakReputationLevel, state.ReputationLevel );
		if ( state.WeatherRemaining <= 0f )
			state.WeatherRemaining = 45f;
		if ( state.GoalsDay < 1 )
			state.GoalsDay = GameBalance.DayFromSimTime( state.SimTime );

		state.NextGuestId = Math.Max( state.NextGuestId, state.Guests.Count == 0 ? 1 : state.Guests.Max( g => g.Id ) + 1 );
		state.NextEmployeeId = Math.Max( state.NextEmployeeId, state.Employees.Count == 0 ? 1 : state.Employees.Max( e => e.Id ) + 1 );
	}
}
