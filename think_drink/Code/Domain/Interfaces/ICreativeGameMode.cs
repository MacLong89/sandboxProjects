namespace ThinkDrink.Domain;

public enum CreativePromptKind
{
	QuipFill,
	CaptionThis,
	SketchQuips
}

public interface ICreativeGameMode : IGameMode
{
	CreativePromptKind PromptKind { get; }
	GameModePrompt PickPrompt( int roundNumber, Random random, IReadOnlyCollection<string> usedPromptIds );
}
