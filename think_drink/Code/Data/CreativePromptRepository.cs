namespace ThinkDrink.Data;

public sealed class CreativePromptRepository
{
	private CreativePromptBank _bank = new();
	private readonly Dictionary<CreativePromptKind, List<CreativePromptEntry>> _byKind = new();

	public bool Load()
	{
		_byKind.Clear();
		_bank = new CreativePromptBank();

		try
		{
			var json = FileSystem.Mounted.ReadAllText( "data/creative_prompts.json" );
			if ( !string.IsNullOrWhiteSpace( json ) )
				_bank = Json.Deserialize<CreativePromptBank>( json ) ?? new CreativePromptBank();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: creative prompts load failed — {e.Message}" );
		}

		RegisterKind( CreativePromptKind.QuipFill, _bank.QuipFill );
		RegisterKind( CreativePromptKind.CaptionThis, _bank.CaptionThis );
		RegisterKind( CreativePromptKind.SketchQuips, _bank.SketchQuips );

		var total = _byKind.Values.Sum( l => l.Count );
		if ( total > 0 )
			Log.Info( $"Think & Drink: loaded {total} creative prompts." );

		return total > 0;
	}

	public CreativePromptEntry Pick( CreativePromptKind kind, Random random, IReadOnlyCollection<string> usedIds )
	{
		if ( !_byKind.TryGetValue( kind, out var list ) || list.Count == 0 )
			return Fallback( kind );

		var used = usedIds is null ? new HashSet<string>() : new HashSet<string>( usedIds );
		var pool = list.Where( p => !used.Contains( p.Id ) ).ToList();
		if ( pool.Count == 0 )
			pool = list.ToList();

		return pool[random.Next( pool.Count )];
	}

	void RegisterKind( CreativePromptKind kind, List<CreativePromptEntry> entries )
	{
		if ( entries is null || entries.Count == 0 ) return;

		var cleaned = entries.Where( e => !string.IsNullOrWhiteSpace( e.Prompt ) ).ToList();
		if ( cleaned.Count > 0 )
			_byKind[kind] = cleaned;
	}

	static CreativePromptEntry Fallback( CreativePromptKind kind ) => kind switch
	{
		CreativePromptKind.CaptionThis => new CreativePromptEntry
		{
			Id = "fallback_caption",
			Category = "Chaos",
			Prompt = "A confused goose wearing sunglasses at a fancy restaurant"
		},
		CreativePromptKind.SketchQuips => new CreativePromptEntry
		{
			Id = "fallback_sketch",
			Category = "Doodle",
			Prompt = "A potato running for class president"
		},
		_ => new CreativePromptEntry
		{
			Id = "fallback_quip",
			Category = "Party",
			Prompt = "The worst thing to bring to a potluck is ___"
		}
	};
}
