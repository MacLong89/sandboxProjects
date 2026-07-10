namespace Terraingen.Foliage;

using Terraingen;

/// <summary>
/// Avoid repeated <see cref="Model.Load"/> on the same vmdl (reduces asset-system churn when many instances spawn).
/// </summary>
static class ThornsFoliageModelCache
{
	static readonly Dictionary<string, Model> _byPath = new( StringComparer.OrdinalIgnoreCase );

	public static Model Load( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return default;

		if ( _byPath.TryGetValue( path, out var cached ) && ThornsModelResourceLoad.IsUsable( cached ) )
			return cached;

		var model = ThornsModelResourceLoad.TryLoad( path );
		if ( ThornsModelResourceLoad.IsUsable( model ) )
			_byPath[path] = model;

		return model;
	}
}
