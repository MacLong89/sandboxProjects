using System.Text.Json;

namespace Sandbox;

/// <summary>
/// Disk read/write and version routing for <see cref="ThornsPersistentWorldDto"/>.
/// Domain services own data; this layer owns serialization only.
/// </summary>
public static class ThornsPersistenceSerializer
{
	public static readonly JsonSerializerOptions InventoryJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		PropertyNameCaseInsensitive = true
	};

	public static bool TryPeekWorldGenerationSeed( string relativePath, out int seed )
	{
		seed = 0;
		if ( string.IsNullOrWhiteSpace( relativePath ) )
			return false;

		var path = relativePath.Trim().Replace( '\\', '/' );
		try
		{
			if ( !FileSystem.Data.FileExists( path ) )
				return false;

			var dto = FileSystem.Data.ReadJson<ThornsPersistentWorldDto>( path );
			if ( dto is null || dto.Version < 1 || !dto.WorldGenerationSeed.HasValue )
				return false;

			seed = dto.WorldGenerationSeed.Value;
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns] Persistence: could not peek world seed at '{path}'." );
			return false;
		}
	}

	public static ThornsPersistentWorldDto ReadWorld( string relativePath, out double readMs, out bool fileExists )
	{
		readMs = 0;
		fileExists = false;
		var dto = new ThornsPersistentWorldDto();

		try
		{
			if ( !FileSystem.Data.FileExists( relativePath ) )
				return dto;

			fileExists = true;
			var sw = ThornsReplicationDiagnostics.StartTiming();
			var loaded = FileSystem.Data.ReadJson<ThornsPersistentWorldDto>( relativePath );
			readMs = sw.Elapsed.TotalMilliseconds;

			if ( loaded is null || loaded.Version < 1 )
			{
				dto.Version = 0;
				return dto;
			}

			TryMigrate( loaded );
			NormalizeCollections( loaded );
			return loaded;
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns] Persistence: failed reading '{relativePath}' — starting fresh runtime snapshot." );
			return new ThornsPersistentWorldDto { Version = 0 };
		}
	}

	public static bool WriteWorld( string relativePath, ThornsPersistentWorldDto dto )
	{
		const int maxAttempts = 4;
		Exception last = null;
		for ( var attempt = 1; attempt <= maxAttempts; attempt++ )
		{
			try
			{
				FileSystem.Data.WriteJson( relativePath, dto );
				return true;
			}
			catch ( Exception e )
			{
				last = e;
				Log.Warning(
					$"[Thorns] Persistence: disk write attempt {attempt}/{maxAttempts} failed ({e.GetType().Name}: {e.Message})." );
			}
		}

		Log.Warning( last, "[Thorns] Persistence: save failed after retries." );
		return false;
	}

	/// <summary>Future schema migrations — no-op until save format bump.</summary>
	public static void TryMigrate( ThornsPersistentWorldDto dto )
	{
		if ( dto is null )
			return;

		// v1 → v2 player fields are additive; legacy skill ranks merged on player restore.
	}

	public static void NormalizeCollections( ThornsPersistentWorldDto dto )
	{
		if ( dto is null )
			return;

		dto.Structures ??= new List<ThornsPersistentStructureDto>();
		dto.Wildlife ??= new List<ThornsPersistentWildlifeDto>();
		dto.PlayersByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerDto>();
	}

	public static bool JsonTryGetPropertyVariants( JsonElement obj, string[] names, out JsonElement value )
	{
		foreach ( var name in names )
		{
			if ( obj.TryGetProperty( name, out value ) )
				return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Some host JSON loaders bind PascalCase only; disk may store camelCase inventorySlotsBlob. Hydrate from raw text.
	/// </summary>
	public static void TryHydrateInventorySlotsBlobFromRawDiskJson(
		ThornsPersistentWorldDto live,
		string relativePath )
	{
		if ( live?.PlayersByAccountKey is null || live.PlayersByAccountKey.Count == 0 )
			return;

		try
		{
			if ( !FileSystem.Data.FileExists( relativePath ) )
				return;

			var text = FileSystem.Data.ReadAllText( relativePath );
			if ( string.IsNullOrWhiteSpace( text ) )
				return;

			using var doc = JsonDocument.Parse( text );
			var root = doc.RootElement;
			if ( !JsonTryGetPropertyVariants( root, new[] { "PlayersByAccountKey", "playersByAccountKey" }, out var playersEl )
			     || playersEl.ValueKind != JsonValueKind.Object )
				return;

			foreach ( var prop in playersEl.EnumerateObject() )
			{
				if ( !live.PlayersByAccountKey.TryGetValue( prop.Name, out var playerDto ) || playerDto is null )
					continue;

				if ( !string.IsNullOrEmpty( playerDto.InventorySlotsBlob ) )
					continue;

				var playerEl = prop.Value;
				if ( playerEl.ValueKind != JsonValueKind.Object )
					continue;

				if ( !JsonTryGetPropertyVariants( playerEl,
					     new[] { "InventorySlotsBlob", "inventorySlotsBlob" },
					     out var blobEl ) )
					continue;

				if ( blobEl.ValueKind != JsonValueKind.String )
					continue;

				var s = blobEl.GetString();
				if ( string.IsNullOrEmpty( s ) )
					continue;

				playerDto.InventorySlotsBlob = s;
				Log.Info(
					$"[Thorns] Persistence [inv] hydrated InventorySlotsBlob from raw JSON for key={prop.Name} blobChars={s.Length}" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Persistence [inv] raw JSON hydrate skipped." );
		}
	}
}
