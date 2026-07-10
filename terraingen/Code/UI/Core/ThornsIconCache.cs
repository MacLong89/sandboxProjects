namespace Terraingen.UI;

using Sandbox.UI;
using Terraingen.Animals;
using Terraingen.GameData;
using Terraingen;

/// <summary>Cached UI icons from <c>Assets/ui/iconsv8/</c> (mounted as <c>ui/iconsv8/...</c>).</summary>
public static class ThornsIconCache
{
	static readonly Dictionary<string, Texture> Cache = new( StringComparer.OrdinalIgnoreCase );
	static readonly HashSet<string> PlaceholderPaths = new( StringComparer.OrdinalIgnoreCase );
	static readonly HashSet<string> LoggedMissingPaths = new( StringComparer.OrdinalIgnoreCase );
	static int _lastWarmMissing = -1;
	static bool _loggedWarmSummary;
	static bool _gameplayIconsWarmed;
	static bool _inventoryUiIconsWarmed;

	public static bool IsGameplayIconsWarmed => _gameplayIconsWarmed;

	public static Texture Get( string path )
	{
		var resolved = ThornsIconManifest.ResolvePath( path );
		if ( string.IsNullOrWhiteSpace( resolved ) )
			return null;

		if ( Cache.TryGetValue( resolved, out var tex ) && tex is not null && tex.IsValid )
			return tex;

		tex = TryLoadTexture( resolved );
		if ( tex is null || !tex.IsValid )
		{
			tex = CreatePlaceholder( resolved );
			PlaceholderPaths.Add( resolved );
		}
		else
		{
			PlaceholderPaths.Remove( resolved );
		}

		Cache[resolved] = tex;
		return tex;
	}

	/// <summary>Sets <paramref name="panel"/> background image; returns true if a real PNG was loaded.</summary>
	public static bool ApplyToPanel( Panel panel, string path, bool addSlotIconClass = true )
	{
		if ( panel is null || !panel.IsValid )
			return false;

		var resolved = ThornsIconManifest.ResolvePath( path );
		if ( string.IsNullOrWhiteSpace( resolved ) )
		{
			panel.Style.BackgroundImage = null;
			return false;
		}

		if ( addSlotIconClass )
			panel.AddClass( "slot-icon" );

		foreach ( var attempt in ThornsIconManifest.GetPathCandidates( resolved ) )
		{
			if ( !ThornsMountedFiles.Exists( attempt ) )
				continue;

			try
			{
				panel.Style.SetBackgroundImage( attempt );
				PlaceholderPaths.Remove( resolved );
				if ( !Cache.TryGetValue( resolved, out var cached ) || cached is null || !cached.IsValid )
				{
					var loaded = Texture.Load( attempt );
					if ( loaded is not null && loaded.IsValid )
						Cache[resolved] = loaded;
				}

				return true;
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns UI] SetBackgroundImage failed for '{attempt}'." );
			}
		}

		var tex = Get( resolved );
		if ( tex is null || !tex.IsValid )
		{
			panel.Style.BackgroundImage = null;
			return false;
		}

