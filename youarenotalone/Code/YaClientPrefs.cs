using System;
using System.Text.Json;

namespace Sandbox;

/// <summary>Local-only client preferences (onboarding, UI).</summary>
public static class YaClientPrefs
{
	const string PrefsPath = "youarenotalone_client_prefs.json";

	static bool _loaded;
	static bool _hasSeenControlsTutorial;

	public static bool HasSeenControlsTutorial
	{
		get
		{
			EnsureLoaded();
			return _hasSeenControlsTutorial;
		}
		set
		{
			EnsureLoaded();
			if ( _hasSeenControlsTutorial == value )
				return;
			_hasSeenControlsTutorial = value;
			Persist();
		}
	}

	static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;
		try
		{
			if ( !FileSystem.Data.FileExists( PrefsPath ) )
				return;

			using var stream = FileSystem.Data.OpenRead( PrefsPath );
			var doc = JsonDocument.Parse( stream );
			if ( doc.RootElement.TryGetProperty( "HasSeenControlsTutorial", out var prop ) && prop.ValueKind == JsonValueKind.True )
				_hasSeenControlsTutorial = true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[YA] Failed reading client prefs: {ex.Message}" );
		}
	}

	static void Persist()
	{
		try
		{
			using var stream = FileSystem.Data.OpenWrite( PrefsPath );
			using var writer = new Utf8JsonWriter( stream );
			writer.WriteStartObject();
			writer.WriteBoolean( "HasSeenControlsTutorial", _hasSeenControlsTutorial );
			writer.WriteEndObject();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[YA] Failed writing client prefs: {ex.Message}" );
		}
	}
}
