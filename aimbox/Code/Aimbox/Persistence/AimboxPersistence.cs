using System.Text.Json;

namespace Sandbox;

public interface IAimboxDatabase
{
	AimboxPlayerData LoadPlayer( string accountId );
	void SavePlayer( AimboxPlayerData data );
	void DeletePlayer( string accountId );
}

public sealed class JsonFileAimboxDatabase : IAimboxDatabase
{
	static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
	readonly string _root;

	public JsonFileAimboxDatabase( string root = "data/aimbox/players" )
	{
		_root = root.Trim( '/' );
		FileSystem.Data.CreateDirectory( _root );
	}

	public AimboxPlayerData LoadPlayer( string accountId )
	{
		var path = GetPath( accountId );
		if ( !FileSystem.Data.FileExists( path ) )
		{
			var created = AimboxPlayerData.CreateFreshStart( accountId );
			SavePlayer( created );
			return created;
		}

		try
		{
			var loaded = JsonSerializer.Deserialize<AimboxPlayerData>( FileSystem.Data.ReadAllText( path ), Options ) ?? new AimboxPlayerData();
			loaded.AccountId = accountId;
			return loaded;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Aimbox failed to load player data for {accountId}: {ex.Message}" );
			var fallback = AimboxPlayerData.CreateFreshStart( accountId );
			SavePlayer( fallback );
			return fallback;
		}
	}

	public void SavePlayer( AimboxPlayerData data )
	{
		data.Validate();
		var path = GetPath( data.AccountId );
		FileSystem.Data.WriteAllText( path, JsonSerializer.Serialize( data, Options ) );
	}

	public void DeletePlayer( string accountId )
	{
		var path = GetPath( accountId );
		if ( FileSystem.Data.FileExists( path ) )
			FileSystem.Data.DeleteFile( path );
	}

	string GetPath( string accountId )
	{
		var safe = string.Concat( accountId.Select( c => char.IsLetterOrDigit( c ) ? c : '_' ) );
		return $"{_root}/{safe}.json";
	}
}

public sealed class AimboxSaveQueueSystem
{
	readonly IAimboxDatabase _database;
	readonly Queue<AimboxPlayerData> _pending = new();
	readonly HashSet<string> _queuedIds = [];
	readonly Dictionary<string, int> _failures = new();

	public AimboxSaveQueueSystem( IAimboxDatabase database )
	{
		_database = database;
	}

	public void Enqueue( AimboxPlayerData data )
	{
		if ( data is null || !_queuedIds.Add( data.AccountId ) )
			return;

		_pending.Enqueue( data );
	}

	public void FlushOne()
	{
		if ( _pending.Count == 0 )
			return;

		var data = _pending.Dequeue();
		_queuedIds.Remove( data.AccountId );

		try
		{
			_database.SavePlayer( data );
			_failures.Remove( data.AccountId );
		}
		catch ( Exception ex )
		{
			var attempts = _failures.GetValueOrDefault( data.AccountId ) + 1;
			_failures[data.AccountId] = attempts;
			Log.Warning( $"Aimbox save failed for {data.AccountId}, attempt {attempts}: {ex.Message}" );

			if ( attempts < 5 )
				Enqueue( data );
		}
	}

	public void FlushAll()
	{
		var guard = 0;
		while ( _pending.Count > 0 && guard++ < 128 )
			FlushOne();
	}

	public void DiscardPending( string accountId = null )
	{
		if ( accountId is null )
		{
			_pending.Clear();
			_queuedIds.Clear();
			return;
		}

		var remaining = new Queue<AimboxPlayerData>();
		while ( _pending.Count > 0 )
		{
			var data = _pending.Dequeue();
			if ( data.AccountId == accountId )
				_queuedIds.Remove( accountId );
			else
				remaining.Enqueue( data );
		}

		while ( remaining.Count > 0 )
			_pending.Enqueue( remaining.Dequeue() );
	}
}