		try
		{
			panel.Style.SetBackgroundImage( tex );
			return !IsPlaceholder( resolved );
		}
		catch
		{
			panel.Style.BackgroundImage = tex;
			return !IsPlaceholder( resolved );
		}
	}

	public static bool IsPlaceholder( string path )
	{
		var resolved = ThornsIconManifest.ResolvePath( path ) ?? path;
		return !string.IsNullOrWhiteSpace( resolved ) && PlaceholderPaths.Contains( resolved );
	}

	public static Texture GetMapTexture() => ThornsMapTextureCache.Texture;

	public static void WarmGameplayIcons()
	{
		if ( _gameplayIconsWarmed )
			return;

		ThornsDefinitionRegistry.EnsureInitialized();
		ThornsIconManifest.Refresh();
		ThornsDefinitionRegistry.RegisterDiscoveredIcons(
			ThornsIconDrivenCatalog.BuildItems(),
			ThornsIconDrivenCatalog.BuildSkillsMissingFromUpgrades() );
		ThornsIconManifest.ApplyToRegistry();

		var paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var item in ThornsDefinitionRegistry.AllItems.Values )
		{
			if ( !string.IsNullOrWhiteSpace( item.IconPath ) )
				paths.Add( item.IconPath );

			if ( item.Category == ThornsItemCategory.Attachment && !string.IsNullOrWhiteSpace( item.Id ) )
				paths.Add( ThornsIconManifest.ResolveItemPath( item.Id ) );
		}

		foreach ( var skill in ThornsDefinitionRegistry.AllSkills.Values )
		{
			if ( !string.IsNullOrWhiteSpace( skill.IconPath ) )
				paths.Add( skill.IconPath );
		}

		paths.Add( ThornsIconManifest.ResolveItemPath( "xp" ) ?? ThornsIconRegistry.Hud( "xp" ) );
		paths.Add( ThornsIconRegistry.Hud( "health" ) );
		paths.Add( ThornsIconRegistry.Hud( "stamina" ) );
		paths.Add( ThornsIconRegistry.Hud( "thirst" ) );
		paths.Add( ThornsIconRegistry.Hud( "hunger" ) );

		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		foreach ( var species in ThornsAnimalSpeciesRegistry.All )
		{
			if ( !string.IsNullOrWhiteSpace( species.Key ) )
				paths.Add( ThornsTameCatalog.CreaturePortraitPath( species.Key ) );
		}

		foreach ( var path in paths )
			TryWarmPath( path );

		var missing = paths.Count( p => IsPlaceholder( p ) );
		var loaded = paths.Count - missing;
		var diskUi = ThornsIconManifest.DiscoveredUiIconCount;
		var diskItems = ThornsIconManifest.GetDiscoveredItemIds().Count;
		var diskSkills = ThornsIconManifest.GetDiscoveredSkillIds().Count;
		var diskCreatures = ThornsIconManifest.DiscoveredCreatureCount;

		if ( !_loggedWarmSummary || missing != _lastWarmMissing )
		{
			Log.Info( $"[Thorns UI] Icon cache: {loaded} loaded, {missing} missing — scanned {diskUi} UI PNG(s) under ui/iconsv8/, {diskSkills} skill PNG(s), {diskItems} legacy item PNG(s), {diskCreatures} creature portrait(s)." );
			_loggedWarmSummary = true;
			_lastWarmMissing = missing;
		}

		if ( diskUi == 0 )
		{
			Log.Warning(
				"[Thorns UI] No PNGs found under ui/iconsv8/ on mount. Published builds need terraingen.sbproj Resources " +
				"(include *.png, *.scss, map/*, ui/**/*) then republish." );
			return;
		}

		_gameplayIconsWarmed = true;

		if ( missing > 0 )
			LogMissing( paths );
	}

	public static void WarmInventoryUiIcons()
	{
		if ( _inventoryUiIconsWarmed )
			return;

		ThornsDefinitionRegistry.EnsureInitialized();
		var paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var recipe in ThornsDefinitionRegistry.AllRecipes.Values )
		{
			if ( !string.IsNullOrWhiteSpace( recipe.OutputItemId ) )
				paths.Add( ThornsIconManifest.ResolveItemPath( recipe.OutputItemId ) ?? recipe.OutputItemId );

			if ( recipe.Ingredients is null )
				continue;

			foreach ( var ing in recipe.Ingredients )
			{
				if ( !string.IsNullOrWhiteSpace( ing.ItemId ) )
					paths.Add( ThornsIconManifest.ResolveItemPath( ing.ItemId ) ?? ing.ItemId );
			}
		}

		foreach ( var categoryId in ThornsCraftCatalog.GetCraftCategories() )
		{
			var iconKey = string.Equals( categoryId, ThornsMenuSnapshotHelpers.AllCraftCategoryId, StringComparison.OrdinalIgnoreCase )
				? "inventory"
				: categoryId;
			paths.Add( ThornsIconRegistry.CraftCategoryIcon( iconKey ) );
		}

		foreach ( var iconKey in new[]
		         {
			         "inventory", "craft_build", "craft_ammo", "craft_tools", "craft_armor", "craft_medical",
			         "filter_all", "filter_building", "filter_weapons", "filter_tools", "filter_apparel", "filter_consumables"
		         } )
			paths.Add( ThornsIconRegistry.InventoryUi( iconKey ) );

		foreach ( var path in paths )
			TryWarmPath( path );

		_inventoryUiIconsWarmed = true;
	}

	static bool TryWarmPath( string path )
	{
		var resolved = ThornsIconManifest.ResolvePath( path );
		if ( string.IsNullOrWhiteSpace( resolved ) )
			return false;

		foreach ( var attempt in ThornsIconManifest.GetPathCandidates( resolved ) )
		{
			if ( !ThornsMountedFiles.Exists( attempt ) )
				continue;

			PlaceholderPaths.Remove( resolved );
			var tex = Texture.Load( attempt );
			if ( tex is not null && tex.IsValid )
			{
				Cache[resolved] = tex;
				return true;
			}

			// PNG exists on mount; slots use SetBackgroundImage even when Texture.Load fails.
			return true;
		}

		Get( resolved );
		return !IsPlaceholder( resolved );
	}

	static void LogMissing( HashSet<string> paths )
	{
		foreach ( var path in paths.Where( IsPlaceholder ).OrderBy( p => p ) )
		{
			if ( !LoggedMissingPaths.Add( path ) )
				continue;

			Log.Warning( $"[Thorns UI] Missing icon: {path}" );
		}
	}

	static Texture TryLoadTexture( string path )
	{
		foreach ( var attempt in ThornsIconManifest.GetPathCandidates( path ) )
		{
			if ( !ThornsMountedFiles.Exists( attempt ) )
				continue;

			try
			{
				var tex = Texture.Load( attempt );
				if ( tex is not null && tex.IsValid )
					return tex;
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns UI] Icon load failed for '{attempt}'." );
			}
		}

		return null;
	}

	static Texture CreatePlaceholder( string path )
	{
		var hash = Math.Abs( path.GetHashCode() );
		var bmp = new Bitmap( 64, 64 );
		var r = (byte)(hash & 0xFF);
		var g = (byte)((hash >> 8) & 0xFF);
		var b = (byte)((hash >> 16) & 0xFF);
		var color = new Color( r / 255f * 0.45f + 0.1f, g / 255f * 0.45f + 0.1f, b / 255f * 0.45f + 0.1f );
		for ( var y = 0; y < 64; y++ )
		for ( var x = 0; x < 64; x++ )
			bmp.SetPixel( x, y, color );

		return bmp.ToTexture();
	}
}
