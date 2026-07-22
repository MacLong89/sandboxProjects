namespace CatchACritter;

/// <summary>Floating world text that rises and fades, then destroys itself.</summary>
public sealed class FloatingText : Component
{
	public float Life { get; set; } = 1.4f;
	public float Rise { get; set; } = 70f;

	TextRenderer _text;
	TimeSince _born;

	public void Init( string message, Color color, float scale )
	{
		_text = Components.Create<TextRenderer>();
		_text.Text = message;
		_text.Color = color;
		_text.Scale = scale;
		_text.FontSize = 40;
		_text.Billboard = TextRenderer.BillboardMode.YOnly;
		_text.HorizontalAlignment = TextRenderer.HAlignment.Center;
		_born = 0;
	}

	protected override void OnUpdate()
	{
		var t = _born / Life;
		if ( t >= 1f ) { GameObject.Destroy(); return; }
		WorldPosition += Vector3.Up * Rise * Time.Delta;
		if ( _text.IsValid() )
			_text.Color = _text.Color.WithAlpha( 1f - t * t );
	}
}

/// <summary>Local, non-networked juice: pops, floaters, and sounds.</summary>
public static class CatchEffects
{
	public static void FloatText( Vector3 pos, string message, Color color, float scale = 0.16f )
	{
		var scene = Game.ActiveScene;
		if ( scene is null ) return;
		var go = new GameObject( true, "FloatText" );
		go.WorldPosition = pos;
		var ft = go.Components.Create<FloatingText>();
		ft.Init( message, color, scale );
	}

	public static void CatchPop( Vector3 pos, SpeciesDef def, bool shiny, bool mine )
	{
		if ( def is null ) return;

		if ( mine )
		{
			var value = def.BaseValue * (shiny ? Balance.ShinyValueMult : 1.0);
			var color = shiny ? CritterBody.ShinyGold : SpeciesCatalog.RarityColor( def.Rarity );
			FloatText( pos + Vector3.Up * 70f, shiny ? $"* SHINY {def.Name}! *" : $"{def.Name}!", color, shiny ? 0.22f : 0.16f );
			FloatText( pos + Vector3.Up * 30f, $"+{Balance.Fmt( value )}", new Color( 1f, 0.95f, 0.6f ), 0.13f );
			Sfx.Play( shiny ? "shiny" : "catch", pos );

			if ( (int)def.Rarity >= (int)Rarity.Epic )
				Sfx.Play( "rare", pos );
		}
		else
		{
			FloatText( pos + Vector3.Up * 50f, "*", Color.White, 0.13f );
		}
	}
}

/// <summary>Tiny sound helper — all clips are generated wavs under Assets/sounds.</summary>
public static class Sfx
{
	static readonly Dictionary<string, SoundFile> _cache = new();
	static readonly HashSet<string> _missing = new();

	public static void Play( string name, Vector3? pos = null )
	{
		try
		{
			if ( _missing.Contains( name ) ) return;
			if ( !_cache.TryGetValue( name, out var file ) )
			{
				file = SoundFile.Load( $"sounds/{name}.wav" );
				if ( file is null ) { _missing.Add( name ); return; }
				_cache[name] = file;
			}

			var handle = Sound.PlayFile( file );
			if ( handle is not null && pos.HasValue )
				handle.Position = pos.Value;
		}
		catch
		{
			_missing.Add( name );
		}
	}
}
