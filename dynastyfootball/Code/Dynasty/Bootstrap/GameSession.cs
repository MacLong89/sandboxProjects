namespace Dynasty.Bootstrap;

public enum GameScreen
{
	MainMenu,
	InGame
}

/// <summary>
/// Tracks menu vs in-game UI flow. UI polls ChangeToken to refresh.
/// </summary>
public sealed class GameSession
{
	public GameScreen Screen { get; private set; } = GameScreen.MainMenu;
	public int ChangeToken { get; private set; }
	public string StatusMessage { get; private set; } = "";

	public void EnterMainMenu( string message = "" )
	{
		Screen = GameScreen.MainMenu;
		StatusMessage = message;
		Bump();
	}

	public void EnterGame( string message = "" )
	{
		Screen = GameScreen.InGame;
		StatusMessage = message;
		Bump();
	}

	public void SetStatus( string message )
	{
		StatusMessage = message;
		Bump();
	}

	void Bump() => ChangeToken++;
}
