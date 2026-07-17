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
			// AUDIT FIX H7 (2026-07-13): corrupt JSON used to SavePlayer(fresh) over the bad file,
			// permanently wiping progression with only a log line. Quarantine the corrupt blob and
			// return an in-memory fresh profile WITHOUT writing over the original path.
			Log.Warning( $"Aimbox failed to load player data for {accountId}: {ex.Message}" );

			try
			{
				var corruptBackup = $"{path}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
				if ( FileSystem.Data.FileExists( path ) )
				{
					FileSystem.Data.WriteAllText( corruptBackup, FileSystem.Data.ReadAllText( path ) );
					Log.Warning( $"[Aimbox] Quarantined corrupt save to {corruptBackup}. Fresh session profile will not overwrite until a clean save." );
				}
			}
			catch ( Exception backupEx )
			{
				Log.Warning( $"[Aimbox] Could not quarantine corrupt save for {accountId}: {backupEx.Message}" );
			}

			return AimboxPlayerData.CreateFreshStart( accountId );
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
