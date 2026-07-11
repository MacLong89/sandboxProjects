namespace FinalOutpost;

/// <summary>
/// Fetches the five recruit world models from individual cloud packages at runtime
/// (without mounting the full facepunch.sboxweapons collection).
/// </summary>
public sealed class WeaponModelLoader : Component
{
	public static WeaponModelLoader Instance { get; private set; }

	private readonly Dictionary<string, Model> _models = new();

	private static readonly (string CloudIdent, string Path)[] Weapons =
	{
		("facepunch.w_usp", WeaponModels.PistolWorld),
		("facepunch.w_mp5", WeaponModels.SmgWorld),
		("facepunch.w_m4a1", WeaponModels.RifleWorld),
		("facepunch.w_spaghellim4", WeaponModels.ShotgunWorld),
		("facepunch.w_m700", WeaponModels.SniperWorld)
	};

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override async void OnStart()
	{
		foreach ( var (ident, path) in Weapons )
		{
			if ( TryLoadLocal( path ) )
				continue;

			await FetchCloudWeapon( ident, path );
		}

		DefenderManager.Instance?.RefreshWeaponModels();
	}

	public Model Get( string path ) =>
		!string.IsNullOrWhiteSpace( path ) && _models.TryGetValue( path, out var model ) ? model : null;

	private bool TryLoadLocal( string path )
	{
		try
		{
			var model = Model.Load( path );
			if ( model is null || model.IsError )
				return false;

			_models[path] = model;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task FetchCloudWeapon( string cloudIdent, string path )
	{
		try
		{
			var package = await Package.Fetch( cloudIdent, false );
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

			var model = Model.Load( primary );
			if ( model is null || model.IsError )
			{
				Log.Warning( $"[FinalOutpost] Weapon model failed to load: {cloudIdent} ({primary})" );
				return;
			}

			_models[path] = model;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Weapon fetch failed ({cloudIdent}): {e.Message}" );
		}
	}
}
