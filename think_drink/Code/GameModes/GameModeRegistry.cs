namespace ThinkDrink.GameModes;

public static class GameModeRegistry
{
	private static readonly IGameMode[] SelectableModesList =
	{
		new TriviaShowdownMode(),
		new QuipFillMode(),
		new CaptionThisMode(),
		new SketchQuipsMode()
	};

	private static readonly IReadOnlyList<IGameMode> _selectable = SelectableModesList;

	public static IReadOnlyList<IGameMode> Selectable => _selectable;

	/// <summary>Legacy list — use <see cref="Selectable"/> for lobby UI.</summary>
	public static IReadOnlyList<IGameMode> All => _selectable;

	public static bool IsSelectable( GameModeId id ) =>
		_selectable.Any( m => m.Definition.Id == id );

	public static IGameMode Create( GameModeId id )
	{
		if ( !IsSelectable( id ) )
			id = GameModeId.TriviaShowdown;

		return id switch
		{
			GameModeId.TriviaShowdown => new TriviaShowdownMode(),
			GameModeId.QuipFill => new QuipFillMode(),
			GameModeId.CaptionThis => new CaptionThisMode(),
			GameModeId.SketchQuips => new SketchQuipsMode(),
			_ => new TriviaShowdownMode()
		};
	}

	public static GameModeDefinition GetDefinition( GameModeId id )
	{
		var mode = _selectable.FirstOrDefault( m => m.Definition.Id == id );
		return mode?.Definition ?? _selectable[0].Definition;
	}

	public static bool UsesCreativeFlow( GameModeId id ) =>
		Create( id ).GetRoundSettings( 1, RandomEventType.None ).UsesCreativeFlow;
}
