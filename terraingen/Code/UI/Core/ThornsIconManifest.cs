namespace Terraingen.UI;

using Terraingen;
using Terraingen.GameData;

/// <summary>
/// Resolves icon paths against <c>ui/iconsv8/</c> (flat UI pack). Legacy item PNGs under <c>ui/icons/</c>
/// are still scanned for optional inventory art and icon-driven item discovery.
/// </summary>
public static class ThornsIconManifest
{
	const string IconFolder = "ui/iconsv8";
	const string LegacyItemFolder = "ui/icons";

	static readonly Dictionary<string, string> IconFiles = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, string> LegacyItemFiles = new( StringComparer.OrdinalIgnoreCase );
	static readonly HashSet<string> AmbiguousShortIconKeys = new( StringComparer.OrdinalIgnoreCase )
	{
		"wood", "stone", "cloth", "ore", "metal", "food", "water", "ammo",
		"leather", "hide", "meat", "scrap", "bed", "wall", "door", "chest", "pick", "axe"
	};
	static bool _scanned;

	public static void Refresh()
	{
		IconFiles.Clear();
		LegacyItemFiles.Clear();
		_scanned = true;

		ScanFolder( IconFolder, IconFiles, recursive: false );
		ScanFolder( LegacyItemFolder, LegacyItemFiles, recursive: false );
	}

	public static void ApplyToRegistry()
	{
		ThornsDefinitionRegistry.EnsureInitialized();

		foreach ( var item in ThornsDefinitionRegistry.AllItems.Values )
			item.IconPath = ResolveItemPath( item.Id );

		foreach ( var skill in ThornsDefinitionRegistry.AllSkills.Values )
			skill.IconPath = ResolveSkillPath( skill.Id );
	}

	public static string ResolvePath( string logicalPath )
	{
		if ( string.IsNullOrWhiteSpace( logicalPath ) )
			return null;

		EnsureScanned();

		if ( TryExistingFile( logicalPath, out var direct ) )
			return direct;

		if ( logicalPath.Contains( IconFolder, StringComparison.OrdinalIgnoreCase ) )
		{
			var stem = ExtractIdFromPath( logicalPath );
			if ( !string.IsNullOrWhiteSpace( stem ) )
				return LookupIcon( stem ) ?? ThornsIconRegistry.Path( stem );
		}

		var id = ExtractIdFromPath( logicalPath );
		if ( string.IsNullOrWhiteSpace( id ) )
			return logicalPath;

		if ( logicalPath.Contains( "/skills/", StringComparison.OrdinalIgnoreCase )
		     || logicalPath.Contains( "/skill/", StringComparison.OrdinalIgnoreCase ) )
			return ResolveSkillPath( id ) ?? logicalPath;

		if ( logicalPath.Contains( "/creatures/", StringComparison.OrdinalIgnoreCase ) )
			return ResolveCreaturePath( id ) ?? logicalPath;

		if ( logicalPath.Contains( "/icons/", StringComparison.OrdinalIgnoreCase )
		     || logicalPath.Contains( "icons_v", StringComparison.OrdinalIgnoreCase ) )
			return ResolveItemPath( id ) ?? ResolveGenericPath( id ) ?? logicalPath;

		return ResolveGenericPath( id ) ?? logicalPath;
	}

	public static string ResolveCreaturePath( string speciesKey )
	{
		EnsureScanned();
		return LookupIcon( speciesKey ) ?? DefaultCreaturePath( speciesKey );
	}

