namespace FinalOutpost;

/// <summary>
/// Loads recruit/takeover weapon world + view models from the mounted
/// <c>facepunch.sboxweapons</c> package (or individual cloud weapon packages as fallback).
/// Pulls packages onto disk when missing so published-game players without Library
/// content pre-installed still get the same meshes as the editor.
/// </summary>
public sealed class WeaponModelLoader : Component
{
	public static WeaponModelLoader Instance { get; private set; }

	/// <summary>True after the mount pass finished (success or box-fallback).</summary>
	public static bool IsReady { get; private set; }

	const string SboxWeaponsPackage = "facepunch.sboxweapons";

	private readonly Dictionary<string, Model> _models = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Cloud ident + every path we need from that package (world and/or view).</summary>
	private static readonly (string CloudIdent, string[] Paths)[] Weapons =
	{
		( "facepunch.w_usp", new[] { WeaponModels.PistolWorld, WeaponModels.PistolView, TakeoverWeaponCatalog.UspView } ),
		( "facepunch.w_mp5", new[] { WeaponModels.SmgWorld, WeaponModels.SmgView, TakeoverWeaponCatalog.Mp5View } ),
		( "facepunch.w_m4a1", new[] { WeaponModels.RifleWorld, WeaponModels.RifleView, TakeoverWeaponCatalog.M4View } ),
		( "facepunch.w_spaghellim4", new[] { WeaponModels.ShotgunWorld, WeaponModels.ShotgunView, TakeoverWeaponCatalog.ShotgunView } ),
		( "facepunch.w_m700", new[] { WeaponModels.SniperWorld, WeaponModels.SniperView, TakeoverWeaponCatalog.SniperView } ),
	};

	private static readonly string[] ExtraEngineModels =
	{
		TakeoverWeaponCatalog.ArmsPath,
		CharacterModel.CitizenVmdl,
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

		foreach ( var (cloudIdent, paths) in Weapons )
		{
			var allCached = true;
			foreach ( var path in paths.Distinct( StringComparer.OrdinalIgnoreCase ) )
			{
				if ( mounted && TryCacheModel( path ) )
					continue;

				allCached = false;
			}

			if ( allCached )
				continue;

			await FetchCloudWeapon( cloudIdent, paths );

			foreach ( var path in paths.Distinct( StringComparer.OrdinalIgnoreCase ) )
			{
				if ( !_models.ContainsKey( path ) )
					Log.Warning( $"[FinalOutpost] Weapon '{path}' unavailable — box placeholder until remount." );
			}
		}

		foreach ( var path in ExtraEngineModels )
		{
			if ( !TryCacheModel( path ) )
				Log.Warning( $"[FinalOutpost] Engine model '{path}' unavailable." );
		}

		IsReady = true;
		DefenderManager.Instance?.RefreshWeaponModels();
		Log.Info( $"[FinalOutpost] Weapon models ready ({_models.Count} cached, packageMounted={mounted})." );
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

	private async Task FetchCloudWeapon( string cloudIdent, string[] paths )
	{
		try
		{
			var package = await Package.Fetch( cloudIdent, true );
			if ( package?.Revision is null )
			{
				Log.Warning( $"[FinalOutpost] Weapon cloud package not found: {cloudIdent}" );
				return;
			}

			await package.MountAsync();

			// After mount, try every requested path; also cache PrimaryAsset under its own path.
			var primary = package.GetMeta( "PrimaryAsset", "" );
			if ( !string.IsNullOrWhiteSpace( primary ) )
				TryCacheModel( primary );

			foreach ( var path in paths.Distinct( StringComparer.OrdinalIgnoreCase ) )
			{
				if ( _models.ContainsKey( path ) )
					continue;

				if ( TryCacheModel( path ) )
					Log.Info( $"[FinalOutpost] Loaded weapon mesh: {path}" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Weapon fetch failed ({cloudIdent}): {e.Message}" );
		}
	}
}
