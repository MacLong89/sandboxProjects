namespace Terraingen.Animals;

/// <summary>ushort species id → data (wire format).</summary>
public static class ThornsAnimalSpeciesRegistry
{
	static readonly Dictionary<ushort, ThornsAnimalSpeciesData> ById = new();
	static readonly Dictionary<string, ushort> ByKey = new( StringComparer.OrdinalIgnoreCase );

	const string ProbeKey = "wolf";
	const int ExpectedCount = 4;

	static bool IsHealthy =>
		ByKey.TryGetValue( ProbeKey, out var probeId )
		&& ById.TryGetValue( probeId, out var probe )
		&& probe is not null
		&& ById.Count >= ExpectedCount;

	public static void EnsureInitialized()
	{
		if ( IsHealthy )
			return;

		ById.Clear();
		ByKey.Clear();

		var loaded = 0;
		ThornsAnimalSpeciesCatalog.RegisterAll( species =>
		{
			if ( Register( species ) )
				loaded++;
		} );

		if ( loaded == 0 )
		{
			Log.Error( "[Thorns Animals] Species registry failed — zero species registered after bootstrap." );
			return;
		}

		if ( !IsHealthy )
		{
			Log.Error(
				$"[Thorns Animals] Species registry incomplete — loaded={loaded}, ids={ById.Count}, " +
				$"keys=[{DescribeKeys()}], probe '{ProbeKey}' found={ByKey.ContainsKey( ProbeKey )}." );
			return;
		}

	}

	public static bool Register( ThornsAnimalSpeciesData data )
	{
		if ( data is null || data.SpeciesId == 0 || string.IsNullOrWhiteSpace( data.Key ) )
		{
			Log.Warning( $"[Thorns Animals] Skipped invalid species (id={data?.SpeciesId}, key='{data?.Key}')." );
			return false;
		}

		ById[data.SpeciesId] = data;
		ByKey[data.Key] = data.SpeciesId;
		return true;
	}

	public static bool TryGet( ushort speciesId, out ThornsAnimalSpeciesData data )
	{
		EnsureInitialized();
		return ById.TryGetValue( speciesId, out data );
	}

	public static bool TryGet( string key, out ThornsAnimalSpeciesData data )
	{
		EnsureInitialized();
		data = null;
		if ( string.IsNullOrWhiteSpace( key ) || !ByKey.TryGetValue( key, out var id ) )
		{
			if ( !string.IsNullOrWhiteSpace( key ) )
			{
				Log.Warning(
					$"[Thorns Animals] TryGet miss for '{key}' — ids={ById.Count}, keys=[{DescribeKeys()}], healthy={IsHealthy}." );
			}

			return false;
		}

		return ById.TryGetValue( id, out data );
	}

	public static IReadOnlyCollection<ThornsAnimalSpeciesData> All
	{
		get
		{
			EnsureInitialized();
			return ById.Values;
		}
	}

	static string DescribeKeys()
	{
		if ( ByKey.Count == 0 )
			return "(none)";

		var keys = new string[ByKey.Count];
		ByKey.Keys.CopyTo( keys, 0 );
		Array.Sort( keys, StringComparer.OrdinalIgnoreCase );
		return string.Join( ", ", keys );
	}
}