	public static string ResolveItemPath( string itemId )
	{
		EnsureScanned();
		var canonical = ThornsItemIdAliases.Canonicalize( itemId );

		if ( ThornsItemIdAliases.AttachmentItemIdToIconStem.ContainsKey( canonical ) )
		{
			var directAttachment = ThornsItemIdAliases.AttachmentLegacyIconPath( canonical );
			if ( TryExistingFile( directAttachment, out var attachmentResolved ) )
				return attachmentResolved;
		}

		var directLegacy = LegacyItemStoragePath( ThornsItemIdAliases.PreferredIconStem( canonical ) );
		if ( TryExistingFile( directLegacy, out var legacyResolved ) )
			return legacyResolved;

		var found = LookupItemFile( canonical );
		if ( !string.IsNullOrWhiteSpace( found ) )
			return found;

		foreach ( var stem in ThornsItemIdAliases.IconLookupStems( canonical ) )
		{
			if ( TryExistingFile( LegacyItemStoragePath( stem ), out var legacy ) )
				return legacy;
		}

		found = FuzzyLookupLegacyItemFile( canonical );
		if ( !string.IsNullOrWhiteSpace( found ) )
			return found;

		foreach ( var stem in ThornsItemIdAliases.IconLookupStems( canonical ) )
		{
			if ( TryExistingFile( ThornsIconRegistry.ItemInventoryIconPath( stem ), out var v8 ) )
				return v8;
		}

		return ThornsIconRegistry.Item( canonical );
	}

	public static string ResolveSkillPath( string skillId )
	{
		EnsureScanned();

		var found = LookupSkillIcon( skillId );
		if ( !string.IsNullOrWhiteSpace( found ) )
			return found;

		var skill = ThornsDefinitionRegistry.GetSkill( skillId );
		if ( skill is not null )
		{
			var categoryStem = skill.Category switch
			{
				ThornsSkillCategory.Persistence => "survival",
				ThornsSkillCategory.Instinct => "combat",
				ThornsSkillCategory.Industry => "building",
				_ => "special"
			};

			if ( IconFiles.TryGetValue( NormalizeKey( categoryStem ), out var categoryPath ) )
				return categoryPath;
		}

		return ThornsIconRegistry.Skill( skillId );
	}

	public static IReadOnlyList<string> GetDiscoveredItemIds() =>
		LegacyItemFiles.Keys.OrderBy( k => k ).ToList();

