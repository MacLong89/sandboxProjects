namespace FinalOutpost;

/// <summary>
/// Loads recruit weapon world models from the mounted <c>facepunch.sboxweapons</c> package
/// (or individual cloud weapon packages as fallback). Pulls packages onto disk when missing
/// so players without the Library content pre-installed still get weapon meshes.
/// </summary>
public sealed class WeaponModelLoader : Component
{
	public static WeaponModelLoader Instance { get; private set; }

	/// <summary>True after the mount pass finished (success or box-fallback).</summary>
	public static bool IsReady { get; private set; }

	const string SboxWeaponsPackage = "facepunch.sboxweapons";

	private readonly Dictionary<string, Model> _models = new();

	private static readonly (string CloudIdent, string Path)[] Weapons =
	{
		( "facepunch.w_usp", WeaponModels.PistolWorld ),
		( "facepunch.w_mp5", WeaponModels.SmgWorld ),
		( "facepunch.w_m4a1", WeaponModels.RifleWorld ),
		( "facepunch.w_spaghellim4", WeaponModels.ShotgunWorld ),
		( "facepunch.w_m700", WeaponModels.SniperWorld )
	};

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override async void OnStart()
	{
		IsReady = false;

		var mounted = await TryMountPackage( SboxWeaponsPackage );

		foreach ( var (cloudIdent, path) in Weapons )
		{
			if ( mounted && TryCacheModel( path ) )
				continue;

			await FetchCloudWeapon( cloudIdent, path );

			if ( !_models.ContainsKey( path ) )
				Log.Warning( $"[FinalOutpost] Weapon '{path}' unavailable — recruits use box placeholder until remount." );
		}

		IsReady = true;
		DefenderManager.Instance?.RefreshWeaponModels();
		Log.Info( $"[FinalOutpost] Weapon models ready ({_models.Count}/{Weapons.Length} loaded)." );
	}

	public Model Get( string path ) =>
		!string.IsNullOrWhiteSpace( path ) && _models.TryGetValue( path, out var model ) ? model : null;

	/// <summary>
	/// Fetch + MountAsync. Second arg <c>true</c> lets the client download the package when
	/// it is not already on disk (same pattern as Thorns / YA bootstraps).
	/// </summary>
	private static async Task<bool> TryMountPackage( string ident )
	{
		try
		{
			var package = await Package.Fetch( ident, true );
			if ( package?.Revision is null )
			{
				Log.Warning( $"[FinalOutpost] {ident} not found on cloud — weapon meshes may use placeholders." );
				return false;
			}

			if ( package.IsMounted() )
			{
				Log.Info( $"[FinalOutpost] Package already mounted: {ident}" );
				return true;
			}

			await package.MountAsync();
			Log.Info( $"[FinalOutpost] Mounted {ident}." );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Failed to mount {ident}: {e.Message}" );
			return false;
		}
	}

	private bool TryCacheModel( string path )
	{
		var model = AssetSafe.Model( path );
		if ( model is null )
			return false;

		_models[path] = model;
		return true;
	}

	private async Task FetchCloudWeapon( string cloudIdent, string path )
	{
		if ( _models.ContainsKey( path ) )
			return;

		try
		{
			var package = await Package.Fetch( cloudIdent, true );
			if ( package?.Revision is null )
			{
				Log.Warning( $"[FinalOutpost] Weapon cloud package not found: {cloudIdent}" );
				return;
			}

			await package.MountAsync();

			var primary = package.GetMeta( "PrimaryAsset", "" );
			if ( string.IsNullOrWhiteSpace( primary ) )
			{
				Log.Warning( $"[FinalOutpost] Weapon package has no PrimaryAsset: {cloudIdent}" );
				return;
			}

			var model = AssetSafe.Model( primary );
			if ( model is null )
			{
				Log.Warning( $"[FinalOutpost] Weapon model failed to load: {cloudIdent} ({primary})" );
				return;
			}

			_models[path] = model;
			Log.Info( $"[FinalOutpost] Loaded weapon from cloud: {cloudIdent}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Weapon fetch failed ({cloudIdent}): {e.Message}" );
		}
	}
}
