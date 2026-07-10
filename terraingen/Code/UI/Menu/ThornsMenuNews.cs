namespace Terraingen.UI.Menu;

using System.Linq;
using Terraingen;
using Terraingen.UI;

public sealed class ThornsMenuNewsDto
{
	public List<ThornsMenuNewsEntryDto> Entries { get; set; } = new();
}

public sealed class ThornsMenuNewsEntryDto
{
	public string Title { get; set; } = "";
	public string Summary { get; set; } = "";
	public string PatchNotesUrl { get; set; } = "";
	public string DevBlogUrl { get; set; } = "";
	public string Thumbnail { get; set; } = "";
}

/// <summary>Loads menu news from data/news.json; hides panel when empty.</summary>
public static class ThornsMenuNews
{
	public const string RelativePath = "news.json";
	public const string MountedPath = "news.json";

	static readonly string[] MountedPathCandidates =
	{
		"news.json",
		"/news.json"
	};

	static ThornsMenuNewsDto _cached;
	static bool _loaded;

	public static IReadOnlyList<ThornsMenuNewsEntryDto> Entries
	{
		get
		{
			EnsureLoaded();
			if ( _cached?.Entries is null || _cached.Entries.Count == 0 )
				return Array.Empty<ThornsMenuNewsEntryDto>();

			return _cached.Entries;
		}
	}

	public static bool HasContent => Entries.Count > 0;

	public static void Reload( bool notifyUi = false )
	{
		_loaded = false;
		_cached = null;
		EnsureLoaded();

		if ( notifyUi )
			UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;
		try
		{
			if ( FileSystem.Data.FileExists( RelativePath ) )
				_cached = FileSystem.Data.ReadJson<ThornsMenuNewsDto>( RelativePath );
			else if ( TryLoadMountedNews( out var mounted ) )
				_cached = mounted;
			else
				_cached = new ThornsMenuNewsDto();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Failed to read news.json." );
			_cached = new ThornsMenuNewsDto();
		}

		_cached ??= new ThornsMenuNewsDto();
		_cached.Entries ??= new List<ThornsMenuNewsEntryDto>();
		if ( _cached.Entries is not List<ThornsMenuNewsEntryDto> )
			_cached.Entries = _cached.Entries.ToList();
		_cached.Entries.RemoveAll( e => e is null || string.IsNullOrWhiteSpace( e.Title ) );
	}

	static bool TryLoadMountedNews( out ThornsMenuNewsDto dto )
	{
		foreach ( var path in MountedPathCandidates )
		{
			if ( ThornsMountedFiles.TryReadJson<ThornsMenuNewsDto>( path, out dto ) )
				return true;
		}

		dto = null;
		return false;
	}

	public static ThornsMenuNewsEntryDto GetLatest()
	{
		EnsureLoaded();
		if ( _cached?.Entries is null || _cached.Entries.Count == 0 )
			return null;

		return _cached.Entries[0];
	}
}