	public static IReadOnlyList<string> GetDiscoveredSkillIds()
	{
		EnsureScanned();
		ThornsDefinitionRegistry.EnsureInitialized();

		return ThornsUpgradeDefinitions.All
			.Where( s => LookupSkillIcon( s.Id ) is not null )
			.Select( s => s.Id )
			.OrderBy( id => id, StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	public static IReadOnlyList<string> GetDiscoveredCreatureIds() =>
		IconFiles.Keys
			.Where( k => IsCreatureStem( k ) )
			.OrderBy( k => k )
			.ToList();

	public static int DiscoveredCreatureCount => GetDiscoveredCreatureIds().Count;

	public static int DiscoveredUiIconCount
	{
		get
		{
			EnsureScanned();
			return IconFiles.Count;
		}
	}

	static bool IsCreatureStem( string stem ) =>
		stem is "deer" or "wolf" or "moose" or "panther";

	static void EnsureScanned()
	{
		if ( !_scanned )
			Refresh();
	}

	static void ScanFolder( string folder, Dictionary<string, string> map, bool recursive )
	{
		if ( !ThornsMountedFiles.IsAvailable )
			return;

		foreach ( var pattern in new[] { "*.png", "*.PNG" } )
		{
			try
			{
				foreach ( var file in FileSystem.Mounted.FindFile( folder, pattern, recursive ) )
					RegisterFile( map, file, folder );
			}
			catch
			{
				// try next pattern / path variant
			}
		}

		foreach ( var alt in new[] { folder.TrimStart( '/' ) } )
		{
			if ( string.Equals( alt, folder, StringComparison.OrdinalIgnoreCase ) )
				continue;

			try
			{
				foreach ( var file in FileSystem.Mounted.FindFile( alt, "*.png", recursive ) )
					RegisterFile( map, file, folder );
			}
			catch
			{
				// ignore
			}
		}
	}

	static void RegisterFile( Dictionary<string, string> map, string file, string folder )
	{
		if ( string.IsNullOrWhiteSpace( file ) )
			return;

		var stem = GetFileStem( file );
		if ( string.IsNullOrWhiteSpace( stem ) )
			return;

		var path = ToIconStoragePath( $"{folder}/{stem}.png" );
		RegisterIconKeys( map, stem, path );

		if ( string.Equals( folder, LegacyItemFolder, StringComparison.OrdinalIgnoreCase ) )
			RegisterLegacyItemAliasKeys( map, stem, path );
	}

	static void RegisterIconKeys( Dictionary<string, string> map, string stem, string path )
	{
		foreach ( var key in ThornsItemIdAliases.IconKeyVariants( stem ) )
		{
			if ( !string.IsNullOrWhiteSpace( key ) )
				map.TryAdd( key, path );
		}

		var fallback = NormalizeKey( stem );
		if ( !string.IsNullOrWhiteSpace( fallback ) )
			map.TryAdd( fallback, path );
	}

	static void RegisterLegacyItemAliasKeys( Dictionary<string, string> map, string stem, string path )
	{
		if ( stem.Contains( "pickaxe", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, stem.Replace( "pickaxe", "pick", StringComparison.OrdinalIgnoreCase ), path );

		if ( stem.Contains( "hatchet", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, stem.Replace( "hatchet", "axe", StringComparison.OrdinalIgnoreCase ), path );

		if ( string.Equals( stem, "kevlar_vest", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, "kevlar_chest", path );

		if ( string.Equals( stem, "kevlar_helmet", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, "kevlar_head", path );

		if ( string.Equals( stem, "kevlar_pants", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, "kevlar_legs", path );

		if ( string.Equals( stem, "scrap_helmet", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, "scrap_head", path );

		if ( string.Equals( stem, "scrap_pants", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, "scrap_legs", path );

		if ( string.Equals( stem, "metal_ingot", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( stem, "ingot", StringComparison.OrdinalIgnoreCase ) )
			RegisterIconKeys( map, "smelt_metal", path );

		RegisterAttachmentIconAliasKeys( map, stem, path );
	}

	static void RegisterAttachmentIconAliasKeys( Dictionary<string, string> map, string stem, string path )
	{
		foreach ( var pair in ThornsItemIdAliases.AttachmentItemIdToIconStem )
		{
			if ( !string.Equals( pair.Value, stem, StringComparison.OrdinalIgnoreCase ) )
				continue;

			RegisterIconKeys( map, pair.Key, path );
			foreach ( var key in ThornsItemIdAliases.IconLookupKeys( pair.Key ) )
				map.TryAdd( key, path );
		}
	}

	static string LookupIcon( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		foreach ( var key in GetLookupKeys( id ) )
		{
			if ( IconFiles.TryGetValue( key, out var path ) )
				return path;
		}

		var resolved = ThornsIconRegistry.ResolveStem( id );
		if ( IconFiles.TryGetValue( NormalizeKey( resolved ), out var aliased ) )
			return aliased;

		return null;
	}

	static string LookupSkillIcon( string skillId )
	{
		if ( string.IsNullOrWhiteSpace( skillId ) )
			return null;

		foreach ( var key in GetSkillLookupKeys( skillId ) )
		{
			if ( IconFiles.TryGetValue( key, out var path ) )
				return path;
		}

		return null;
	}

	static IEnumerable<string> GetSkillLookupKeys( string skillId )
	{
		foreach ( var key in GetLookupKeys( skillId ) )
			yield return key;

		yield return NormalizeKey( ThornsIconRegistry.ResolveSkillStem( skillId ) );
	}

	static string LookupItemFile( string canonicalItemId )
	{
		if ( string.IsNullOrWhiteSpace( canonicalItemId ) )
			return null;

		foreach ( var key in GetItemLookupKeys( canonicalItemId ) )
		{
			if ( IconFiles.TryGetValue( key, out var path ) )
				return path;

			if ( LegacyItemFiles.TryGetValue( key, out var legacy ) )
				return legacy;
		}

		return null;
	}

	static IEnumerable<string> GetItemLookupKeys( string canonicalItemId )
	{
		foreach ( var key in ThornsItemIdAliases.IconLookupKeys( canonicalItemId ) )
			yield return key;

		foreach ( var stem in ThornsItemIdAliases.IconLookupStems( canonicalItemId ) )
		{
			foreach ( var key in GetLookupKeys( stem ) )
				yield return key;
		}
	}

	static string FuzzyLookupLegacyItemFile( string canonicalItemId )
	{
		if ( string.IsNullOrWhiteSpace( canonicalItemId ) || LegacyItemFiles.Count == 0 )
			return null;

		var queries = ThornsItemIdAliases.IconLookupKeys( canonicalItemId ).ToList();
		if ( queries.Count == 0 )
			return null;

		string bestPath = null;
		var bestScore = 0;

		foreach ( var pair in LegacyItemFiles )
		{
			foreach ( var query in queries )
			{
				var score = ScoreLegacyIconKeys( query, pair.Key );
				if ( score <= bestScore )
					continue;

				bestScore = score;
				bestPath = pair.Value;
			}
		}

		return bestScore >= 58 ? bestPath : null;
	}

	static int ScoreLegacyIconKeys( string query, string fileKey )
	{
		if ( string.IsNullOrWhiteSpace( query ) || string.IsNullOrWhiteSpace( fileKey ) )
			return 0;

		if ( string.Equals( query, fileKey, StringComparison.OrdinalIgnoreCase ) )
			return 100;

		var longer = query.Length >= fileKey.Length ? query : fileKey;
		var shorter = query.Length < fileKey.Length ? query : fileKey;
		if ( shorter.Length < 4 || AmbiguousShortIconKeys.Contains( shorter ) )
			return 0;

		if ( longer.StartsWith( shorter, StringComparison.OrdinalIgnoreCase ) )
			return 72 + Math.Min( 18, shorter.Length );

		if ( longer.Contains( shorter, StringComparison.OrdinalIgnoreCase ) )
			return 58 + Math.Min( 12, shorter.Length );

		return 0;
	}

	static string LegacyItemStoragePath( string stem ) =>
		ToIconStoragePath( $"{LegacyItemFolder}/{stem}.png" );

	static string ResolveGenericPath( string id )
	{
		var icon = LookupIcon( id );
		if ( !string.IsNullOrWhiteSpace( icon ) )
			return icon;

		return ThornsIconRegistry.Path( id );
	}

	static IEnumerable<string> GetLookupKeys( string id )
	{
		yield return NormalizeKey( id );
		yield return NormalizeKey( id.Replace( "_", "" ) );

		if ( id.EndsWith( "_skill", StringComparison.OrdinalIgnoreCase ) )
			yield return NormalizeKey( id[..^6] );

		if ( id.StartsWith( "skill_", StringComparison.OrdinalIgnoreCase ) )
			yield return NormalizeKey( id[6..] );
	}

	static string NormalizeKey( string stem )
	{
		var s = stem.Trim().ToLowerInvariant();
		foreach ( var suffix in new[] { "_icon", "icon_", "_skill", "skill_" } )
		{
			if ( s.EndsWith( suffix, StringComparison.Ordinal ) )
				s = s[..^suffix.Length];
			if ( s.StartsWith( suffix, StringComparison.Ordinal ) )
				s = s[suffix.Length..];
		}

		return new string( s.Where( char.IsLetterOrDigit ).ToArray() );
	}

	static string ExtractIdFromPath( string path )
	{
		var name = GetFileStem( path );
		return string.IsNullOrWhiteSpace( name ) ? null : name;
	}

	static string GetFileStem( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return "";

		var normalized = path.Replace( '\\', '/' ).Trim();
		var slash = normalized.LastIndexOf( '/' );
		var name = slash >= 0 ? normalized[(slash + 1)..] : normalized;
		var dot = name.LastIndexOf( '.' );
		return dot > 0 ? name[..dot] : name;
	}

	static string DefaultItemPath( string id ) =>
		ThornsIconRegistry.Item( ThornsItemIdAliases.Canonicalize( id ) );
	static string DefaultSkillPath( string id ) => ThornsIconRegistry.Skill( id );
	static string DefaultCreaturePath( string id ) => ThornsIconRegistry.Creature( id );

	static string ToIconStoragePath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return path;

		return path.Replace( '\\', '/' ).Trim().TrimStart( '/' );
	}

	static bool TryExistingFile( string path, out string resolved )
	{
		resolved = null;
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		foreach ( var attempt in GetPathCandidates( path ) )
		{
			if ( !ThornsMountedFiles.Exists( attempt ) )
				continue;

			resolved = ToIconStoragePath( attempt );
			return true;
		}

		return false;
	}

	internal static IEnumerable<string> GetPathCandidates( string path ) => ThornsContentPath.Candidates( path );
}
