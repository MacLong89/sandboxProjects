using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Per-account local persistence for seen tips and cooldown timestamps.</summary>
public sealed class ThornsHotTipMemory
{
	public sealed class Dto
	{
		public string AccountKey { get; set; } = "";
		public List<string> SeenOnceIds { get; set; } = new();
		public Dictionary<string, double> LastShownUnix { get; set; } = new();
	}

	readonly HashSet<string> _seenOnce = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, double> _lastShown = new( StringComparer.OrdinalIgnoreCase );
	string _accountKey = "";
	bool _dirty;

	public void BindAccountKey( string accountKey )
	{
		var key = accountKey?.Trim() ?? "";
		if ( string.Equals( _accountKey, key, StringComparison.Ordinal ) )
			return;

		_accountKey = key;
		Load();
	}

	public bool TryBeginShow( ThornsHotTipDefinition def, double now )
	{
		if ( string.IsNullOrWhiteSpace( def.Id ) )
			return false;

		if ( !def.Repeatable && _seenOnce.Contains( def.Id ) )
			return false;

		if ( _lastShown.TryGetValue( def.Id, out var last )
		     && now - last < def.PerTipCooldownSeconds )
			return false;

		if ( !def.Repeatable )
			_seenOnce.Add( def.Id );

		_lastShown[def.Id] = now;
		_dirty = true;
		return true;
	}

	public void MarkSeen( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return;

		if ( _seenOnce.Add( id ) )
			_dirty = true;
	}

	public void FlushIfDirty()
	{
		if ( !_dirty || string.IsNullOrWhiteSpace( _accountKey ) )
			return;

		_dirty = false;
		try
		{
			var path = PathFor( _accountKey );
			var dto = new Dto
			{
				AccountKey = _accountKey,
				SeenOnceIds = new List<string>( _seenOnce ),
				LastShownUnix = new Dictionary<string, double>( _lastShown )
			};
			FileSystem.Data.WriteJson( path, dto );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Hot tips: failed to save memory." );
		}
	}

	void Load()
	{
		_seenOnce.Clear();
		_lastShown.Clear();
		_dirty = false;

		if ( string.IsNullOrWhiteSpace( _accountKey ) )
			return;

		try
		{
			var path = PathFor( _accountKey );
			if ( !FileSystem.Data.FileExists( path ) )
				return;

			var dto = FileSystem.Data.ReadJson<Dto>( path );
			if ( dto is null )
				return;

			if ( dto.SeenOnceIds is not null )
			{
				foreach ( var id in dto.SeenOnceIds )
				{
					if ( !string.IsNullOrWhiteSpace( id ) )
						_seenOnce.Add( id.Trim() );
				}
			}

			if ( dto.LastShownUnix is not null )
			{
				foreach ( var kv in dto.LastShownUnix )
				{
					if ( !string.IsNullOrWhiteSpace( kv.Key ) )
						_lastShown[kv.Key.Trim()] = kv.Value;
				}
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Hot tips: failed to load memory." );
		}
	}

	static string PathFor( string accountKey )
	{
		var safe = accountKey.Replace( "/", "_", StringComparison.Ordinal ).Replace( "\\", "_", StringComparison.Ordinal );
		return $"Thorns/hot_tips/{safe}.json";
	}
}
