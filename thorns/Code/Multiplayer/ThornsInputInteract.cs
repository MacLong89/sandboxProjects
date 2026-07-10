namespace Sandbox;

/// <summary>Interact / Use detection — action strings plus physical E (building uses keyboard E elsewhere).</summary>
public static class ThornsInputInteract
{
	public static bool IsUseOrInteractHeld()
	{
		return Input.Down( "use" ) || Input.Down( "Use" )
		       || Input.Keyboard.Down( "e" ) || Input.Keyboard.Down( "E" );
	}

	public static bool IsUseOrInteractPressed()
	{
		return Input.Pressed( "use" ) || Input.Pressed( "Use" )
		       || Input.Keyboard.Pressed( "e" ) || Input.Keyboard.Pressed( "E" );
	}
}
